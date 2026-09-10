using System;

namespace KingdomCollapse.Core
{
    /// <summary>
    /// Onde cai o excedente de um ataque que passou pela defesa (design D4).
    ///
    /// Undeclared existe e vale zero de proposito: um asset recem-criado no Inspector
    /// nasce com esse valor, e a validacao do catalogo o reprova. Se o padrao fosse
    /// Territory, um rival esquecido viraria rival de assalto sem ninguem decidir isso.
    /// </summary>
    public enum PressureAxis
    {
        Undeclared = 0,

        /// <summary>O excedente arrasa celulas a partir da borda; o resto fere a base.</summary>
        Territory = 1,

        /// <summary>O excedente fere a base direto, sem tocar o territorio.</summary>
        Integrity = 2,

        /// <summary>O excedente mata gente, sem tocar o territorio.</summary>
        Population = 3,

        /// <summary>O excedente leva um recurso especifico, declarado na identidade.</summary>
        Resource = 4
    }

    /// <summary>
    /// O tipo de pressao que um rival exerce. E o que faz rivais diferentes exigirem
    /// respostas diferentes: sem isso, a resposta a qualquer rival seria mais defesa.
    /// </summary>
    public sealed class RivalIdentity
    {
        public const double DefaultRaidFraction = 0.5;

        public RivalIdentity(
            string id,
            string displayName,
            string description,
            PressureAxis axis,
            ResourceKind? pressuredResource = null,
            double raidFraction = DefaultRaidFraction,
            double lossPerForce = 1.0)
        {
            Id = id;
            DisplayName = displayName;
            Description = description ?? string.Empty;
            Axis = axis;
            PressuredResource = axis == PressureAxis.Resource ? pressuredResource : null;
            RaidFraction = IsCounteredByDefense ? 0 : Math.Max(0, Math.Min(1, raidFraction));
            LossPerForce = Math.Max(0, lossPerForce);
        }

        public string Id { get; }

        public string DisplayName { get; }

        /// <summary>
        /// O que esperar deste rival. A spec exige que o jogador saiba o tipo de
        /// pressao antes do primeiro ataque, e e daqui que esse texto sai.
        /// </summary>
        public string Description { get; }

        public PressureAxis Axis { get; }

        /// <summary>Recurso levado pelo excedente. So existe quando o eixo e Resource.</summary>
        public ResourceKind? PressuredResource { get; }

        public bool DeclaresAxis => Axis != PressureAxis.Undeclared;

        /// <summary>
        /// Se a defesa guarnecida e a resposta a este rival. Territorio e integridade
        /// sao pressao de assalto; populacao e recurso sao pressao economica, respondida
        /// com estoque e redundancia (spec rival-kingdoms).
        /// </summary>
        public bool IsCounteredByDefense => Axis == PressureAxis.Territory || Axis == PressureAxis.Integrity;

        /// <summary>
        /// Fracao da forca que o rival economico leva mesmo quando o ataque e
        /// repelido. Zero no rival de assalto. E o que impede a muralha de ser
        /// resposta suficiente: a defesa decide se o jogador vence a guerra, o
        /// estoque decide se ele aguenta (design D4).
        /// </summary>
        public double RaidFraction { get; }

        /// <summary>
        /// Quanto de recurso cada ponto de forca leva, no rival economico. Existe
        /// porque forca e recurso nao tem a mesma escala: 12 de forca como 12 de
        /// comida pode ser pouco, e como 12 de populacao e um massacre.
        /// </summary>
        public double LossPerForce { get; }

        /// <summary>Recurso que o excedente atinge no rival economico.</summary>
        public ResourceKind? LostResource =>
            Axis == PressureAxis.Population ? ResourceKind.Population : PressuredResource;

        public override string ToString() => Id;
    }

    /// <summary>
    /// Forca de cada ataque da campanha, pelo indice do ataque e nao pelo dia.
    ///
    /// Nao tem teto nem fim: a campanha exige um numero de ataques repelidos, e um
    /// jogador que falha continua recebendo ataques. Se a curva parasse no ultimo
    /// ataque previsto, sobreviver sem repelir viraria estrategia (spec run-loop,
    /// "Escalada garante o fim").
    /// </summary>
    public sealed class RivalCampaignCurve
    {
        public RivalCampaignCurve(int firstForce, double growthPerAttack, double exponent = 1.0)
        {
            FirstForce = Math.Max(1, firstForce);
            GrowthPerAttack = growthPerAttack;
            Exponent = exponent;
        }

        public int FirstForce { get; }

        public double GrowthPerAttack { get; }

        public double Exponent { get; }

        /// <summary>Forca do ataque de indice dado, contando a partir de zero.</summary>
        public int ForceFor(int attackIndex)
        {
            int index = Math.Max(0, attackIndex);
            double growth = GrowthPerAttack * Math.Pow(index, Exponent);
            return Math.Max(1, FirstForce + (int)Math.Round(growth, MidpointRounding.AwayFromZero));
        }
    }

    /// <summary>
    /// Um reino rival nomeado (design D3). E dado, autorado no Inspector como o resto
    /// do conteudo; a campanha que ele trava e derivada daqui e preenche o relogio.
    /// </summary>
    public sealed class RivalDefinition
    {
        public RivalDefinition(
            string id,
            string displayName,
            RivalIdentity identity,
            int attacksToDefeat,
            RivalCampaignCurve campaign)
        {
            Id = id;
            DisplayName = displayName;
            Identity = identity;
            AttacksToDefeat = attacksToDefeat;
            Campaign = campaign;
        }

        public string Id { get; }

        public string DisplayName { get; }

        public RivalIdentity Identity { get; }

        /// <summary>
        /// Ataques que o jogador precisa repelir para derrotar o rival. E conhecido
        /// desde a declaracao de guerra e nao muda durante a campanha.
        /// </summary>
        public int AttacksToDefeat { get; }

        public RivalCampaignCurve Campaign { get; }

        /// <summary>
        /// Forca do ultimo ataque que o jogador precisa repelir numa campanha sem
        /// falhas. E a medida de quanto a campanha exige do reino.
        /// </summary>
        public int PeakForce => Campaign != null ? Campaign.ForceFor(Math.Max(0, AttacksToDefeat - 1)) : 0;

        /// <summary>Eixo pressionado, lido da identidade. Undeclared se nao houver uma.</summary>
        public PressureAxis Axis => Identity != null ? Identity.Axis : PressureAxis.Undeclared;

        public override string ToString() => Id;
    }
}
