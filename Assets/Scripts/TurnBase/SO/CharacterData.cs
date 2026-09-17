using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "CharacterData", menuName = "TurnBased/CharacterData")]
public class CharacterData : ScriptableObject
{
    public string characterName;
    public int fullHP;
    public int currentHP;
    [Header("Base stats")]
    public int attack;
    public int defense;
    public int specialAttack;
    public int specialDefense;
    public int speed;

    [Header("Battle loadout")]
    [Tooltip("An Aether may provide up to three skills. 0 means no Aether is equipped.")]
    public int equippedAetherItemId;
    [Tooltip("A Weapon may provide only SkillId1. 0 means no Weapon is equipped.")]
    public int equippedWeaponItemId;

    // Legacy prototype data. New battle code reads skills from the equipped item IDs
    // through GameDataCatalog, so this list is retained only for existing assets.
    public List<AttackData> attackList = new List<AttackData>();

    public void Init() //추후에 현재 hp를 충전행동하지 않으면 초기화 없이 진행하도록 수정
    {
        currentHP = fullHP;
        foreach (var attack in attackList)
        {
            if (attack !=  null)
            {
                attack.ResetCount();
            }
        }
    }
    public bool HasRemainingAttacks()
    {
        foreach (var attack in attackList)
        {
            if (attack != null && attack.currentCount > 0)
            {
                return true;
            }
        }
        return false;
    }

}
