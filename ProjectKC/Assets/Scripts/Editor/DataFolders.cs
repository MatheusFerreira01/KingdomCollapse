using System.Collections.Generic;
using KingdomCollapse.Game;
using UnityEditor;
using UnityEngine;

namespace KingdomCollapse.EditorTools
{
    /// <summary>
    /// Onde cada tipo de conteudo mora. Uma pasta so, com dezenas de assets soltos,
    /// esconde justamente os dois arquivos que importam — a base de conteudo e o
    /// balanceamento — no meio do conteudo em si.
    /// </summary>
    public static class DataFolders
    {
        public const string Root = "Assets/Data";

        public const string Buildings = Root + "/Edificios";
        public const string Cards = Root + "/Cartas";
        public const string Events = Root + "/Eventos";
        public const string Weather = Root + "/Clima";
        public const string Races = Root + "/Racas";
        public const string Meta = Root + "/Meta";

        /// <summary>
        /// GameDatabase e Balance ficam na raiz de propósito: sao os pontos de entrada,
        /// e enterra-los numa subpasta os tornaria tao dificeis de achar quanto antes.
        /// </summary>
        public static readonly string[] All = { Buildings, Cards, Events, Weather, Races, Meta };

        public static void EnsureAll()
        {
            if (!AssetDatabase.IsValidFolder(Root))
            {
                AssetDatabase.CreateFolder("Assets", "Data");
            }

            for (int i = 0; i < All.Length; i++)
            {
                if (AssetDatabase.IsValidFolder(All[i]))
                {
                    continue;
                }

                string leaf = All[i].Substring(All[i].LastIndexOf('/') + 1);
                AssetDatabase.CreateFolder(Root, leaf);
            }
        }

        public static string For(Object asset)
        {
            if (asset is BuildingAsset)
            {
                return Buildings;
            }

            if (asset is CardAsset)
            {
                return Cards;
            }

            if (asset is EventAsset)
            {
                return Events;
            }

            if (asset is WeatherAsset)
            {
                return Weather;
            }

            if (asset is RaceAsset)
            {
                return Races;
            }

            if (asset is MetaNodeAsset)
            {
                return Meta;
            }

            return null;
        }
    }

    /// <summary>
    /// Move os assets de conteudo para as subpastas certas. Usa AssetDatabase.MoveAsset
    /// em vez de mexer nos arquivos: mover pelo Explorer separaria o asset do seu .meta
    /// e o Unity trataria como arquivo novo, perdendo toda referencia que aponta pra ele.
    /// </summary>
    public static class DataFolderOrganizer
    {
        [MenuItem("Kingdom Collapse/Organizar pasta Data")]
        public static void Organize()
        {
            DataFolders.EnsureAll();

            int moved = 0;
            List<string> problems = new List<string>();

            moved += MoveAll<BuildingAsset>(problems);
            moved += MoveAll<CardAsset>(problems);
            moved += MoveAll<EventAsset>(problems);
            moved += MoveAll<WeatherAsset>(problems);
            moved += MoveAll<RaceAsset>(problems);
            moved += MoveAll<MetaNodeAsset>(problems);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            for (int i = 0; i < problems.Count; i++)
            {
                Debug.LogWarning(problems[i]);
            }

            Debug.Log(
                moved + " asset(s) organizados. GameDatabase e Balance ficam na raiz de " +
                DataFolders.Root + "; o resto foi para as subpastas.");

            ReportEntryPoints();
        }

        private static int MoveAll<T>(List<string> problems) where T : Object
        {
            string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
            int moved = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                T asset = AssetDatabase.LoadAssetAtPath<T>(path);

                if (asset == null)
                {
                    continue;
                }

                string folder = DataFolders.For(asset);
                if (string.IsNullOrEmpty(folder))
                {
                    continue;
                }

                string target = folder + "/" + asset.name + ".asset";
                if (path == target)
                {
                    continue;
                }

                string error = AssetDatabase.MoveAsset(path, target);
                if (string.IsNullOrEmpty(error))
                {
                    moved++;
                    continue;
                }

                problems.Add("Nao movi " + path + ": " + error);
            }

            return moved;
        }

        /// <summary>Diz onde estao os dois arquivos que o usuario precisa achar.</summary>
        private static void ReportEntryPoints()
        {
            string[] databases = AssetDatabase.FindAssets("t:GameDatabase");
            if (databases.Length == 0)
            {
                Debug.LogWarning(
                    "Nenhum GameDatabase no projeto. Crie um em " + DataFolders.Root +
                    " com Create > Kingdom Collapse > Base de Conteudo.");
            }
            else
            {
                for (int i = 0; i < databases.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(databases[i]);
                    Debug.Log("GameDatabase: " + path,
                        AssetDatabase.LoadAssetAtPath<Object>(path));
                }
            }

            string[] balances = AssetDatabase.FindAssets("t:BalanceAsset");
            if (balances.Length == 0)
            {
                Debug.LogWarning(
                    "Nenhum Balance no projeto. Sem ele a run usa as curvas padrao do codigo.");
                return;
            }

            for (int i = 0; i < balances.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(balances[i]);
                Debug.Log("Balance: " + path, AssetDatabase.LoadAssetAtPath<Object>(path));
            }
        }
    }
}
