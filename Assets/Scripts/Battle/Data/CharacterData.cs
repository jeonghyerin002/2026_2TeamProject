using UnityEngine;


/// <summary>캐릭터의 기본 능력치와 전투 표시용 원본 데이터를 저장한다.</summary>
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
    [SerializeField, Range(1, 100)] private int level = 1;
    [SerializeField] private int weaponid;
    [SerializeField] private int aetherid;
    [SerializeField, Min(0)] private int rewardexp;
    [SerializeField] private WeaponData weapon;
    [SerializeField] private AetherData aether;
    [SerializeField] private Sprite battleSprite; // 전투 씬에서 상대 캐릭터를 표시할 이미지
    [SerializeField] private Vector2 battleLaunch = new(0.5f, 0.5f); // 실제 표시 이미지의 왼쪽 아래가 (0, 0)인 발사 지점, 바깥 좌표도 허용
    [SerializeField] private Vector2 battleHit = new(0.5f, 0.5f);    // 실제 표시 이미지 안에서 스킬이 닿을 몸 부위


    public int CharacterId => characterid;
    public string Charactername => charactername;
    public int MaxHp => maxhp;
    public int Attack => attack;
    public int Defense => defense;
    public int SpecialAttack => specialattack;
    public int SpecialDefense => specialdefense;
    public int Speed => speed;
    public int Level => level;
    public int WeaponId => weaponid;
    public int AetherId => aetherid;
    public int RewardExp => rewardexp;
    public WeaponData Weapon => weapon;
    public AetherData Aether => aether;
    public Sprite BattleSprite => battleSprite;
    public Vector2 BattleLaunch => battleLaunch;
    public Vector2 BattleHit => battleHit;
    public bool IsValid => aether != null && aether.itemId == aetherid &&
        (weaponid == 0 ? weapon == null : weapon != null && weapon.ItemId == weaponid);
}
