using UnityEngine;

public class WeaponData : ScriptableObject
{
    [SerializeField] private int itemid;
    [SerializeField] private string weaponName;
    [SerializeField] private float healthMultiplier;
    [SerializeField] private float attackMultiplier;
    [SerializeField] private float defenseMultiplier;
    [SerializeField] private float specialAttackMultiplier;
    [SerializeField] private float specialDefenseMultiplier;
    [SerializeField] private float speedMultiplier;
    [SerializeField] private SkillData[] skills = new SkillData[1];

    public int ItemId => itemid;
    public string WeaponName => weaponName;
    public float HealthMultiplier => healthMultiplier;
    public float AttackMultiplier => attackMultiplier;
    public float DefenseMultiplier => defenseMultiplier;
    public float SpecialAttackMultiplier => specialAttackMultiplier;
    public float SpecialDefenseMultiplier => specialDefenseMultiplier;
    public float SpeedMultiplier => speedMultiplier;
    public SkillData[] Skills => skills;

}
