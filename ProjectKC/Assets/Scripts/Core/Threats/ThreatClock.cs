using System;
using System.Collections.Generic;

namespace KingdomCollapse.Core
{
    public enum ThreatKind
    {
        Horde = 0,
        Fire = 1,
        StructuralCollapse = 2,

        /// <summary>Saque de rival economico: leva recurso, nao toca muralha nem territorio.</summary>
        Raid = 3
    }

    /// <summary>
    /// Ameaca ja anunciada. Guarda o dia em que foi anunciada para que a regra de
    /// antecedencia minima possa ser verificada, e nao apenas prometida.
    /// </summary>
    public sealed class ScheduledThreat
    {
        public ScheduledThreat(
            ThreatKind kind, int arrivalDay, int force, int announcedOnDay, RivalCampaign campaign = null)
        {
            Kind = kind;
            ArrivalDay = arrivalDay;
            Force = force;
            AnnouncedOnDay = announcedOnDay;
            Campaign = campaign;
        }

        public ThreatKind Kind { get; }

        /// <summary>
        /// Campanha a que o ataque pertence. Nula so na ameaca anonima, que existe
        /// enquanto o conteudo nao declara rivais.
        /// </summary>
        public RivalCampaign Campaign { get; }

        /// <summary>De qual rival vem o ataque (spec threat-clock).</summary>
        public RivalDefinition Rival => Campaign?.Rival;

        public int ArrivalDay { get; internal set; }

        /// <summary>Forca prevista. Um evento pode altera-la antes do dia de chegada.</summary>
        public int Force { get; internal set; }

        public int AnnouncedOnDay { get; }

        public int LeadTime => ArrivalDay - AnnouncedOnDay;

        public int DaysUntil(int currentDay) => ArrivalDay - currentDay;

        public override string ToString()
        {
            string origin = Rival != null ? Rival.Id + " " : string.Empty;
            return origin + Kind + " dia " + ArrivalDay + " forca " + Force;
        }
    }

    /// <summary>
    /// Curva de forca da ameaca. Escala com o tempo, sem teto (spec threat-clock).
    ///
    /// Nao escala com o territorio de proposito. O termo por celula existiu enquanto
    /// nada na economia impedia crescer para sempre: era uma penalidade artificial
    /// por expandir, inventada para substituir um limite que faltava. Com populacao
    /// que come e guarnece, o teto virou economico, e punir o tamanho seria cobrar
    /// duas vezes pela mesma expansao.
    /// </summary>
    public sealed class ThreatCurve
    {
        public ThreatCurve(
            double baseForce = 3,
            double perDay = 1.0,
            double dayExponent = 1.25)
        {
            BaseForce = baseForce;
            PerDay = perDay;
            DayExponent = dayExponent;
        }

        public double BaseForce { get; }

        public double PerDay { get; }

        public double DayExponent { get; }

        public int ForceFor(int day)
        {
            double byDay = PerDay * Math.Pow(Math.Max(1, day), DayExponent);
            return Math.Max(1, (int)Math.Round(BaseForce + byDay, MidpointRounding.AwayFromZero));
        }

        /// <summary>
        /// Sobrecarga que ignora o territorio. Existe para nao quebrar quem ainda
        /// chama com o numero de celulas; o parametro nao tem efeito.
        /// </summary>
        public int ForceFor(int day, int ownedTiles) => ForceFor(day);
    }

    /// <summary>
    /// Fila visivel de ameacas. Toda ameaca e agendada com antecedencia e fica
    /// consultavel durante o Planejamento; nada e sorteado no momento da resolucao.
    ///
    /// Com rivais, a fila reune as campanhas dos rivais em guerra (design D3, D12):
    /// o relogio continua cuidando de quando e com que antecedencia, e cada janela
    /// sorteia qual rival em guerra vem, com a forca da campanha dele. Sem rivais,
    /// cai na curva anonima por dia, que so existe enquanto o conteudo ainda nao
    /// declara rivais.
    /// </summary>
    public sealed class ThreatClock
    {
        /// <summary>Antecedencia minima exigida pela spec.</summary>
        public const int MinimumLeadDays = 2;

