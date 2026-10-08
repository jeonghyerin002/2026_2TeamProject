using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>장착 항목을 보관함 영역으로 드롭해 장착 해제한다.</summary>
public sealed class AetherStorageDropTarget : MonoBehaviour, IDropHandler
{
    private AetherEquipmentUI equipmentUI;
    private PlayerBattleData playerData;

    internal void Bind(AetherEquipmentUI ui, PlayerBattleData data)
    {
        equipmentUI = ui;
        playerData = data;
    }

    public void OnDrop(PointerEventData eventData)
    {
        AetherDragSource source = eventData.pointerDrag != null ? eventData.pointerDrag.GetComponent<AetherDragSource>() : null;
        if (equipmentUI == null || !equipmentUI.IsDragging(source))
            return;
        int slot = playerData.GetEquippedSlot(source.DraggedAether);
        source.AcceptDrop();
        if (slot < 0)
            return;
        equipmentUI.ShowMessage(playerData.TryUnequipAether(slot) ?
            "Aether를 보관함으로 이동했습니다." : "보관함이 가득 찼습니다. 다른 Aether와 교환하세요.");
    }
}
