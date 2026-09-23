using System;
using System.Collections.Generic;
using UnityEngine;

public enum BattleStatus { None, Paralysis, Poison, Burn, Frostbite, Toxic, Sleep }
public enum BattleStat { Attack, Defense, SpecialAttack, SpecialDefense, Speed }

/// <summary>SO 원본을 보존하면서 장비 능력치와 전투 중 HP, PP, 상태이상을 관리한다</summary>
public sealed class BattleState
{
    private readonly Dictionary<SkillData, int> currentPp = new();
    private readonly SkillData[] skills;
    private readonly int[] ranks = new int[5];
    private int sleepTurns;
    private int confusionTurns;
    private int toxicStage;

    public CharacterData Character { get; }
    public AetherData Aether { get; }
    public WeaponData Weapon { get; }
    public int Level { get; }
    public int MaxHp { get; }
    public int CurrentHp { get; private set; }
    public int Experience { get; private set; }
    public BattleStatus Status { get; private set; }
    public bool IsConfused => confusionTurns > 0;
    public int SkillCount => skills.Length;
    public int Speed => GetStat(BattleStat.Speed);
    public bool IsDead => CurrentHp <= 0;

    // 원본 데이터와 선택 장비로 독립적인 전투 상태를 생성한다
    public BattleState(CharacterData character, AetherData aether, WeaponData weapon = null, int level = 1)
    {
        Character = character != null ? character : throw new ArgumentNullException(nameof(character));
        Aether = aether != null ? aether : throw new ArgumentNullException(nameof(aether));
        Weapon = weapon;
        Level = Mathf.Clamp(level, 1, 100);
        MaxHp = Scale(Character.MaxHp, Weapon != null ? Weapon.HealthMultiplier : 1f);
        CurrentHp = MaxHp;
        List<SkillData> list = new();
        AddSkills(list, Aether.Skills);
        if (Weapon != null)
            AddSkills(list, Weapon.Skills);
        skills = list.ToArray();
    }

    // 슬롯을 복사하고 같은 SO의 PP를 공유한다
    private void AddSkills(List<SkillData> list, SkillData[] source)
    {
        if (source == null)
            return;
        foreach (SkillData skill in source)
        {
            list.Add(skill);
            if (skill != null && !currentPp.ContainsKey(skill))
                currentPp.Add(skill, Mathf.Max(0, skill.PP));
        }
    }

    // 슬롯 번호로 스킬을 반환한다
    public SkillData GetSkill(int index)
    {
        return index >= 0 && index < skills.Length ? skills[index] : null;
    }

    // 스킬의 현재 PP를 반환한다
    public int GetCurrentPp(SkillData skill)
    {
        return skill != null && currentPp.TryGetValue(skill, out int pp) ? pp : 0;
    }

    // 실제 기술 사용 시 PP를 소비한다
    public bool TryConsumePp(SkillData skill)
    {
        int pp = GetCurrentPp(skill);
        if (pp <= 0 || IsDead)
            return false;
        currentPp[skill] = pp - 1;
        return true;
    }

    // 사용 가능한 첫 번째 슬롯을 반환한다
    public int GetAvailableSkill()
    {
        for (int i = 0; i < skills.Length; i++)
        {
            if (GetCurrentPp(skills[i]) > 0)
                return i;
        }
        return -1;
    }

    // 무기 배율과 랭크, 상태이상을 적용한 능력치를 반환한다
    public int GetStat(BattleStat stat)
    {
        int value;
        float multiplier;
        switch (stat)
        {
            case BattleStat.Attack: value = Character.Attack; multiplier = Weapon != null ? Weapon.AttackMultiplier : 1f; break;
            case BattleStat.Defense: value = Character.Defense; multiplier = Weapon != null ? Weapon.DefenseMultiplier : 1f; break;
            case BattleStat.SpecialAttack: value = Character.SpecialAttack; multiplier = Weapon != null ? Weapon.SpecialAttackMultiplier : 1f; break;
            case BattleStat.SpecialDefense: value = Character.SpecialDefense; multiplier = Weapon != null ? Weapon.SpecialDefenseMultiplier : 1f; break;
            default: value = Character.Speed; multiplier = Weapon != null ? Weapon.SpeedMultiplier : 1f; break;
        }
        int rank = ranks[(int)stat];
        float rankMultiplier = rank >= 0 ? (2f + rank) / 2f : 2f / (2f - rank);
        float statusMultiplier = (stat == BattleStat.Speed && Status == BattleStatus.Paralysis) ||
            (stat == BattleStat.Attack && Status == BattleStatus.Burn) ||
            (stat == BattleStat.SpecialAttack && Status == BattleStatus.Frostbite) ? 0.5f : 1f;
        return Scale(Scale(value, multiplier), rankMultiplier * statusMultiplier);
    }

