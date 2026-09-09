using System;
using UnityEngine;
using UnityEngine.InputSystem;

public enum UIActionType
{
    None = 0,
    SelectPrevious = 1,
    SelectNext = 2,
    Confirm = 3,
    OpenGroup = 4,
    Back = 5,
    PreviousImage = 6,
    NextImage = 7,
    Invoke = 8
}

/// <summary>피드백의 정의. Scene의 함수 연결은 UIGroupController에서만 한다.</summary>
[Serializable]
public class UIFeedbackData
{
    [SerializeField] private string id;
    [SerializeField] private AudioClip sound;
    [SerializeField, Range(0f, 1f)] private float volume = 1f;

    public string Id => id;
    public AudioClip Sound => sound;
    public float Volume => Mathf.Clamp01(volume);
    public bool HasFeedback => !string.IsNullOrEmpty(id) || sound != null;
}

/// <summary>행동의 설정만 보관하며 실행 상태와 Scene 참조를 갖지 않는다.</summary>
[Serializable]
public class UIInteractionData
{
    [SerializeField] private UIActionType action = UIActionType.Invoke;
    [Tooltip("-1이면 현재 선택 Item, 0 이상이면 해당 Item을 대상으로 한다.")]
    [SerializeField, Min(-1)] private int itemIndex = -1;
    [Tooltip("OpenGroup의 목적지. 비어 있으면 대상 Item의 Next Group을 사용한다.")]
    [SerializeField] private UIGroupData targetGroup;
    [SerializeField] private UIFeedbackData feedback = new UIFeedbackData();

    public UIActionType Action => action;
    public int ItemIndex => itemIndex;
    public UIGroupData TargetGroup => targetGroup;
    public UIFeedbackData Feedback => feedback;
}

/// <summary>한 번 누른 키와 행동을 연결한다. 목록 앞쪽의 일치하는 키가 우선한다.</summary>
[Serializable]
public class UIInputBinding
{
    [SerializeField] private Key key = Key.None;
    [SerializeField] private UIInteractionData interaction = new UIInteractionData();

    public Key Key => key;
    public UIInteractionData Interaction => interaction;
}
