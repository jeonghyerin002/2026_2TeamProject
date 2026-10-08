using UnityEngine;

public class WeaponData : ScriptableObject
{
    [SerializeField] private int itemid;
    [SerializeField] private string weaponname;
    [SerializeField] private Sprite icon;
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
    public float HealthMultiplier => healthmultiplier;
    public float AttackMultiplier => attackmultiplier;
    public float DefenseMultiplier => defensemultiplier;
    public float SpecialAttackMultiplier => specialattackmultiplier;
    public float SpecialDefenseMultiplier => specialdefensemultiplier;
    public float SpeedMultiplier => speedmultiplier;
    public SkillData[] Skills => skills;

}
