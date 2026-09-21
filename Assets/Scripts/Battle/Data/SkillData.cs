using UnityEngine;


public enum SkillCategory
{
    Physical,   // 물리 ( 공격 )
    Special,    // 특수 ( 특수공격 )
    Status      // 변화 ( 버프, 디버프 )
}

public enum SkillEffectType
{
    None,

    // 능력치 증가
    AttackUp,
    DefenseUp,
    SpecialAttackUp,
    SpecialDefenseUp,
    SpeedUp,

    // 능력치 감소
    AttackDown,
    DefenseDown,
    SpecialAttackDown,
    SpecialDefenseDown,
    SpeedDown,

    // 상태이상
    Burn,
    Poison,
    Paralysis,
    Sleep,
    Freeze,

    // 기타
    Heal
}

public enum ElementType
{
    Normal,     // 일반
    Fire,       // 불
    Water,      // 물
    Grass,      // 풀
    Electric    // 전기
}


// 스킬의 원본 전투 데이터를 저장한다
public class SkillData : ScriptableObject
{
    [SerializeField] private int id;                      // 스킬 ID
    [SerializeField] private string displayName;           // 스킬 이름
    [SerializeField] private int power;                   // 위력
    [SerializeField] private int accuracy;                // 정확도
    [SerializeField] private int maxPP;                   // 사용 횟수
    [SerializeField] private int priority;                // 우선도
    [SerializeField] private ElementType type;             // 속성 타입
    [SerializeField] private SkillCategory category;       // 스킬 종류 -> 물리, 특수, 변화
    [SerializeField] private SkillEffectType effectType;   // 스킬 특징 -> 버프, 디버프, 상태이상


    public int Id => id;
    public string Name => displayName;
    public int Power => power;
    public int Accuracy => accuracy;
    public int MaxPP => maxPP;
    public int Priority => priority;
    public ElementType Type => type;
    public SkillCategory Category => category;
    public SkillEffectType EffectType => effectType;
}