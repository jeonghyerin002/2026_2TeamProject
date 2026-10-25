using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>전투 메뉴와 상태 표시를 연결하고 UI 이동·페이드와 HP 피해 연출을 관리한다.</summary>
public sealed class BattleSceneUI : MonoBehaviour
{
    [SerializeField] private BattleSystem battle;
    [SerializeField] private BattleFlow flow;
    [SerializeField] private GameObject playerUI;     // 대화창과 명령 패널을 담는 공통 부모
    [SerializeField] private GameObject playerHud;    // 행동 선택 또는 NPC 공격 중 표시할 플레이어 상태
    [SerializeField] private GameObject enemyHud;     // 행동 선택 또는 플레이어 공격 중 표시할 적 상태
    [SerializeField] private GameObject commandPanel; // 행동 선택 중에만 표시할 명령 패널
    [SerializeField] private GameObject dialogue;     // 행동 연출과 결과 문구를 표시할 대화창
    [SerializeField] private TMP_Text message;
    [SerializeField] private TMP_Text playerInfo;
    [SerializeField] private TMP_Text enemyInfo;
    [SerializeField] private TMP_Text playerHealth;
    [SerializeField] private TMP_Text enemyHealth;
    [SerializeField] private TMP_Text detail;
    [SerializeField] private TMP_Text turnLabel;
    [SerializeField] private Image playerFill;
    [SerializeField] private Image enemyFill;
    [SerializeField] private Button[] choices;
    [SerializeField] private TMP_Text[] labels;
    [SerializeField] private Button back;

    [Header("UI 표시 연출")]
    [SerializeField, Min(0f)] private float uiTransitionDuration = 0.25f; // UI가 자기 가로폭만큼 이동하고 페이드하는 시간(초), 0이면 즉시 전환

    private PanelDisplay playerPanel;  // 왼쪽으로 이동하는 플레이어 HUD
    private PanelDisplay enemyPanel;   // 오른쪽으로 이동하는 적 HUD
    private PanelDisplay commandView;  // 오른쪽으로 이동하는 명령 패널
    private PanelDisplay dialogueView; // 명령 패널 퇴장 시 오른쪽에서 왼쪽으로 등장하는 대화창

    [Header("체력 감소 연출")]
    [SerializeField, Min(0.01f)] private float healthDuration = 0.12f; // 점멸 후 HP 바와 숫자가 감소하는 시간(초)
    [SerializeField, Min(0f)] private float trailDelay = 0.15f;      // 앞쪽 바 감소 후 잔상이 대기하는 시간(초)
    [SerializeField, Min(0.01f)] private float trailDuration = 0.25f; // 잔상이 목표 체력까지 줄어드는 시간(초)
    [SerializeField] private Color trailColor = new Color32(135, 135, 135, 255); // 잃은 체력 구간을 구분하는 회색 잔상
    [SerializeField, Min(0.01f)] private float healthFlashDuration = 0.05f; // 흰색 점멸과 확대에 사용하는 시간(초)
    [SerializeField, Range(1f, 1.2f)] private float healthImpactScale = 1.08f; // 타격 순간 HP 바와 잔상의 최대 확대 배율
    [SerializeField, Min(0f)] private float healthShake = 2f; // HP 패널의 감쇠 흔들림 크기(UI 단위)

    private HealthDisplay playerDisplay; // 플레이어 체력의 표시 상태
    private HealthDisplay enemyDisplay;  // 적 체력의 표시 상태

    private enum Menu { Root, Skills, Aethers, Info, End }

    private Menu menu;
    private int selected;
    private int aetherPage;

    private bool CanChoose => battle.PlayerState != null && battle.EnemyState != null &&
        battle.Phase == TurnPhase.WaitingPlayer && !flow.IsBusy;

