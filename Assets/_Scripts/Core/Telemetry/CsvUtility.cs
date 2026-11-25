#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Globalization;
using System.IO;
using System.Text;
using ProjectB.LevelUp;

namespace ProjectB.Core.Telemetry
{
    public static class CsvUtility
    {
        public static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        public static void AppendCell(StringBuilder sb, string value, bool isLast = false)
        {
            if (!string.IsNullOrEmpty(value))
            {
                bool needsQuotes = value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0;
                if (needsQuotes)
                {
                    sb.Append('"');
                    for (int i = 0; i < value.Length; i++)
                    {
                        char c = value[i];
                        if (c == '"') sb.Append("\"\"");
                        else sb.Append(c);
                    }
                    sb.Append('"');
                }
                else
                {
                    sb.Append(value);
                }
            }

            if (!isLast) sb.Append(',');
        }

        public static void AppendInt(StringBuilder sb, int value, bool isLast = false)
        {
            sb.Append(value.ToString(CultureInfo.InvariantCulture));
            if (!isLast) sb.Append(',');
        }

        public static void AppendLong(StringBuilder sb, long value, bool isLast = false)
        {
            sb.Append(value.ToString(CultureInfo.InvariantCulture));
            if (!isLast) sb.Append(',');
        }

        public static void AppendFloat(StringBuilder sb, float value, string format = "F2", bool isLast = false)
        {
            sb.Append(value.ToString(format, CultureInfo.InvariantCulture));
            if (!isLast) sb.Append(',');
        }

        public static void AppendDouble(StringBuilder sb, double value, string format = "F2", bool isLast = false)
        {
            sb.Append(value.ToString(format, CultureInfo.InvariantCulture));
            if (!isLast) sb.Append(',');
        }

        public static void AppendBool(StringBuilder sb, bool value, bool isLast = false)
        {
            sb.Append(value ? "true" : "false");
            if (!isLast) sb.Append(',');
        }

        public static string GetCardId(CardData card)
        {
            if (card == null) return string.Empty;
            if (!string.IsNullOrEmpty(card.name)) return card.name;
            if (!string.IsNullOrEmpty(card.cardName)) return card.cardName;
            return "unknown_card";
        }

        public static void EnsureHeaderAndAppendLine(string filePath, string headerLine, string rowLine)
        {
            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            bool writeHeader = !File.Exists(filePath) || new FileInfo(filePath).Length == 0;
            using (var writer = new StreamWriter(filePath, append: true, Utf8NoBom))
            {
                if (writeHeader)
                {
                    writer.WriteLine(headerLine);
                }
                writer.WriteLine(rowLine);
            }
        }
    }
}
#endif
