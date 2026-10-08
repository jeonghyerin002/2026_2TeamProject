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

    internal void Bind(AetherEquipmentUI ui, PlayerBattleData data)
    {
        equipmentUI = ui;
        playerData = data;
        Refresh();
    }

    internal void Refresh()
    {
        AetherData aether = playerData != null ? playerData.GetEquippedAether(slotIndex) : null;
        if (iconImage != null)
        {
            iconImage.sprite = aether != null ? aether.Icon : null;
            iconImage.color = AetherEquipmentUI.GetIconColor(aether);
            iconImage.enabled = iconImage.sprite != null;
        }
        if (label != null)
        {
            bool active = aether != null && playerData.EquippedAether != null && aether.itemId == playerData.EquippedAether.itemId;
            label.text = aether == null ? $"슬롯 {slotIndex + 1}\n비어 있음" :
                $"{aether.AetherName}\n<size=80%>{(active ? "현재 사용" : "장착됨")}</size>";
        }
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (equipmentUI == null || playerData == null || eventData.pointerDrag == null)
            return;
        AetherDragSource source = eventData.pointerDrag.GetComponent<AetherDragSource>();
        if (!equipmentUI.IsDragging(source) || source.DraggedAether == null)
            return;
        if (!playerData.TryEquipAether(slotIndex, source.DraggedAether))
        {
            source.AcceptDrop();
            equipmentUI.ShowMessage("장착할 수 없습니다. 보유 여부와 중복 장착을 확인하세요.");
            return;
        }
        source.AcceptDrop();
        equipmentUI.ShowMessage($"슬롯 {slotIndex + 1}에 {source.DraggedAether.AetherName} 장착");
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Right || equipmentUI == null || !equipmentUI.CanEdit || equipmentUI.HasDrag || playerData == null)
            return;
        if (playerData.TryUnequipAether(slotIndex))
            equipmentUI.ShowMessage($"슬롯 {slotIndex + 1} 장착 해제");
    }
}
