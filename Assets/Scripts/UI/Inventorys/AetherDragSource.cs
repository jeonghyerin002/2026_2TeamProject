using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>보유 에테르 이미지의 드래그 입력을 처리함</summary>
[DisallowMultipleComponent]
public sealed class AetherDragSource : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text label;

    private AetherEquipmentUI equipmentUI;
    private AetherData aether;
    private AetherData draggedAether;
    private bool accepted;

    internal AetherData DraggedAether => draggedAether;

    internal void Bind(AetherEquipmentUI ui, AetherData data)
    {
        equipmentUI = ui;
        aether = data;
        if (iconImage != null)
        {
            iconImage.sprite = data != null ? data.Icon : null;
            iconImage.color = AetherEquipmentUI.GetIconColor(data);
            iconImage.enabled = iconImage.sprite != null;
        }
        if (label != null)
            label.text = data != null ? $"{data.AetherName}\n<size=75%>{data.AetherType}</size>" : string.Empty;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || equipmentUI == null || aether == null || draggedAether != null)
            return;
        accepted = false;
        draggedAether = aether;
        if (!equipmentUI.BeginDrag(this, draggedAether, eventData))
            draggedAether = null;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (draggedAether != null && equipmentUI != null)
            equipmentUI.MoveDrag(this, eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (draggedAether == null)
            return;
        if (equipmentUI != null)
            equipmentUI.EndDrag(this, accepted);
        draggedAether = null;
    }

    internal void AcceptDrop()
    {
        accepted = true;
    }

    internal void CancelDrag()
    {
        draggedAether = null;
        accepted = false;
    }

    private void OnDisable()
    {
        if (equipmentUI != null && draggedAether != null)
            equipmentUI.EndDrag(this, true);
        CancelDrag();
    }
}
