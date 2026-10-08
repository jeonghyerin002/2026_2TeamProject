using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>필드 보관함 기기의 F 입력과 상호작용 범위를 관리한다.</summary>
[DisallowMultipleComponent]
public sealed class AetherStorageTerminal : MonoBehaviour
{
    [SerializeField] private AetherEquipmentUI equipmentUI;
    [SerializeField] private DialogManager dialogManager;
    [SerializeField, Min(0.1f)] private float interactionRange = 1.5f;
    private static AetherStorageTerminal activeTerminal;

    public static bool IsPlayerInRange => activeTerminal != null && activeTerminal.InRange;
    private bool InRange => PlayerController.Instance != null &&
        ((Vector2)(PlayerController.Instance.transform.position - transform.position)).sqrMagnitude <= interactionRange * interactionRange;

    private void OnEnable() => activeTerminal = this;
    private void OnDisable()
    {
        if (activeTerminal == this) activeTerminal = null;
    }

    private void Update()
    {
        if (!InRange || equipmentUI == null || equipmentUI.BlocksWorldInput ||
            BattleSession.IsActive || (dialogManager != null && !dialogManager.CanShowMessage))
            return;
        if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
            equipmentUI.OpenStorage();
    }
}
