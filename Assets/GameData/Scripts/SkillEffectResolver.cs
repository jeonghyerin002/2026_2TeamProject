using System;

namespace UnityGameData
{
    public static class SkillEffectResolver
    {
        public const string ApplyStatusEffect = "ApplyStatusEffect";
        public const int GoToSleepEffectId = 60007;

        // This is intentionally separate from accuracy. Call it only at the point
        // selected by the future action-order policy.
        public static bool TryApply(BattleCombatantState target, SkillEffectData effect,
            float chanceRoll0To100, TimedSkillEffectState timedEffects, out StatusApplyResult result)
        {
            if (target == null) throw new ArgumentNullException("target");
            if (effect == null) throw new ArgumentNullException("effect");
            if (timedEffects == null) throw new ArgumentNullException("timedEffects");
            if (chanceRoll0To100 < 0f || chanceRoll0To100 > 100f)
                throw new ArgumentOutOfRangeException("chanceRoll0To100");

            result = StatusApplyResult.BlockedByPrimaryStatus;
            if (effect.EffectType != ApplyStatusEffect || effect.StatusEffectId == 0 || chanceRoll0To100 >= effect.Chance)
                return false;

            result = target.Statuses.TryApply(effect.StatusEffectId, effect.EffectId == GoToSleepEffectId);
            if (result == StatusApplyResult.Applied || result == StatusApplyResult.ReplacedPoisonWithToxic ||
                result == StatusApplyResult.SleptAndClearedAll)
                timedEffects.Apply(effect);
            return true;
        }
    }
}
