using System;
using UnityEngine;

/// <summary>행동 가능 여부와 명중을 판정하고 데미지와 부가효과를 적용한다</summary>
public static class BattleResolver
{
    // 다음 행동을 즉시 실행하고 완료한다
    public static bool TryResolveNext(BattleSystem battleSystem, out BattleState actor, out BattleResolveResult result)
    {
        actor = null;
        result = default;
        if (battleSystem == null || !battleSystem.TryGetNextAction(out actor, out BattleState target, out SkillData skill))
            return false;
        result = Resolve(actor, target, skill);
        battleSystem.CompleteAction();
        return true;
    }

    // 연출이 없는 환경에서 선택된 행동을 순서대로 처리한다
    public static BattleResolveResult Resolve(BattleState actor, BattleState target, SkillData skill)
    {
        if (!TryBegin(actor, target, skill, out BattleResolveResult result))
            return result;
        if (!IsHit(skill))
            return new BattleResolveResult(true, false, 0, target.IsDead);
        result = ApplyDamage(actor, target, skill);
        ApplyEffects(actor, target, skill, result);
        return new BattleResolveResult(true, true, result.Damage, target.IsDead, result.Critical, result.Effectiveness);
    }

    // 행동 가능 여부를 확인한 뒤 실제 기술의 PP를 소비한다
    public static bool TryBegin(BattleState actor, BattleState target, SkillData skill, out BattleResolveResult result)
    {
        result = default;
        if (actor == null || target == null || actor.IsDead || target.IsDead)
            return false;
        if (!actor.CanAct(out string reason))
        {
            result = new BattleResolveResult(false, false, 0, target.IsDead, message: reason);
            return false;
        }
        // 모든 PP가 소진된 경우에만 null 스킬을 발버둥으로 취급한다
        if (skill == null ? actor.GetAvailableSkill() >= 0 : !actor.TryConsumePp(skill))
            return false;
        return true;
    }

    // 정확도 백분율로 명중 여부를 판정한다
    public static bool IsHit(SkillData skill)
    {
        return skill == null || UnityEngine.Random.Range(0, 100) < Mathf.Clamp(skill.Accuracy, 0, 100);
    }

    // 기본값, 자속, 상성, 급소 순서로 계산한 피해를 적용한다
    public static BattleResolveResult ApplyDamage(BattleState actor, BattleState target, SkillData skill)
    {
        int power = skill != null ? skill.Damage : 50;
        bool physical = skill == null || skill.AttackType == SkillAttackType.Physical;
        int attack = actor.GetStat(physical ? BattleStat.Attack : BattleStat.SpecialAttack);
        int defense = target.GetStat(physical ? BattleStat.Defense : BattleStat.SpecialDefense);
        int damage = GetBaseDamage(actor.Level, power, attack, defense);
        float stab = skill != null && skill.Type == actor.Aether.AetherType ? 1.5f : 1f;
        float effectiveness = skill != null ? GetEffectiveness(skill.Type, target.Aether.AetherType) : 1f;
        damage = Mathf.FloorToInt(damage * stab);
        damage = Mathf.FloorToInt(damage * effectiveness);
        bool critical = power > 0 && effectiveness > 0f && UnityEngine.Random.value < (skill != null ? skill.CriticalChance : 0.0625f);
        if (critical)
            damage *= 2;
        if (power > 0 && effectiveness > 0f)
            damage = Mathf.Max(1, damage);
        int actual = Mathf.Min(damage, target.CurrentHp);
        target.TakeDamage(damage);
        return new BattleResolveResult(true, true, actual, target.IsDead, critical, effectiveness);
    }

    // 정수 나눗셈으로 기본 데미지를 계산한다
    public static int GetBaseDamage(int level, int power, int attack, int defense)
    {
        if (power <= 0)
            return 0;
        long damage = (2L * Mathf.Clamp(level, 1, 100) / 5 + 2) * power;
        damage = damage * Mathf.Max(1, attack) / Mathf.Max(1, defense) / 50 + 2;
        return (int)Math.Min(damage, int.MaxValue / 4);
    }

