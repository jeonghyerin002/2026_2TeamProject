using System;
using UnityEngine;

/// <summary>UI Group의 정적 설정. 선택 상태, 이미지 상태, 이동 기록은 저장하지 않는다.</summary>
[CreateAssetMenu(fileName = "UIGroupData", menuName = "UI/Group Data")]
public class UIGroupData : ScriptableObject
{
    [SerializeField] private string id;
    [SerializeField] private UIItemData[] items = Array.Empty<UIItemData>();
    [SerializeField] private UIInputBinding[] inputBindings = Array.Empty<UIInputBinding>();
    [SerializeField, Min(0)] private int initialItemIndex;
    [SerializeField] private bool wrapSelection = true;
    [SerializeField] private bool wrapImageStates = true;
    [Tooltip("상호작용과 Item에 피드백이 없을 때 사용할 기본 피드백.")]
    [SerializeField] private UIFeedbackData feedback = new UIFeedbackData();

    public string Id => id;
    public int ItemCount => items?.Length ?? 0;
    public int InputBindingCount => inputBindings?.Length ?? 0;
    public int InitialItemIndex => initialItemIndex;
    public bool WrapSelection => wrapSelection;
    public bool WrapImageStates => wrapImageStates;
    public UIFeedbackData Feedback => feedback;

    public UIItemData GetItem(int index)
    {
        return index >= 0 && index < ItemCount ? items[index] : null;
    }

    public UIInputBinding GetInputBinding(int index)
    {
        return index >= 0 && index < InputBindingCount ? inputBindings[index] : null;
    }
}
