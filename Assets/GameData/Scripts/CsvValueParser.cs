using System;
using System.Globalization;
using UnityEngine;

namespace Game.Data
{
    internal static class CsvValueParser
    {
        public static bool Required(CsvTable t, int row, string column, string manager, out string value)
        {
            value = t.Get(row, column);
            if (!string.IsNullOrWhiteSpace(value)) return true;
            Error(manager, t, row, column, "Required value is empty.");
            return false;
        }

        public static bool Int(CsvTable t, int row, string column, string manager, bool required, int defaultValue, out int value)
        {
            var raw = t.Get(row, column);
            if (string.IsNullOrWhiteSpace(raw) && !required) { value = defaultValue; return true; }
            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)) return true;
            Error(manager, t, row, column, "Invalid integer: '" + raw + "'."); return false;
        }

        public static bool Float(CsvTable t, int row, string column, string manager, float defaultValue, out float value)
        {
            var raw = t.Get(row, column);
            if (string.IsNullOrWhiteSpace(raw)) { value = defaultValue; return true; }
            if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return true;
            Error(manager, t, row, column, "Invalid number: '" + raw + "'. Use '.' as decimal separator."); return false;
        }

        public static bool Bool(CsvTable t, int row, string column, string manager, out bool value)
        {
            var raw = t.Get(row, column);
            if (bool.TryParse(raw, out value)) return true;
            if (raw == "1") { value = true; return true; }
            if (raw == "0") { value = false; return true; }
            Error(manager, t, row, column, "Invalid boolean: '" + raw + "'. Use TRUE/FALSE."); return false;
        }

        public static bool EnumValue<T>(CsvTable t, int row, string column, string manager, bool required, T defaultValue, out T value) where T : struct
        {
            var raw = t.Get(row, column);
            if (string.IsNullOrWhiteSpace(raw) && !required) { value = defaultValue; return true; }
            if (Enum.TryParse(raw, true, out value) && Enum.IsDefined(typeof(T), value)) return true;
            Error(manager, t, row, column, "Invalid " + typeof(T).Name + ": '" + raw + "'."); return false;
        }

        private static void Error(string manager, CsvTable t, int row, string column, string message)
        {
            Debug.LogError("[" + manager + "] Row " + t.SourceLine(row) + ", column '" + column + "': " + message);
        }
    }
}