        /// <summary>Distancia entre as entradas quando o nivel nao declara os dias.</summary>
        public const int DefaultEntrySpacingDays = 8;

        /// <summary>
        /// Intervalo entre janelas por numero de rivais em guerra: 1 rival a cada 4
        /// dias, 2 a cada 3, 3 ou mais a cada 2. E o que faz mais rivais apertarem de
        /// verdade, e nao so variarem quem aparece.
        /// </summary>
        public static readonly int[] DefaultWindowIntervals = { 4, 3, 2 };

        private readonly List<ScheduledThreat> _pending = new List<ScheduledThreat>();
        private readonly List<ScheduledThreat> _resolved = new List<ScheduledThreat>();
        private readonly List<RivalCampaign> _campaigns = new List<RivalCampaign>();
        private readonly int[] _entryDays;
        private readonly int[] _windowIntervals;

        public ThreatClock(
            ThreatCurve curve = null,
            int firstThreatDay = 4,
            int intervalDays = 4,
            int leadDays = 3,
            IReadOnlyList<RivalDefinition> rivals = null,
            IReadOnlyList<int> entryDays = null,
            IReadOnlyList<int> windowIntervals = null)
        {
            Curve = curve ?? new ThreatCurve();
            FirstThreatDay = firstThreatDay;
            IntervalDays = Math.Max(1, intervalDays);
            LeadDays = Math.Max(MinimumLeadDays, leadDays);
            Rivals = rivals ?? new List<RivalDefinition>();

            _entryDays = new int[Rivals.Count];
            for (int i = 0; i < Rivals.Count; i++)
            {
                // O primeiro rival sempre abre a run: um nivel que o fizesse esperar
                // comecaria com dias sem ninguem em guerra.
                int declared = entryDays != null && i < entryDays.Count
                    ? entryDays[i]
                    : i * DefaultEntrySpacingDays;
                _entryDays[i] = i == 0 ? 1 : Math.Max(1, declared);
            }

            IReadOnlyList<int> intervals = windowIntervals != null && windowIntervals.Count > 0
                ? windowIntervals
                : DefaultWindowIntervals;
            _windowIntervals = new int[intervals.Count];
            for (int i = 0; i < intervals.Count; i++)
            {
                _windowIntervals[i] = Math.Max(1, intervals[i]);
            }
        }

        public ThreatCurve Curve { get; }

        /// <summary>Rivais da run, na ordem em que entram em guerra.</summary>
        public IReadOnlyList<RivalDefinition> Rivals { get; }

        /// <summary>Campanhas ja abertas na run, em ordem de entrada, derrotadas inclusive.</summary>
        public IReadOnlyList<RivalCampaign> Campaigns => _campaigns;

        public bool HasRivals => Rivals.Count > 0;

        /// <summary>Campanhas de rivais em guerra: entraram e ainda nao foram derrotadas.</summary>
        public List<RivalCampaign> ActiveCampaigns()
        {
            List<RivalCampaign> active = new List<RivalCampaign>();
            for (int i = 0; i < _campaigns.Count; i++)
            {
                if (!_campaigns[i].IsDefeated)
                {
                    active.Add(_campaigns[i]);
                }
            }

            return active;
        }

        /// <summary>Proximo rival a entrar em guerra, ou nulo se todos ja entraram.</summary>
        public RivalDefinition NextRivalToEnter => _campaigns.Count < Rivals.Count ? Rivals[_campaigns.Count] : null;

        /// <summary>
        /// Dia declarado da proxima entrada, ou -1. Pode chegar antes: se ninguem
        /// estiver em guerra, o proximo entra no dia seguinte (spec rival-kingdoms).
        /// </summary>
        public int NextEntryDay => _campaigns.Count < Rivals.Count ? _entryDays[_campaigns.Count] : -1;

        public int EntryDayOf(int rivalIndex) => _entryDays[rivalIndex];

        /// <summary>Todo rival do nivel entrou e caiu.</summary>
        public bool AllRivalsDefeated => HasRivals && _campaigns.Count == Rivals.Count && ActiveCampaigns().Count == 0;

