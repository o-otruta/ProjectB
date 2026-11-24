using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ProjectB.Core
{
    /// <summary>
    /// Minimal flat JSON object parser/serializer for localization tables (<c>{ "key": "value" }</c>).
    /// Zero external dependencies; handles standard escapes and <c>\uXXXX</c>.
    /// </summary>
    public static class LocalizationJsonParser
    {
        public static void ParseInto(string json, IDictionary<string, string> target)
        {
            if (string.IsNullOrEmpty(json) || target == null) return;

            int i = 0;
            int len = json.Length;
            SkipWhitespace(json, ref i, len);
            if (i >= len || json[i] != '{') return;
            i++;

            var sb = new StringBuilder(128);
            while (i < len)
            {
                SkipWhitespace(json, ref i, len);
                if (i >= len) break;
                if (json[i] == '}') break;
                if (json[i] == ',')
                {
                    i++;
                    continue;
                }

                if (json[i] != '"') break;
                string key = ReadString(json, ref i, len, sb);
                if (key == null) break;

                SkipWhitespace(json, ref i, len);
                if (i >= len || json[i] != ':') break;
                i++;

                SkipWhitespace(json, ref i, len);
                if (i >= len || json[i] != '"') break;
                string val = ReadString(json, ref i, len, sb);
                if (val == null) break;

                target[key] = val;
            }
        }

        public static string Serialize(SortedDictionary<string, string> map)
        {
            var sb = new StringBuilder(map != null ? map.Count * 48 : 16);
            sb.Append("{\n");
            if (map != null)
            {
                int index = 0;
                int count = map.Count;
                foreach (var kvp in map)
                {
                    sb.Append("  \"");
                    AppendEscaped(sb, kvp.Key);
                    sb.Append("\": \"");
                    AppendEscaped(sb, kvp.Value ?? string.Empty);
                    sb.Append(index < count - 1 ? "\",\n" : "\"\n");
                    index++;
                }
            }
            sb.Append("}\n");
            return sb.ToString();
        }

        private static void SkipWhitespace(string s, ref int i, int len)
        {
            while (i < len && char.IsWhiteSpace(s[i])) i++;
        }

        private static string ReadString(string s, ref int i, int len, StringBuilder sb)
        {
            if (i >= len || s[i] != '"') return null;
            i++;
            sb.Clear();

            while (i < len)
            {
                char c = s[i++];
                if (c == '"')
                {
                    return sb.ToString();
                }

                if (c == '\\')
                {
                    if (i >= len) return null;
                    char esc = s[i++];
                    switch (esc)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (i + 4 <= len && int.TryParse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int code))
                            {
                                sb.Append((char)code);
                                i += 4;
                            }
                            break;
                        default:
                            sb.Append(esc);
                            break;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }

            return null;
        }

        private static void AppendEscaped(StringBuilder sb, string value)
        {
            if (string.IsNullOrEmpty(value)) return;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default: sb.Append(c); break;
                }
            }
        }
    }
}