    private void Awake()
    {
        if (playerUI == null || playerHud == null || enemyHud == null || commandPanel == null || dialogue == null)
        {
            Debug.LogError("BattleSceneUI: PlayerUI, 양측 HUD, Command Panel과 Dialogue를 연결하세요.", this);
            enabled = false;
            return;
        }

        if (playerFill == null || enemyFill == null || playerHealth == null || enemyHealth == null)
        {
            Debug.LogError("BattleSceneUI: 양측 HP Fill과 HP 숫자를 연결하세요.", this);
            enabled = false;
            return;
        }

        if (playerHud.GetComponent<RectTransform>() == null || enemyHud.GetComponent<RectTransform>() == null ||
            commandPanel.GetComponent<RectTransform>() == null || dialogue.GetComponent<RectTransform>() == null)
        {
            Debug.LogError("BattleSceneUI: HUD, Command Panel과 Dialogue에 RectTransform이 필요합니다.", this);
            enabled = false;
            return;
        }

        playerPanel = new PanelDisplay(playerHud, -1f);
        enemyPanel = new PanelDisplay(enemyHud, 1f);
        commandView = new PanelDisplay(commandPanel, 1f);
        dialogueView = new PanelDisplay(dialogue, 1f, false);
        playerDisplay = new HealthDisplay(playerFill, playerHealth, trailColor);
        enemyDisplay = new HealthDisplay(enemyFill, enemyHealth, trailColor);

        for (int i = 0; i < choices.Length; i++)
        {
            int index = i;
            choices[i].onClick.AddListener(() => Choose(index));
        }

        back.onClick.AddListener(Back);
    }

    // 전투 상태와 연출 이벤트를 구독한다
    private void OnEnable()
    {
        battle.StateChanged += Refresh;
        flow.PresentationEnded += Refresh;
        if (playerDisplay != null && enemyDisplay != null)
            Refresh();
    }

    private void OnDisable()
    {
        if (battle != null)
            battle.StateChanged -= Refresh;

        if (flow != null)
            flow.PresentationEnded -= Refresh;

        playerDisplay?.Reset();
        enemyDisplay?.Reset();
        playerPanel?.Reset();
        enemyPanel?.Reset();
        commandView?.Reset();
        dialogueView?.Reset();
    }

    private void OnDestroy()
    {
        playerDisplay?.Dispose();
        enemyDisplay?.Dispose();
    }

    // 다른 컴포넌트 초기화 이후 현재 상태를 표시한다
    private void Start()
    {
        Refresh();
    }

    // 전투 메뉴의 키보드 입력을 처리한다
    private void Update()
    {
        // 입력이 막힌 공격 연출 중에도 체력 표시의 시간축은 계속 진행한다.
        playerDisplay?.Tick(Time.deltaTime, healthDuration, trailDelay, trailDuration,
            healthFlashDuration, healthImpactScale, healthShake);
        enemyDisplay?.Tick(Time.deltaTime, healthDuration, trailDelay, trailDuration,
            healthFlashDuration, healthImpactScale, healthShake);

        Keyboard key = Keyboard.current;

        if (key == null || !CanChoose || commandView == null || !commandView.IsReady || menu == Menu.End)
            return;

        if (key.eKey.wasPressedThisFrame || key.escapeKey.wasPressedThisFrame)
        {
            Back();
            return;
        }

        int step =
            key.aKey.wasPressedThisFrame ? -1 :
            key.dKey.wasPressedThisFrame ? 1 :
            key.leftArrowKey.wasPressedThisFrame ? -1 :
            key.rightArrowKey.wasPressedThisFrame ? 1 :
            key.upArrowKey.wasPressedThisFrame || key.downArrowKey.wasPressedThisFrame ? 2 :
            0;

        if (step != 0)
            Focus(step);

        if (key.fKey.wasPressedThisFrame)
            Choose(selected);
    }

    private void LateUpdate()
    {
        // HP 감소와 잔상 연출이 완료된 뒤의 유지 시간을 확인하고 HUD 퇴장을 시작한다.
        RefreshHudVisibility();
        // HP 피격 흔들림과 표시 이동을 마지막에 합성하여 같은 위치를 덮어쓰지 않는다.
        float delta = Time.unscaledDeltaTime;
        playerPanel?.Tick(delta, uiTransitionDuration, playerDisplay.ShakeOffset);
        enemyPanel?.Tick(delta, uiTransitionDuration, enemyDisplay.ShakeOffset);
        commandView?.Tick(delta, uiTransitionDuration, Vector2.zero);
        dialogueView?.Tick(delta, uiTransitionDuration, Vector2.zero);
    }

