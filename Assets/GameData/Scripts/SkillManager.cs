using System.Collections.Generic;
using UnityEngine;

namespace Game.Data
{
    public sealed class SkillManager : MonoBehaviour
    {
        public static SkillManager Instance { get; private set; }
        [SerializeField] private TextAsset skillCsv;
        [SerializeField] private bool loadOnAwake = true;

        private readonly Dictionary<int, SkillData> byId = new Dictionary<int, SkillData>();
        private readonly Dictionary<string, SkillData> byKey = new Dictionary<string, SkillData>(System.StringComparer.OrdinalIgnoreCase);
        public IEnumerable<SkillData> AllSkills { get { return byId.Values; } }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Debug.LogError("[SkillManager] Duplicate manager instance."); return; }
            Instance = this;
            if (loadOnAwake && skillCsv != null) Load(skillCsv);
        }

        public bool Load(TextAsset csv)
        {
            byId.Clear(); byKey.Clear();
            if (csv == null) { Debug.LogError("[SkillManager] Skill CSV is not assigned."); return false; }
            CsvTable table;
            if (!CsvTable.TryParse(csv.text, "SkillManager", out table)) return false;
            var required = new[] { "SkillId", "SkillKey", "SkillName", "Description", "Element", "AttackType", "Damage", "PP", "Accuracy", "EffectId", "Memo" };
            foreach (var column in required) if (!table.HasColumn(column)) { Debug.LogError("[SkillManager] Missing column: " + column); return false; }

            var ok = true;
            for (var row = 0; row < table.RowCount; row++)
            {
                int id, pp, effectId; float damage, accuracy; string key, name; ElementType element; AttackType attackType;
                var valid = CsvValueParser.Int(table, row, "SkillId", "SkillManager", true, 0, out id)
                    & CsvValueParser.Required(table, row, "SkillKey", "SkillManager", out key)
                    & CsvValueParser.Required(table, row, "SkillName", "SkillManager", out name)
                    & CsvValueParser.EnumValue(table, row, "Element", "SkillManager", true, ElementType.None, out element)
                    & CsvValueParser.EnumValue(table, row, "AttackType", "SkillManager", true, AttackType.Physical, out attackType)
                    & CsvValueParser.Float(table, row, "Damage", "SkillManager", 0f, out damage)
                    & CsvValueParser.Int(table, row, "PP", "SkillManager", true, 0, out pp)
                    & CsvValueParser.Float(table, row, "Accuracy", "SkillManager", 0f, out accuracy)
                    & CsvValueParser.Int(table, row, "EffectId", "SkillManager", false, 0, out effectId);
                if (!valid) { ok = false; continue; }
                if (pp < 0 || accuracy < 0f || accuracy > 100f) { Debug.LogError("[SkillManager] Row " + table.SourceLine(row) + ": PP must be >= 0 and Accuracy must be between 0 and 100."); ok = false; continue; }
                if (byId.ContainsKey(id)) { Debug.LogError("[SkillManager] Duplicate Skill ID " + id + " at row " + table.SourceLine(row) + "."); ok = false; continue; }
                if (byKey.ContainsKey(key)) { Debug.LogError("[SkillManager] Duplicate Skill Key '" + key + "' at row " + table.SourceLine(row) + "."); ok = false; continue; }
                var data = new SkillData { SkillId=id, SkillKey=key, SkillName=name, Description=table.Get(row,"Description"), Element=element, AttackType=attackType, Damage=damage, PP=pp, Accuracy=accuracy, EffectId=effectId, Memo=table.Get(row,"Memo") };
                byId.Add(id, data); byKey.Add(key, data);
            }
            Debug.Log("[SkillManager] Loaded " + byId.Count + " skills. Valid=" + ok);
            return ok;
        }

        public SkillData GetSkillById(int id) { SkillData value; byId.TryGetValue(id, out value); return value; }
        public SkillData GetSkillByKey(string key) { SkillData value; if (key != null) byKey.TryGetValue(key, out value); else value = null; return value; }
        public bool TryGetSkillById(int id, out SkillData value) { return byId.TryGetValue(id, out value); }
    }
}