    // 현재 프로젝트의 여섯 속성 사이 상성을 반환한다
    public static float GetEffectiveness(ElementType attack, ElementType defense)
    {
        switch (attack)
        {
            case ElementType.Fire:
                if (defense == ElementType.Grass) return 2f;
                if (defense == ElementType.Fire || defense == ElementType.Water) return 0.5f;
                break;
            case ElementType.Water:
                if (defense == ElementType.Fire) return 2f;
                if (defense == ElementType.Water || defense == ElementType.Grass) return 0.5f;
                break;
            case ElementType.Grass:
                if (defense == ElementType.Water) return 2f;
                if (defense == ElementType.Fire || defense == ElementType.Grass) return 0.5f;
                break;
            case ElementType.Electric:
                if (defense == ElementType.Water) return 2f;
                if (defense == ElementType.Electric || defense == ElementType.Grass) return 0.5f;
                break;
            case ElementType.Fighting:
                if (defense == ElementType.Normal) return 2f;
                break;
        }
        return 1f;
    }

    // 명중 후 CSV 효과와 회복, 흡수, 반동을 적용한다
    public static string ApplyEffects(BattleState actor, BattleState target, SkillData skill, BattleResolveResult result)
    {
        if (!result.Hit || result.Effectiveness <= 0f)
            return null;
        if (skill == null)
        {
            actor.TakeDamage(Mathf.Max(1, actor.MaxHp / 4));
            return "발버둥의 반동을 받았다!";
        }
        string message = ApplyEffectId(actor, target, skill.EffectId);
        int before = actor.CurrentHp;
        actor.Heal(Mathf.FloorToInt(actor.MaxHp * skill.HealRatio) + Mathf.FloorToInt(result.Damage * skill.DrainRatio));
        if (actor.CurrentHp > before)
            message = Append(message, "HP를 회복했다!");
        int recoil = Mathf.FloorToInt(result.Damage * skill.RecoilRatio);
        if (recoil > 0)
        {
            actor.TakeDamage(recoil);
            message = Append(message, "반동 데미지를 받았다!");
        }
        return message;
    }

    // 기존 SkillEffectData CSV의 효과 ID를 전투 규칙에 연결한다
    private static string ApplyEffectId(BattleState actor, BattleState target, int id)
    {
        if (id >= 60001 && id <= 60006)
        {
            if (UnityEngine.Random.value >= 0.3f)
                return null;
            bool applied = id == 60006 ? target.ApplyConfusion(UnityEngine.Random.Range(2, 6)) :
                target.ApplyStatus((BattleStatus)(id - 60000));
            return applied ? $"{target.Character.Charactername}: {(id == 60006 ? "Confusion" : target.Status.ToString())}!" : null;
        }
        if (id >= 60007 && id <= 60009)
        {
            BattleState recipient = id == 60007 ? actor : target;
            if (id == 60008 && UnityEngine.Random.value >= 0.6f)
                return null;
            return recipient.ApplyStatus(BattleStatus.Sleep, UnityEngine.Random.Range(1, 4), id == 60007) ?
                $"{recipient.Character.Charactername}이(가) 잠들었다!" : null;
        }
        if (id >= 60012 && id <= 60016)
        {
            BattleStat stat = id == 60012 ? BattleStat.Defense : id == 60013 ? BattleStat.Attack : (BattleStat)(id - 60012);
            return actor.ChangeRank(stat, 1) ? $"{actor.Character.Charactername}: {stat} 상승!" : "능력치는 더 올라가지 않는다!";
        }
        return null;
    }

    // 여러 부가효과 메시지를 순서대로 합친다
    private static string Append(string first, string next)
    {
        return string.IsNullOrEmpty(first) ? next : first + "\n" + next;
    }
}

/// <summary>행동 실행 여부와 피해, 상성, 급소 정보를 전달한다</summary>
public readonly struct BattleResolveResult
{
    public bool Executed { get; }
    public bool Hit { get; }
    public int Damage { get; }
    public bool TargetDead { get; }
    public bool Critical { get; }
    public float Effectiveness { get; }
    public string Message { get; }

    // 스킬 실행 결과를 저장한다
    public BattleResolveResult(bool executed, bool hit, int damage, bool targetDead, bool critical = false, float effectiveness = 1f, string message = null)
    {
        Executed = executed;
        Hit = hit;
        Damage = damage;
        TargetDead = targetDead;
        Critical = critical;
        Effectiveness = effectiveness;
        Message = message;
    }
}
