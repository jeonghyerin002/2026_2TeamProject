using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 캐릭터 능력치 표시와 포인트 투자 버튼 및 기존 메뉴 입력 연결
/// </summary>
[DisallowMultipleComponent]
public sealed class CharacterStatsUI : MonoBehaviour
{
    // 프리팹의 표시 및 입력 참조
    [SerializeField] private TMP_Text pointsText;
    [SerializeField] private TMP_Text[] valueTexts;
    [SerializeField] private Button[] increaseButtons;
    [SerializeField] private UIGroupController group;

    // 활성 화면의 데이터 구독 대상
    private PlayerBattleData playerData;
    private PlayerMenuUI playerMenu;

    // 필수 참조 검사 후 데이터 변경 구독
    private void OnEnable()
    {
        // 영속 플레이어의 데이터와 메뉴 제어 조회
        playerData = GetComponentInParent<PlayerBattleData>();
        playerMenu = PlayerMenuUI.Instance;

        if (playerData == null || group == null || pointsText == null ||
            valueTexts == null || valueTexts.Length != PlayerStatAllocation.Count ||
            increaseButtons == null || increaseButtons.Length != PlayerStatAllocation.Count)
        {
            Debug.LogError("CharacterStatsUI: 플레이어와 스텟 UI 참조를 연결하세요", this);
            enabled = false;
            return;
        }

        for (int i = 0; i < PlayerStatAllocation.Count; i++)
        {
            if (valueTexts[i] == null || increaseButtons[i] == null)
            {
                Debug.LogError("CharacterStatsUI: 스텟 행 참조가 누락되었습니다", this);
                enabled = false;
                return;
            }
        }

        playerData.StatsChanged += Refresh;
        Refresh();
    }


    // 비활성화와 파괴 시 데이터 이벤트 해제
    private void OnDisable()
    {
        // 활성화 반복의 중복 구독 방지
        if (playerData != null)
        {
            playerData.StatsChanged -= Refresh;
        }
    }


    // 마우스 및 기존 그룹 확인 입력으로 지정 스텟 투자
    public void Invest(int index)
    {
        // 닫힌 화면과 전환 연출 및 잘못된 행 입력 차단
        if (!isActiveAndEnabled || playerData == null || !group.CanReceiveInput ||
            index < 0 || index >= PlayerStatAllocation.Count ||
            (playerMenu != null && (!playerMenu.CanReceiveInput || playerMenu.IsOpeningPanel ||
                playerMenu.IsReturningToCategory || playerMenu.IsClosingCategory)))
        {
            return;
        }

        // 데이터 소유자의 원자적 한도 검사와 투자 결과로 화면 갱신
        playerData.TryInvestStat((PlayerStat)index);
    }


    // 장비 적용 전 스텟과 남은 포인트 및 버튼 상태 갱신
    private void Refresh()
    {
        // 데이터 변경 이벤트와 화면 재진입에 동일한 조회 적용
        pointsText.text = $"스텟 포인트 : {playerData.StatPoints}";
        for (int i = 0; i < PlayerStatAllocation.Count; i++)
        {
            int value = (PlayerStat)i switch
            {
                PlayerStat.MaxHp => playerData.MaxHp,
                PlayerStat.Attack => playerData.Attack,
                PlayerStat.SpecialAttack => playerData.SpecialAttack,
                PlayerStat.Defense => playerData.Defense,
                PlayerStat.SpecialDefense => playerData.SpecialDefense,
                _ => playerData.Speed
            };
            valueTexts[i].text = value.ToString();
            increaseButtons[i].interactable = playerData.CanInvestStat((PlayerStat)i);
        }
    }
}
