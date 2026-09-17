namespace UnityGameData
{
    public enum TurnOrderResult { First, Second, UnresolvedTie }

    public static class TurnOrderResolver
    {
        // The caller owns the unresolved Priority+Speed tie rule.
        public static TurnOrderResult Compare(BattleCombatantState first, SkillData firstSkill,
            BattleCombatantState second, SkillData secondSkill)
        {
            if (firstSkill.Priority != secondSkill.Priority)
                return firstSkill.Priority > secondSkill.Priority ? TurnOrderResult.First : TurnOrderResult.Second;

            int firstSpeed = first.GetFinalSpeed();
            int secondSpeed = second.GetFinalSpeed();
            if (firstSpeed != secondSpeed)
                return firstSpeed > secondSpeed ? TurnOrderResult.First : TurnOrderResult.Second;

            return TurnOrderResult.UnresolvedTie;
        }
    }
}
