using System;

namespace Game.Data
{
    public enum ItemType { Recovery, Weapon, Enhancement, Aether }
    public enum ItemUseType { None, Instant, Equip, Material, Passive }
    public enum ElementType { None, Fire, Water, Grass, Lightning, Earth, Wind, Light, Dark }
    public enum AttackType { Physical, Special }

    [Serializable]
    public sealed class ItemData
    {
        public int ItemId;
        public string ItemKey;
        public string Name;
        public string Description;
        public string UseDescription;
        public ItemType ItemType;
        public string ImageKey;
        public int InitialAmount;
        public int StackLimit;
        public bool Consumable;
        public ItemUseType UseType;
        public int EffectId;
        public float UseValue;
        public float HealthMultiplier = 1f;
        public float AttackMultiplier = 1f;
        public float DefenseMultiplier = 1f;
        public float SpecialAttackMultiplier = 1f;
        public float SpecialDefenseMultiplier = 1f;
        public float SpeedMultiplier = 1f;
        public ElementType AetherType;
        public int SkillId1;
        public int SkillId2;
        public int SkillId3;
        public string Memo;
    }

    [Serializable]
    public sealed class SkillData
    {
        public int SkillId;
        public string SkillKey;
        public string SkillName;
        public string Description;
        public ElementType Element;
        public AttackType AttackType;
        public float Damage;
        public int PP;
        public float Accuracy;
        public int EffectId;
        public string Memo;
    }
}
