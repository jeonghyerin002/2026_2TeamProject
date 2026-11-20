using System.Collections;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 플레이어 프리팹의 인벤토리 화면에 보유 데이터와 입력 연결
/// </summary>
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public sealed class InventoryUI : MonoBehaviour
{
    // 보관함이 배치된 씬의 선택적 장비 UI와 대화 제한 참조
    [SerializeField] private AetherEquipmentUI aetherUI;
    [SerializeField] private GameObject dialogPanel;

    // 플레이어 소유 데이터와 프리팹 화면 상태
    private const int CategoryCount = 2;
    private PlayerController player;
    private PlayerBattleData data;
    private InventoryPanelView panelView;
    private GameObject panel;
    private WeaponData selected;
    private WeaponData[] visibleWeapons = System.Array.Empty<WeaponData>();
    private int categoryIndex;
    private int closedFrame = -1;
    private bool initialized;
    private bool subscribed;

    public static InventoryUI Instance { get; private set; }
    public bool IsOpen => panel != null && panel.activeSelf;
    public bool BlocksWorldInput => IsOpen || closedFrame == Time.frameCount;

    // 장비 변경 스텟 표시 문자열 재사용
    private readonly StringBuilder statComparison = new StringBuilder(192);

    // 기존 메뉴와 공유하는 위아래 연출 상태
    private PlayerMenuUI menu;
    private RectTransform panelRect;
    private Vector2 panelPosition;
    private Coroutine slideRoutine;
    private bool isClosing;


    // 플레이어 준비 후 프리팹에 저장된 인벤토리 화면 연결
    private void Start()
    {
        // 플레이어와 기존 메뉴의 직렬화 참조 조회
        player = PlayerController.Instance;
        data = player != null ? player.GetComponent<PlayerBattleData>() : null;
        menu = PlayerMenuUI.Instance;
        panelView = menu != null ? menu.InventoryPanel : null;

        if (data == null || panelView == null || !panelView.Initialize())
        {
            Debug.LogError("InventoryUI: 플레이어 데이터와 프리팹의 Inventory Panel을 연결하세요", this);
            enabled = false;
            return;
        }

        // 미리 배치된 화면의 초기 닫힘 상태와 데이터 구독 설정
        panel = panelView.gameObject;
        panelRect = (RectTransform)panel.transform;
        panelPosition = panelRect.anchoredPosition;
        panel.SetActive(false);
        Instance = this;
        initialized = true;
        Subscribe();
        Refresh();
    }


    // 재활성화 시 데이터 및 화면 요청 구독 복원
    private void OnEnable()
    {
        // Start 이전에는 참조 준비까지 구독 보류
        if (!initialized)
        {
            return;
        }

        Subscribe();
        Refresh();
    }


    // 중복 없이 플레이어 데이터 및 화면 버튼 요청 구독
    private void Subscribe()
    {
        // 활성 수명 안에서 한 번만 이벤트 연결
        if (subscribed || data == null || panelView == null)
        {
            return;
        }

        data.OwnedWeaponsChanged += Refresh;
        data.EquipmentChanged += Refresh;
        data.StatsChanged += Refresh;
        panelView.CategorySelected += SelectCategory;
        panelView.SlotSelected += SelectSlot;
        panelView.DismissRequested += ClearSelection;
        panelView.EquipmentRequested += ToggleSelectedEquipment;
        subscribed = true;
    }


    // 사용 조건과 인벤토리 전용 닫기 및 탭 전환 입력 처리
    private void Update()
    {
        // 닫힌 화면에서는 키 입력 처리 생략
        if (!IsOpen || data == null)
        {
            return;
        }

        if (!data.CanEditEquipment || GameSaveSystem.IsBusy || SceneFadeIn.IsTransitioning ||
            (dialogPanel != null && dialogPanel.activeInHierarchy))
        {
            SetOpen(false);
            return;
        }

        // 내려가는 동안 중복 닫기와 복귀 요청 차단
        if (isClosing)
        {
            return;
        }

        // 기존 Q 닫기와 E 카테고리 복귀 경로 유지
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
        {
            return;
        }

        bool returnToCategory = keyboard.eKey.wasPressedThisFrame;

        if (returnToCategory || keyboard.qKey.wasPressedThisFrame)
        {
            // 올라오는 중에도 현재 위치에서 내려가기 연출로 전환
            if (slideRoutine != null)
            {
                StopCoroutine(slideRoutine);
            }

            isClosing = true;
            slideRoutine = StartCoroutine(SlidePanel(true, returnToCategory));
            return;
        }

        // 올라오기 완료 전 카테고리 입력 보류
        if (slideRoutine != null)
        {
            return;
        }

        // 한 번의 키 입력마다 인접 상위 카테고리로 순환
        if (keyboard.aKey.wasPressedThisFrame)
        {
            SelectCategory((categoryIndex + CategoryCount - 1) % CategoryCount);
        }
        else if (keyboard.dKey.wasPressedThisFrame)
        {
            SelectCategory((categoryIndex + 1) % CategoryCount);
        }
    }


    // 기존 카테고리 및 장비 UI의 열기 요청 처리
    public void SetOpen(bool open)
    {
        // 초기화 전 요청과 같은 상태의 중복 호출 차단
        if (panel == null || open == IsOpen)
        {
            return;
        }

        if (open && (!isActiveAndEnabled || data == null || !data.CanEditEquipment ||
            (dialogPanel != null && dialogPanel.activeInHierarchy)))
        {
            return;
        }

        // 보관함이 있는 씬에서만 두 장비 화면의 동시 열림 방지
        if (open && aetherUI != null)
        {
            aetherUI.SetOpen(false);
        }

        // 외부 강제 닫기와 재진입 시 연출 취소 및 프리팹 기준 위치 복원
        StopSlide();

        // 실제 패널 상태를 배경 갱신보다 먼저 적용
        panel.SetActive(open);

        if (open)
        {
            selected = null;
            Refresh();
            panel.transform.SetAsLastSibling();
            player.SetMovementEnabled(false);
            panelRect.anchoredPosition = menu.GetHiddenPosition(panelRect);
            slideRoutine = StartCoroutine(SlidePanel(false));
        }
        else
        {
            closedFrame = Time.frameCount;

            if (player != null && !BattleSession.IsActive && (aetherUI == null || !aetherUI.IsOpen))
            {
                player.SetMovementEnabled(true);
            }
        }

        if (PlayerMenuUI.Instance != null)
        {
            PlayerMenuUI.Instance.RefreshInventoryBackdrop();
        }
    }


    // 기존 메뉴의 시간과 곡선을 사용하는 패널 진입 및 종료 연출
    private IEnumerator SlidePanel(bool closing, bool returnToCategory = false)
    {
        // 화면 아래 경계와 프리팹에 저장된 위치 사이 이동
        Vector2 start = panelRect.anchoredPosition;
        Vector2 target = closing ? menu.GetHiddenPosition(panelRect) : panelPosition;
        float duration = menu.PanelSlideDuration;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            yield return null;
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            panelRect.anchoredPosition = Vector2.LerpUnclamped(start, target, progress);
        }

        panelRect.anchoredPosition = target;
        slideRoutine = null;

        // 내려가기 완료까지 배경과 월드 입력 차단을 유지한 뒤 닫기
        if (closing)
        {
            SetOpen(false);

            if (returnToCategory && menu != null)
            {
                menu.ReturnFromWeaponInventory();
            }
        }
    }


    // 비활성화 및 강제 닫기 시 연출과 기준 위치 정리
    private void StopSlide()
    {
        // 진행 중인 코루틴 취소 후 다음 진입을 위한 위치 복원
        if (slideRoutine != null)
        {
            StopCoroutine(slideRoutine);
            slideRoutine = null;
        }

        isClosing = false;

        if (panelRect != null)
        {
            panelRect.anchoredPosition = panelPosition;
        }
    }


    // 탭 전환 시 선택 해제와 해당 목록의 첫 줄 표시
    private void SelectCategory(int index)
    {
        // 닫힌 패널과 유효하지 않은 탭 요청 차단
        if (!IsOpen || slideRoutine != null || index < 0 || index >= CategoryCount || index == categoryIndex)
        {
            return;
        }

        categoryIndex = index;
        selected = null;
        Refresh();
        panelView.ResetScroll();
    }


    // 보유 아이템과 선택 정보를 프리팹의 화면에 전달
    private void Refresh()
    {
        // 참조 연결 전 데이터 알림 무시
        if (panelView == null || data == null)
        {
            return;
        }

        // 기존 보유 무기를 조회하고 회복 탭은 빈 목록 표시
        visibleWeapons = categoryIndex == 0 ? data.OwnedWeapons : System.Array.Empty<WeaponData>();

        if (selected != null && !data.OwnsWeapon(selected))
        {
            selected = null;
        }

        panelView.Refresh(visibleWeapons, selected, data.Weapon, categoryIndex, BuildStatComparison());
    }


    // 선택 무기의 변경 스텟만 장비 적용 전후 기준으로 구성
    private string BuildStatComparison()
    {
        // 선택 해제 시 이전 비교 문자열 정리
        statComparison.Clear();

        if (selected == null)
        {
            return "";
        }

        // 장착 중인 무기는 미장착 기본값과 비교하고 다른 무기는 현재 장비와 비교
        bool isEquipped = data.Weapon != null && data.Weapon.ItemId == selected.ItemId;
        WeaponData current = isEquipped ? null : data.Weapon;
        AppendStatComparison("최대 HP", data.MaxHp, current != null ? current.HealthMultiplier : 1f, selected.HealthMultiplier, isEquipped);
        AppendStatComparison("공격", data.Attack, current != null ? current.AttackMultiplier : 1f, selected.AttackMultiplier, isEquipped);
        AppendStatComparison("특수공격", data.SpecialAttack, current != null ? current.SpecialAttackMultiplier : 1f, selected.SpecialAttackMultiplier, isEquipped);
        AppendStatComparison("방어", data.Defense, current != null ? current.DefenseMultiplier : 1f, selected.DefenseMultiplier, isEquipped);
        AppendStatComparison("특수방어", data.SpecialDefense, current != null ? current.SpecialDefenseMultiplier : 1f, selected.SpecialDefenseMultiplier, isEquipped);
        AppendStatComparison("속도", data.Speed, current != null ? current.SpeedMultiplier : 1f, selected.SpeedMultiplier, isEquipped);
        return statComparison.ToString();
    }


    // 소수점 최대 두 자리의 표시값이 다른 항목만 추가
    private void AppendStatComparison(string name, int baseValue, float currentMultiplier, float selectedMultiplier, bool isEquipped)
    {
        // 정수 내림 전의 장비 효과를 표시값으로 변환해 미세한 변화도 비교
        string before = FormatStatPreview(baseValue, currentMultiplier);
        string after = FormatStatPreview(baseValue, selectedMultiplier);

        if (before == after)
        {
            return;
        }

        if (statComparison.Length > 0)
        {
            statComparison.Append('\n');
        }

        // 미장착 선택은 전후 비교를 표시하고 장착 중인 선택은 최종값만 표시
        statComparison.Append(name).Append(' ');

        if (!isEquipped)
        {
            statComparison.Append(before).Append(" -> ");
        }

        statComparison.Append(after);

        // 화면에 표시한 전후 값의 차이를 부호와 함께 표시
        if (!isEquipped)
        {
            double change = double.Parse(after, CultureInfo.InvariantCulture) - double.Parse(before, CultureInfo.InvariantCulture);
            statComparison.Append("   (")
                .Append(change.ToString("+0.##;-0.##;0", CultureInfo.InvariantCulture))
                .Append(')');
        }
    }


    // 장비 배율 적용 수치를 불필요한 끝자리 0 없이 표시
    private static string FormatStatPreview(int baseValue, float multiplier)
    {
        // 전투 계산과 동일한 유효 배율 및 최솟값 보장
        if (float.IsNaN(multiplier) || float.IsInfinity(multiplier) || multiplier <= 0f)
        {
            multiplier = 1f;
        }

        // 실제 전투의 정수 내림은 유지하고 미리보기 문자열만 소수점 두 자리로 제한
        float value = Mathf.Max(1f, Mathf.Max(1, baseValue) * multiplier);
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }


    // 빈 슬롯을 제외한 무기의 상세 정보 선택
    private void SelectSlot(int index)
    {
        // 이벤트 전달 직전 목록이 바뀐 경우 범위 재확인
        if (!IsOpen || slideRoutine != null || categoryIndex != 0 || index < 0 || index >= visibleWeapons.Length)
        {
            return;
        }

        selected = selected == visibleWeapons[index] ? null : visibleWeapons[index];
        Refresh();
    }


    // 빈 공간 클릭 시 아이템 선택과 설명창 해제
    private void ClearSelection()
    {
        // 패널 전환 중 요청과 중복 해제 무시
        if (!IsOpen || slideRoutine != null || selected == null)
        {
            return;
        }

        selected = null;
        Refresh();
    }


    // 기존 플레이어 데이터 API를 통한 장착 및 해제 요청
    private void ToggleSelectedEquipment()
    {
        // 닫힌 화면과 선택 유실 및 편집 제한 상태 차단
        if (!IsOpen || slideRoutine != null || selected == null || !data.CanEditEquipment ||
            (dialogPanel != null && dialogPanel.activeInHierarchy))
        {
            return;
        }

        if (data.Weapon != null && data.Weapon.ItemId == selected.ItemId)
        {
            data.TryUnequipWeapon();
        }
        else
        {
            data.TryEquipWeapon(selected);
        }
    }


    // 비활성화 시 패널과 데이터 이벤트 정리
    private void OnDisable()
    {
        // 구독 상태를 초기화해 재활성화 중복 호출 방지
        if (subscribed && data != null)
        {
            data.OwnedWeaponsChanged -= Refresh;
            data.EquipmentChanged -= Refresh;
            data.StatsChanged -= Refresh;
        }

        if (subscribed && panelView != null)
        {
            panelView.CategorySelected -= SelectCategory;
            panelView.SlotSelected -= SelectSlot;
            panelView.DismissRequested -= ClearSelection;
            panelView.EquipmentRequested -= ToggleSelectedEquipment;
        }

        subscribed = false;
        StopSlide();
        SetOpen(false);
    }


    // 씬 종료 시 컨트롤러 인스턴스 참조 정리
    private void OnDestroy()
    {
        // 프리팹이 소유한 패널은 보존하고 현재 컨트롤러만 해제
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
