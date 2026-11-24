namespace ProjectB.UI
{
    /// <summary>
    /// Zero-GC number formatting into caller-owned char buffers (for <c>TMP_Text.SetCharArray</c>).
    /// Thousands are separated with a no-break space ("14 702"), which both theme fonts render.
    /// </summary>
    public static class UiFormat
    {
        public const char THOUSANDS_SEPARATOR = '\u00A0';

        /// <summary>Writes <paramref name="value"/> with thousands separators at <paramref name="start"/>; returns the end index.</summary>
        public static int WriteGrouped(char[] buffer, int start, int value)
        {
            if (buffer == null) return start;
            int pos = start;
            long v = value;
            if (v < 0)
            {
                buffer[pos++] = '-';
                v = -v;
            }

            if (v == 0)
            {
                buffer[pos++] = '0';
                return pos;
            }

            int digits = 0;
            long tmp = v;
            while (tmp > 0)
            {
                tmp /= 10;
                digits++;
            }

            int separators = (digits - 1) / 3;
            int end = pos + digits + separators;
            int write = end - 1;
            int group = 0;
            while (v > 0)
            {
                if (group == 3)
                {
                    buffer[write--] = THOUSANDS_SEPARATOR;
                    group = 0;
                }
                buffer[write--] = (char)('0' + (int)(v % 10));
                v /= 10;
                group++;
            }
            return end;
        }

        /// <summary>Plain non-negative integer (no grouping).</summary>
        public static int WriteInt(char[] buffer, int start, int value)
        {
            if (value < 0)
            {
                buffer[start++] = '-';
                value = -value;
            }
            if (value == 0)
            {
                buffer[start] = '0';
                return start + 1;
            }
            int digits = 0;
            int tmp = value;
            while (tmp > 0)
            {
                tmp /= 10;
                digits++;
            }
            int end = start + digits;
            int write = end - 1;
            while (value > 0)
            {
                buffer[write--] = (char)('0' + value % 10);
                value /= 10;
            }
            return end;
        }

        /// <summary>mm:ss (minutes may exceed 99).</summary>
        public static int WriteClock(char[] buffer, int start, int totalSeconds)
        {
            if (totalSeconds < 0) totalSeconds = 0;
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            if (minutes < 10) buffer[start++] = '0';
            start = WriteInt(buffer, start, minutes);
            buffer[start++] = ':';
            buffer[start++] = (char)('0' + seconds / 10);
            buffer[start++] = (char)('0' + seconds % 10);
            return start;
        }

        public static int WriteString(char[] buffer, int start, string text)
        {
            if (string.IsNullOrEmpty(text)) return start;
            for (int i = 0; i < text.Length && start < buffer.Length; i++)
            {
                buffer[start++] = text[i];
            }
            return start;
        }

        /// <summary>Allocating helper for non-hot paths (menus, popups).</summary>
        public static string Grouped(int value)
        {
            var buffer = new char[16];
            int len = WriteGrouped(buffer, 0, value);
            return new string(buffer, 0, len);
        }
    }
}