    // 기절한 대상의 HP 감소 연출이 끝나면 0.3초 유지한 뒤 자동으로 HUD를 숨긴다.
    private void RefreshHudVisibility()
    {
        bool canChoose = CanChoose;
        bool presenting = flow.IsBusy;
        playerPanel?.SetVisible(battle.PlayerState != null && !playerDisplay.CanHideAfterDefeat() &&
            (battle.PlayerState.IsDead || canChoose || (presenting && flow.ActingSide == BattleSide.Enemy)));
        enemyPanel?.SetVisible(battle.EnemyState != null && !enemyDisplay.CanHideAfterDefeat() &&
            (battle.EnemyState.IsDead || canChoose || (presenting && flow.ActingSide == BattleSide.Player)));
    }

    // 현재 전투 상태와 메뉴를 다시 표시한다
    public void Refresh()
    {
        if (playerUI == null || playerHud == null || enemyHud == null || commandPanel == null || dialogue == null)
            return;

        bool canChoose = CanChoose;
        bool presenting = flow.IsBusy;
        bool showResult = battle.Phase == TurnPhase.Ended &&
            battle.PlayerState != null && battle.EnemyState != null &&
            (battle.PlayerState.IsDead || battle.EnemyState.IsDead);

        // 공통 부모를 유지해야 명령 입력을 숨긴 뒤에도 공격 대사를 표시할 수 있다.
        // 자식의 퇴장 연출이 끝나기 전에 공통 부모를 꺼서 화면이 끊기지 않게 한다.
        playerUI.SetActive(true);
        // 두 패널은 같은 프레임의 진행도로 갱신되어 명령 패널 퇴장과 대화창 진입이 겹친다.
        commandView?.SetVisible(canChoose);
        dialogueView?.SetVisible(flow.IsShowingMessage || (!presenting && showResult));

        // 숨긴 메뉴에 남은 키보드·게임패드 포커스를 해제해 연출 중 입력을 막는다.
        if (!canChoose && EventSystem.current != null &&
            EventSystem.current.currentSelectedGameObject != null &&
            EventSystem.current.currentSelectedGameObject.transform.IsChildOf(playerUI.transform))
            EventSystem.current.SetSelectedGameObject(null);

        if (battle.PlayerState == null || battle.EnemyState == null)
            return;

        ShowUnit(battle.PlayerState, playerInfo, playerDisplay);
        ShowUnit(battle.EnemyState, enemyInfo, enemyDisplay);

        turnLabel.text = $"TURN {battle.TurnNumber:00}";

        if (!flow.IsBusy)
        {
            if (battle.Phase == TurnPhase.Ended)
                menu = Menu.End;
            else if (menu == Menu.End)
                menu = Menu.Root;
        }

        ShowMenu();
    }

    // 캐릭터 이름과 레벨 및 현재 HP를 표시한다
    private static void ShowUnit(BattleState unit, TMP_Text info, HealthDisplay display)
    {
        string status = unit.Status == BattleStatus.None ? "" : $"  [{unit.Status}]";

        info.text = $"{unit.Character.Charactername}   <size=75%>Lv.{unit.Level}</size>{status}";
        display?.SetTarget(unit);
    }

