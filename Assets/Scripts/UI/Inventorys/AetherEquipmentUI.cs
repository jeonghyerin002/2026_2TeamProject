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
    [SerializeField] private GameObject storagePanel;
    [SerializeField] private TMP_Text storageTitle;

    private PlayerController player;
    private PlayerBattleData playerData;
    private AetherDragSource dragSource;
    private Image dragImage;
    private bool initialized;
    private bool subscribed;
    private bool opened;
    private bool storageMode;
    private bool ownedDirty;
    private GameObject storageHeading;
    private int closedFrame = -1;
    private int openedFrame = -1;
    private readonly List<AetherDragSource> ownedViews = new();

    public bool IsOpen => equipmentPanel != null && equipmentPanel.activeInHierarchy;
    public bool BlocksWorldInput => IsOpen || closedFrame == Time.frameCount;
    internal bool CanEdit => storageMode && IsOpen && isActiveAndEnabled && playerData != null && playerData.CanEditEquipment && (dialogPanel == null || !dialogPanel.activeInHierarchy);
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
        ConfigureStorage();
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
        AetherData[] stored = playerData.StoredAethers;
        for (int i = 0; i < stored.Length; i++)
        {
            if (i >= ownedViews.Count)
                ownedViews.Add(Instantiate(itemTemplate, ownedRoot));
            AetherDragSource source = ownedViews[i];
            source.name = $"Aether_{stored[i].itemId}";
            source.Bind(this, stored[i]);
            source.gameObject.SetActive(true);
        }
        for (int i = stored.Length; i < ownedViews.Count; i++)
            ownedViews[i].gameObject.SetActive(false);
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
        playerData.OwnedAethersChanged += MarkOwnedDirty;
        subscribed = true;
    }

    private void OnDisable()
    {
        if (subscribed && playerData != null)
        {
            playerData.EquipmentChanged -= Refresh;
            playerData.OwnedAethersChanged -= MarkOwnedDirty;
        }
        subscribed = false;
        SetOpen(false);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (!initialized || keyboard == null)
            return;
        if (openedFrame == Time.frameCount)
            return;
        if (IsOpen && (!playerData.CanEditEquipment || (dialogPanel != null && dialogPanel.activeInHierarchy)))
        {
            SetOpen(false);
            return;
        }
        if (keyboard.qKey.wasPressedThisFrame)
            SetOpen(!IsOpen);
        else if (IsOpen && (keyboard.eKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame || (storageMode && keyboard.fKey.wasPressedThisFrame)))
            SetOpen(false);
    }

    private void MarkOwnedDirty() => ownedDirty = true;

    private void LateUpdate()
    {
        // 드롭 콜백이 끝난 후 목록을 갱신해 드래그 원본의 항목이 도중에 바뀌지 않게 한다.
        if (!initialized || !ownedDirty || HasDrag)
            return;
        ownedDirty = false;
        RefreshOwnedAethers();
    }

    public void OpenStorage()
    {
        if (!initialized || playerData == null || !playerData.CanEditEquipment ||
            (dialogPanel != null && dialogPanel.activeInHierarchy) || BlocksWorldInput)
            return;
        storageMode = true;
        SetOpen(true);
    }

    public void SetOpen(bool open)
    {
        if (equipmentPanel == null)
            return;
        if (open && (!initialized || playerData == null || !playerData.CanEditEquipment || (dialogPanel != null && dialogPanel.activeInHierarchy)))
            return;
        bool wasOpen = opened;
        if (!open)
        {
            ClearDrag();
            storageMode = false;
        }
        if (storagePanel != null)
            storagePanel.SetActive(open && storageMode);
        if (storageHeading != null)
            storageHeading.SetActive(open && storageMode);
        equipmentPanel.SetActive(open);
        opened = open;
        if (open)
        {
            openedFrame = Time.frameCount;
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
        MarkOwnedDirty();
        foreach (AetherEquipSlot slot in slots)
        {
            if (slot != null)
                slot.Refresh();
        }
        if (storageTitle != null)
            storageTitle.text = $"Aether 보관함 {playerData.StorageCount}/{PlayerBattleData.StorageCapacity}";
        ShowMessage(string.Empty);
    }

    private void ConfigureStorage()
    {
        if (storagePanel == null)
            storagePanel = ownedRoot.parent.gameObject;
        storageHeading = storageTitle != null ? storageTitle.gameObject : null;
        foreach (TMP_Text text in equipmentPanel.GetComponentsInChildren<TMP_Text>(true))
            text.margin = Vector4.zero;
        if (message != null) message.color = new Color32(235, 242, 250, 255);
        AetherStorageDropTarget target = storagePanel.GetComponent<AetherStorageDropTarget>();
        if (target == null) target = storagePanel.AddComponent<AetherStorageDropTarget>();
        target.Bind(this, playerData);

        RectTransform panel = (RectTransform)storagePanel.transform;
        GameObject viewportObject = new GameObject("StorageViewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        RectTransform viewport = viewportObject.GetComponent<RectTransform>();
        viewport.SetParent(panel, false);
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(10, 10);
        viewport.offsetMax = new Vector2(-10, -10);
        viewportObject.GetComponent<Image>().color = new Color(0, 0, 0, 0);
        ownedRoot.SetParent(viewport, false);
        RectMask2D oldMask = ownedRoot.GetComponent<RectMask2D>();
        if (oldMask != null) oldMask.enabled = false;
        ownedRoot.anchorMin = new Vector2(0, 1);
        ownedRoot.anchorMax = Vector2.one;
        ownedRoot.pivot = new Vector2(0.5f, 1);
        ownedRoot.anchoredPosition = Vector2.zero;
        ownedRoot.sizeDelta = Vector2.zero;
        ContentSizeFitter fitter = ownedRoot.GetComponent<ContentSizeFitter>();
        if (fitter == null) fitter = ownedRoot.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        ScrollRect scroll = storagePanel.GetComponent<ScrollRect>();
        if (scroll == null) scroll = storagePanel.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = ownedRoot;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40;
        if (message != null) message.margin = Vector4.zero;
    }

    public void ShowMessage(string text)
    {
        // 기존 상호작용의 호출은 유지하되 하단 부가 설명은 표시하지 않는다.
        if (message != null)
        {
            message.text = string.Empty;
            message.gameObject.SetActive(false);
        }
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
