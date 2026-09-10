using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "CharacterData", menuName = "TurnBased/CharacterData")]
public class CharacterData : ScriptableObject
{
    public string characterName;
    public int fullHP;
    public int currentHP;
    public int speed;
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
