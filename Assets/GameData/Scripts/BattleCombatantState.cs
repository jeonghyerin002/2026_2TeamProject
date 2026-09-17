using System;
using System.Collections.Generic;

namespace UnityGameData
{
    // Per-battle mutable state. Never write HP, PP, or conditions back to the
    // CharacterData / SkillData ScriptableObject or CSV definitions.
    public sealed class BattleCombatantState
    {
        public readonly CharacterData Definition;
        public readonly StatusEffectState Statuses;
        private readonly Dictionary<int, int> remainingPp = new Dictionary<int, int>();

        public int CurrentHealth { get; private set; }
        public int MaxHealth { get { return Definition.fullHP; } }
        public bool IsFainted { get { return CurrentHealth <= 0; } }

        public BattleCombatantState(CharacterData definition, StatusEffectManager statusDefinitions,
            IEnumerable<SkillData> skills)
        {
            if (definition == null) throw new ArgumentNullException("definition");
            if (statusDefinitions == null) throw new ArgumentNullException("statusDefinitions");

            Definition = definition;
            Statuses = new StatusEffectState(statusDefinitions);
            CurrentHealth = Math.Max(0, definition.fullHP);
            if (skills == null) return;

            foreach (SkillData skill in skills)
                if (skill != null && !remainingPp.ContainsKey(skill.SkillId))
                    remainingPp.Add(skill.SkillId, skill.PP);
        }

        public int GetRemainingPp(int skillId)
        {
            int pp;
            return remainingPp.TryGetValue(skillId, out pp) ? pp : 0;
        }

        // Exposed deliberately; the unresolved PP-consumption timing belongs to the
        // action policy, not to UI or damage code.
        public bool TryConsumePp(int skillId)
        {
            int pp;
            if (!remainingPp.TryGetValue(skillId, out pp) || pp <= 0) return false;
            remainingPp[skillId] = pp - 1;
            return true;
        }

        public void ApplyDamage(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException("amount");
            CurrentHealth = Math.Max(0, CurrentHealth - amount);
        }

        public void RestoreHealth(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException("amount");
            CurrentHealth = Math.Min(MaxHealth, CurrentHealth + amount);
        }

        public void RestoreAllHealth()
        {
            CurrentHealth = MaxHealth;
        }

        public int GetFinalAttack()
        {
            return Statuses.HasBurn ? Half(Definition.attack) : Definition.attack;
        }

        public int GetFinalSpecialAttack()
        {
            return Statuses.HasFrostbite ? Half(Definition.specialAttack) : Definition.specialAttack;
        }

        public int GetFinalSpeed()
        {
            return Statuses.HasParalysis ? Half(Definition.speed) : Definition.speed;
        }

        private static int Half(int value) { return (int)Math.Floor(value * 0.5f); }
    }
}
