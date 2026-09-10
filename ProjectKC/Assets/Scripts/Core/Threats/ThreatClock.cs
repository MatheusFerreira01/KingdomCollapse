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
    /// Com rivais, a fila e a campanha do rival vigente (design D3): o relogio
    /// continua cuidando de quando e com que antecedencia, e a campanha passa a dizer
    /// com que forca e de quem. Sem rivais, cai na curva anonima por dia, que so
    /// existe enquanto o conteudo ainda nao declara rivais.
    /// </summary>
    public sealed class ThreatClock
    {
        /// <summary>Antecedencia minima exigida pela spec.</summary>
        public const int MinimumLeadDays = 2;

        private readonly List<ScheduledThreat> _pending = new List<ScheduledThreat>();
        private readonly List<ScheduledThreat> _resolved = new List<ScheduledThreat>();

        public ThreatClock(
            ThreatCurve curve = null,
            int firstThreatDay = 4,
            int intervalDays = 4,
            int leadDays = 3,
            IReadOnlyList<RivalDefinition> rivals = null,
            int breatherDays = 4)
        {
            Curve = curve ?? new ThreatCurve();
            FirstThreatDay = firstThreatDay;
            IntervalDays = Math.Max(1, intervalDays);
            LeadDays = Math.Max(MinimumLeadDays, leadDays);
            Rivals = rivals ?? new List<RivalDefinition>();

            // Ao menos um dia sem ataque entre campanhas e exigencia da spec, e nao
            // gosto de autoria: sem ele a derrota de um rival e a chegada do proximo
            // se fundem, e a vitoria parcial nao tem onde ser sentida.
            BreatherDays = Math.Max(1, breatherDays);

            if (Rivals.Count > 0)
            {
                CurrentCampaign = new RivalCampaign(Rivals[0]);
                _campaigns.Add(CurrentCampaign);
            }
        }

        private readonly List<RivalCampaign> _campaigns = new List<RivalCampaign>();

        public ThreatCurve Curve { get; }

        /// <summary>Rivais da run, na ordem em que declaram guerra.</summary>
        public IReadOnlyList<RivalDefinition> Rivals { get; }

        /// <summary>Dias sem ataque entre a derrota de um rival e o primeiro ataque do proximo.</summary>
        public int BreatherDays { get; }

        /// <summary>Campanha do rival vigente. Nula so na run sem rivais.</summary>
        public RivalCampaign CurrentCampaign { get; private set; }

        /// <summary>Campanhas ja travadas na run, a vigente inclusive, em ordem.</summary>
        public IReadOnlyList<RivalCampaign> Campaigns => _campaigns;

        public bool HasRivals => Rivals.Count > 0;

        /// <summary>Rival vigente derrotado e nenhum outro por vir.</summary>
        public bool AllRivalsDefeated =>
            HasRivals && _campaigns.Count == Rivals.Count && CurrentCampaign.IsDefeated;

        /// <summary>
        /// Passa a guerra para o proximo rival depois de uma derrota. O primeiro
        /// ataque dele so chega depois do respiro, e ainda assim com a antecedencia
        /// minima, porque quem agenda continua sendo AnnounceDue. Devolve a nova
        /// campanha, ou nulo se nao ha derrota a suceder ou nenhum rival restante.
        /// </summary>
        public RivalCampaign DeclareNextWar(int defeatDay)
        {
            if (CurrentCampaign == null || !CurrentCampaign.IsDefeated || _campaigns.Count >= Rivals.Count)
            {
                return null;
            }

            CurrentCampaign = new RivalCampaign(Rivals[_campaigns.Count]);
            _campaigns.Add(CurrentCampaign);
            _nextArrivalDay = defeatDay + BreatherDays + 1;
            return CurrentCampaign;
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
                // Rival derrotado nao agenda mais nada. Sem esta parada a run cairia
                // na ameaca anonima, que so existe para quem nao tem rival nenhum.
                if (HasRivals && (CurrentCampaign == null || CurrentCampaign.IsDefeated))
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

                ScheduledThreat threat = CurrentCampaign != null
                    ? new ScheduledThreat(
                        KindFor(CurrentCampaign.Rival.Axis),
                        _nextArrivalDay,
                        CurrentCampaign.LaunchNextAttack(),
                        currentDay,
                        CurrentCampaign)
                    : new ScheduledThreat(
                        PickKind(random), _nextArrivalDay, Curve.ForceFor(_nextArrivalDay, ownedTiles), currentDay);

                _pending.Add(threat);
                announced.Add(threat);
                _nextArrivalDay += IntervalDays;
            }

            _pending.Sort((a, b) => a.ArrivalDay.CompareTo(b.ArrivalDay));
            return announced;
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
        /// quebraria a antecedência minima que a spec garante. Devolve os dias
        /// efetivamente aplicados.
        /// </summary>
        public int Delay(ScheduledThreat threat, int days)
        {
            if (threat == null || days <= 0 || !_pending.Contains(threat))
            {
                return 0;
            }

            threat.ArrivalDay += days;
            _pending.Sort((a, b) => a.ArrivalDay.CompareTo(b.ArrivalDay));
            return days;
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
