using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace UnityGameData
{
    public enum ItemType { Recovery, Weapon, Enhancement, Aether }
    public enum UseType { None, Instant, Equip, Material }
    public enum Element { None, Fire, Water, Grass, Lightning, Earth, Wind, Light, Dark }
    public enum AttackType { Physical, Special, Status }
    public sealed class ItemData
    {
        public int ItemId { get; internal set; }
        public string ItemKey { get; internal set; }
        public string Name { get; internal set; }
        public string Description { get; internal set; }
        public string UseDescription { get; internal set; }
        public ItemType ItemType { get; internal set; }
        public string ImageKey { get; internal set; }
        public int InitialAmount { get; internal set; }
        public int StackLimit { get; internal set; }
        public bool Consumable { get; internal set; }
        public UseType UseType { get; internal set; }
        public int EffectId { get; internal set; }
        public float UseValue { get; internal set; }
        public float HealthMultiplier { get; internal set; }
        public float AttackMultiplier { get; internal set; }
        public float DefenseMultiplier { get; internal set; }
        public float SpecialAttackMultiplier { get; internal set; }
        public float SpecialDefenseMultiplier { get; internal set; }
        public float SpeedMultiplier { get; internal set; }
        public Element AetherType { get; internal set; }
        public int SkillId1 { get; internal set; }
        public int SkillId2 { get; internal set; }
        public int SkillId3 { get; internal set; }
        public string Memo { get; internal set; }
    }
    public sealed class SkillData
    {
        public int SkillId { get; internal set; }
        public string SkillKey { get; internal set; }
        public string SkillName { get; internal set; }
        public string Description { get; internal set; }
        public Element Element { get; internal set; }
        public AttackType AttackType { get; internal set; }
        public float Damage { get; internal set; }
        public int PP { get; internal set; }
        public float Accuracy { get; internal set; }
        public int Priority { get; internal set; }
        public int EffectId { get; internal set; }
        public string Memo { get; internal set; }
    }
    public sealed class SkillEffectData
    {
        public int EffectId { get; internal set; }
        public string EffectKey { get; internal set; }
        public string EffectName { get; internal set; }
        public string Description { get; internal set; }
        public string EffectType { get; internal set; }
        public string TargetType { get; internal set; }
        public int StatusEffectId { get; internal set; }
        public float Value { get; internal set; }
        public int Duration { get; internal set; }
        public float Chance { get; internal set; }
        public string Memo { get; internal set; }
    }
    public sealed class StatusEffectData
    {
        public int StatusEffectId { get; internal set; }
        public string StatusEffectKey { get; internal set; }
        public string StatusEffectName { get; internal set; }
        public string Description { get; internal set; }
        public int Duration { get; internal set; }
        public string Memo { get; internal set; }
    }

    public class DataIndex<T>
    {
        private readonly Dictionary<int,T> byId = new Dictionary<int,T>();
        private readonly Dictionary<string,T> byKey = new Dictionary<string,T>(StringComparer.Ordinal);
        public int Count { get { return byId.Count; } }
        public IEnumerable<T> All { get { return byId.Values; } }
        public T GetById(int id) { return byId[id]; }
        public T GetByKey(string key) { return byKey[key]; }
        public bool TryGetById(int id, out T value) { return byId.TryGetValue(id,out value); }
        public bool TryGetByKey(string key, out T value)
        {
            value=default(T); return key!=null && byKey.TryGetValue(key,out value);
        }
        internal void Add(int id, string key, T value, CsvRow row, string idColumn, string keyColumn)
        {
            if (byId.ContainsKey(id)) throw row.Error(idColumn,"duplicate ID");
            if (byKey.ContainsKey(key)) throw row.Error(keyColumn,"duplicate Key");
            byId.Add(id,value); byKey.Add(key,value);
        }
    }
    public sealed class ItemManager : DataIndex<ItemData> { }
    public sealed class SkillManager : DataIndex<SkillData> { }
    public sealed class SkillEffectManager : DataIndex<SkillEffectData> { }
    public sealed class StatusEffectManager : DataIndex<StatusEffectData> { }

    public sealed class GameDataCatalog
    {
        public ItemManager Items { get; private set; }
        public SkillManager Skills { get; private set; }
        public SkillEffectManager SkillEffects { get; private set; }
        public StatusEffectManager StatusEffects { get; private set; }
        private readonly List<string> errors=new List<string>();
        private readonly List<string> warnings=new List<string>();
        public IList<string> Errors { get { return errors.AsReadOnly(); } }
        public IList<string> Warnings { get { return warnings.AsReadOnly(); } }
        private GameDataCatalog()
        {
            Items=new ItemManager(); Skills=new SkillManager();
            SkillEffects=new SkillEffectManager(); StatusEffects=new StatusEffectManager();
        }
        // Invalid rows are reported and skipped. Other valid rows remain available.
        public static GameDataCatalog Load(string itemCsv,string skillCsv,string effectCsv,string statusCsv)
        {
            var result=new GameDataCatalog();
            foreach (var r in CsvRows.Read(statusCsv,"StatusEffectManager",new [] { "StatusEffectId", "StatusEffectKey", "StatusEffectName", "Description", "Duration", "Memo" },result.errors))
            {
                try
                {
                    var d=new StatusEffectData {
                        StatusEffectId=r.Int("StatusEffectId",null,1),
                        StatusEffectKey=r.Text("StatusEffectKey",true),
                        StatusEffectName=r.Text("StatusEffectName",true),
                        Description=r.Text("Description",false),
                        Duration=r.Int("Duration",0,0),
                        Memo=r.Text("Memo",false) };
                    r.ValidateKey("StatusEffectKey");
                    result.StatusEffects.Add(d.StatusEffectId,d.StatusEffectKey,d,r,"StatusEffectId","StatusEffectKey");
                }
                catch (FormatException ex) { result.errors.Add(ex.Message); }
            }
            foreach (var r in CsvRows.Read(effectCsv,"SkillEffectManager",new [] { "EffectId", "EffectKey", "EffectName", "Description", "EffectType", "TargetType", "StatusEffectId", "Value", "Duration", "Chance", "Memo" },result.errors))
            {
                try
                {
                    var d=new SkillEffectData {
                        EffectId=r.Int("EffectId",null,1),
                        EffectKey=r.Text("EffectKey",true),
                        EffectName=r.Text("EffectName",true),
                        Description=r.Text("Description",false),
                        EffectType=r.Text("EffectType",false),
                        TargetType=r.Text("TargetType",false),
                        StatusEffectId=r.Int("StatusEffectId",0,0),
                        Value=r.Float("Value",1f),
                        Duration=r.Int("Duration",0,0),
                        Chance=r.Float("Chance",100f),
                        Memo=r.Text("Memo",false) };
                    r.ValidateKey("EffectKey");
                    r.Range("Chance",d.Chance,0f,100f);
                    StatusEffectData status;
                    if (d.StatusEffectId!=0 && !result.StatusEffects.TryGetById(d.StatusEffectId,out status))
                        throw r.Error("StatusEffectId","reference missing or invalid");
                    if (d.EffectType.Length==0) result.warnings.Add(r.Describe("EffectType","not specified; enum is undecided"));
                    if (d.TargetType.Length==0) result.warnings.Add(r.Describe("TargetType","not specified; enum is undecided"));
                    result.SkillEffects.Add(d.EffectId,d.EffectKey,d,r,"EffectId","EffectKey");
                }
                catch (FormatException ex) { result.errors.Add(ex.Message); }
            }
            foreach (var r in CsvRows.Read(skillCsv,"SkillManager",new [] { "SkillId", "SkillKey", "SkillName", "Description", "Element", "AttackType", "Damage", "PP", "Accuracy", "Priority", "EffectId", "Memo" },result.errors))
            {
                try
                {
                    var d=new SkillData {
                        SkillId=r.Int("SkillId",null,1),
                        SkillKey=r.Text("SkillKey",true),
                        SkillName=r.Text("SkillName",true),
                        Description=r.Text("Description",false),
                        Element=r.EnumValue<Element>("Element","None"),
                        AttackType=r.EnumValue<AttackType>("AttackType",null),
                        Damage=r.Float("Damage",0f),
                        PP=r.Int("PP",null,0),
                        Accuracy=r.Float("Accuracy",null),
                        Priority=r.Int("Priority",0,Int32.MinValue),
                        EffectId=r.Int("EffectId",0,0),
                        Memo=r.Text("Memo",false) };
                    r.ValidateKey("SkillKey");
                    r.Range("Accuracy",d.Accuracy,0f,100f);
                    SkillEffectData effect;
                    if (d.EffectId!=0 && !result.SkillEffects.TryGetById(d.EffectId,out effect))
                        throw r.Error("EffectId","reference missing or invalid");
                    result.Skills.Add(d.SkillId,d.SkillKey,d,r,"SkillId","SkillKey");
                }
                catch (FormatException ex) { result.errors.Add(ex.Message); }
            }
            foreach (var r in CsvRows.Read(itemCsv,"ItemManager",new [] { "ItemId", "ItemKey", "Name", "Description", "UseDescription", "ItemType", "ImageKey", "InitialAmount", "StackLimit", "Consumable", "UseType", "EffectId", "UseValue", "HealthMultiplier", "AttackMultiplier", "DefenseMultiplier", "SpecialAttackMultiplier", "SpecialDefenseMultiplier", "SpeedMultiplier", "AetherType", "SkillId1", "SkillId2", "SkillId3", "Memo" },result.errors))
            {
                try
                {
                    var d=new ItemData {
                        ItemId=r.Int("ItemId",null,1),
                        ItemKey=r.Text("ItemKey",true),
                        Name=r.Text("Name",true),
                        Description=r.Text("Description",false),
                        UseDescription=r.Text("UseDescription",false),
                        ItemType=r.EnumValue<ItemType>("ItemType",null),
                        ImageKey=r.Text("ImageKey",false),
                        InitialAmount=r.Int("InitialAmount",0,0),
                        StackLimit=r.Int("StackLimit",1,0),
                        Consumable=r.Bool("Consumable",false),
                        UseType=r.EnumValue<UseType>("UseType","None"),
                        EffectId=r.Int("EffectId",0,0),
                        UseValue=r.Float("UseValue",0f),
                        HealthMultiplier=r.Float("HealthMultiplier",1f),
                        AttackMultiplier=r.Float("AttackMultiplier",1f),
                        DefenseMultiplier=r.Float("DefenseMultiplier",1f),
                        SpecialAttackMultiplier=r.Float("SpecialAttackMultiplier",1f),
                        SpecialDefenseMultiplier=r.Float("SpecialDefenseMultiplier",1f),
                        SpeedMultiplier=r.Float("SpeedMultiplier",1f),
                        AetherType=r.EnumValue<Element>("AetherType","None"),
                        SkillId1=r.Int("SkillId1",0,0),
                        SkillId2=r.Int("SkillId2",0,0),
                        SkillId3=r.Int("SkillId3",0,0),
                        Memo=r.Text("Memo",false) };
                    r.ValidateKey("ItemKey");
                    if (d.ItemType==ItemType.Weapon && d.SkillId2!=0) throw r.Error("SkillId2","Weapon supports only SkillId1");
                    if (d.ItemType==ItemType.Weapon && d.SkillId3!=0) throw r.Error("SkillId3","Weapon supports only SkillId1");
                    foreach (string column in new [] {"SkillId1","SkillId2","SkillId3"})
                    {
                        int id=r.Int(column,0,0); SkillData skill;
                        if (id!=0 && !result.Skills.TryGetById(id,out skill)) throw r.Error(column,"reference missing or invalid");
                    }
                    result.Items.Add(d.ItemId,d.ItemKey,d,r,"ItemId","ItemKey");
                }
                catch (FormatException ex) { result.errors.Add(ex.Message); }
            }
            return result;
        }
    }

    internal sealed class CsvRow
    {
        private readonly Dictionary<string,string> cells;
        private readonly string location;
        internal CsvRow(Dictionary<string,string> cells,string location) { this.cells=cells; this.location=location; }
        internal string Describe(string column,string message)
        {
            string value; cells.TryGetValue(column,out value);
            return location + " column " + column + " value [" + value + "]: " + message;
        }
        internal FormatException Error(string column,string message) { return new FormatException(Describe(column,message)); }
        internal string Text(string name,bool required)
        {
            string value;
            if (!cells.TryGetValue(name,out value)) throw Error(name,"missing column");
            value=value.Trim();
            if (required && value.Length==0) throw Error(name,"required value missing");
            return value;
        }
        internal void ValidateKey(string name)
        {
            if (!Regex.IsMatch(Text(name,true),"^[a-z][a-z0-9]*(?:_[a-z0-9]+)*$")) throw Error(name,"expected snake_case Key");
        }
        internal int Int(string name,int? fallback,int minimum)
        {
            string raw=Text(name,false); int value;
            if (raw.Length==0 && fallback.HasValue) return fallback.Value;
            if (!Int32.TryParse(raw,NumberStyles.Integer,CultureInfo.InvariantCulture,out value) || value<minimum)
                throw Error(name,"invalid integer or out of range");
            return value;
        }
        internal float Float(string name,float? fallback)
        {
            string raw=Text(name,false); float value;
            if (raw.Length==0 && fallback.HasValue) return fallback.Value;
            if (!Single.TryParse(raw,NumberStyles.Float,CultureInfo.InvariantCulture,out value) || Single.IsNaN(value) || Single.IsInfinity(value))
                throw Error(name,"invalid finite float");
            return value;
        }
        internal bool Bool(string name,bool fallback)
        {
            string raw=Text(name,false); bool value;
            if (raw.Length==0) return fallback;
            if (!Boolean.TryParse(raw,out value)) throw Error(name,"invalid boolean");
            return value;
        }
        internal T EnumValue<T>(string name,string fallback) where T:struct
        {
            string raw=Text(name,false); T value;
            if (raw.Length==0 && fallback!=null) raw=fallback;
            if (Array.IndexOf(Enum.GetNames(typeof(T)),raw)<0 || !Enum.TryParse<T>(raw,out value)) throw Error(name,"invalid enum name");
            return value;
        }
        internal void Range(string name,float value,float min,float max)
        { if (value<min || value>max) throw Error(name,"out of range"); }
    }

    internal static class CsvRows
    {
        internal static List<CsvRow> Read(string text,string table,string[] required,List<string> errors)
        {
            var result=new List<CsvRow>(); List<List<string>> rows;
            try { rows=Parse(text); }
            catch (FormatException ex) { errors.Add(table + ": " + ex.Message); return result; }
            if (rows.Count==0) { errors.Add(table + ": missing header"); return result; }
            var headers=rows[0]; var names=new HashSet<string>(StringComparer.Ordinal);
            for (int c=0;c<headers.Count;c++)
            {
                headers[c]=headers[c].Trim();
                if (headers[c].Length==0 || !names.Add(headers[c])) { errors.Add(table + ": empty or duplicate header"); return result; }
            }
            foreach (string name in required)
                if (!names.Contains(name)) { errors.Add(table + ": missing column " + name); return result; }
            for (int r=1;r<rows.Count;r++)
            {
                if (rows[r].Count!=headers.Count)
                { errors.Add(table + " row " + (r+1) + ": expected " + headers.Count + " columns; got " + rows[r].Count); continue; }
                var cells=new Dictionary<string,string>(StringComparer.Ordinal);
                for (int c=0;c<headers.Count;c++) cells.Add(headers[c],rows[r][c]);
                result.Add(new CsvRow(cells,table + " row " + (r+1)));
            }
            return result;
        }
        private static List<List<string>> Parse(string text)
        {
            if (text==null) throw new FormatException("CSV text missing");
            text=text.TrimStart('\uFEFF');
            var rows=new List<List<string>>(); var row=new List<string>(); var cell=new StringBuilder();
            bool quoted=false,closed=false;
            for (int i=0;i<text.Length;i++)
            {
                char ch=text[i];
                if (quoted)
                {
                    if (ch=='"')
                    {
                        if (i+1<text.Length && text[i+1]=='"') { cell.Append('"'); i++; }
                        else { quoted=false; closed=true; }
                    }
                    else cell.Append(ch);
                    continue;
                }
                if (ch==',') { row.Add(cell.ToString()); cell.Length=0; closed=false; }
                else if (ch=='\r' || ch=='\n')
                {
                    row.Add(cell.ToString());
                    if (!(row.Count==1 && row[0].Length==0)) rows.Add(row);
                    row=new List<string>(); cell.Length=0; closed=false;
                    if (ch=='\r' && i+1<text.Length && text[i+1]=='\n') i++;
                }
                else if (ch=='"')
                {
                    if (cell.Length!=0 || closed) throw new FormatException("unexpected quote at character " + i);
                    quoted=true;
                }
                else
                {
                    if (closed) throw new FormatException("characters after closing quote at character " + i);
                    cell.Append(ch);
                }
            }
            if (quoted) throw new FormatException("unclosed quoted CSV field; table cannot be safely recovered");
            if (cell.Length>0 || row.Count>0 || closed) { row.Add(cell.ToString()); rows.Add(row); }
            return rows;
        }
    }
}