    /// <summary>UI의 표시 상태를 보간하여 자기 가로폭만큼 이동하고 선택적 페이드와 입력을 관리한다.</summary>
    private sealed class PanelDisplay
    {
        private readonly RectTransform rect; // 표시 위치와 실제 가로폭을 제공하는 패널
        private readonly CanvasGroup group;  // 자식 전체의 투명도와 입력을 함께 제어하는 그룹
        private readonly Vector2 origin;     // 완전히 표시되었을 때의 기준 위치
        private readonly float direction;    // 퇴장 방향: 왼쪽 -1, 오른쪽 1, 제자리 0
        private readonly bool fade;          // 대화창은 이동만 사용하고 나머지 UI는 페이드를 함께 적용
        private readonly float opacity;      // 원래 설정된 최대 불투명도
        private readonly bool interactable;  // 표시 완료 후 복원할 원래 상호작용 설정
        private readonly bool blocksRaycasts; // 표시 완료 후 복원할 원래 포인터 차단 설정
        private float progress;              // 숨김 0부터 완전 표시 1까지의 진행도
        private bool visible;                // 반복 갱신에도 연출을 재시작하지 않는 목표 표시 상태

        internal bool IsReady => visible && progress >= 1f;

        // 기존 레이아웃을 보존하고 각 패널에 필요한 페이드 그룹만 준비한다.
        internal PanelDisplay(GameObject target, float direction, bool fade = true)
        {
            rect = target.GetComponent<RectTransform>();
            origin = rect.anchoredPosition;
            this.direction = direction;
            this.fade = fade;
            group = target.GetComponent<CanvasGroup>();
            if (group == null)
                group = target.AddComponent<CanvasGroup>();
            opacity = group.alpha;
            interactable = group.interactable;
            blocksRaycasts = group.blocksRaycasts;
            Reset();
        }

        // 퇴장 중에는 즉시 입력을 막고 재표시 요청은 현재 진행도에서 반대로 이어간다.
        internal void SetVisible(bool show)
        {
            if (rect == null || group == null || visible == show)
                return;

            visible = show;
            group.interactable = false;
            group.blocksRaycasts = false;
            if (show)
                rect.gameObject.SetActive(true);
        }

        // 스케일이 적용된 가로폭을 부모 좌표로 환산하고 설정에 따라 이동과 페이드를 보간한다.
        internal void Tick(float delta, float duration, Vector2 shakeOffset)
        {
            if (rect == null || group == null)
                return;

            progress = duration <= 0f ? (visible ? 1f : 0f) :
                Mathf.MoveTowards(progress, visible ? 1f : 0f, delta / duration);
            float eased = Mathf.SmoothStep(0f, 1f, progress);
            float distance = rect.rect.width * Mathf.Abs(rect.localScale.x);
            rect.anchoredPosition = origin + Vector2.right * (direction * distance * (1f - eased)) + shakeOffset;
            group.alpha = fade ? opacity * eased : opacity;
            group.interactable = visible && progress >= 1f && interactable;
            group.blocksRaycasts = visible && progress >= 1f && blocksRaycasts;
            if (!visible && progress <= 0f)
                rect.gameObject.SetActive(false);
        }

        // 비활성화 시 중간 위치와 입력 상태를 남기지 않고 다음 표시를 숨김 상태에서 시작한다.
        internal void Reset()
        {
            visible = false;
            progress = 0f;
            Tick(0f, 0f, Vector2.zero);
        }
    }

    /// <summary>실제 전투 HP와 별도로 바, 숫자와 피해 잔상의 진행 상태를 유지한다.</summary>
    private sealed class HealthDisplay
    {
        private readonly Image fill;      // 현재 체력을 표시하는 앞쪽 바
        private readonly Image trail;     // 감소 전 체력을 잠시 유지하는 뒤쪽 바
        private readonly TMP_Text label;  // 앞쪽 바와 같은 진행도를 사용하는 체력 숫자
        private readonly RectTransform fillRect;  // 파괴 여부를 직접 확인할 현재 HP 바 영역
        private readonly RectTransform trailRect; // 파괴 여부를 직접 확인할 잔상 영역
        private readonly Vector3 fillScale;   // 바의 원래 크기
        private readonly Vector2 fillOrigin;  // 바 확대 시 기준이 되는 위치
        private readonly Vector3 trailScale;  // 잔상의 원래 크기
        private readonly Vector2 trailOrigin; // 잔상 확대 시 기준이 되는 위치
        private BattleState state;        // 전투 재시작을 구분하는 상태 참조
        private int target;               // 마지막으로 전달받은 실제 체력
        private float shown;              // 현재 화면에 표시 중인 체력
        private float trailHp;            // 잔상에 표시 중인 체력
        private float startHp;            // 이번 감소 시작 시 앞쪽 바의 체력
        private float startTrail;         // 이번 감소 시작 시 잔상의 체력
        private float elapsed;            // 이번 감소 시작 후 경과 시간(초)
        private bool animating;           // 피해 감소 연출 진행 여부
        private int lastNumber = -1;      // 숫자가 바뀔 때만 텍스트를 갱신하기 위한 값
        private float zeroCompletedAt = -1f; // HP와 잔상 감소 완료 시각(실제 시간, 초), -1이면 대기 시작 전

