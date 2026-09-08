using UnityEngine;
using UnityEngine.UI;


/// <summary>
/// 슬롯 하나의 이미지, 위치, 크기를 표시함
/// </summary>
public class InventorySlotView : MonoBehaviour
{
    [SerializeField] private Image iconImage;

    private RectTransform rectTransform;


    private void Awake()
    {
        EnsureReferences();
    }


    /// <summary>
    /// 슬롯에 표시할 이미지를 변경
    /// </summary>
    public void SetSprite(Sprite sprite)
    {
        EnsureReferences();

        if (iconImage == null)
        {
            return;
        }

        iconImage.sprite = sprite;
        iconImage.enabled = sprite != null;
    }


    /// <summary>
    /// 슬롯의 UI 위치를 변경
    /// </summary>
    public void SetPosition(Vector2 position)
    {
        EnsureReferences();

        if (rectTransform == null)
        {
            return;
        }

        rectTransform.anchoredPosition = position;
    }


    /// <summary>
    /// 슬롯의 크기를 변경
    /// </summary>
    public void SetScale(float scale)
    {
        EnsureReferences();

        if (rectTransform == null)
        {
            return;
        }

        rectTransform.localScale = Vector3.one * scale;
    }


    /// <summary>
    /// RectTransform과 Image 참조를 확인
    /// </summary>
    private void EnsureReferences()
    {
        if (rectTransform == null)
        {
            rectTransform = transform as RectTransform;
        }

        if (iconImage == null)
        {
            iconImage = GetComponent<Image>();
        }
    }
}