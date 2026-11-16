using UnityEngine;

/// <summary>
/// 무기의 식별 정보와 표시 설명 및 전투 보정 원본 보관
/// </summary>
public class WeaponData : ScriptableObject
{
    // 무기 식별 정보
    [SerializeField] private int itemid;
    [SerializeField] private string weaponname;
    [SerializeField] private Sprite icon;

    // 인벤토리 상세창에 표시할 설명
    [TextArea(3, 10)]
    [SerializeField] private string description = "";

    // 전투 능력치 보정과 제공 기술
    [SerializeField] private float healthmultiplier;
    [SerializeField] private float attackmultiplier;
    [SerializeField] private float defensemultiplier;
    [SerializeField] private float specialattackmultiplier;
    [SerializeField] private float specialdefensemultiplier;
    [SerializeField] private float speedmultiplier;
    [SerializeField] private SkillData[] skills = new SkillData[1];

    public int ItemId => itemid;
    public string WeaponName => weaponname;
    public Sprite Icon => icon;
    public string Description => description;
    public float HealthMultiplier => healthmultiplier;
    public float AttackMultiplier => attackmultiplier;
    public float DefenseMultiplier => defensemultiplier;
    public float SpecialAttackMultiplier => specialattackmultiplier;
    public float SpecialDefenseMultiplier => specialdefensemultiplier;
    public float SpeedMultiplier => speedmultiplier;
    public SkillData[] Skills => skills;

}
