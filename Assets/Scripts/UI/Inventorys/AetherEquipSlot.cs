using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>장착 슬롯의 드롭 요청과 표시를 처리함</summary>
[DisallowMultipleComponent]
public sealed class AetherEquipSlot : MonoBehaviour, IDropHandler, IPointerClickHandler
{
    [SerializeField, Min(0)] private int slotIndex;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text label;

    private AetherEquipmentUI equipmentUI;
    private PlayerBattleData playerData;
    private AetherDragSource dragSource;

    internal void Bind(AetherEquipmentUI ui, PlayerBattleData data)
    {
        equipmentUI = ui;
        playerData = data;
        dragSource = GetComponent<AetherDragSource>();
        if (dragSource == null) dragSource = gameObject.AddComponent<AetherDragSource>();
        dragSource.Configure(iconImage, label);
        if (label != null)
        {
            label.rectTransform.sizeDelta = new Vector2(140f, 70f);
            label.enableAutoSizing = true;
            label.fontSizeMin = 12;
            label.fontSizeMax = 19;
            label.overflowMode = TextOverflowModes.Ellipsis;
        }
        Refresh();
    }

    internal void Refresh()
    {
        AetherData aether = playerData != null ? playerData.GetEquippedAether(slotIndex) : null;
        if (dragSource != null) dragSource.Bind(equipmentUI, aether);
        if (iconImage != null)
        {
            iconImage.sprite = aether != null ? aether.Icon : null;
            iconImage.color = AetherEquipmentUI.GetIconColor(aether);
            iconImage.enabled = iconImage.sprite != null;
        }
        if (label != null)
            label.text = aether != null ? aether.AetherName : string.Empty;
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (equipmentUI == null || playerData == null || eventData.pointerDrag == null)
            return;
        AetherDragSource source = eventData.pointerDrag.GetComponent<AetherDragSource>();
        if (!equipmentUI.IsDragging(source) || source.DraggedAether == null)
            return;
        AetherData dropped = source.DraggedAether;
        if (!playerData.TryMoveAetherToSlot(dropped, slotIndex))
        {
            source.AcceptDrop();
            equipmentUI.ShowMessage("장착할 수 없습니다. 보유 여부와 중복 장착을 확인하세요.");
            return;
        }
        source.AcceptDrop();
        equipmentUI.ShowMessage($"슬롯 {slotIndex + 1}에 {dropped.AetherName} 장착");
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Right || equipmentUI == null || !equipmentUI.CanEdit || equipmentUI.HasDrag || playerData == null)
            return;
        if (playerData.TryUnequipAether(slotIndex))
            equipmentUI.ShowMessage($"슬롯 {slotIndex + 1} 장착 해제");
        else
            equipmentUI.ShowMessage("보관함이 가득 찼거나 빈 슬롯입니다.");
    }
}
