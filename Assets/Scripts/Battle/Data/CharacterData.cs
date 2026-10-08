using UnityEngine;


// 캐릭터의 기본 능력치 원본 데이터를 저장한다
public class CharacterData : ScriptableObject
{
    [SerializeField] private int characterid;                // 캐릭터 ID
    [SerializeField] private string charactername;  // 캐릭터 이름
    [SerializeField] private int maxhp;             // 체력
    [SerializeField] private int attack;            // 공격
    [SerializeField] private int defense;           // 방어
    [SerializeField] private int specialattack;     // 특수공격
    [SerializeField] private int specialdefense;    // 특수방어
    [SerializeField] private int speed;            // 스피드


    public int CharacterId => characterid;
    public string Charactername => charactername;
    public int MaxHp => maxhp;
    public int Attack => attack;
    public int Defense => defense;
    public int SpecialAttack => specialattack;
    public int SpecialDefense => specialdefense;
    public int Speed => speed;
}