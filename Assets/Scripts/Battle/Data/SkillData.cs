using UnityEngine;


public enum SkillAttackType
{
    Physical,   // 물리 ( 공격 )
    Special,    // 특수 ( 특수공격 )
}

public enum ElementType
{
    Normal,     // 일반
    Fire,       // 불
    Water,      // 물
    Grass,      // 풀
    Electric,   // 전기
    Fighting    // 격투
}


/// <summary>스킬의 원본 전투 데이터와 추가 효과 비율을 저장한다</summary>
public class SkillData : ScriptableObject
{
    [SerializeField] private int skillid;                      // 스킬 ID

    [SerializeField] private string skillName;            // 스킬 이름

    [SerializeField] private ElementType type;             // 속성 타입

    [SerializeField] private SkillAttackType attackType;   // 공격 타입

    [SerializeField] private int damage;                // 위력

    [SerializeField] private int pp;                   // 사용 횟수

    [SerializeField] private int accuracy;                // 정확도
    
    [SerializeField] private int priority;                // 우선도

    [SerializeField] private int effectId;         // 스킬 특징 -> 버프, 디버프, 상태이상
    [SerializeField, Range(0f, 1f)] private float criticalChance = 0.04f;
    [SerializeField, Range(0f, 1f)] private float healRatio;
    [SerializeField, Range(0f, 1f)] private float drainRatio;
    [SerializeField, Range(0f, 1f)] private float recoilRatio;


    public int SkillId => skillid;
    public string Skillname => skillName;
    public ElementType Type => type;
    public SkillAttackType AttackType => attackType;
    public int Damage => damage;
    public int PP => pp;
    public int Accuracy => accuracy;
    public int Priority => priority;
    public int EffectId => effectId;
    public float CriticalChance => effectId == 60010 ? 0.25f : Mathf.Clamp01(criticalChance);
    public float HealRatio => Mathf.Clamp01(healRatio);
    public float DrainRatio => Mathf.Clamp01(drainRatio);
    public float RecoilRatio => Mathf.Clamp01(recoilRatio);
}
