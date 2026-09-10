using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Group 패널의 루트에 둔다. Update 없이 Manager와 InventoryInput의 요청을 처리한다.
/// Item 순서와 Item Views 순서를 맞추며, 기존 InventorySlotView를 표시 용도로 재사용한다.
/// </summary>
[DisallowMultipleComponent]
public class UIGroupController : MonoBehaviour
{
    [SerializeField] private UIGroupData data;
    [Tooltip("연결하면 두 슬롯의 표시와 애니메이션을 캐러셀에 맡긴다.")]
    [SerializeField] private InventoryCarouselUI carouselUI;
    [SerializeField] private InventorySlotView[] itemViews = Array.Empty<InventorySlotView>();
    [SerializeField, Min(0f)] private float selectedScale = 1.1f;
    [SerializeField, Min(0f)] private float normalScale = 1f;
    [Tooltip("이동 직후에도 소리가 재생되도록 항상 활성인 GameObject의 AudioSource를 연결한다.")]
    [SerializeField] private AudioSource feedbackAudioSource;
    [SerializeField] private UnityEvent<int> onSelectionChanged = new UnityEvent<int>();
    [Tooltip("Invoke 행동의 실제 게임 로직. 인수는 실행한 Item의 Index이다.")]
    [SerializeField] private UnityEvent<int> onItemInvoked = new UnityEvent<int>();
    [Tooltip("텍스트, 애니메이션 등 추가 피드백. 인수는 Feedback의 Id이다.")]
    [SerializeField] private UnityEvent<string> onFeedbackRequested = new UnityEvent<string>();

    private UINavigationManager navigation;
    private int selectedIndex = -1;
    private int[] imageStateIndices = Array.Empty<int>();
    private bool isOpen;
    private bool isExecuting;
    private readonly UIInteractionData previousInteraction = new UIInteractionData(UIActionType.SelectPrevious);
    private readonly UIInteractionData nextInteraction = new UIInteractionData(UIActionType.SelectNext);

    public UIGroupData Data => data;
    public int SelectedIndex => selectedIndex;
    public UIItemData SelectedItem => data != null ? data.GetItem(selectedIndex) : null;
    public bool CanReceiveInput => data != null && isOpen && isActiveAndEnabled && navigation != null &&
        navigation.isActiveAndEnabled && !navigation.IsTransitioning &&
        navigation.CurrentController == this;

    internal bool CanOpen => enabled && data != null &&
        (transform.parent == null || transform.parent.gameObject.activeInHierarchy);

    internal bool Initialize(UINavigationManager manager)
    {
        if (manager == null || data == null || (navigation != null && navigation != manager))
        {
            return false;
        }

        if (navigation == manager)
        {
            return true;
        }

        navigation = manager;
        imageStateIndices = new int[data.ItemCount];
        selectedIndex = data.GetItem(data.InitialItemIndex) != null ? data.InitialItemIndex : -1;
        if (selectedIndex < 0)
        {
            for (int i = 0; i < data.ItemCount; i++)
            {
                if (data.GetItem(i) != null)
                {
                    selectedIndex = i;
                    break;
                }
            }
        }

        if (carouselUI != null)
        {
            carouselUI.Initialize(this);
        }

        return true;
    }

    // 다른 Group을 직접 조작하지 않는다. 이 메서드는 Manager만 호출한다.
    internal void SetOpen(bool open)
    {
        isOpen = open;
        if (!open && carouselUI != null)
        {
            carouselUI.StopPresentation();
        }

        gameObject.SetActive(open);
        if (open)
        {
            RefreshViews();
            onSelectionChanged?.Invoke(selectedIndex);
        }
    }

    internal void RequestOpen(bool open)
    {
        if (navigation == null)
        {
            return;
        }

        if (open)
        {
            navigation.TryOpenGroup(data);
        }
        else if (navigation.CurrentController == this)
        {
            navigation.Close();
        }
    }

    internal bool RequestMove(int direction)
    {
        return ExecuteInteraction(direction < 0 ? previousInteraction : nextInteraction);
    }

    /// <summary>Button.onClick에 정수 Item Index를 지정하여 연결할 수 있다.</summary>
    public void OnItemClicked(int itemIndex)
    {
        TryExecuteItem(itemIndex);
    }

    public bool TryExecuteItem(int itemIndex)
    {
        UIItemData item = data != null ? data.GetItem(itemIndex) : null;
        return item != null && ExecuteInteraction(item.Interaction, itemIndex);
    }

    /// <summary>키와 클릭의 공통 실행 경로. 비활성 UI와 이벤트의 재귀 실행을 차단한다.</summary>
    public bool ExecuteInteraction(UIInteractionData interaction, int itemIndex = -1)
    {
        if (!CanReceiveInput || isExecuting || interaction == null || itemIndex < -1)
        {
            return false;
        }

        isExecuting = true;
        try
        {
            return ExecuteCore(interaction, itemIndex, true);
        }
        finally
        {
            isExecuting = false;
        }
    }

