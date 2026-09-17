using System;

namespace UnityGameData
{
    public readonly struct DamageInputs
    {
        public readonly float Power;
        public readonly int AttackingStat;
        public readonly int DefendingStat;
        public DamageInputs(float power, int attackingStat, int defendingStat)
        {
            Power = power;
            AttackingStat = attackingStat;
            DefendingStat = defendingStat;
        }
    }

    public static class DamageCalculator
    {
        // The final formula is intentionally not implemented because it is not yet
        // a game rule. This method only selects the required stat pair.
        public static DamageInputs GetInputs(BattleCombatantState attacker,
            BattleCombatantState defender, SkillData skill)
        {
            if (skill.AttackType == AttackType.Physical)
                return new DamageInputs(skill.Damage, attacker.GetFinalAttack(), defender.Definition.defense);
            if (skill.AttackType == AttackType.Special)
                return new DamageInputs(skill.Damage, attacker.GetFinalSpecialAttack(), defender.Definition.specialDefense);
            return new DamageInputs(0f, 0, 0);
        }

        public static int CalculateFinalDamage(DamageInputs inputs)
        {
            throw new NotSupportedException("Final damage formula has not been specified.");
        }
    }
}
