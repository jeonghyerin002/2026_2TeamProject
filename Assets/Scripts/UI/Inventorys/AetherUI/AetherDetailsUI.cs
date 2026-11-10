using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 선택한 에테르의 기본 정보와 스킬 버튼 및 선택 스킬 상세 정보 표시
/// </summary>
[DisallowMultipleComponent]
public sealed class AetherDetailsUI : MonoBehaviour
{
    // 에테르 기본 정보와 스킬 표시 대상
    [SerializeField] private TMP_Text aetherName;
    [SerializeField] private TMP_Text aetherType;
    [SerializeField] private TMP_Text equipmentStatus;
    [SerializeField] private TMP_Text skillSlot1;
    [SerializeField] private TMP_Text skillSlot2;
    [SerializeField] private TMP_Text skillSlot3;

    // 스킬 선택 버튼과 상세 정보 표시 대상
    [SerializeField] private Button skillButton1;
    [SerializeField] private Button skillButton2;
    [SerializeField] private Button skillButton3;
    [SerializeField] private TMP_Text skillType;
    [SerializeField] private TMP_Text skillPower;
    [SerializeField] private TMP_Text skillAttackType;
    [SerializeField] private TMP_Text skillPp;
    [SerializeField] private TMP_Text skillAccuracy;

    // 장착 데이터 소유자와 현재 선택 슬롯
    private PlayerBattleData playerData;
    private int selectedSlot = -1;
    private int selectedSkill = -1;


    // 부모 플레이어 데이터와 필수 텍스트 참조 확인
    private void Awake()
    {
        // 화면을 소유한 플레이어의 장착 데이터 확보 및 설정 누락 시 동작 중단
        playerData = GetComponentInParent<PlayerBattleData>();

        if (playerData == null || aetherName == null || aetherType == null ||
            skillSlot1 == null || skillSlot2 == null || skillSlot3 == null ||
            skillButton1 == null || skillButton2 == null || skillButton3 == null ||
            skillType == null || skillPower == null ||
            skillAttackType == null || skillPp == null || skillAccuracy == null)
        {
            Debug.LogError("AetherDetailsUI: 부모 PlayerBattleData와 에테르 텍스트, 스킬 버튼 3개, 스킬 상세 텍스트 5개를 연결하세요.", this);
            enabled = false;
        }
    }


    // 활성 상태의 장착 변경 구독과 선택 정보 갱신
    private void OnEnable()
    {
        // 검증된 참조로 장착 변경 수신 및 재진입 시 최신 정보 표시
        playerData.EquipmentChanged += Refresh;
        Refresh();
    }


    // 비활성 상태의 장착 변경 구독 해제
    private void OnDisable()
    {
        // 초기화 실패 또는 플레이어 파괴 상태를 고려한 구독 정리
        if (playerData != null)
        {
            playerData.EquipmentChanged -= Refresh;
        }
    }


    // 클릭한 장착 슬롯 선택과 상세 정보 갱신
    public void ShowSlot(int slotIndex)
    {
        // 비활성 화면과 장착 범위를 벗어난 슬롯 선택 제외
        if (!isActiveAndEnabled || slotIndex < 0 || slotIndex >= playerData.AetherSlotCount)
        {
            return;
        }

        // 선택 번호 보관 후 현재 장착 정보를 텍스트에 반영
        selectedSlot = slotIndex;
        selectedSkill = -1;
        Refresh();
    }


