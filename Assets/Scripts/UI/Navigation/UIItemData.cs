using System;
using UnityEngine;

/// <summary>UIGroupData 안에 직렬화되는 Item 정의. 별도 Asset은 필요하지 않다.</summary>
[Serializable]
public class UIItemData
{
    [SerializeField] private string id;
    [SerializeField] private string displayName;
    [SerializeField, TextArea] private string description;
    [SerializeField] private Sprite sprite;
    [Tooltip("기본 Sprite가 상태 0이며, 이 배열은 상태 1부터 순서대로 사용한다.")]
    [SerializeField] private Sprite[] additionalSprites = Array.Empty<Sprite>();
    [SerializeField] private UIInteractionData interaction = new UIInteractionData();
    [SerializeField] private UIGroupData nextGroup;
    [SerializeField] private UIFeedbackData feedback = new UIFeedbackData();

    public string Id => id;
    public string DisplayName => string.IsNullOrEmpty(displayName) ? id : displayName;
    public string Description => description;
    public Sprite Sprite => sprite;
    public UIInteractionData Interaction => interaction;
    public UIGroupData NextGroup => nextGroup;
    public UIFeedbackData Feedback => feedback;
    public int ImageStateCount => 1 + (additionalSprites?.Length ?? 0);

    public Sprite GetSprite(int stateIndex)
    {
        if (stateIndex == 0)
        {
            return sprite;
        }

        return stateIndex > 0 && stateIndex < ImageStateCount
            ? additionalSprites[stateIndex - 1]
            : null;
    }
}
