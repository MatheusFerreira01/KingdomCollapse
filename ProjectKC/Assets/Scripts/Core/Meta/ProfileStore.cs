using System;
using System.Collections.Generic;
using System.IO;

namespace KingdomCollapse.Core
{
    public enum ProfileLoadStatus
    {
        Loaded = 0,
        Missing = 1,
        Corrupt = 2
    }

    public sealed class ProfileLoadResult
    {
        public ProfileLoadResult(MetaProfile profile, ProfileLoadStatus status, string message, string backupPath = null)
        {
            Profile = profile;
            Status = status;
            Message = message;
            BackupPath = backupPath;
        }

        public MetaProfile Profile { get; }

        public ProfileLoadStatus Status { get; }

        public string Message { get; }

        /// <summary>Onde o arquivo ilegivel foi preservado, quando houve corrupcao.</summary>
        public string BackupPath { get; }
    }

    /// <summary>Converte o perfil para JSON e de volta. Sem IO: testavel sozinho.</summary>
    public static class ProfileCodec
    {
        public static string Serialize(MetaProfile profile)
        {
            List<object> nodes = new List<object>();
            foreach (string nodeId in profile.PurchasedNodes)
            {
                nodes.Add(nodeId);
            }

            nodes.Sort((a, b) => string.CompareOrdinal((string)a, (string)b));

            Dictionary<string, object> map = new Dictionary<string, object>
            {
                { "version", profile.Version },
                { "currency", profile.Currency },
                { "runsPlayed", profile.RunsPlayed },
                { "bestDay", profile.BestDayReached },
                { "bestScore", profile.BestScore },
                { "nodes", nodes }
            };

            return MiniJson.Serialize(map);
        }

        /// <summary>Lanca JsonException quando o conteudo nao bate com o formato.</summary>
        public static MetaProfile Deserialize(string json)
        {
            if (!(MiniJson.Deserialize(json) is Dictionary<string, object> map))
            {
                throw new JsonException("raiz do save nao e um objeto");
            }

            int version = ReadInt(map, "version");
            if (version <= 0 || version > MetaProfile.CurrentVersion)
            {
                throw new JsonException("versao de save nao suportada: " + version);
            }

            MetaProfile profile = new MetaProfile(version);
            profile.SetCurrency(ReadInt(map, "currency"));
            profile.RunsPlayed = ReadInt(map, "runsPlayed");
            profile.BestDayReached = ReadInt(map, "bestDay");
            profile.BestScore = ReadInt(map, "bestScore");

            if (!map.TryGetValue("nodes", out object rawNodes) || !(rawNodes is List<object> nodes))
            {
                throw new JsonException("campo 'nodes' ausente ou invalido");
            }

            for (int i = 0; i < nodes.Count; i++)
            {
                if (!(nodes[i] is string nodeId))
                {
                    throw new JsonException("id de no invalido na posicao " + i);
                }

                profile.MarkPurchased(nodeId);
            }

            return profile;
        }

        private static int ReadInt(IDictionary<string, object> map, string key)
        {
            if (!map.TryGetValue(key, out object value) || !(value is double number))
            {
                throw new JsonException("campo '" + key + "' ausente ou nao numerico");
            }

            return (int)number;
        }
    }

    /// <summary>
    /// Persistencia do perfil em arquivo. O Core recebe o caminho pronto; quem sabe
    /// o que e Application.persistentDataPath e a camada Unity (design D7).
    /// </summary>
    public sealed class FileProfileStore
    {
        public FileProfileStore(string filePath)
        {
            FilePath = filePath;
        }

        public string FilePath { get; }

        /// <summary>
        /// Le o perfil. Arquivo ausente ou ilegivel nunca derruba o jogo: o ilegivel
        /// e preservado ao lado com sufixo .corrupt e a partida segue com perfil novo
        /// (spec meta-progression).
        /// </summary>
        public ProfileLoadResult Load()
        {
            if (!File.Exists(FilePath))
            {
                return new ProfileLoadResult(
                    MetaProfile.NewProfile(),
                    ProfileLoadStatus.Missing,
                    "Nenhum save encontrado. Perfil novo criado.");
            }

            string raw;
            try
            {
                raw = File.ReadAllText(FilePath);
            }
            catch (Exception readError)
            {
                return new ProfileLoadResult(
                    MetaProfile.NewProfile(),
                    ProfileLoadStatus.Corrupt,
                    "Save ilegivel: " + readError.Message);
            }

            try
            {
                MetaProfile profile = ProfileCodec.Deserialize(raw);
                return new ProfileLoadResult(profile, ProfileLoadStatus.Loaded, "Save carregado.");
            }
            catch (Exception parseError)
            {
                string backup = PreserveCorruptFile();
                return new ProfileLoadResult(
                    MetaProfile.NewProfile(),
                    ProfileLoadStatus.Corrupt,
                    "Save corrompido (" + parseError.Message + "). Arquivo preservado para inspecao.",
                    backup);
            }
        }

        private string PreserveCorruptFile()
        {
            try
            {
                string backup = FilePath + "." + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + ".corrupt";
                File.Move(FilePath, backup);
                return backup;
            }
            catch
            {
                // Se nem mover funcionar, seguir com perfil novo ainda e melhor do
                // que travar a abertura do jogo.
                return null;
            }
        }

        /// <summary>
        /// Grava por arquivo temporario e troca no lugar: um crash no meio da escrita
        /// nao pode deixar o save pela metade.
        /// </summary>
        public void Save(MetaProfile profile)
        {
            string directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string temporary = FilePath + ".tmp";
            File.WriteAllText(temporary, ProfileCodec.Serialize(profile));

            if (File.Exists(FilePath))
            {
                File.Delete(FilePath);
            }

            File.Move(temporary, FilePath);
        }
    }
}
