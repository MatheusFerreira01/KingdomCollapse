using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using KingdomCollapse.Core;
using KingdomCollapse.Game;
using UnityEditor;
using UnityEngine;

namespace KingdomCollapse.EditorTools
{
    /// <summary>
    /// Roda milhares de runs com o conteudo real do GameDatabase e reporta a
    /// distribuicao do dia de Colapso.
    ///
    /// Existe porque o harness de linha de comando so consegue montar catalogo em
    /// codigo: o conteudo de verdade vive em ScriptableObjects, que so o Editor sabe
    /// carregar. Sem esta janela, calibrar a curva seria chute.
    /// </summary>
    public sealed class BalanceSimulatorWindow : EditorWindow
    {
        private GameDatabase _database;
        private string _raceId = "humans";
        private int _runs = 1000;
        private bool _builtIn = true;
        private int _level = 1;
        private float _expansionReserve = 1.5f;

        private string _report = string.Empty;
        private Vector2 _scroll;

        /// <summary>Janela alvo de duracao da run, definida na spec run-loop.</summary>
        private const int TargetMinDay = 25;
        private const int TargetMaxDay = 35;

        [MenuItem("Kingdom Collapse/Simulador de Balanceamento")]
        public static void Open()
        {
            BalanceSimulatorWindow window = GetWindow<BalanceSimulatorWindow>();
            window.titleContent = new GUIContent("Balanceamento");
            window.minSize = new Vector2(460f, 380f);
            window.Show();
        }

        private void OnEnable()
        {
            if (_database == null)
            {
                _database = FindFirstDatabase();
            }
        }

        private static GameDatabase FindFirstDatabase()
        {
            string[] guids = AssetDatabase.FindAssets("t:GameDatabase");
            if (guids.Length == 0)
            {
                return null;
            }

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<GameDatabase>(path);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Simulacao em lote", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Roda N runs com uma IA heuristica usando o conteudo do GameDatabase. " +
                "Alvo da spec: Colapso tipico entre os dias " + TargetMinDay + " e " + TargetMaxDay + ".",
                MessageType.None);

            _builtIn = EditorGUILayout.Toggle(
                new GUIContent("Conteudo embutido", "Mede o StarterContent, que e o que o jogo usa por padrao."), _builtIn);
            if (_builtIn)
            {
                _level = Mathf.Clamp(EditorGUILayout.IntField("Nivel", _level), 1, 9);
            }

            _database = (GameDatabase)EditorGUILayout.ObjectField(
                "Base de conteudo", _database, typeof(GameDatabase), false);

            _raceId = EditorGUILayout.TextField("Raca", _raceId);
            _runs = Mathf.Clamp(EditorGUILayout.IntField("Runs", _runs), 1, 20000);

            _expansionReserve = EditorGUILayout.Slider(
                new GUIContent("Reserva do bot", "Quantas vezes o custo da celula o bot guarda antes de expandir."),
                _expansionReserve, 1f, 8f);

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(_database == null && !_builtIn))
            {
                if (GUILayout.Button("Simular", GUILayout.Height(30)))
                {
                    Run();
                }
            }

            if (_database == null && !_builtIn)
            {
                EditorGUILayout.HelpBox("Arraste um GameDatabase para simular.", MessageType.Warning);
            }

