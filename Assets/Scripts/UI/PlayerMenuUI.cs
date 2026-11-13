using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Player 자식 메뉴의 씬별 사용 여부와 월드 입력 차단을 관리한다.</summary>
[DisallowMultipleComponent]
public sealed class PlayerMenuUI : MonoBehaviour
{
    [SerializeField] private UINavigationManager navigation; // Player와 함께 유지되는 그룹 관리자
    [SerializeField] private UINavigationInput menuInput;     // 제외 씬에서 중지할 메뉴 입력
    [SerializeField] private Canvas menuCanvas;              // 제외 씬에서 숨길 메뉴 화면
    [SerializeField] private RectTransform categoryPanel;    // 화면 아래에서 올라오는 Category 패널
    [SerializeField] private GameObject categoryBackdrop;   // 메뉴 사용 중 월드 화면을 어둡게 덮는 전체 화면 패널
    [SerializeField, Min(0.01f)] private float backdropFadeDuration = 0.25f; // 배경의 페이드 인과 아웃 시간(초)
    private Image backdropImage;                           // 메뉴 패널과 독립적으로 투명도를 조절할 배경 이미지
    private float backdropAlpha;                           // 프리팹에 설정된 배경의 최대 알파값
    private bool backdropVisible;                          // 그룹 이동 중 동일한 페이드의 재시작을 방지하기 위한 목표 상태
    private Coroutine backdropRoutine;                     // 빠른 재열기와 씬 전환 때 취소할 배경 페이드
    [SerializeField, Min(0.01f)] private float slideDuration = 0.25f; // 패널의 열기와 닫기 이동 시간(초)
    private Vector2 categoryPosition;                       // 연출이 끝났을 때 복원할 패널 위치
    private Coroutine slideRoutine;                         // 닫기와 씬 전환 때 중단할 위치 연출
    private bool isClosingCategory;                         // 내려가는 동안 메뉴 키의 중복 처리 방지 상태
    private RectTransform returningPanel;                   // Category 복귀 전에 아래로 내릴 하위 패널
    private Vector2 returningPosition;                      // 하위 패널의 다음 진입을 위해 보존하는 위치
    private RectTransform openingPanel;                     // Category에서 선택해 아래에서 올리는 하위 패널
    private Vector2 openingPosition;                        // 하위 패널의 올라오기 완료 위치
    private readonly Vector3[] panelCorners = new Vector3[4]; // 화면 아래까지 이동할 거리를 계산하는 모서리 좌표
    private readonly UIInteractionData confirmInteraction = new UIInteractionData(UIActionType.Confirm); // 내려간 뒤 실행할 기존 확인 처리 요청
    private PlayerController player;                        // 이 메뉴를 소유하는 플레이어
    private bool sceneAllowed;                              // 현재 씬의 메뉴 사용 가능 여부
    private int closedFrame = -1;                           // 닫기 입력의 월드 전달을 막기 위한 프레임 기록
    public static PlayerMenuUI Instance { get; private set; } // 현재 Player 메뉴의 입력 차단 조회 대상

    public static bool BlocksWorldInput => Instance != null && Instance.sceneAllowed &&
        (Instance.navigation.IsOpen || Instance.categoryBackdrop.activeSelf || Instance.closedFrame == Time.frameCount);
    public bool IsClosingCategory => isClosingCategory;
    public bool IsReturningToCategory => returningPanel != null;
    public bool IsOpeningPanel => openingPanel != null;
    public bool CanReceiveInput => sceneAllowed && !GameSaveSystem.IsBusy &&
        !SceneFadeIn.IsTransitioning && Time.timeScale != 0f && player.IsMovementEnabled &&
        (WeaponInventoryUI.Instance == null || !WeaponInventoryUI.Instance.IsOpen) &&
        (equipmentUI == null || !equipmentUI.IsOpen);
    private AetherEquipmentUI equipmentUI; // 현재 씬에서 사용하는 기존 에테르 메뉴

