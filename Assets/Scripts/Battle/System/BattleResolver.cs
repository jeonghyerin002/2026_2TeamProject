using UnityEngine;

/// <summary>BattleSystem의 다음 스킬 행동을 가져와 명중 판정, 데미지 계산, HP 변경까지 처리한다.</summary>
public static class BattleResolver
{
    // BattleSystem의 다음 행동을 가져와 실행하고 완료 처리
    public static bool TryResolveNext(BattleSystem battleSystem, out BattleState actor, out BattleResolveResult result)
    {
        actor = null;
        result = default;

        if (battleSystem == null ||
            !battleSystem.TryGetNextAction(
                out actor,
                out BattleState target,
                out SkillData skill))
            return false;

        result = Resolve(actor, target, skill);

        battleSystem.CompleteAction();

        return true;
    }

    // 선택된 스킬 하나를 실제 전투에 적용
    public static BattleResolveResult Resolve(BattleState actor, BattleState target, SkillData skill)
    {
        if (actor == null || target == null || skill == null || actor.IsDead || target.IsDead)
            return default;

        // PP는 BattleSystem에서 스킬 선택 시 이미 소비한다
        if (!IsHit(skill))
            return new BattleResolveResult(true, false, 0, target.IsDead);

        int damage = CalculateDamage(actor, target, skill);

        // 데미지 계산 후 HP 변경
        target.TakeDamage(damage);

        return new BattleResolveResult(true, true, damage, target.IsDead);
    }

    // 스킬 Accuracy 기준 명중 여부 결정
    private static bool IsHit(SkillData skill)
    {
        int accuracy = Mathf.Clamp(skill.Accuracy, 0, 100);
        return accuracy >= 100 || Random.Range(1, 101) <= accuracy;
    }

    // Physical / Special에 맞는 능력치를 사용해 데미지 계산
    private static int CalculateDamage(BattleState actor, BattleState target, SkillData skill)
    {
        if (skill.Damage <= 0)
            return 0;

        int attack = skill.AttackType == SkillAttackType.Physical
            ? actor.Character.Attack
            : actor.Character.SpecialAttack;

        int defense = skill.AttackType == SkillAttackType.Physical
            ? target.Character.Defense
            : target.Character.SpecialDefense;

        // 임시 기본 공식. 최종 데미지 공식 확정 시 이 함수만 교체한다
        return Mathf.Max(1, Mathf.RoundToInt(skill.Damage * (float)attack / Mathf.Max(1, defense)));
    }
}

/// <summary>BattleResolver의 스킬 실행 결과를 전달한다.</summary>
public readonly struct BattleResolveResult
{
    public bool Executed { get; }
    public bool Hit { get; }
    public int Damage { get; }
    public bool TargetDead { get; }

    // 스킬 실행 결과 저장
    public BattleResolveResult(
        bool executed,
        bool hit,
        int damage,
        bool targetDead)
    {
        Executed = executed;
        Hit = hit;
        Damage = damage;
        TargetDead = targetDead;
    }
}