    // 선택 슬롯의 현재 에테르와 스킬 이름 표시
    private void Refresh()
    {
        // 선택 전 또는 빈 슬롯의 기본 정보 표시 초기화
        AetherData aether = playerData.GetEquippedAether(selectedSlot);
        aetherName.text = aether != null ? aether.AetherName : string.Empty;
        aetherType.text = aether != null ? GetElementName(aether.AetherType) : string.Empty;

        // 상태 표시가 연결된 화면에서 선택 슬롯 기준의 장착 문구 갱신
        if (equipmentStatus != null)
        {
            equipmentStatus.text = aether != null ? (selectedSlot == 0 ? "장착중" : "미장착") : string.Empty;
        }

        // 원본 스킬 배열 순서 유지 및 누락 항목의 빈 텍스트 표시
        SkillData[] skills = aether != null ? aether.Skills : null;
        skillSlot1.text = GetSkillName(skills, 0);
        skillSlot2.text = GetSkillName(skills, 1);
        skillSlot3.text = GetSkillName(skills, 2);

        // 미등록 스킬의 버튼 입력 차단 및 현재 선택 스킬 상세 정보 반영
        skillButton1.interactable = GetSkill(skills, 0) != null;
        skillButton2.interactable = GetSkill(skills, 1) != null;
        skillButton3.interactable = GetSkill(skills, 2) != null;
        RefreshSkillDetails(GetSkill(skills, selectedSkill));
    }


    // 버튼의 0부터 시작하는 스킬 번호로 상세 정보 선택
    public void ShowSkill(int skillIndex)
    {
        // 비활성 화면과 스킬 슬롯 범위 밖의 요청 제외
        if (!isActiveAndEnabled || skillIndex < 0 || skillIndex > 2)
        {
            return;
        }

        // 현재 선택 에테르의 스킬 데이터 존재 여부 확인
        AetherData aether = playerData.GetEquippedAether(selectedSlot);
        SkillData skill = GetSkill(aether != null ? aether.Skills : null, skillIndex);

        if (skill == null)
        {
            return;
        }

        // 선택 스킬 보관 후 상세 텍스트 갱신
        selectedSkill = skillIndex;
        RefreshSkillDetails(skill);
    }


    // 선택 스킬의 원본 수치와 한글 타입 표시
    private void RefreshSkillDetails(SkillData skill)
    {
        // 선택 전 또는 빈 스킬의 상세 표시 초기화
        skillType.text = skill != null ? GetElementName(skill.Type) : string.Empty;
        skillPower.text = skill != null ? skill.Damage.ToString() : string.Empty;
        skillAttackType.text = skill != null ? GetAttackTypeName(skill.AttackType) : string.Empty;
        // 전투 후에도 보관한 남은 횟수와 원본 최대 횟수 표시
        skillPp.text = skill != null ? $"{playerData.GetCurrentPp(skill)} / {Mathf.Max(0, skill.PP)}" : string.Empty;
        skillAccuracy.text = skill != null ? skill.Accuracy.ToString() : string.Empty;
    }


    // 스킬 공격 분류를 화면 표시용 한글 이름으로 변환
    private static string GetAttackTypeName(SkillAttackType type)
    {
        // 원본 공격 분류에 대응하는 한글 이름 전달
        return type switch
        {
            SkillAttackType.Physical => "물리",
            SkillAttackType.Special => "특수",
            SkillAttackType.Status => "변화",
            _ => string.Empty
        };
    }


    // 원본 속성 타입을 화면 표시용 한글 이름으로 변환
    private static string GetElementName(ElementType type)
    {
        // 에테르 데이터의 속성값에 대응하는 한글 이름 전달
        return type switch
        {
            ElementType.Normal => "일반",
            ElementType.Fire => "불",
            ElementType.Water => "물",
            ElementType.Grass => "풀",
            ElementType.Electric => "전기",
            ElementType.Fighting => "격투",
            ElementType.Rock => "바위",
            ElementType.Earth => "땅",
            _ => string.Empty
        };
    }


    // 스킬 배열 범위와 데이터 존재 여부에 따른 이름 조회
    private static string GetSkillName(SkillData[] skills, int index)
    {
        // 미등록 스킬과 짧은 배열의 빈 문자열 반환
        SkillData skill = GetSkill(skills, index);
        return skill != null ? skill.Skillname : string.Empty;
    }


    // 슬롯 범위와 원본 배열 길이에 따른 스킬 조회
    private static SkillData GetSkill(SkillData[] skills, int index)
    {
        // 선택 전 음수 인덱스와 짧은 배열의 접근 제외
        return skills != null && index >= 0 && index < skills.Length ? skills[index] : null;
    }
}
