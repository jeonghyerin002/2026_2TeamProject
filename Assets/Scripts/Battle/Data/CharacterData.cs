using UnityEngine;


// 캐릭터의 기본 능력치 원본 데이터를 저장한다
public class CharacterData : ScriptableObject
{
    [SerializeField] private int id;                // 캐릭터 ID
    [SerializeField] private string name;    // 캐릭터 이름
    [SerializeField] private int maxHp;             // 체력
    [SerializeField] private int attack;            // 공격
    [SerializeField] private int defense;           // 방어
    [SerializeField] private int specialAttack;     // 특수공격
    [SerializeField] private int specialDefense;    // 특수방어
    [SerializeField] private int speed;            // 스피드


    public int Id => id;
    public string Name => name;
    public int MaxHp => maxHp;
    public int Attack => attack;
    public int Defense => defense;
    public int SpecialAttack => specialAttack;
    public int SpecialDefense => specialDefense;
    public int Speed => speed;
}