        /// <summary>
        /// Coloca em guerra os rivais cujo dia chegou, somando-os aos que ja lutam.
        /// Se ninguem estiver em guerra, o proximo entra hoje sem esperar o seu dia:
        /// a run nao fica com dias mortos entre campanhas. Chamado no Inicio do Dia,
        /// antes de AnnounceDue, para que quem entra ja possa ser sorteado, e sempre
        /// com a antecedencia minima, o que garante o dia sem ataque antes do
        /// primeiro ataque dele.
        /// </summary>
        public List<RivalCampaign> AdmitDue(int currentDay)
        {
            List<RivalCampaign> admitted = new List<RivalCampaign>();

            while (_campaigns.Count < Rivals.Count)
            {
                bool dayArrived = _entryDays[_campaigns.Count] <= currentDay;
                bool nobodyAtWar = ActiveCampaigns().Count == 0;
                if (!dayArrived && !nobodyAtWar)
                {
                    break;
                }

                RivalCampaign campaign = new RivalCampaign(Rivals[_campaigns.Count], currentDay);
                _campaigns.Add(campaign);
                admitted.Add(campaign);
            }

            return admitted;
        }

        /// <summary>Intervalo ate a proxima janela com tantos rivais em guerra.</summary>
        public int WindowIntervalFor(int rivalsAtWar)
        {
            int index = Math.Max(1, rivalsAtWar) - 1;
            return _windowIntervals[Math.Min(index, _windowIntervals.Length - 1)];
        }

        public int FirstThreatDay { get; }

        public int IntervalDays { get; }

        public int LeadDays { get; }

        public IReadOnlyList<ScheduledThreat> Pending => _pending;

        public IReadOnlyList<ScheduledThreat> Resolved => _resolved;

        private int _nextArrivalDay = -1;

        /// <summary>
        /// Anuncia com antecedencia tudo que chega dentro da janela. Chamado no
        /// Inicio do Dia, antes do Planejamento, para que o jogador sempre planeje
        /// com a fila completa a vista.
        /// </summary>
        public List<ScheduledThreat> AnnounceDue(int currentDay, int ownedTiles, IRandomSource random)
        {
            List<ScheduledThreat> announced = new List<ScheduledThreat>();

            if (_nextArrivalDay < 0)
            {
                _nextArrivalDay = FirstThreatDay;
            }

            while (_nextArrivalDay - currentDay <= LeadDays)
            {
                List<RivalCampaign> active = ActiveCampaigns();

                // Sem ninguem em guerra, nada e agendado. Sem esta parada a run cairia
                // na ameaca anonima, que so existe para quem nao tem rival nenhum.
                if (HasRivals && active.Count == 0)
                {
                    break;
                }

                if (_nextArrivalDay < currentDay + MinimumLeadDays)
                {
                    // Nunca agenda algo que nao caberia na antecedencia minima;
                    // empurra para o proximo ciclo em vez de encurtar o aviso.
                    _nextArrivalDay = currentDay + MinimumLeadDays;
                    continue;
                }

                // Um ataque adiado pelo clima pode ter ocupado o dia da janela. Dois
                // ataques no mesmo dia derrubariam a run sem erro do jogador.
                if (IsOccupied(_nextArrivalDay, null))
                {
                    _nextArrivalDay++;
                    continue;
                }

                ScheduledThreat threat;
                if (HasRivals)
                {
                    // O sorteio acontece aqui, no anuncio, e nunca na resolucao: a
                    // aleatoriedade esta em quem vem, nao em quando se fica sabendo.
                    RivalCampaign attacker = active[random.NextInt(0, active.Count)];
                    threat = new ScheduledThreat(
                        KindFor(attacker.Rival.Axis),
                        _nextArrivalDay,
                        attacker.LaunchNextAttack(),
                        currentDay,
                        attacker);
                    _nextArrivalDay += WindowIntervalFor(active.Count);
                }
                else
                {
                    threat = new ScheduledThreat(
                        PickKind(random), _nextArrivalDay, Curve.ForceFor(_nextArrivalDay, ownedTiles), currentDay);
                    _nextArrivalDay += IntervalDays;
                }

                _pending.Add(threat);
                announced.Add(threat);
            }

            _pending.Sort((a, b) => a.ArrivalDay.CompareTo(b.ArrivalDay));
            return announced;
        }