        internal Vector2 ShakeOffset { get; private set; } // UI 표시 이동에 더할 피격 흔들림 오프셋

        // 체력 숫자와 바, 회색 잔상까지 감소한 후 실제 시간으로 0.3초가 지나면 퇴장을 허용한다.
        internal bool CanHideAfterDefeat()
        {
            if (state == null || !state.IsDead || animating || lastNumber != 0)
            {
                zeroCompletedAt = -1f;
                return false;
            }

            if (zeroCompletedAt < 0f)
                zeroCompletedAt = Time.unscaledTime;
            return Time.unscaledTime - zeroCompletedAt >= 0.3f;
        }

        private bool HasView => fill != null && fillRect != null &&
            trail != null && trailRect != null && label != null;

        // 기존 바와 같은 크기의 잔상을 한 번 생성하고 바로 뒤에 배치한다.
        internal HealthDisplay(Image fill, TMP_Text label, Color color)
        {
            this.fill = fill;
            this.label = label;
            trail = new GameObject("HP Trail", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            trail.gameObject.layer = fill.gameObject.layer;
            RectTransform rect = trail.rectTransform;
            RectTransform source = fill.rectTransform;
            fillRect = source;
            trailRect = rect;
            rect.SetParent(source.parent, false);
            rect.anchorMin = source.anchorMin;
            rect.anchorMax = source.anchorMax;
            rect.pivot = source.pivot;
            rect.sizeDelta = source.sizeDelta;
            rect.anchoredPosition = source.anchoredPosition;
            rect.localScale = source.localScale;
            rect.localRotation = source.localRotation;
            rect.SetSiblingIndex(source.GetSiblingIndex());
            trail.sprite = fill.sprite;
            trail.type = fill.type;
            trail.color = color;
            trail.raycastTarget = false;
            trail.enabled = false;
            fillScale = source.localScale;
            fillOrigin = source.anchoredPosition;
            trailScale = rect.localScale;
            trailOrigin = rect.anchoredPosition;
        }

        // 초기 표시와 회복은 즉시 맞추고 피해만 현재 표시값에서 이어서 감소시킨다.
        internal void SetTarget(BattleState unit)
        {
            if (!HasView)
                return;

            if (state != unit || unit.CurrentHp > target)
            {
                state = unit;
                target = unit.CurrentHp;
                shown = trailHp = target;
                animating = false;
                lastNumber = -1;
                zeroCompletedAt = -1f;
                RestoreImpact();
                Draw();
                return;
            }

            // 문구나 PP 갱신으로 같은 HP가 다시 전달되어도 진행 중인 연출을 재시작하지 않는다.
            if (target == unit.CurrentHp)
                return;

            target = unit.CurrentHp;
            startHp = shown;
            startTrail = trailHp;
            elapsed = 0f;
            animating = true;
            RestoreImpact();
            fill.color = Color.white;
        }

        // 점멸과 확대 후 바와 숫자를 함께 감속시키고 잔상을 뒤따라 줄인다.
        internal void Tick(float delta, float duration, float delay, float tailDuration,
            float flashDuration, float impactScale, float shake)
        {
            if (!animating || state == null)
                return;

            if (!HasView)
            {
                Reset();
                return;
            }

            elapsed += delta;
            float frontTime = Mathf.Max(0.01f, duration);
            float tailTime = Mathf.Max(0.01f, tailDuration);
            float flashTime = Mathf.Max(0.01f, flashDuration);
            float progress = Mathf.Clamp01((elapsed - flashTime) / frontTime);
            // 초반에 빠르게 감소하고 끝에서 감속하며 숫자도 같은 표시값을 사용한다.
            float eased = 1f - (1f - progress) * (1f - progress);
            shown = Mathf.Lerp(startHp, target, eased);
            trailHp = Mathf.Lerp(startTrail, target, Mathf.Clamp01((elapsed - flashTime - frontTime - delay) / tailTime));
            animating = elapsed < flashTime + frontTime + delay + tailTime;
            Draw();

            float strength = elapsed < flashTime ? Mathf.Clamp01(elapsed / flashTime) : 1f - Mathf.SmoothStep(0f, 1f, progress);
            float scale = Mathf.Lerp(1f, impactScale, strength);
            ScaleBar(fillRect, fillScale, fillOrigin, scale);
            ScaleBar(trailRect, trailScale, trailOrigin, scale);
            fill.color = Color.Lerp(Color.white, new Color32(75, 75, 75, 255), progress);
            float shakeProgress = Mathf.Clamp01(elapsed / (flashTime + frontTime));
            ShakeOffset = Vector2.right *
                (Mathf.Sin(shakeProgress * Mathf.PI * 4f) * shake * (1f - shakeProgress));
            if (!animating)
                RestoreImpact();
        }

        // 왼쪽 끝과 세로 중심을 고정하고 가로 확대는 HP 트랙의 오른쪽 경계로 제한한다.
        private static void ScaleBar(RectTransform rect, Vector3 originalScale, Vector2 origin, float scale)
        {
            Vector2 size = rect.rect.size;
            float widthScale = scale;
            RectTransform track = rect.parent as RectTransform;
            float width = size.x * Mathf.Abs(originalScale.x);
            if (track != null && width > 0f)
            {
                // 풀 체력의 바와 잔상이 HUD 밖으로 돌출되지 않도록 남은 가로 공간만 사용한다.
                float available = track.rect.width - rect.anchorMin.x * track.rect.width - origin.x;
                widthScale = Mathf.Min(scale, Mathf.Max(1f, available / width));
            }
            rect.localScale = new Vector3(originalScale.x * widthScale, originalScale.y * scale, originalScale.z);
            rect.anchoredPosition = origin + new Vector2(
                rect.pivot.x * size.x * originalScale.x * (widthScale - 1f),
                (rect.pivot.y - 0.5f) * size.y * originalScale.y * (scale - 1f));
        }

        // 연속 피격과 회복, 비활성화에서도 확대와 흔들림이 누적되지 않도록 복원한다.
        private void RestoreImpact()
        {
            if (fillRect != null)
            {
                fillRect.localScale = fillScale;
                fillRect.anchoredPosition = fillOrigin;
            }
            if (fill != null)
                fill.color = new Color32(75, 75, 75, 255);
            if (trailRect != null)
            {
                trailRect.localScale = trailScale;
                trailRect.anchoredPosition = trailOrigin;
            }
            ShakeOffset = Vector2.zero;
        }

        // 비활성화 시 진행 중인 표시를 확정하고 다음 활성화에서 다시 초기화한다.
        internal void Reset()
        {
            // 씬 언로드에서는 HUD의 RectTransform이 먼저 파괴될 수 있어 표시 갱신을 분리한다.
            if (state != null)
            {
                target = state.CurrentHp;
                shown = trailHp = target;
                Draw();
            }
            animating = false;
            state = null;
            zeroCompletedAt = -1f;
            RestoreImpact();
        }

        // UI 컴포넌트만 제거되는 경우에도 생성한 잔상 오브젝트를 정리한다.
        internal void Dispose()
        {
            RestoreImpact();
            if (trail != null)
                Object.Destroy(trail.gameObject);
        }

        // 체력 비율과 숫자를 함께 적용하고 잔상은 감소 구간이 남아 있을 때만 표시한다.
        private void Draw()
        {
            if (!HasView || state == null)
                return;

            float maximum = Mathf.Max(1, state.MaxHp);
            float ratio = Mathf.Clamp01(shown / maximum);
            fillRect.anchorMax = new Vector2(ratio, 1f);
            trailRect.anchorMax = new Vector2(Mathf.Clamp01(trailHp / maximum), 1f);
            trail.enabled = trailHp > shown;
            fill.color = new Color32(75, 75, 75, 255);
            int number = Mathf.Clamp(Mathf.CeilToInt(shown), 0, state.MaxHp);
            if (number == lastNumber)
                return;
            lastNumber = number;
            label.text = $"{number} / {state.MaxHp}";
        }
    }

