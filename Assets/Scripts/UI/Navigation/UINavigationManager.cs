using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 항상 활성인 별도 GameObject에 둔다. 등록된 Group의 열기/닫기와 이동 기록을 전담한다.
/// Group Controller는 각각 독립된 패널의 루트에 배치한다.
/// </summary>
[DisallowMultipleComponent]
public class UINavigationManager : MonoBehaviour
{
    [SerializeField] private UIGroupData initialGroup;
    [SerializeField] private UIGroupController[] groups = Array.Empty<UIGroupController>();
    [SerializeField] private UnityEvent<UIGroupData> onGroupChanged = new UnityEvent<UIGroupData>();

    private readonly Dictionary<UIGroupData, UIGroupController> controllers =
        new Dictionary<UIGroupData, UIGroupController>();
    private readonly Stack<UIGroupData> history = new Stack<UIGroupData>();
    private bool initialized;
    private bool isTransitioning;

    public UIGroupData CurrentGroup { get; private set; }
    public UIGroupController CurrentController { get; private set; }
    public int HistoryCount => history.Count;
    public bool CanGoBack => history.Count > 0;
    public bool IsTransitioning => isTransitioning;

    private void Awake()
    {
        Initialize();
    }

    private void Start()
    {
        if (CurrentGroup == null && initialGroup != null)
        {
            TryOpenGroup(initialGroup);
        }
    }

    private void Initialize()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;
        isTransitioning = true;
        try
        {
            if (groups == null)
            {
                return;
            }

            foreach (UIGroupController controller in groups)
            {
                if (controller == null || controller.Data == null)
                {
                    Debug.LogWarning("UI Navigation: Group Controller 또는 Data가 비어 있습니다.", this);
                    continue;
                }

                if (controllers.ContainsKey(controller.Data))
                {
                    Debug.LogWarning("UI Navigation: 같은 Group Data가 중복 등록되었습니다.", controller);
                    continue;
                }

                // 패널을 닫으면서 Manager 또는 다른 Group까지 꺼지는 계층 구성을 거부한다.
                if (transform.IsChildOf(controller.transform) || HasOverlappingRoot(controller))
                {
                    Debug.LogWarning("UI Navigation: Manager와 Group 루트는 독립된 계층으로 구성하세요.", controller);
                    continue;
                }

                if (controller.Initialize(this))
                {
                    controllers.Add(controller.Data, controller);
                }
            }

            foreach (UIGroupController controller in controllers.Values)
            {
                controller.SetOpen(false);
            }
        }
        finally
        {
            isTransitioning = false;
        }
    }

    private bool HasOverlappingRoot(UIGroupController candidate)
    {
        foreach (UIGroupController controller in controllers.Values)
        {
            if (candidate.transform.IsChildOf(controller.transform) ||
                controller.transform.IsChildOf(candidate.transform))
            {
                return true;
            }
        }

        return false;
    }

    // void 진입점은 나중에 Button.onClick 등 Inspector 이벤트에 연결할 수 있다.
    public void OpenGroup(UIGroupData group)
    {
        TryOpenGroup(group);
    }

    public void OpenInitialGroup()
    {
        TryOpenGroup(initialGroup);
    }

    public bool TryOpenGroup(UIGroupData group)
    {
        if (!isActiveAndEnabled || isTransitioning || group == null)
        {
            return false;
        }

        Initialize();
        if (group == CurrentGroup || !TryGetAvailableController(group, out UIGroupController next))
        {
            return false;
        }

        if (CurrentGroup != null)
        {
            history.Push(CurrentGroup);
        }

        ChangeGroup(group, next);
        return true;
    }

    public void Back()
    {
        TryBack();
    }

    public bool TryBack()
    {
        if (!isActiveAndEnabled || isTransitioning)
        {
            return false;
        }

        // 파괴되었거나 더 이상 열 수 없는 기록은 건너뛴다. 실패 시 현재 Group은 유지한다.
        while (history.Count > 0)
        {
            UIGroupData previous = history.Pop();
            if (previous != CurrentGroup && TryGetAvailableController(previous, out UIGroupController next))
            {
                ChangeGroup(previous, next);
                return true;
            }
        }

        return false;
    }

    public void ClearHistory()
    {
        if (!isTransitioning)
        {
            history.Clear();
        }
    }

    /// <summary>현재 Group을 닫고 기록을 비운다. 런타임 선택/이미지 상태는 유지한다.</summary>
    public void Close()
    {
        if (isTransitioning)
        {
            return;
        }

        history.Clear();
        if (CurrentGroup != null || CurrentController != null)
        {
            ChangeGroup(null, null);
        }
    }

    private bool TryGetAvailableController(UIGroupData group, out UIGroupController controller)
    {
        controller = null;
        return group != null && controllers.TryGetValue(group, out controller) &&
            controller != null && controller.CanOpen;
    }

    private void ChangeGroup(UIGroupData group, UIGroupController next)
    {
        isTransitioning = true;
        try
        {
            UIGroupController previous = CurrentController;
            CurrentController = null;
            CurrentGroup = null;
            if (previous != null)
            {
                previous.SetOpen(false);
            }

            CurrentGroup = group;
            CurrentController = next;
            if (next != null)
            {
                next.SetOpen(true);
            }

            // 콜백 중 Open/Back 재진입을 막아 한 번의 이동을 원자적으로 마친다.
            onGroupChanged?.Invoke(CurrentGroup);
        }
        finally
        {
            isTransitioning = false;
        }
    }
}
