using UnityEngine;


// 에테르의 기본 정보와 사용 가능한 스킬 목록을 저장한다
public class AetherData : ScriptableObject
{
    [SerializeField] private int skillId;                // 에테르 ID
    [SerializeField] private string aetherName;     // 에테르 이름
    [SerializeField] private ElementType type;       // 속성 타입
    [SerializeField] private SkillData[] skills = new SkillData[3];     // 에테르 스킬 목록

    public int SkillId => skillId;
    public string AetherName => aetherName;
    public ElementType Type => type;
    public SkillData[] Skills => skills;
}