    // 현재 메뉴에서 사용할 선택지를 표시한다
    private void ShowMenu()
    {
        for (int i = 0; i < choices.Length; i++)
        {
            labels[i].text = "—";
            choices[i].interactable = false;
        }

        back.interactable = CanChoose && menu != Menu.Root && menu != Menu.End;

        if (!CanChoose)
        {
            detail.text = "전투 진행 중…";
            return;
        }

        BattleState player = battle.PlayerState;

        switch (menu)
        {
            case Menu.Root:
                SetChoice(0, "싸운다", true);
                SetChoice(1, "에테르", true);
                SetChoice(2, "빌드 정보", true);
                SetChoice(3, "도망", false);

                message.text = $"{player.Character.Charactername}, 무엇을 할까?";
                detail.text = "A / D 선택    F 결정    E 뒤로\n이 전투에서는 도망칠 수 없습니다";
                break;

            case Menu.Skills:
                for (int i = 0; i < choices.Length; i++)
                {
                    SkillData skill = player.GetSkill(i);

                    if (skill != null)
                    {
                        SetChoice(
                            i,
                            $"{skill.Skillname}\n<size=65%>{skill.Type}   PP {player.GetCurrentPp(skill)}/{skill.PP}</size>",
                            player.GetCurrentPp(skill) > 0);
                    }
                }

                if (player.GetAvailableSkill() < 0)
                    SetChoice(0, "발버둥", true);

                message.text = "사용할 기술을 선택하세요.";
                detail.text = "에테르 기술 3개 + 무기 기술 1개";
                break;

            case Menu.Aethers:
                ShowAethers();
                break;

            case Menu.Info:
                message.text =
                    $"에테르: {player.Aether.AetherName}\n" +
                    $"무기: {(player.Weapon != null ? player.Weapon.WeaponName : "없음")}";

                detail.text =
                    $"공격 {player.GetStat(BattleStat.Attack)} / 방어 {player.GetStat(BattleStat.Defense)}\n" +
                    $"특공 {player.GetStat(BattleStat.SpecialAttack)} / 특방 {player.GetStat(BattleStat.SpecialDefense)} / 속도 {player.Speed}";

                SetChoice(0, "돌아가기", true);
                break;

            case Menu.End:
                message.text = battle.Winner == BattleSide.Player ? "전투에서 승리했다!" :
                    battle.Winner == BattleSide.Enemy ? "전투에서 패배했다!" :
                    "전투가 무승부로 끝났다!";

                detail.text = "2초 후 맵으로 돌아갑니다";
                break;
        }

        if (!choices[selected].interactable)
        {
            selected = 0;

            for (int i = 0; i < choices.Length; i++)
            {
                if (!choices[i].interactable)
                    continue;

                selected = i;
                break;
            }
        }

        SelectCurrent();
    }

