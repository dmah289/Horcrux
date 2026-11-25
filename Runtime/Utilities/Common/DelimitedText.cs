#if UNITY_EDITOR
using System.Collections.Generic;

namespace Horcrux.Runtime.Utilities.Common
{
    /// <summary>One non-blank line of delimited text: its 1-based number in the source, the line trimmed, and its trimmed cells.</summary>
    public readonly struct DelimitedLine
    {
        public int Number { get; }
        public string Text { get; }
        public IReadOnlyList<string> Cells { get; }

        public DelimitedLine(int number, string text, IReadOnlyList<string> cells)
        {
            Number = number;
            Text = text;
            Cells = cells;
        }
    }

    /// <summary>
    /// Splits text pasted from a sheet or saved as CSV into lines and cells. Header, comments and cell rules stay with the
    /// caller. No quoting: a cell cannot hold its separator.
    /// </summary>
    /// <remarks>Editor authoring only, like the importers that call it.</remarks>
    public static class DelimitedText
    {
        private const char Tab = '\t';
        private const char Comma = ',';

        /// <summary>
        /// Non-blank lines in source order, trailing empty cells kept. A line holding a tab splits on tabs, since a copy
        /// from Google Sheets or Excel is tab-separated; any other line splits on commas.
        /// </summary>
        public static List<DelimitedLine> ReadLines(string text)
        {
            var lines = new List<DelimitedLine>();
            if (string.IsNullOrEmpty(text)) return lines;

            string[] rawLines = text.Split('\n');
            for (int i = 0; i < rawLines.Length; i++)
            {
                string raw = rawLines[i].TrimEnd('\r');
                if (string.IsNullOrWhiteSpace(raw)) continue;

                string[] cells = raw.Split(raw.IndexOf(Tab) >= 0 ? Tab : Comma);
                for (int c = 0; c < cells.Length; c++) cells[c] = cells[c].Trim();
                lines.Add(new DelimitedLine(i + 1, raw.Trim(), cells));
            }

            return lines;
        }
    }
}
#endif
