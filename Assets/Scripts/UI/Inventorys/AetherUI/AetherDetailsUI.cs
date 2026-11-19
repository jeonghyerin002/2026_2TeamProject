using System;
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

    // 항목명과 값을 구분하는 개선 화면의 선택 표시
    [SerializeField] private bool useLabeledLayout;
    [SerializeField] private Image[] slotFrames = Array.Empty<Image>();
    [SerializeField] private TMP_Text[] slotNames = Array.Empty<TMP_Text>();
    [SerializeField] private TMP_Text selectedSkillName;
    private const int LabelSize = 18;
    private const int ValueSize = 30;
    private const int AetherNameSize = 34;
    private const int SkillNameSize = 26;
    private const int BadgeValueSize = 24;
    private const int SlotNameSize = 22;
    private const int SkillSlotCount = 3;
    private static readonly Color SelectedSlotColor = new Color32(255, 224, 163, 255);
    private static readonly Color NormalSlotColor = new Color32(194, 205, 211, 255);
    private static readonly Color SelectedSkillColor = new Color32(128, 111, 79, 255);
    private static readonly Color NormalSkillColor = new Color32(103, 118, 128, 255);

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
            return;
        }

        // 개선 화면의 슬롯 표시 참조 누락 시 동작 중단
        if (useLabeledLayout && (slotFrames == null || slotNames == null ||
            slotFrames.Length != PlayerBattleData.EquipmentCapacity ||
            slotNames.Length != PlayerBattleData.EquipmentCapacity || selectedSkillName == null))
        {
            Debug.LogError("AetherDetailsUI: 개선 화면의 슬롯 프레임 5개와 이름 5개 및 선택 스킬 이름을 연결하세요", this);
            enabled = false;
            return;
        }

        if (useLabeledLayout)
        {
            for (int i = 0; i < slotFrames.Length; i++)
            {
                if (slotFrames[i] == null || slotNames[i] == null)
                {
                    Debug.LogError("AetherDetailsUI: 개선 화면의 슬롯 표시 참조가 누락되었습니다", this);
                    enabled = false;
                    return;
                }
            }
        }
    }


    // 활성 상태의 장착 변경 구독과 선택 정보 갱신
    private void OnEnable()
    {
        // 검증된 참조로 장착 변경 수신 및 재진입 시 최신 정보 표시
        playerData.EquipmentChanged += Refresh;

        // 첫 진입에 실제 장착 항목을 선택하고 빈 화면 방지
        if (useLabeledLayout && (selectedSlot < 0 || selectedSlot >= playerData.AetherSlotCount))
        {
            selectedSlot = FindFirstEquippedSlot();
        }

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
        // 개선 프리팹에서만 항목명과 값 및 선택 표시 갱신
        if (useLabeledLayout)
        {
            RefreshLabeledLayout();
            return;
        }

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

        if (useLabeledLayout)
        {
            Refresh();
            return;
        }

        RefreshSkillDetails(skill);
    }


    // 선택 스킬의 원본 수치와 한글 타입 표시
    private void RefreshSkillDetails(SkillData skill)
    {
        // 개선 화면의 항목명을 유지하며 선택 스킬의 실제 원본과 남은 PP 표시
        if (useLabeledLayout)
        {
            selectedSkillName.text = skill != null ? $"선택 스킬  /  {skill.Skillname}" : "선택 스킬  /  등록된 스킬 없음";
            skillType.text = FormatInfo("에테르 스킬 타입", skill != null ? GetElementName(skill.Type) : "-");
            skillPower.text = FormatInfo("에테르 스킬 위력", skill != null ? skill.Damage.ToString() : "-");
            skillAttackType.text = FormatInfo("에테르 어택 타입", skill != null ? GetAttackTypeName(skill.AttackType) : "-");
            skillPp.text = FormatInfo("에테르 사용 횟수", skill != null ? $"{playerData.GetCurrentPp(skill)} / {Mathf.Max(0, skill.PP)}" : "-");
            skillAccuracy.text = FormatInfo("에테르 명중률", skill != null ? $"{skill.Accuracy}%" : "-");
            return;
        }

        // 선택 전 또는 빈 스킬의 상세 표시 초기화
        skillType.text = skill != null ? GetElementName(skill.Type) : string.Empty;
        skillPower.text = skill != null ? skill.Damage.ToString() : string.Empty;
        skillAttackType.text = skill != null ? GetAttackTypeName(skill.AttackType) : string.Empty;
        // 전투 후에도 보관한 남은 횟수와 원본 최대 횟수 표시
        skillPp.text = skill != null ? $"{playerData.GetCurrentPp(skill)} / {Mathf.Max(0, skill.PP)}" : string.Empty;
        skillAccuracy.text = skill != null ? skill.Accuracy.ToString() : string.Empty;
    }


    // 개선 화면의 에테르 정보와 스킬 목록 및 장착 슬롯 표시
    private void RefreshLabeledLayout()
    {
        // 현재 슬롯의 실제 장착 데이터에서 기본 정보 구성
        AetherData aether = playerData.GetEquippedAether(selectedSlot);
        aetherName.text = FormatInfo("에테르 이름", GetAetherDisplayName(aether), AetherNameSize);
        aetherType.text = FormatInfo("에테르 타입", aether != null ? GetElementName(aether.AetherType) : "-", BadgeValueSize, true);

        if (equipmentStatus != null)
        {
            equipmentStatus.text = FormatInfo("장착 여부", aether != null ? "장착중" : "미장착", BadgeValueSize);
        }

        // 등록 순서 유지 및 변경된 에테르의 유효한 첫 스킬 선택
        SkillData[] skills = aether != null ? aether.Skills : null;

        if (GetSkill(skills, selectedSkill) == null)
        {
            selectedSkill = FindFirstSkill(skills);
        }

        skillSlot1.text = FormatSkill(skills, 0);
        skillSlot2.text = FormatSkill(skills, 1);
        skillSlot3.text = FormatSkill(skills, 2);
        skillButton1.interactable = GetSkill(skills, 0) != null;
        skillButton2.interactable = GetSkill(skills, 1) != null;
        skillButton3.interactable = GetSkill(skills, 2) != null;
        skillButton1.image.color = selectedSkill == 0 ? SelectedSkillColor : NormalSkillColor;
        skillButton2.image.color = selectedSkill == 1 ? SelectedSkillColor : NormalSkillColor;
        skillButton3.image.color = selectedSkill == 2 ? SelectedSkillColor : NormalSkillColor;
        RefreshSkillDetails(GetSkill(skills, selectedSkill));

        // 선택 슬롯 강조와 슬롯별 실제 이름 및 빈 상태 표시
        for (int i = 0; i < slotFrames.Length; i++)
        {
            AetherData item = playerData.GetEquippedAether(i);
            slotFrames[i].color = i == selectedSlot ? SelectedSlotColor : NormalSlotColor;
            string heading = i == 0 ? "01 / 전투 시작" : $"{i + 1:00} / 에테르";
            slotNames[i].text = FormatInfo(heading, GetAetherDisplayName(item), SlotNameSize);
        }
    }


    // 빈 슬롯과 이름이 누락된 원본 데이터 구분
    private static string GetAetherDisplayName(AetherData aether)
    {
        // 실제 장착 여부와 이름 등록 상태에 따른 표시 문구 반환
        if (aether == null)
        {
            return "빈 슬롯";
        }

        return string.IsNullOrWhiteSpace(aether.AetherName) ? "이름 없음" : aether.AetherName;
    }


    // 첫 진입에 선택할 실제 장착 슬롯 조회
    private int FindFirstEquippedSlot()
    {
        // 슬롯 순서대로 비어 있지 않은 장착 항목 탐색
        for (int i = 0; i < playerData.AetherSlotCount; i++)
        {
            if (playerData.GetEquippedAether(i) != null)
            {
                return i;
            }
        }

        return 0;
    }


    // 선택 가능한 첫 스킬 번호 조회
    private static int FindFirstSkill(SkillData[] skills)
    {
        // 기존 스킬 세 칸의 순서와 null 항목 유지
        for (int i = 0; i < SkillSlotCount; i++)
        {
            if (GetSkill(skills, i) != null)
            {
                return i;
            }
        }

        return -1;
    }


    // 스킬 번호와 실제 이름을 분리한 버튼 문구 구성
    private static string FormatSkill(SkillData[] skills, int index)
    {
        // 빈 스킬의 표시를 유지하고 사용 불가 상태 구분
        SkillData skill = GetSkill(skills, index);
        return FormatInfo($"스킬{index + 1}", skill != null ? skill.Skillname : "등록 없음", SkillNameSize);
    }


    // 기존 항목명과 실제 값을 다른 크기와 명도로 구분
    private static string FormatInfo(string label, string value, int valueSize = ValueSize, bool useDarkLabel = false)
    {
        // 기존 TMP 리치 텍스트로 새 아트 없이 정보 계층 표현
        string labelColor = useDarkLabel ? "#343F48" : "#B3BCC0";
        return $"<size={LabelSize}><color={labelColor}>{label}</color></size>\n<size={valueSize}>{value}</size>";
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