    // 능력치 배율의 유효 범위를 보장한다
    private static int Scale(int value, float multiplier)
    {
        if (float.IsNaN(multiplier) || float.IsInfinity(multiplier) || multiplier <= 0f)
            multiplier = 1f;
        return Mathf.Max(1, Mathf.FloorToInt(Mathf.Max(1, value) * multiplier));
    }

    // 능력치 랭크를 제한 범위 안에서 변경한다
    public bool ChangeRank(BattleStat stat, int amount)
    {
        int index = (int)stat;
        int next = Mathf.Clamp(ranks[index] + amount, -6, 6);
        if (next == ranks[index])
            return false;
        ranks[index] = next;
        return true;
    }

    // 상태이상 중복과 독의 맹독 전환을 판정한다
    public bool ApplyStatus(BattleStatus status, int turns = 0, bool clear = false)
    {
        if (IsDead || status == BattleStatus.None)
            return false;
        if (!clear && Status != BattleStatus.None && !(Status == BattleStatus.Poison && status == BattleStatus.Toxic))
            return false;
        if (clear)
            confusionTurns = 0;
        Status = status;
        sleepTurns = status == BattleStatus.Sleep ? Mathf.Max(1, turns) : 0;
        toxicStage = 0;
        return true;
    }

    // 다른 상태이상과 공존하는 혼란을 적용한다
    public bool ApplyConfusion(int turns)
    {
        if (IsDead || IsConfused)
            return false;
        confusionTurns = Mathf.Max(1, turns);
        return true;
    }

    // 자신의 행동 기회마다 행동 가능 여부를 확인한다
    public bool CanAct(out string reason)
    {
        reason = null;
        if (IsDead)
            return false;
        if (Status == BattleStatus.Sleep)
        {
            if (sleepTurns-- > 0)
            {
                reason = "잠들어 있어 움직일 수 없다!";
                return false;
            }
            Status = BattleStatus.None;
        }
        if (Status == BattleStatus.Paralysis && UnityEngine.Random.value < 0.25f)
        {
            reason = "몸이 마비되어 움직일 수 없다!";
            return false;
        }
        if (confusionTurns > 0)
        {
            confusionTurns--;
            if (UnityEngine.Random.value < 1f / 3f)
            {
                TakeDamage(BattleResolver.GetBaseDamage(Level, 40, GetStat(BattleStat.Attack), GetStat(BattleStat.Defense)));
                reason = "혼란에 빠져 자신을 공격했다!";
                return false;
            }
        }
        return true;
    }

    // 턴 종료 시 지속 상태이상 피해를 적용한다
    public int ApplyResidualDamage()
    {
        int damage = 0;
        if (Status == BattleStatus.Poison)
            damage = Mathf.Max(1, MaxHp / 8);
        else if (Status == BattleStatus.Burn || Status == BattleStatus.Frostbite)
            damage = Mathf.Max(1, MaxHp / 16);
        else if (Status == BattleStatus.Toxic)
            damage = Mathf.Max(1, MaxHp * Mathf.Min(++toxicStage, 15) / 16);
        int before = CurrentHp;
        TakeDamage(damage);
        return before - CurrentHp;
    }

    // 교체 시 일시적인 상태와 랭크를 해제한다
    public void ResetVolatileState()
    {
        Array.Clear(ranks, 0, ranks.Length);
        confusionTurns = 0;
        toxicStage = 0;
    }

    // 전투 종료 시 일시적인 상태를 해제한다
    public void FinishBattle()
    {
        ResetVolatileState();
        if (Status == BattleStatus.Sleep)
            Status = BattleStatus.None;
    }

    // 경험치를 전투 상태에 누적한다
    public void GainExperience(int amount)
    {
        Experience += Mathf.Max(0, amount);
    }

    // 데미지만큼 현재 HP를 감소한다
    public void TakeDamage(int damage)
    {
        if (damage > 0 && !IsDead)
            CurrentHp = Mathf.Max(0, CurrentHp - damage);
    }

    // 최대 HP를 넘지 않도록 현재 HP를 회복한다
    public void Heal(int amount)
    {
        if (amount > 0 && !IsDead)
            CurrentHp += Mathf.Min(amount, MaxHp - CurrentHp);
    }
}
