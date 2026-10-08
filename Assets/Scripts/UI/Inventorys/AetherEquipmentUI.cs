using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>플레이어의 보유 및 장착 에테르를 맵 UI에 연결함</summary>
[DisallowMultipleComponent]
public sealed class AetherEquipmentUI : MonoBehaviour
{
    [SerializeField] private GameObject equipmentPanel;
    [SerializeField] private RectTransform ownedRoot;
    [SerializeField] private AetherDragSource itemTemplate;
    [SerializeField] private AetherEquipSlot[] slots = Array.Empty<AetherEquipSlot>();
    [SerializeField] private TMP_Text message;
    [SerializeField] private GameObject dialogPanel;
    [SerializeField] private Canvas canvas;

    private PlayerController player;
    private PlayerBattleData playerData;
    private AetherDragSource dragSource;
    private Image dragImage;
    private bool initialized;
    private bool subscribed;
    private bool opened;
    private int closedFrame = -1;
    private readonly List<AetherDragSource> ownedViews = new();

    public bool IsOpen => equipmentPanel != null && equipmentPanel.activeInHierarchy;
    public bool BlocksWorldInput => IsOpen || closedFrame == Time.frameCount;
    internal bool CanEdit => IsOpen && isActiveAndEnabled && playerData != null && playerData.CanEditEquipment && (dialogPanel == null || !dialogPanel.activeInHierarchy);
    internal bool HasDrag => dragSource != null;

    private void Start()
    {
        player = PlayerController.Instance;
        playerData = player != null ? player.GetComponent<PlayerBattleData>() : null;
        if (playerData == null || equipmentPanel == null || canvas == null || ownedRoot == null || itemTemplate == null)
        {
            Debug.LogError("AetherEquipmentUI: 플레이어 데이터와 UI 참조를 연결하세요.", this);
            if (equipmentPanel != null)
                equipmentPanel.SetActive(false);
            enabled = false;
            return;
        }

        itemTemplate.gameObject.SetActive(false);
        RefreshOwnedAethers();
        foreach (AetherEquipSlot slot in slots)
        {
            if (slot != null)
                slot.Bind(this, playerData);
        }
        initialized = true;
        Subscribe();
        SetOpen(false);
        Refresh();
    }

    private void RefreshOwnedAethers()
    {
        ClearDrag();
        foreach (AetherDragSource view in ownedViews)
        {
            if (view == null)
                continue;
            view.gameObject.SetActive(false);
            Destroy(view.gameObject);
        }
        ownedViews.Clear();
        HashSet<int> ids = new();
        foreach (AetherData aether in playerData.OwnedAethers)
        {
            if (aether == null || !ids.Add(aether.itemId))
                continue;
            AetherDragSource source = Instantiate(itemTemplate, ownedRoot);
            source.name = $"Aether_{aether.itemId}";
            source.Bind(this, aether);
            source.gameObject.SetActive(true);
            ownedViews.Add(source);
        }
    }

    private void OnEnable()
    {
        if (!initialized)
            return;
        Subscribe();
        RefreshOwnedAethers();
        Refresh();
    }

    private void Subscribe()
    {
        if (subscribed || playerData == null)
            return;
        playerData.EquipmentChanged += Refresh;
        playerData.OwnedAethersChanged += RefreshOwnedAethers;
        subscribed = true;
    }

    private void OnDisable()
    {
        if (subscribed && playerData != null)
        {
            playerData.EquipmentChanged -= Refresh;
            playerData.OwnedAethersChanged -= RefreshOwnedAethers;
        }
        subscribed = false;
        SetOpen(false);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (!initialized || keyboard == null)
            return;
        if (IsOpen && (!playerData.CanEditEquipment || (dialogPanel != null && dialogPanel.activeInHierarchy)))
        {
            SetOpen(false);
            return;
        }
        if (keyboard.qKey.wasPressedThisFrame)
            SetOpen(!IsOpen);
        else if (IsOpen && (keyboard.eKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame))
            SetOpen(false);
    }

    public void SetOpen(bool open)
    {
        if (equipmentPanel == null)
            return;
        if (open && (!initialized || playerData == null || !playerData.CanEditEquipment || (dialogPanel != null && dialogPanel.activeInHierarchy)))
            return;
        bool wasOpen = opened;
        if (!open)
            ClearDrag();
        equipmentPanel.SetActive(open);
        opened = open;
        if (open)
        {
            player.SetMovementEnabled(false);
            Refresh();
        }
        else if (wasOpen)
        {
            closedFrame = Time.frameCount;
            if (player != null && !BattleSession.IsActive)
                player.SetMovementEnabled(true);
        }
    }

    private void Refresh()
    {
        foreach (AetherEquipSlot slot in slots)
        {
            if (slot != null)
                slot.Refresh();
        }
        ShowMessage(playerData != null && playerData.IsValid ?
            "보유 이미지를 슬롯에 드래그하세요. 우클릭: 장착 해제\nQ: 열기/닫기 · E: 닫기" :
            "에테르를 하나 이상 장착해야 전투에 참가할 수 있습니다.\nQ: 열기/닫기 · E: 닫기");
    }

    public void ShowMessage(string text)
    {
        if (message != null)
            message.text = text;
    }

    internal bool BeginDrag(AetherDragSource source, AetherData aether, PointerEventData eventData)
    {
        if (!CanEdit || HasDrag || source == null || aether == null)
            return false;
        if (dragImage == null)
        {
            GameObject ghost = new GameObject("Aether Drag", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            ghost.transform.SetParent(canvas.transform, false);
            dragImage = ghost.GetComponent<Image>();
            dragImage.raycastTarget = false;
            dragImage.preserveAspect = true;
            dragImage.rectTransform.sizeDelta = new Vector2(96f, 96f);
        }
        dragSource = source;
        dragImage.sprite = aether.Icon;
        dragImage.color = GetIconColor(aether);
        dragImage.gameObject.SetActive(true);
        dragImage.transform.SetAsLastSibling();
        MoveDrag(source, eventData);
        return true;
    }

    internal bool IsDragging(AetherDragSource source)
    {
        return CanEdit && source != null && dragSource == source;
    }

    internal void MoveDrag(AetherDragSource source, PointerEventData eventData)
    {
        if (!IsDragging(source) || dragImage == null)
            return;
        Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle((RectTransform)canvas.transform, eventData.position, camera, out Vector3 position))
            dragImage.rectTransform.position = position;
    }

    internal void EndDrag(AetherDragSource source, bool accepted)
    {
        if (source != dragSource)
            return;
        ClearDrag();
        if (!accepted)
            ShowMessage("장착이 취소되었습니다. 슬롯 위에서 놓아주세요.");
    }

    private void ClearDrag()
    {
        if (dragSource != null)
            dragSource.CancelDrag();
        dragSource = null;
        if (dragImage != null)
            dragImage.gameObject.SetActive(false);
    }

    internal static Color GetIconColor(AetherData aether)
    {
        if (aether == null)
            return Color.white;
        return aether.AetherType switch
        {
            ElementType.Fire => new Color32(245, 119, 73, 255),
            ElementType.Water => new Color32(88, 169, 244, 255),
            ElementType.Grass => new Color32(104, 207, 139, 255),
            _ => Color.white
        };
    }
}
