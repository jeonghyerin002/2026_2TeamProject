using UnityEngine;


// 에테르의 기본 정보와 사용 가능한 스킬 목록을 저장한다
public class AetherData : ScriptableObject
{
    [SerializeField] private int itemid;                // 에테르 ID
    [SerializeField] private string aethername;     // 에테르 이름
    [SerializeField] private ElementType aethertype;       // 속성 타입
    [SerializeField] private Sprite icon;
    [SerializeField] private SkillData[] skills = new SkillData[3];     // 에테르 스킬 목록

    public int itemId => itemid;
    public string AetherName => aethername;
    public ElementType AetherType => aethertype;
    public Sprite Icon => icon;
    public SkillData[] Skills => skills;
}