    // 보유한 에테르를 세 개씩 표시한다
    private void ShowAethers()
    {
        int count = battle.OwnedAethers.Count;
        int pages = Mathf.Max(1, (count + 2) / 3);

        aetherPage = Mathf.Clamp(aetherPage, 0, pages - 1);

        int start = aetherPage * 3;

        for (int i = 0; i < 3 && start + i < count; i++)
        {
            AetherData aether = battle.OwnedAethers[start + i];
            bool equipped = aether.itemId == battle.PlayerState.Aether.itemId;

            SetChoice(
                i,
                $"<size=70%>{aether.AetherName}</size>\n<size=65%>{aether.AetherType}{(equipped ? "  현재 사용" : "  교체")}</size>",
                !equipped);
        }

        SetChoice(
            3,
            $"다음 페이지\n<size=65%>{aetherPage + 1} / {pages}</size>",
            pages > 1);

        message.text =
            $"<size=80%>현재: {battle.PlayerState.Aether.AetherName}</size>\n교체할 에테르를 선택하세요.";

        detail.text = count > 1 ?
            "교체하면 내 턴을 소비하고 적이 행동합니다" :
            "교체 가능한 다른 에테르가 없습니다";
    }

    // 한 선택 슬롯의 문구와 입력 가능 상태를 설정한다
    private void SetChoice(int index, string text, bool enabled)
    {
        labels[index].text = text;
        choices[index].interactable = enabled;
    }

