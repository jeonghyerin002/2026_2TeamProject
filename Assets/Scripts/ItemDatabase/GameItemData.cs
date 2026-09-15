using System;

namespace TeamProject.ItemManagement
{
    [Serializable]
    public sealed class GameItemDataFile
    {
        public int schemaVersion;
        public GameItemCategory[] categories;
    }

    [Serializable]
    public sealed class GameItemCategory
    {
        public string id;
        public string displayName;
        public string description;
        public GameItemDefinition[] items;
    }

    [Serializable]
    public sealed class GameItemDefinition
    {
        public string id;
        public string displayName;
        public string description;
        public string usageDescription;
        public string spriteKey;
        public int initialQuantity;
        public int maxStack;
        public bool consumable;
        public string useActionId;
        public GameItemStatModifier[] statModifiers;
        public string etherType;
        public string skillId;
        public string skillName;
        public int skillDamage;
        public int recoveryAmount;
        public string[] curedStatusEffects;
        public string enhancementTarget;
        public float enhancementPercentage;
    }

    [Serializable]
    public sealed class GameItemStatModifier
    {
        public string statId;
        public float percentage;
    }
}