    private bool ExecuteCore(UIInteractionData interaction, int contextIndex, bool allowConfirm, UIFeedbackData feedbackOverride = null)
    {
        if (interaction == null || interaction.Action == UIActionType.None || interaction.ItemIndex < -1)
        {
            return false;
        }

        int itemIndex = interaction.ItemIndex >= 0 ? interaction.ItemIndex : contextIndex;
        bool hasExplicitItem = itemIndex >= 0;
        if (!hasExplicitItem)
        {
            itemIndex = selectedIndex;
        }

        UIItemData item = data.GetItem(itemIndex);
        if (hasExplicitItem && item == null)
        {
            return false;
        }

        if (interaction.Action == UIActionType.Confirm)
        {
            // Item 자체에 Confirm을 잘못 지정해도 무한 재귀하지 않는다.
            UIFeedbackData confirmFeedback = interaction.Feedback != null && interaction.Feedback.HasFeedback
                ? interaction.Feedback : null;
            return allowConfirm && item != null &&
                ExecuteCore(item.Interaction, itemIndex, false, confirmFeedback);
        }

        if (hasExplicitItem && selectedIndex != itemIndex)
        {
            SetSelection(itemIndex);
            if (!CanReceiveInput)
            {
                return false;
            }
        }

        bool executed;
        switch (interaction.Action)
        {
            case UIActionType.SelectPrevious:
                return MoveSelection(-1, feedbackOverride ?? interaction.Feedback);
            case UIActionType.SelectNext:
                return MoveSelection(1, feedbackOverride ?? interaction.Feedback);
            case UIActionType.PreviousImage:
                executed = ChangeImage(itemIndex, -1);
                break;
            case UIActionType.NextImage:
                executed = ChangeImage(itemIndex, 1);
                break;
            case UIActionType.OpenGroup:
                UIGroupData target = interaction.TargetGroup != null
                    ? interaction.TargetGroup
                    : item?.NextGroup;
                executed = navigation.TryOpenGroup(target);
                break;
            case UIActionType.Back:
                executed = navigation.TryBack();
                break;
            case UIActionType.Invoke:
                executed = item != null;
                if (executed)
                {
                    onItemInvoked?.Invoke(itemIndex);
                }
                break;
            default:
                executed = false;
                break;
        }

        if (executed)
        {
            PlayFeedback(feedbackOverride ?? interaction.Feedback, item?.Feedback);
        }

        return executed;
    }

    private bool MoveSelection(int direction, UIFeedbackData feedback)
    {
        if (carouselUI != null && carouselUI.IsMoving)
        {
            return false;
        }

        int index = GetAdjacentIndex(selectedIndex, direction);
        if (index < 0)
        {
            return false;
        }

        if (carouselUI != null)
        {
            return carouselUI.AnimateSelection(index, direction, () =>
            {
                if (!CanReceiveInput)
                {
                    return;
                }

                bool wasExecuting = isExecuting;
                isExecuting = true;
                try
                {
                    selectedIndex = index;
                    onSelectionChanged?.Invoke(selectedIndex);
                    PlayFeedback(feedback, data.GetItem(index)?.Feedback);
                }
                finally
                {
                    isExecuting = wasExecuting;
                }
            });
        }

        SetSelection(index);
        PlayFeedback(feedback, data.GetItem(index)?.Feedback);
        return true;
    }

    internal int GetAdjacentIndex(int fromIndex, int direction)
    {
        int count = data.ItemCount;
        if (count == 0)
        {
            return -1;
        }

        int index = fromIndex < 0 ? (direction > 0 ? -1 : count) : fromIndex;
        for (int checkedCount = 0; checkedCount < count; checkedCount++)
        {
            index += direction;
            if (index < 0 || index >= count)
            {
                if (!data.WrapSelection)
                {
                    return -1;
                }

                index = (index + count) % count;
            }

            if (data.GetItem(index) != null)
            {
                if (index == fromIndex)
                {
                    return -1;
                }

                return index;
            }
        }

        return -1;
    }

    internal Sprite GetItemSprite(int itemIndex)
    {
        return data.GetItem(itemIndex)?.GetSprite(GetImageStateIndex(itemIndex));
    }

    private void SetSelection(int index)
    {
        selectedIndex = index;
        RefreshViews();
        onSelectionChanged?.Invoke(selectedIndex);
    }

    private bool ChangeImage(int itemIndex, int direction)
    {
        UIItemData item = data.GetItem(itemIndex);
        if (item == null || itemIndex < 0 || itemIndex >= imageStateIndices.Length || item.ImageStateCount <= 1)
        {
            return false;
        }

        int count = item.ImageStateCount;
        int next = imageStateIndices[itemIndex] + direction;
        next = data.WrapImageStates ? (next + count) % count : Mathf.Clamp(next, 0, count - 1);
        if (next == imageStateIndices[itemIndex])
        {
            return false;
        }

        imageStateIndices[itemIndex] = next;
        RefreshViews();
        return true;
    }

    public int GetImageStateIndex(int itemIndex)
    {
        return itemIndex >= 0 && itemIndex < imageStateIndices.Length ? imageStateIndices[itemIndex] : -1;
    }

    private void RefreshViews()
    {
        if (carouselUI != null)
        {
            carouselUI.RefreshSelection();
            return;
        }

        if (itemViews == null || data == null)
        {
            return;
        }

        for (int i = 0; i < itemViews.Length; i++)
        {
            InventorySlotView view = itemViews[i];
            if (view == null)
            {
                continue;
            }

            UIItemData item = data.GetItem(i);
            view.SetSprite(item?.GetSprite(GetImageStateIndex(i)));
            view.SetScale(i == selectedIndex ? selectedScale : normalScale);
        }
    }

    private void PlayFeedback(UIFeedbackData interactionFeedback, UIFeedbackData itemFeedback)
    {
        UIFeedbackData feedback = interactionFeedback != null && interactionFeedback.HasFeedback
            ? interactionFeedback
            : itemFeedback != null && itemFeedback.HasFeedback ? itemFeedback : data.Feedback;
        if (feedback == null || !feedback.HasFeedback)
        {
            return;
        }

        if (feedback.Sound != null && feedbackAudioSource != null && feedbackAudioSource.isActiveAndEnabled)
        {
            feedbackAudioSource.PlayOneShot(feedback.Sound, feedback.Volume);
        }

        if (!string.IsNullOrEmpty(feedback.Id))
        {
            onFeedbackRequested?.Invoke(feedback.Id);
        }
    }
}