    private void Awake()
    {
        player = GetComponentInParent<PlayerController>();
        if (player == null || navigation == null || menuInput == null || menuCanvas == null || categoryPanel == null ||
            categoryBackdrop == null || !categoryBackdrop.TryGetComponent(out backdropImage))
        {
            Debug.LogError("PlayerMenuUI: Player와 메뉴 참조를 연결하세요.", this);
            if (menuInput != null) menuInput.enabled = false;
            if (menuCanvas != null) menuCanvas.enabled = false;
            enabled = false;
            return;
        }
        categoryPosition = categoryPanel.anchoredPosition;
        backdropAlpha = backdropImage.color.a;
        ResetBackdrop();
        Instance = this;
    }

    private void OnEnable()
    {
        Instance = this;
        navigation.GroupChanged += HandleGroupChanged;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void Start() => RefreshScene(SceneManager.GetActiveScene());

    private void Update()
    {
        // 컷신이나 대화의 이동 제한 시 메뉴를 닫아 게임 흐름 우선 처리
        if (navigation.IsOpen && !CanReceiveInput) navigation.Close();
    }

    // 씬 참조 갱신과 이전 메뉴 및 이동 기록 정리
    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => RefreshScene(SceneManager.GetActiveScene());

    // Player의 영속성을 이용한 메뉴 유지와 씬별 상태 갱신
    private void RefreshScene(Scene scene)
    {
        navigation.Close();
        StopCategorySlide();
        ResetBackdrop();
        closedFrame = -1;
        sceneAllowed = scene.name != "StartMenu" && scene.name != "Prologue" && scene.name != "Battle";
        equipmentUI = FindFirstObjectByType<AetherEquipmentUI>();
        navigation.enabled = sceneAllowed;
        menuInput.enabled = sceneAllowed;
        menuCanvas.enabled = sceneAllowed;
    }

    // Category 진입 연출 시작과 닫은 프레임까지의 월드 입력 차단
    private void HandleGroupChanged(UIGroupData group)
    {
        StopCategorySlide();
        if (group == null) closedFrame = Time.frameCount;
        bool isCategory = navigation.CurrentController != null && navigation.CurrentController.transform == categoryPanel;
        // 그룹 간 이동 중 배경 유지와 메뉴 종료 후 페이드 아웃
        SetBackdrop(group != null);
        if (isCategory)
            slideRoutine = StartCoroutine(SlideCategory());
    }

    // 빠른 재열기 시 화면 농도가 튀지 않도록 현재 알파값에서 페이드 연결
    private void SetBackdrop(bool visible)
    {
        if (backdropVisible == visible) return;
        backdropVisible = visible;
        if (backdropRoutine != null) StopCoroutine(backdropRoutine);
        categoryBackdrop.SetActive(true);
        backdropRoutine = StartCoroutine(FadeBackdrop(visible));
    }

    // 메뉴 색상 유지와 검은 배경 알파의 단계적 변경
    private IEnumerator FadeBackdrop(bool visible)
    {
        Color color = backdropImage.color;
        float start = color.a;
        float target = visible ? backdropAlpha : 0f;
        float duration = Mathf.Max(0.01f, backdropFadeDuration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            yield return null;
            elapsed += Time.unscaledDeltaTime;
            color.a = Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration)));
            backdropImage.color = color;
        }
        color.a = target;
        backdropImage.color = color;
        backdropRoutine = null;
        if (!visible)
        {
            categoryBackdrop.SetActive(false);
            closedFrame = Time.frameCount;
        }
    }

    // 씬 전환 및 비활성화 시 페이드 취소와 화면 가림 및 입력 차단의 즉시 정리
    private void ResetBackdrop()
    {
        if (backdropRoutine != null) StopCoroutine(backdropRoutine);
        backdropRoutine = null;
        backdropVisible = false;
        if (backdropImage != null)
        {
            Color color = backdropImage.color;
            color.a = 0f;
            backdropImage.color = color;
        }
        if (categoryBackdrop != null) categoryBackdrop.SetActive(false);
    }

    // Q 또는 E 입력 시 Category 패널의 내려가기 연출 후 닫기 처리
    public bool TryCloseCategory()
    {
        if (!CanReceiveInput || isClosingCategory || navigation.CurrentController == null ||
            navigation.CurrentController.transform != categoryPanel)
            return false;

        // 올라오는 도중 닫아도 위치가 튀지 않도록 현재 위치에서 방향 전환
        if (slideRoutine != null) StopCoroutine(slideRoutine);
        isClosingCategory = true;
        slideRoutine = StartCoroutine(SlideCategory(true));
        return true;
    }

    // F 입력 시 선택 고정과 Category의 내려가기 연출 후 확인 처리
    public bool TryConfirmCategory()
    {
        UIGroupController controller = navigation.CurrentController;
        if (!CanReceiveInput || isClosingCategory || controller == null ||
            controller.transform != categoryPanel ||
            (controller.SelectedItem?.Interaction?.Action != UIActionType.OpenGroup &&
                controller.SelectedItem?.Interaction?.Action != UIActionType.Invoke))
            return false;

        // 캐러셀 이동 중 기존 확인 처리와 동일하게 입력을 소비해 전환 방지
        InventoryCarouselUI carousel = controller.GetComponent<InventoryCarouselUI>();
        if (carousel != null && carousel.IsMoving) return true;

        if (slideRoutine != null) StopCoroutine(slideRoutine);
        isClosingCategory = true;
        slideRoutine = StartCoroutine(SlideCategory(true, controller, controller.SelectedIndex));
        return true;
    }


    // 가방 카테고리 실행 시 기존 무기 인벤토리 열기
    public void OpenWeaponInventory(int itemIndex)
    {
        // 다른 카테고리와 잘못된 참조 및 메뉴 전환 상태 검사
        UIGroupController controller = navigation.CurrentController;
        WeaponInventoryUI inventory = WeaponInventoryUI.Instance;

        if (!CanReceiveInput || controller == null || controller.transform != categoryPanel ||
            controller.Data.GetItem(itemIndex)?.Id != "inventory" || inventory == null || !inventory.isActiveAndEnabled)
        {
            return;
        }

        // 무기 화면을 먼저 연 뒤 성공한 경우에만 카테고리 닫기
        inventory.SetOpen(true);

        if (inventory.IsOpen)
        {
            navigation.Close();
        }
    }


    // 무기 화면을 닫은 뒤 카테고리 복귀
    public void ReturnFromWeaponInventory()
    {
        // 제외 씬과 대화 및 전환 중 메뉴 재진입 차단
        if (!isActiveAndEnabled || !CanReceiveInput || navigation.IsOpen)
        {
            return;
        }

        navigation.OpenInitialGroup();
    }


    // Category 복귀 시 현재 패널의 내려가기 연출 후 기존 뒤로 가기 처리
    public bool TryReturnToCategory()
    {
        UIGroupController controller = navigation.CurrentController;
        if (!CanReceiveInput || isClosingCategory || returningPanel != null || controller == null ||
            controller.transform == categoryPanel ||
            !navigation.WillBackTo(categoryPanel.GetComponent<UIGroupController>().Data))
            return false;

        RectTransform panel = controller.transform as RectTransform;
        if (panel == null) return false;
        StopCategorySlide();
        returningPanel = panel;
        returningPosition = panel.anchoredPosition;
        slideRoutine = StartCoroutine(SlideBackToCategory());
        return true;
    }

    // 패널 크기와 배율에 맞춘 화면 밖 이동 후 Category 진입 연출 연결
    private IEnumerator SlideBackToCategory()
    {
        RectTransform panel = returningPanel;
        Vector2 target = GetHiddenPosition(panel);
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, slideDuration);
        while (elapsed < duration)
        {
            yield return null;
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            panel.anchoredPosition = Vector2.LerpUnclamped(returningPosition, target, progress);
        }
        panel.anchoredPosition = target;
        slideRoutine = null;
        // 그룹 변경 이벤트를 통한 하위 패널 위치 복원과 Category 올라오기 연출 시작
        if (!navigation.TryBack()) StopCategorySlide();
    }

    // 배율과 화면 크기를 반영한 패널의 화면 밖 위치 계산
    private Vector2 GetHiddenPosition(RectTransform panel)
    {
        Transform parent = panel.parent;
        panel.GetWorldCorners(panelCorners);
        float top = parent.InverseTransformPoint(panelCorners[1]).y;
        ((RectTransform)menuCanvas.transform).GetWorldCorners(panelCorners);
        float bottom = parent.InverseTransformPoint(panelCorners[0]).y;
        return panel.anchoredPosition - Vector2.up * Mathf.Max(0f, top - bottom);
    }

    // 투명도를 유지하며 선택한 하위 패널을 화면 아래에서 원래 위치까지 이동
    private IEnumerator SlideSelectedPanel()
    {
        Vector2 start = GetHiddenPosition(openingPanel);
        openingPanel.anchoredPosition = start;
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, slideDuration);
        while (elapsed < duration)
        {
            yield return null;
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            openingPanel.anchoredPosition = Vector2.LerpUnclamped(start, openingPosition, progress);
        }
        openingPanel.anchoredPosition = openingPosition;
        openingPanel = null;
        slideRoutine = null;
    }

    // 투명도를 유지한 이동과 내려가기 완료 후 닫기 또는 선택 그룹 전환
    private IEnumerator SlideCategory(bool closing = false, UIGroupController confirmedController = null, int confirmedIndex = -1)
    {
        Vector2 hidden = categoryPosition - Vector2.up * categoryPanel.rect.height;
        Vector2 start = closing ? categoryPanel.anchoredPosition : hidden;
        Vector2 target = closing ? hidden : categoryPosition;
        categoryPanel.anchoredPosition = start;
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, slideDuration);
        while (elapsed < duration)
        {
            yield return null;
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            categoryPanel.anchoredPosition = Vector2.LerpUnclamped(start, target, progress);
        }
        categoryPanel.anchoredPosition = target;
        slideRoutine = null;
        isClosingCategory = false;
        // 기존 확인 경로를 이용한 E 복귀용 이동 기록 및 피드백 보존
        if (closing)
        {
            if (confirmedController != null)
            {
                if (!confirmedController.ExecuteInteraction(confirmInteraction, confirmedIndex))
                    StopCategorySlide();
                else if (navigation.CurrentController != null && navigation.CurrentController != confirmedController &&
                    navigation.CurrentController.transform is RectTransform panel)
                {
                    openingPanel = panel;
                    openingPosition = panel.anchoredPosition;
                    slideRoutine = StartCoroutine(SlideSelectedPanel());
                }
            }
            else navigation.Close();
        }
    }

    // 연출 도중 닫기 및 그룹 이동 시 다음 진입을 위한 기준 위치 보존
    private void StopCategorySlide()
    {
        isClosingCategory = false;
        if (slideRoutine != null)
        {
            StopCoroutine(slideRoutine);
            slideRoutine = null;
        }
        if (returningPanel != null)
        {
            returningPanel.anchoredPosition = returningPosition;
            returningPanel = null;
        }
        if (openingPanel != null)
        {
            openingPanel.anchoredPosition = openingPosition;
            openingPanel = null;
        }
        if (categoryPanel != null) categoryPanel.anchoredPosition = categoryPosition;
    }

    private void OnDisable()
    {
        StopCategorySlide();
        if (navigation != null)
        {
            navigation.GroupChanged -= HandleGroupChanged;
            navigation.Close();
        }
        ResetBackdrop();
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        if (Instance == this) Instance = null;
    }
}
