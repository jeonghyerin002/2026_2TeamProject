using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>R 무기 목록. 보유 순서는 플레이어 데이터에, 선택 메뉴는 현재 UI에 저장한다.</summary>
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public sealed class WeaponInventoryUI : MonoBehaviour
{
    [SerializeField] private Canvas canvas;
    [SerializeField] private AetherEquipmentUI aetherUI;
    [SerializeField] private GameObject dialogPanel;
    [SerializeField] private TMP_Text textTemplate;
    private PlayerController player;
    private PlayerBattleData data;
    private GameObject panel;
    private RectTransform content;
    private ScrollRect scroll;
    private WeaponData selected;
    private int closedFrame = -1;
    private readonly List<GameObject> rows = new();
    public static WeaponInventoryUI Instance { get; private set; }
    public bool IsOpen => panel != null && panel.activeSelf;
    public bool BlocksWorldInput => IsOpen || closedFrame == Time.frameCount;

    private void Start()
    {
        player = PlayerController.Instance;
        data = player != null ? player.GetComponent<PlayerBattleData>() : null;
        if (data == null || canvas == null || aetherUI == null || textTemplate == null)
        {
            Debug.LogError("WeaponInventoryUI: 플레이어와 UI 참조를 연결하세요.", this);
            enabled = false;
            return;
        }
        Instance = this;
        BuildPanel();
        data.OwnedWeaponsChanged += Refresh;
        data.EquipmentChanged += Refresh;
        Refresh();
    }

    private void Update()
    {
        if (data == null || panel == null) return;
        if (!data.CanEditEquipment || (dialogPanel != null && dialogPanel.activeInHierarchy))
        {
            SetOpen(false);
            return;
        }
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;
        if (keyboard.rKey.wasPressedThisFrame) SetOpen(!IsOpen);
        else if (IsOpen && (keyboard.qKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame || keyboard.eKey.wasPressedThisFrame))
            SetOpen(false);
    }

    public void SetOpen(bool open)
    {
        if (panel == null || open == IsOpen) return;
        if (open && (!data.CanEditEquipment || (dialogPanel != null && dialogPanel.activeInHierarchy))) return;
        if (open) aetherUI.SetOpen(false);
        panel.SetActive(open);
        if (open)
        {
            selected = null;
            Refresh();
            panel.transform.SetAsLastSibling();
            player.SetMovementEnabled(false);
        }
        else
        {
            closedFrame = Time.frameCount;
            if (player != null && !BattleSession.IsActive && !aetherUI.IsOpen)
                player.SetMovementEnabled(true);
        }
    }

    private RectTransform Rect(string name, Transform parent, Vector2 size)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.layer = canvas.gameObject.layer;
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.sizeDelta = size;
        return rect;
    }

    private void Stretch(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = min;
        rect.offsetMax = max;
    }

    private Image Background(RectTransform rect, Color color)
    {
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private TMP_Text Label(Transform parent, string text)
    {
        RectTransform rect = Rect("Text", parent, Vector2.zero);
        Stretch(rect, new Vector2(16, 0), new Vector2(-16, 0));
        TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = textTemplate.font;
        label.fontSharedMaterial = textTemplate.fontSharedMaterial;
        label.text = text;
        label.fontSize = 24;
        label.enableAutoSizing = true;
        label.fontSizeMin = 14;
        label.fontSizeMax = 24;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.color = Color.white;
        label.raycastTarget = false;
        return label;
    }

    private void BuildPanel()
    {
        RectTransform root = Rect("WeaponInventoryPanel", canvas.transform, new Vector2(560, 640));
        panel = root.gameObject;
        Background(root, new Color32(25, 33, 48, 250));
        RectTransform title = Rect("Title", root, new Vector2(520, 64));
        title.anchorMin = title.anchorMax = new Vector2(0.5f, 1);
        title.pivot = new Vector2(0.5f, 1);
        title.anchoredPosition = new Vector2(0, -12);
        Label(title, "무기 인벤토리  [R]");

        RectTransform viewport = Rect("Viewport", root, Vector2.zero);
        Stretch(viewport, new Vector2(20, 20), new Vector2(-20, -88));
        Background(viewport, new Color32(17, 23, 35, 255));
        viewport.gameObject.AddComponent<RectMask2D>();
        content = Rect("Content", viewport, Vector2.zero);
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(0.5f, 1);
        VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.spacing = 8;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40;
        panel.SetActive(false);
    }

    private void Refresh()
    {
        if (content == null || data == null) return;
        float position = scroll.verticalNormalizedPosition;
        foreach (GameObject row in rows)
        {
            row.SetActive(false);
            Destroy(row);
        }
        rows.Clear();
        WeaponData[] items = data.OwnedWeapons;
        if (items.Length == 0)
        {
            RectTransform empty = Rect("Empty", content, Vector2.zero);
            empty.gameObject.AddComponent<LayoutElement>().preferredHeight = 70;
            Label(empty, "보유한 무기가 없습니다.");
            rows.Add(empty.gameObject);
        }
        foreach (WeaponData item in items)
        {
            bool equipped = data.Weapon != null && data.Weapon.ItemId == item.ItemId;
            RectTransform row = Rect($"Weapon_{item.ItemId}", content, Vector2.zero);
            rows.Add(row.gameObject);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 70;
            Image background = Background(row, equipped ? new Color32(43, 86, 104, 255) : new Color32(43, 53, 72, 255));
            Button select = row.gameObject.AddComponent<Button>();
            select.targetGraphic = background;
            TMP_Text name = Label(row, item.WeaponName);
            name.rectTransform.offsetMax = new Vector2(-135, 0);
            select.onClick.AddListener(() => { selected = selected == item ? null : item; Refresh(); });
            if (selected == item)
            {
                RectTransform action = Rect("Action", row, new Vector2(112, 50));
                action.anchorMin = action.anchorMax = new Vector2(1, 0.5f);
                action.pivot = new Vector2(1, 0.5f);
                action.anchoredPosition = new Vector2(-8, 0);
                Image actionImage = Background(action, new Color32(66, 102, 141, 255));
                Button button = action.gameObject.AddComponent<Button>();
                button.targetGraphic = actionImage;
                Label(action, equipped ? "해제" : "장착").alignment = TextAlignmentOptions.Center;
                button.onClick.AddListener(() =>
                {
                    if (equipped) data.TryUnequipWeapon();
                    else data.TryEquipWeapon(item);
                });
            }
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        scroll.verticalNormalizedPosition = position;
    }

    private void OnDisable() => SetOpen(false);
    private void OnDestroy()
    {
        if (data != null)
        {
            data.OwnedWeaponsChanged -= Refresh;
            data.EquipmentChanged -= Refresh;
        }
        if (Instance == this) Instance = null;
        if (panel != null) Destroy(panel);
    }
}
