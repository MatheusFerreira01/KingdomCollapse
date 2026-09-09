using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace KingdomCollapse.Core
{
    public sealed class JsonException : Exception
    {
        public JsonException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// Leitor e escritor de JSON minimo. Escrito a mao porque o Core nao pode
    /// referenciar UnityEngine (JsonUtility) e nao vale arrastar uma dependencia
    /// externa para um arquivo de save de algumas centenas de bytes. Estrito de
    /// proposito: qualquer coisa fora do formato lanca, e quem chama trata como
    /// save corrompido.
    /// </summary>
    public static class MiniJson
    {
        // --- Escrita ---

        public static string Serialize(object value)
        {
            StringBuilder builder = new StringBuilder();
            WriteValue(builder, value);
            return builder.ToString();
        }

        private static void WriteValue(StringBuilder builder, object value)
        {
            if (value == null)
            {
                builder.Append("null");
                return;
            }

            if (value is string text)
            {
                WriteString(builder, text);
                return;
            }

            if (value is bool flag)
            {
                builder.Append(flag ? "true" : "false");
                return;
            }

            if (value is int || value is long)
            {
                builder.Append(Convert.ToInt64(value).ToString(CultureInfo.InvariantCulture));
                return;
            }

            if (value is float || value is double)
            {
                builder.Append(Convert.ToDouble(value).ToString("R", CultureInfo.InvariantCulture));
                return;
            }

            if (value is IDictionary<string, object> map)
            {
                builder.Append('{');
                bool first = true;
                foreach (KeyValuePair<string, object> pair in map)
                {
                    if (!first)
                    {
                        builder.Append(',');
                    }

                    first = false;
                    WriteString(builder, pair.Key);
                    builder.Append(':');
                    WriteValue(builder, pair.Value);
                }

                builder.Append('}');
                return;
            }

            if (value is IEnumerable<object> list)
            {
                builder.Append('[');
                bool first = true;
                foreach (object item in list)
                {
                    if (!first)
                    {
                        builder.Append(',');
                    }

                    first = false;
                    WriteValue(builder, item);
                }

                builder.Append(']');
                return;
            }

            throw new JsonException("tipo nao serializavel: " + value.GetType().Name);
        }

        private static void WriteString(StringBuilder builder, string value)
        {
            builder.Append('"');
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        if (c < ' ')
                        {
                            builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            builder.Append(c);
                        }

                        break;
                }
            }

            builder.Append('"');
        }

        // --- Leitura ---

        public static object Deserialize(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                throw new JsonException("json vazio");
            }

            int index = 0;
            object value = ReadValue(json, ref index);
            SkipWhitespace(json, ref index);

            if (index != json.Length)
            {
                throw new JsonException("lixo depois do fim do json, posicao " + index);
            }

            return value;
        }

        private static object ReadValue(string json, ref int index)
        {
            SkipWhitespace(json, ref index);
            if (index >= json.Length)
            {
                throw new JsonException("fim inesperado");
            }

            char c = json[index];
            switch (c)
            {
                case '{':
                    return ReadObject(json, ref index);
                case '[':
                    return ReadArray(json, ref index);
                case '"':
                    return ReadString(json, ref index);
                case 't':
                    Expect(json, ref index, "true");
                    return true;
                case 'f':
                    Expect(json, ref index, "false");
                    return false;
                case 'n':
                    Expect(json, ref index, "null");
                    return null;
                default:
                    return ReadNumber(json, ref index);
            }
        }

        private static Dictionary<string, object> ReadObject(string json, ref int index)
        {
            Dictionary<string, object> map = new Dictionary<string, object>();
            index++; // '{'
            SkipWhitespace(json, ref index);

            if (index < json.Length && json[index] == '}')
            {
                index++;
                return map;
            }

            while (true)
            {
                SkipWhitespace(json, ref index);
                string key = ReadString(json, ref index);
                SkipWhitespace(json, ref index);

                if (index >= json.Length || json[index] != ':')
                {
                    throw new JsonException("esperado ':' depois da chave " + key);
                }

                index++;
                map[key] = ReadValue(json, ref index);
                SkipWhitespace(json, ref index);

                if (index >= json.Length)
                {
                    throw new JsonException("objeto nao fechado");
                }

                if (json[index] == ',')
                {
                    index++;
                    continue;
                }

                if (json[index] == '}')
                {
                    index++;
                    return map;
                }

                throw new JsonException("esperado ',' ou '}' na posicao " + index);
            }
        }

        private static List<object> ReadArray(string json, ref int index)
        {
            List<object> list = new List<object>();
            index++; // '['
            SkipWhitespace(json, ref index);

            if (index < json.Length && json[index] == ']')
            {
                index++;
                return list;
            }

            while (true)
            {
                list.Add(ReadValue(json, ref index));
                SkipWhitespace(json, ref index);

                if (index >= json.Length)
                {
                    throw new JsonException("array nao fechado");
                }

                if (json[index] == ',')
                {
                    index++;
                    continue;
                }

                if (json[index] == ']')
                {
                    index++;
                    return list;
                }

                throw new JsonException("esperado ',' ou ']' na posicao " + index);
            }
        }

        private static string ReadString(string json, ref int index)
        {
            if (index >= json.Length || json[index] != '"')
            {
                throw new JsonException("esperado texto na posicao " + index);
            }

            index++;
            StringBuilder builder = new StringBuilder();

            while (true)
            {
                if (index >= json.Length)
                {
                    throw new JsonException("texto nao fechado");
                }

                char c = json[index++];
                if (c == '"')
                {
                    return builder.ToString();
                }

                if (c != '\\')
                {
                    builder.Append(c);
                    continue;
                }

                if (index >= json.Length)
                {
                    throw new JsonException("escape incompleto");
                }

                char escape = json[index++];
                switch (escape)
                {
                    case '"':
                        builder.Append('"');
                        break;
                    case '\\':
                        builder.Append('\\');
                        break;
                    case '/':
                        builder.Append('/');
                        break;
                    case 'b':
                        builder.Append('\b');
                        break;
                    case 'f':
                        builder.Append('\f');
                        break;
                    case 'n':
                        builder.Append('\n');
                        break;
                    case 'r':
                        builder.Append('\r');
                        break;
                    case 't':
                        builder.Append('\t');
                        break;
                    case 'u':
                        if (index + 4 > json.Length)
                        {
                            throw new JsonException("escape unicode incompleto");
                        }

                        builder.Append((char)ushort.Parse(
                            json.Substring(index, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        index += 4;
                        break;
                    default:
                        throw new JsonException("escape desconhecido \\" + escape);
                }
            }
        }

        private static double ReadNumber(string json, ref int index)
        {
            int start = index;

            if (index < json.Length && (json[index] == '-' || json[index] == '+'))
            {
                index++;
            }

            while (index < json.Length)
            {
                char c = json[index];
                if ((c >= '0' && c <= '9') || c == '.' || c == 'e' || c == 'E' || c == '-' || c == '+')
                {
                    index++;
                    continue;
                }

                break;
            }

            string slice = json.Substring(start, index - start);
            if (!double.TryParse(slice, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            {
                throw new JsonException("numero invalido: '" + slice + "'");
            }

            return value;
        }

        private static void Expect(string json, ref int index, string literal)
        {
            if (index + literal.Length > json.Length ||
                string.CompareOrdinal(json, index, literal, 0, literal.Length) != 0)
            {
                throw new JsonException("esperado '" + literal + "' na posicao " + index);
            }

            index += literal.Length;
        }

        private static void SkipWhitespace(string json, ref int index)
        {
            while (index < json.Length)
            {
                char c = json[index];
                if (c == ' ' || c == '\t' || c == '\n' || c == '\r')
                {
                    index++;
                    continue;
                }

                break;
            }
        }
    }
}