        private bool IsOccupied(int day, ScheduledThreat except)
        {
            for (int i = 0; i < _pending.Count; i++)
            {
                if (_pending[i] != except && _pending[i].ArrivalDay == day)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// O tipo do ataque de um rival vem da identidade, nao do sorteio: e ela que
        /// o jogador conhece desde a declaracao de guerra.
        /// </summary>
        private static ThreatKind KindFor(PressureAxis axis)
        {
            switch (axis)
            {
                case PressureAxis.Integrity:
                    return ThreatKind.Fire;
                case PressureAxis.Population:
                case PressureAxis.Resource:
                    return ThreatKind.Raid;
                default:
                    return ThreatKind.Horde;
            }
        }

        private static ThreatKind PickKind(IRandomSource random)
        {
            int roll = random.NextInt(0, 10);
            if (roll < 6)
            {
                return ThreatKind.Horde;
            }

            return roll < 8 ? ThreatKind.Fire : ThreatKind.StructuralCollapse;
        }

        /// <summary>Ameacas que chegam hoje. So sai daqui o que ja foi anunciado antes.</summary>
        public List<ScheduledThreat> DueOn(int day)
        {
            List<ScheduledThreat> due = new List<ScheduledThreat>();
            for (int i = 0; i < _pending.Count; i++)
            {
                if (_pending[i].ArrivalDay == day)
                {
                    due.Add(_pending[i]);
                }
            }

            return due;
        }

        public void MarkResolved(ScheduledThreat threat)
        {
            _pending.Remove(threat);
            _resolved.Add(threat);
        }

        public bool IsPending(ScheduledThreat threat) => _pending.Contains(threat);

        /// <summary>
        /// Conta o desfecho de um ataque ja resolvido na campanha a que ele pertence.
        /// Se isso derrota o rival, limpa da fila o que restava da campanha dele e
        /// devolve a campanha derrotada; senao devolve nulo.
        /// </summary>
        public RivalCampaign RecordOutcome(ScheduledThreat threat, bool repelled)
        {
            RivalCampaign campaign = threat?.Campaign;
            if (campaign == null || campaign.IsDefeated)
            {
                return null;
            }

            campaign.RecordOutcome(repelled);
            if (!campaign.IsDefeated)
            {
                return null;
            }

            // Rival derrotado nao ataca mais: o que ja estava anunciado sai da fila,
            // e nao fica pendurado para resolver contra quem ja perdeu a guerra.
            _pending.RemoveAll(pending => pending.Campaign == campaign);
            return campaign;
        }

        /// <summary>
        /// Reforca ou enfraquece uma ameaca ja anunciada. E o unico jeito de um
        /// evento tocar no relogio: nao pode agendar nem cancelar (spec day-events).
        /// </summary>
        public bool ModifyForce(ScheduledThreat threat, int delta)
        {
            if (threat == null || !_pending.Contains(threat))
            {
                return false;
            }

            threat.Force = Math.Max(1, threat.Force + delta);
            return true;
        }

        /// <summary>
        /// Adia uma ameaca ja anunciada. So adia, nunca antecipa: encurtar o aviso
        /// quebraria a antecedência minima que a spec garante. Se o dia de destino ja
        /// tem ataque, segue para o primeiro dia livre, para nao empilhar dois
        /// ataques. Devolve os dias efetivamente aplicados.
        /// </summary>
        public int Delay(ScheduledThreat threat, int days)
        {
            if (threat == null || days <= 0 || !_pending.Contains(threat))
            {
                return 0;
            }

            int original = threat.ArrivalDay;
            int target = original + days;
            while (IsOccupied(target, threat))
            {
                target++;
            }

            threat.ArrivalDay = target;
            _pending.Sort((a, b) => a.ArrivalDay.CompareTo(b.ArrivalDay));
            return target - original;
        }

        public ScheduledThreat NextThreat(int currentDay)
        {
            ScheduledThreat next = null;
            for (int i = 0; i < _pending.Count; i++)
            {
                if (_pending[i].ArrivalDay < currentDay)
                {
                    continue;
                }

                if (next == null || _pending[i].ArrivalDay < next.ArrivalDay)
                {
                    next = _pending[i];
                }
            }

            return next;
        }
    }
}