            EditorGUILayout.Space();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.TextArea(_report, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        private void Run()
        {
            if (_builtIn)
            {
                RunBuiltIn();
                return;
            }

            ContentCatalog catalog = _database.BuildCatalog();

            if (!catalog.Races.ContainsKey(_raceId))
            {
                _report = "Raca '" + _raceId + "' nao existe no GameDatabase.";
                return;
            }

            GreedyPolicy policy = new GreedyPolicy(_expansionReserve);
            Stopwatch stopwatch = Stopwatch.StartNew();

            SimulationReport report = SimulationHarness.RunBatch(
                _runs,
                seed => _database.CreateSetup(_raceId, seed + 1),
                catalog,
                policy);

            stopwatch.Stop();
            _report = Describe(report, stopwatch.Elapsed.TotalSeconds, catalog);
        }

        /// <summary>
        /// Mede o jogo como ele e jogado: conteudo embutido, nivel escolhido, e o bot
        /// que sabe plantar, abrigar e guarnecer. A metrica e a fracao de vitorias.
        /// </summary>
        private void RunBuiltIn()
        {
            ContentCatalog catalog = StarterContent.BuildCatalog();
            Stopwatch stopwatch = Stopwatch.StartNew();

            SimulationReport report = SimulationHarness.RunBatch(
                _runs,
                seed => StarterContent.CreateSetup(seed + 1, _level),
                catalog,
                new StewardPolicy());

            stopwatch.Stop();

            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Conteudo embutido, nivel " + _level + ", " + report.Count + " runs.");
            builder.AppendLine("Vitorias: " + (report.WinFraction * 100).ToString("0.0") + "%   (alvo do nivel 1 para o bot: 40% a 65%)");
            builder.AppendLine("Dia mediano da vitoria: " + report.MedianWinDay);
            builder.AppendLine("Dia mediano de qualquer desfecho: " + report.Median +
                               "  (p10 " + report.Percentile(0.10) + ", p90 " + report.Percentile(0.90) + ")");
            builder.AppendLine("Tempo: " + stopwatch.Elapsed.TotalSeconds.ToString("0.00") + "s");
            builder.AppendLine();

            List<CatalogViolation> violations = StarterContent.Validate();
            builder.AppendLine(violations.Count == 0 ? "Catalogo valido." : "VIOLACOES DE CATALOGO:");
            for (int i = 0; i < violations.Count; i++)
            {
                builder.AppendLine("  * " + violations[i]);
            }

            builder.AppendLine();
            builder.AppendLine(Histogram(report));
            _report = builder.ToString();
        }

        private string Describe(SimulationReport report, double seconds, ContentCatalog catalog)
        {
            StringBuilder builder = new StringBuilder();

            builder.AppendLine(report.Describe());
            builder.AppendLine("Tempo: " + seconds.ToString("0.00") + "s");
            builder.AppendLine();

            builder.AppendLine("Conteudo em jogo:");
            builder.AppendLine("  edificios liberados: " + catalog.StartingBuildings.Count +
                               " de " + catalog.Buildings.Count);
            builder.AppendLine("  cartas liberadas: " + catalog.StartingCards.Count +
                               " de " + catalog.Cards.Count);
            builder.AppendLine("  eventos liberados: " + catalog.StartingEvents.Count +
                               " de " + catalog.Events.Count);
            builder.AppendLine();

            builder.AppendLine(Histogram(report));
            builder.AppendLine();
            builder.AppendLine(Verdict(report));

            return builder.ToString();
        }

        /// <summary>Histograma em texto. Ler a forma da distribuicao importa mais que a mediana.</summary>
        private static string Histogram(SimulationReport report)
        {
            if (report.Count == 0)
            {
                return "sem dados";
            }

            const int BucketSize = 5;
            Dictionary<int, int> buckets = new Dictionary<int, int>();
            int tallest = 0;

            for (int i = 0; i < report.SortedDays.Count; i++)
            {
                int bucket = report.SortedDays[i] / BucketSize * BucketSize;
                buckets.TryGetValue(bucket, out int count);
                count++;
                buckets[bucket] = count;
                tallest = Mathf.Max(tallest, count);
            }

            List<int> keys = new List<int>(buckets.Keys);
            keys.Sort();

            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Dia de Colapso (faixas de " + BucketSize + " dias):");

            for (int i = 0; i < keys.Count; i++)
            {
                int bucket = keys[i];
                int count = buckets[bucket];
                int bars = tallest == 0 ? 0 : Mathf.RoundToInt(count / (float)tallest * 40f);

                string label = (bucket + "-" + (bucket + BucketSize - 1)).PadLeft(7);
                bool inTarget = bucket + BucketSize - 1 >= TargetMinDay && bucket <= TargetMaxDay;

                builder.AppendLine(label + " " + new string('#', bars).PadRight(41) +
                                   count + (inTarget ? "  <= alvo" : string.Empty));
            }

            return builder.ToString();
        }

        private static string Verdict(SimulationReport report)
        {
            int median = report.Median;

            if (median < TargetMinDay)
            {
                return "VEREDITO: runs curtas demais (mediana " + median + ").\n" +
                       "Afrouxe a economia antes da ameaca: baixe o custo base da celula ou " +
                       "suba a producao dos edificios. Mexer so na curva de ameaca costuma " +
                       "esconder o problema real, que e o jogador nao ter o que fazer nos " +
                       "primeiros dias.";
            }

            if (median > TargetMaxDay)
            {
                return "VEREDITO: runs longas demais (mediana " + median + ").\n" +
                       "Suba a escalada da ameaca por dia, ou o termo por celula se a " +
                       "expansao estiver saindo barata demais.";
            }

            return "VEREDITO: mediana " + median + " dentro do alvo. " +
                   "Confira tambem a dispersao: uma distribuicao muito estreita significa " +
                   "que as decisoes do jogador nao estao mudando o desfecho.";
        }
    }
}
