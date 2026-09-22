using System.Collections.Generic;
using UnityEngine;

/// <summary>CharacterData와 AetherData를 기반으로 전투 중 변하는 HP와 PP를 관리한다.</summary>
public sealed class BattleState
{
    private readonly Dictionary<int, int> currentPp = new();

    public CharacterData Character { get; }
    public AetherData Aether { get; }
    public int CurrentHp { get; private set; }
    public int Speed => Character.Speed;
    public bool IsDead => CurrentHp <= 0;

    // 캐릭터와 에테르 원본 데이터로 전투 상태 생성
    public BattleState(CharacterData character, AetherData aether)
    {
        Character = character;
        Aether = aether;
        CurrentHp = character.MaxHp;
        InitializePp();
    }

    // 에테르 스킬의 현재 PP 초기화
    private void InitializePp()
    {
        currentPp.Clear();

        SkillData[] skills = Aether.Skills;
        if (skills == null)
            return;

        foreach (SkillData skill in skills)
        {
            if (skill == null || currentPp.ContainsKey(skill.SkillId))
                continue;

            currentPp.Add(skill.SkillId, skill.PP);
        }
    }

    // 슬롯 번호로 스킬 반환
    public SkillData GetSkill(int index)
    {
        SkillData[] skills = Aether.Skills;

        if (skills == null || index < 0 || index >= skills.Length)
            return null;

        return skills[index];
    }

    // 스킬의 현재 PP 반환
    public int GetCurrentPp(SkillData skill)
    {
        if (skill == null)
            return 0;

        return currentPp.TryGetValue(skill.SkillId, out int pp) ? pp : 0;
    }

    // 사용 가능한 스킬이면 PP 1 감소
    public bool TryConsumePp(SkillData skill)
    {
        if (skill == null ||
            !currentPp.TryGetValue(skill.SkillId, out int pp) ||
            pp <= 0)
            return false;

        currentPp[skill.SkillId] = pp - 1;
        return true;
    }

    // 데미지만큼 현재 HP 감소
    public void TakeDamage(int damage)
    {
        if (damage <= 0 || IsDead)
            return;

        CurrentHp = Mathf.Max(0, CurrentHp - damage);
    }

    // 최대 HP를 넘지 않도록 현재 HP 회복
    public void Heal(int amount)
    {
        if (amount <= 0 || IsDead)
            return;

        CurrentHp = Mathf.Min(Character.MaxHp, CurrentHp + amount);
    }
}
