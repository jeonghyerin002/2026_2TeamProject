using UnityEngine;

/// <summary>NPC 한 명이 사용할 전투 원본 데이터를 저장한다</summary>
[CreateAssetMenu(fileName = "NpcBattleData", menuName = "Battle/NPC Battle Data")]
public sealed class NpcBattleData : ScriptableObject
{
    [SerializeField] private CharacterData character;
    [SerializeField] private AetherData aether;
    [SerializeField] private WeaponData weapon;
    [SerializeField, Range(1, 100)] private int level = 1;
    [SerializeField, Min(0)] private int experienceReward = 50;

    public CharacterData Character => character;
    public AetherData Aether => aether;
    public WeaponData Weapon => weapon;
    public int Level => level;
    public int ExperienceReward => experienceReward;
    public bool IsValid => character != null && aether != null;
}