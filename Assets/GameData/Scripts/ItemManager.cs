using System.Collections.Generic;
using UnityEngine;

namespace Game.Data
{
    public sealed class ItemManager : MonoBehaviour
    {
        public static ItemManager Instance { get; private set; }
        [SerializeField] private TextAsset itemCsv;
        [SerializeField] private bool loadOnAwake = true;

        private readonly Dictionary<int, ItemData> byId = new Dictionary<int, ItemData>();
        private readonly Dictionary<string, ItemData> byKey = new Dictionary<string, ItemData>(System.StringComparer.OrdinalIgnoreCase);
        public IEnumerable<ItemData> AllItems { get { return byId.Values; } }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Debug.LogError("[ItemManager] Duplicate manager instance."); return; }
            Instance = this;
            if (loadOnAwake && itemCsv != null) Load(itemCsv);
        }

        public bool Load(TextAsset csv)
        {
            byId.Clear(); byKey.Clear();
            if (csv == null) { Debug.LogError("[ItemManager] Item CSV is not assigned."); return false; }
            CsvTable table;
            var source = "ItemManager";
            if (!CsvTable.TryParse(csv.text, source, out table)) return false;
            var required = new[] { "ItemId","ItemKey","Name","Description","UseDescription","ItemType","ImageKey","InitialAmount","StackLimit","Consumable","UseType","EffectId","UseValue","HealthMultiplier","AttackMultiplier","DefenseMultiplier","SpecialAttackMultiplier","SpecialDefenseMultiplier","SpeedMultiplier","AetherType","SkillId1","SkillId2","SkillId3","Memo" };
            foreach (var column in required) if (!table.HasColumn(column)) { Debug.LogError("[" + source + "] Missing column: " + column); return false; }

            var ok = true;
            for (var row = 0; row < table.RowCount; row++)
            {
                int id, initial, stack, effectId, skillId1, skillId2, skillId3; float useValue, health, attack, defense, specialAttack, specialDefense, speed; bool consumable;
                string key, name; ItemType type; ItemUseType useType; ElementType aetherType;
                var valid = CsvValueParser.Int(table,row,"ItemId",source,true,0,out id)
                    & CsvValueParser.Required(table,row,"ItemKey",source,out key)
                    & CsvValueParser.Required(table,row,"Name",source,out name)
                    & CsvValueParser.EnumValue(table,row,"ItemType",source,true,ItemType.Recovery,out type)
                    & CsvValueParser.Int(table,row,"InitialAmount",source,true,0,out initial)
                    & CsvValueParser.Int(table,row,"StackLimit",source,true,1,out stack)
                    & CsvValueParser.Bool(table,row,"Consumable",source,out consumable)
                    & CsvValueParser.EnumValue(table,row,"UseType",source,true,ItemUseType.None,out useType)
                    & CsvValueParser.Int(table,row,"EffectId",source,false,0,out effectId)
                    & CsvValueParser.Float(table,row,"UseValue",source,0f,out useValue)
                    & CsvValueParser.Float(table,row,"HealthMultiplier",source,1f,out health)
                    & CsvValueParser.Float(table,row,"AttackMultiplier",source,1f,out attack)
                    & CsvValueParser.Float(table,row,"DefenseMultiplier",source,1f,out defense)
                    & CsvValueParser.Float(table,row,"SpecialAttackMultiplier",source,1f,out specialAttack)
                    & CsvValueParser.Float(table,row,"SpecialDefenseMultiplier",source,1f,out specialDefense)
                    & CsvValueParser.Float(table,row,"SpeedMultiplier",source,1f,out speed)
                    & CsvValueParser.EnumValue(table,row,"AetherType",source,false,ElementType.None,out aetherType)
                    & CsvValueParser.Int(table,row,"SkillId1",source,false,0,out skillId1)
                    & CsvValueParser.Int(table,row,"SkillId2",source,false,0,out skillId2)
                    & CsvValueParser.Int(table,row,"SkillId3",source,false,0,out skillId3);
                if (!valid) { ok = false; continue; }
                if (id <= 0 || initial < 0 || stack < 1) { Debug.LogError("[" + source + "] Row " + table.SourceLine(row) + ": ItemId must be positive, InitialAmount >= 0, StackLimit >= 1."); ok = false; continue; }
                if (byId.ContainsKey(id)) { Debug.LogError("[" + source + "] Duplicate Item ID " + id + " at row " + table.SourceLine(row) + "."); ok = false; continue; }
                if (byKey.ContainsKey(key)) { Debug.LogError("[" + source + "] Duplicate Item Key '" + key + "' at row " + table.SourceLine(row) + "."); ok = false; continue; }
                var data = new ItemData { ItemId=id, ItemKey=key, Name=name, Description=table.Get(row,"Description"), UseDescription=table.Get(row,"UseDescription"), ItemType=type, ImageKey=table.Get(row,"ImageKey"), InitialAmount=initial, StackLimit=stack, Consumable=consumable, UseType=useType, EffectId=effectId, UseValue=useValue, HealthMultiplier=health, AttackMultiplier=attack, DefenseMultiplier=defense, SpecialAttackMultiplier=specialAttack, SpecialDefenseMultiplier=specialDefense, SpeedMultiplier=speed, AetherType=aetherType, SkillId1=skillId1, SkillId2=skillId2, SkillId3=skillId3, Memo=table.Get(row,"Memo") };
                byId.Add(id, data); byKey.Add(key, data);
            }
            Debug.Log("[ItemManager] Loaded " + byId.Count + " items. Valid=" + ok);
            return ok;
        }

        public bool ValidateSkillReferences(SkillManager skills)
        {
            if (skills == null) { Debug.LogError("[ItemManager] SkillManager is missing; cannot validate references."); return false; }
            var ok = true;
            foreach (var item in byId.Values)
            {
                ok &= ValidateSkillSlot(skills, item, item.SkillId1, "SkillId1");
                ok &= ValidateSkillSlot(skills, item, item.SkillId2, "SkillId2");
                ok &= ValidateSkillSlot(skills, item, item.SkillId3, "SkillId3");
            }
            return ok;
        }

        private static bool ValidateSkillSlot(SkillManager skills, ItemData item, int skillId, string column)
        {
            if (skillId == 0) return true;
            SkillData ignored;
            if (skills.TryGetSkillById(skillId, out ignored)) return true;
            Debug.LogError("[ItemManager] Item " + item.ItemId + " " + column + " references missing Skill ID: " + skillId);
            return false;
        }

        public ItemData GetItemById(int id) { ItemData value; byId.TryGetValue(id, out value); return value; }
        public ItemData GetItemByKey(string key) { ItemData value; if (key != null) byKey.TryGetValue(key, out value); else value = null; return value; }
    }
}
