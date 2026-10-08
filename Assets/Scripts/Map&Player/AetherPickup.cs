using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>맵 에테르 이미지를 가까이에서 획득하고 안내 대화를 표시함</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class AetherPickup : MonoBehaviour
{
    [SerializeField] private AetherData aether;
    [SerializeField, Min(0.1f)] private float pickupRange = 1.5f;
    [SerializeField] private DialogManager dialogManager;
    [SerializeField] private AetherEquipmentUI equipmentUI;

    private PlayerController player;
    private PlayerBattleData playerData;

    private void Awake()
    {
        SpriteRenderer image = GetComponent<SpriteRenderer>();
        if (aether == null)
        {
            Debug.LogError("AetherPickup: 획득할 AetherData를 연결하세요.", this);
            enabled = false;
            return;
        }
        image.sprite = aether.Icon;
        image.color = AetherEquipmentUI.GetIconColor(aether);
    }

    private void Start()
    {
        player = PlayerController.Instance;
        playerData = player != null ? player.GetComponent<PlayerBattleData>() : null;
        if (playerData == null || dialogManager == null)
        {
            Debug.LogError("AetherPickup: 플레이어 데이터와 DialogManager를 연결하세요.", this);
            enabled = false;
            return;
        }
        if (playerData.OwnsAether(aether))
            gameObject.SetActive(false);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.fKey.wasPressedThisFrame || player == null || playerData == null ||
            !playerData.CanEditEquipment || !dialogManager.CanShowMessage || (equipmentUI != null && equipmentUI.BlocksWorldInput))
            return;
        Vector2 distance = player.transform.position - transform.position;
        if (distance.sqrMagnitude > pickupRange * pickupRange || !playerData.TryAddAether(aether))
            return;
        dialogManager.TryShowMessage($"{aether.AetherName}을/를 획득했습니다");
        gameObject.SetActive(false);
    }
}
