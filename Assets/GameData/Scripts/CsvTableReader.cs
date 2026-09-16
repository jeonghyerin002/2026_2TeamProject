using System;
using System.Collections.Generic;
using System.Text;

namespace Game.Data
{
    /// <summary>RFC 4180 style CSV reader. Supports UTF-8 BOM, quotes, commas and line breaks in quoted cells.</summary>
    public sealed class CsvTable
    {
        private readonly List<string[]> rows = new List<string[]>();
        private readonly Dictionary<string, int> columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public int RowCount { get { return rows.Count; } }
        public int SourceLine(int row) { return row + 2; }

        public static bool TryParse(string text, string tableName, out CsvTable table)
        {
            table = null;
            List<string[]> parsed;
            string error;
            if (!TryReadRows(text, out parsed, out error))
            {
                UnityEngine.Debug.LogError("[" + tableName + "] CSV parse error: " + error);
                return false;
            }
            if (parsed.Count == 0)
            {
                UnityEngine.Debug.LogError("[" + tableName + "] CSV is empty.");
                return false;
            }

            var result = new CsvTable();
            for (var i = 0; i < parsed[0].Length; i++)
            {
                var header = parsed[0][i].Trim().TrimStart('\uFEFF');
                if (string.IsNullOrEmpty(header))
                {
                    UnityEngine.Debug.LogError("[" + tableName + "] Empty header at column " + (i + 1) + ".");
                    return false;
                }
                if (result.columns.ContainsKey(header))
                {
                    UnityEngine.Debug.LogError("[" + tableName + "] Duplicate header: " + header);
                    return false;
                }
                result.columns.Add(header, i);
            }
            for (var i = 1; i < parsed.Count; i++)
            {
                if (parsed[i].Length == 1 && string.IsNullOrWhiteSpace(parsed[i][0])) continue;
                result.rows.Add(parsed[i]);
            }
            table = result;
            return true;
        }

        public bool HasColumn(string name) { return columns.ContainsKey(name); }

        public string Get(int row, string column)
        {
            int index;
            if (!columns.TryGetValue(column, out index) || index >= rows[row].Length) return string.Empty;
            return rows[row][index].Trim();
        }

        private static bool TryReadRows(string text, out List<string[]> result, out string error)
        {
            result = new List<string[]>();
            error = null;
            if (text == null) { error = "Input is null."; return false; }

            var row = new List<string>();
            var cell = new StringBuilder();
            var quoted = false;
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (quoted)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                        else quoted = false;
                    }
                    else cell.Append(c);
                }
                else if (c == '"')
                {
                    if (cell.Length != 0) { error = "Unexpected quote near character " + i + "."; return false; }
                    quoted = true;
                }
                else if (c == ',') { row.Add(cell.ToString()); cell.Length = 0; }
                else if (c == '\r' || c == '\n')
                {
                    row.Add(cell.ToString()); cell.Length = 0;
                    result.Add(row.ToArray()); row.Clear();
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                }
                else cell.Append(c);
            }
            if (quoted) { error = "Unclosed quoted field."; return false; }
            if (cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString()); result.Add(row.ToArray()); }
            return true;
        }
    }
}