    // 현재 선택 항목에 포커스하고 상세 정보를 표시한다
    private void SelectCurrent()
    {
        if (EventSystem.current != null && choices[selected].interactable)
            EventSystem.current.SetSelectedGameObject(choices[selected].gameObject);

        SkillData skill = menu == Menu.Skills ?
            battle.PlayerState.GetSkill(selected) :
            null;

        if (skill != null)
        {
            detail.text =
                $"{skill.Type} / {skill.AttackType}   위력 {skill.Damage}\n" +
                $"명중 {skill.Accuracy}%   우선도 {skill.Priority}";
        }

        if (menu != Menu.Aethers || selected >= 3)
            return;

        int index = aetherPage * 3 + selected;

        if (index >= battle.OwnedAethers.Count)
            return;

        AetherData aether = battle.OwnedAethers[index];
        string skills = "";

        if (aether.Skills != null)
        {
            foreach (SkillData item in aether.Skills)
            {
                if (item != null)
                    skills += (skills.Length > 0 ? " / " : "") + item.Skillname;
            }
        }

        detail.text =
            $"교체 시 내 턴 소비 · {aether.AetherType}\n" +
            $"{(skills.Length > 0 ? skills : "에테르 기술 없음")}";
    }

    // 입력할 수 없는 슬롯을 건너뛰며 선택 위치를 이동한다
    private void Focus(int step)
    {
        for (int i = 0; i < choices.Length; i++)
        {
            selected = (selected + step + choices.Length) % choices.Length;

            if (choices[selected].interactable)
                break;

            if (step == 2)
                step = 1;
        }

        SelectCurrent();
    }

    // 선택 메뉴를 이동하거나 선택한 행동을 전투 흐름에 전달한다
    private void Choose(int index)
    {
        if (!CanChoose || commandView == null || !commandView.IsReady || menu == Menu.End || !choices[index].interactable)
            return;

        selected = index;

        switch (menu)
        {
            case Menu.Root:
                menu = index == 0 ? Menu.Skills :
                    index == 1 ? Menu.Aethers :
                    Menu.Info;
                break;

            case Menu.Skills:
                menu = Menu.Root;
                flow.SelectPlayerSkill(
                    battle.PlayerState.GetAvailableSkill() < 0 ? -1 : index);
                break;

            case Menu.Aethers:
                if (index == 3)
                {
                    aetherPage =
                        (aetherPage + 1) %
                        Mathf.Max(1, (battle.OwnedAethers.Count + 2) / 3);
                }
                else
                {
                    menu = Menu.Root;

                    if (!flow.SelectPlayerAether(aetherPage * 3 + index))
                        menu = Menu.Aethers;
                }
                break;

            case Menu.Info:
                menu = Menu.Root;
                break;
        }

        Refresh();
    }

    // 현재 하위 메뉴를 닫고 기본 메뉴로 돌아간다
    private void Back()
    {
        if (!CanChoose || commandView == null || !commandView.IsReady || menu == Menu.End)
            return;

        menu = Menu.Root;
        Refresh();
    }
}
