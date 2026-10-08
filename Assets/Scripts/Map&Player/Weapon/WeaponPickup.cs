using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class WeaponPickup : MonoBehaviour
{
    [SerializeField] private WeaponData weapon;
    [SerializeField, Min(0.1f)] private float pickupRange = 1.5f;
    [SerializeField] private DialogManager dialogManager;
    [SerializeField] private AetherEquipmentUI equipmentUI;
    private PlayerController player;
    private PlayerBattleData data;

    private void Start()
    {
        player = PlayerController.Instance;
        data = player != null ? player.GetComponent<PlayerBattleData>() : null;
        if (weapon == null || data == null || dialogManager == null)
        {
            Debug.LogError("WeaponPickup: WeaponData, 플레이어, DialogManager를 연결하세요.", this);
            enabled = false;
            return;
        }
        GetComponent<SpriteRenderer>().sprite = weapon.Icon;
        if (data.OwnsWeapon(weapon)) gameObject.SetActive(false);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.fKey.wasPressedThisFrame || !data.CanEditEquipment ||
            !dialogManager.CanShowMessage || (equipmentUI != null && equipmentUI.BlocksWorldInput) ||
            AetherStorageTerminal.IsPlayerInRange) return;
        if (((Vector2)(player.transform.position - transform.position)).sqrMagnitude > pickupRange * pickupRange) return;
        if (!data.TryAddWeapon(weapon))
        {
            dialogManager.TryShowMessage("이미 보유한 종류의 무기입니다.");
            return;
        }
        dialogManager.TryShowMessage($"{weapon.WeaponName}을/를 획득했습니다.");
        gameObject.SetActive(false);
    }
}
