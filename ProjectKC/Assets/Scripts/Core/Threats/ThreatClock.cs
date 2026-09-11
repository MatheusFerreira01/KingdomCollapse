using System;
using System.Collections.Generic;

namespace KingdomCollapse.Core
{
    public enum ThreatKind
    {
        Horde = 0,
        Fire = 1,
        StructuralCollapse = 2
    }

    /// <summary>
    /// Ameaca ja anunciada. Guarda o dia em que foi anunciada para que a regra de
    /// antecedencia minima possa ser verificada, e nao apenas prometida.
    /// </summary>
    public sealed class ScheduledThreat
    {
        public ScheduledThreat(
            ThreatKind kind, int arrivalDay, int force, int announcedOnDay,
            RivalIdentity identity = null, string rivalId = null)
        {
            Kind = kind;
            ArrivalDay = arrivalDay;
            Force = force;
            AnnouncedOnDay = announcedOnDay;
            Identity = identity;
            RivalId = rivalId;
        }

        public ThreatKind Kind { get; }

        public int ArrivalDay { get; internal set; }

        /// <summary>Forca prevista. Um evento pode altera-la antes do dia de chegada.</summary>
        public int Force { get; internal set; }

        public int AnnouncedOnDay { get; }

        /// <summary>
        /// Identidade do rival dono deste ataque, quando veio de uma campanha (design
        /// D3). Nulo para ameaca anonima da curva antiga — CombatResolver cai no
        /// comportamento de hoje nesse caso.
        /// </summary>
        public RivalIdentity Identity { get; }

        /// <summary>Id do <see cref="RivalDefinition"/> dono, para limpar a campanha ao derrotar.</summary>
        public string RivalId { get; }

        public int LeadTime => ArrivalDay - AnnouncedOnDay;

        public int DaysUntil(int currentDay) => ArrivalDay - currentDay;

        public override string ToString()
        {
            return Kind + " dia " + ArrivalDay + " forca " + Force;
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
    /// </summary>
    public sealed class ThreatClock
    {
        /// <summary>Antecedencia minima exigida pela spec.</summary>
        public const int MinimumLeadDays = 2;

        private readonly List<ScheduledThreat> _pending = new List<ScheduledThreat>();
        private readonly List<ScheduledThreat> _resolved = new List<ScheduledThreat>();

        public ThreatClock(ThreatCurve curve = null, int firstThreatDay = 4, int intervalDays = 4, int leadDays = 3)
        {
            Curve = curve ?? new ThreatCurve();
            FirstThreatDay = firstThreatDay;
            IntervalDays = Math.Max(1, intervalDays);
            LeadDays = Math.Max(MinimumLeadDays, leadDays);
        }

        public ThreatCurve Curve { get; }

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
                if (_nextArrivalDay < currentDay + MinimumLeadDays)
                {
                    // Nunca agenda algo que nao caberia na antecedencia minima;
                    // empurra para o proximo ciclo em vez de encurtar o aviso.
                    _nextArrivalDay = currentDay + MinimumLeadDays;
                    continue;
                }

                int force = Curve.ForceFor(_nextArrivalDay, ownedTiles);
                ThreatKind kind = PickKind(random);
                ScheduledThreat threat = new ScheduledThreat(kind, _nextArrivalDay, force, currentDay);
                _pending.Add(threat);
                announced.Add(threat);
                _nextArrivalDay += IntervalDays;
            }

            _pending.Sort((a, b) => a.ArrivalDay.CompareTo(b.ArrivalDay));
            return announced;
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

        /// <summary>
        /// Agenda um ataque vindo de uma campanha de rival, em vez da curva anonima
        /// interna (design D3 — "so troca a origem"). Respeita a mesma antecedencia
        /// minima que a geracao interna.
        /// </summary>
        public ScheduledThreat Schedule(int arrivalDay, int force, int currentDay, RivalIdentity identity, string rivalId)
        {
            int clampedArrival = Math.Max(arrivalDay, currentDay + MinimumLeadDays);
            ScheduledThreat threat = new ScheduledThreat(
                ThreatKind.Horde, clampedArrival, Math.Max(1, force), currentDay, identity, rivalId);

            _pending.Add(threat);
            _pending.Sort((a, b) => a.ArrivalDay.CompareTo(b.ArrivalDay));
            return threat;
        }

        /// <summary>
        /// Remove da fila todo ataque ainda pendente de um rival — chamado ao
        /// derrota-lo, pra nao sobrar ataque de quem ja perdeu a guerra (spec
        /// rival-kingdoms — "derrotar limpa os ataques restantes").
        /// </summary>
        public List<ScheduledThreat> ClearPendingForRival(string rivalId)
        {
            List<ScheduledThreat> removed = new List<ScheduledThreat>();
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                if (_pending[i].RivalId == rivalId)
                {
                    removed.Add(_pending[i]);
                    _pending.RemoveAt(i);
                }
            }

            return removed;
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
