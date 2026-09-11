using System;
using System.Collections.Generic;

namespace KingdomCollapse.Core
{
    /// <summary>
    /// Estado da sucessao de rivais numa run: quem e o vigente, quantos ataques ele
    /// ja teve repelidos, e quando o proximo ataque da campanha deve ser agendado.
    /// O relogio de ameaca continua sendo a fila visivel (design D3); esta classe so
    /// decide QUANDO alimenta-lo, no lugar da curva anonima antiga.
    /// </summary>
    public sealed class RivalCampaignState
    {
        private readonly List<RivalDefinition> _roster;
        private readonly int _intervalDays;
        private readonly int _restDays;
        private readonly int _leadDays;

        private int _rosterIndex = -1;
        private int _repelsAchieved;
        private int _nextAttackDay = -1;
        private int _campaignStartDay;
        private bool _pendingDeclaration;

        public RivalCampaignState(
            List<RivalDefinition> roster, int intervalDays = 4, int restDays = 3, int leadDays = 3)
        {
            _roster = roster ?? new List<RivalDefinition>();
            _intervalDays = Math.Max(1, intervalDays);
            _restDays = Math.Max(1, restDays);
            _leadDays = Math.Max(ThreatClock.MinimumLeadDays, leadDays);
        }

        public bool HasRoster => _roster.Count > 0;

        public RivalDefinition Current =>
            _rosterIndex >= 0 && _rosterIndex < _roster.Count ? _roster[_rosterIndex] : null;

        /// <summary>Rival que acabou de assumir neste Inicio do Dia, se for o caso.
        /// Consumido por RunEngine para emitir RivalDeclaredEvent uma vez so.</summary>
        public RivalDefinition JustDeclared { get; private set; }

        public int RepelsAchieved => _repelsAchieved;

        public int RepelsNeeded => Current?.AttackCount ?? 0;

        /// <summary>Todo o roster foi derrotado: a run termina em Vitoria.</summary>
        public bool AllDefeated => HasRoster && _rosterIndex >= _roster.Count;

        /// <summary>
        /// Chamado uma vez por Inicio do Dia. Declara o primeiro rival se a campanha
        /// ainda nao comecou, e agenda o proximo ataque quando a janela de
        /// antecedencia permitir. Devolve o ataque agendado agora, ou nulo.
        /// </summary>
        public ScheduledThreat AnnounceIfDue(ThreatClock clock, int currentDay)
        {
            JustDeclared = null;

            if (!HasRoster || AllDefeated)
            {
                return null;
            }

            if (_rosterIndex < 0)
            {
                _rosterIndex = 0;
                _repelsAchieved = 0;
                _nextAttackDay = currentDay + _leadDays;
                _campaignStartDay = currentDay;
                JustDeclared = Current;
            }
            else if (_pendingDeclaration)
            {
                _pendingDeclaration = false;
                JustDeclared = Current;
            }

            if (_nextAttackDay - currentDay > _leadDays)
            {
                return null;
            }

            // Forca e sempre lida na curva do rival vigente a partir do dia 1 dele,
            // nao do dia absoluto da run (task 11.2): sem isso, o segundo ou terceiro
            // rival da escada herda o "dia" do jogo inteiro e a curva explode antes
            // do primeiro ataque dele, tornando a campanha impossivel de vencer.
            int dayInCampaign = Math.Max(1, _nextAttackDay - _campaignStartDay + 1);
            int force = Current.CampaignCurve.ForceFor(dayInCampaign);
            ScheduledThreat scheduled = clock.Schedule(_nextAttackDay, force, currentDay, Current.Identity, Current.Id);
            _nextAttackDay += _intervalDays;
            return scheduled;
        }

        /// <summary>
        /// Registra a resolucao de um ataque desta campanha. Devolve true quando essa
        /// resolucao derrotou o rival vigente — quem chama emite RivalDefeatedEvent
        /// (e RunVictoryEvent, se era o ultimo) e confere AllDefeated depois.
        /// </summary>
        public bool RegisterResolution(ThreatClock clock, ScheduledThreat threat, bool repelled, int currentDay)
        {
            if (Current == null || threat.RivalId != Current.Id || !repelled)
            {
                return false;
            }

            _repelsAchieved++;
            if (_repelsAchieved < RepelsNeeded)
            {
                return false;
            }

            // Derrotado: limpa o resto da fila desta campanha (nao sobra ataque de
            // quem ja perdeu a guerra) e abre o respiro antes do proximo declarar
            // guerra (spec rival-kingdoms — "Ha respiro entre campanhas").
            clock.ClearPendingForRival(Current.Id);
            _rosterIndex++;
            _repelsAchieved = 0;

            if (AllDefeated)
            {
                _nextAttackDay = -1;
            }
            else
            {
                _nextAttackDay = currentDay + _restDays;
                _campaignStartDay = _nextAttackDay;
                _pendingDeclaration = true;
            }

            return true;
        }
    }
}
