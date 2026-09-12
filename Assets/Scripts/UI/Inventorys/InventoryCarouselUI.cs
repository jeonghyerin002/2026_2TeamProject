using System.Collections;
using UnityEngine;
using System;
using UnityEngine.UI;

/// <summary>
/// 슬롯을 재사용해 인벤토리 순환 이동과 중앙 확대를 관리함
/// Group 연결 시 선택 상태는 Group이 관리하고 이 컴포넌트는 표시를 담당함
/// </summary>
public class InventoryCarouselUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject inventoryPanel;
    [SerializeField] private InventorySlotView[] slotViews;
    [SerializeField] private Image selectedItemImage;
    [SerializeField] private Image previousItemImage;
    [SerializeField] private Image nextItemImage;

    [Header("아이템 이미지")]
    [SerializeField] private Sprite[] itemSprites;

    [Header("슬롯 설정")]
    [SerializeField] private float slotSpacing = 180f;
    [SerializeField] private float centerScale = 1.2f;
    [SerializeField] private float sideScale = 1f;

    [Header("이동 설정")]
    [SerializeField] private float moveDuration = 0.15f;
    [SerializeField] private float fadePortion = 0.25f;
    [SerializeField] private float restFadeDuration = 0.06f;
    [SerializeField]
    private AnimationCurve moveCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    private bool startOpen = false;

    private InventorySlotView currentView;
    private InventorySlotView bufferView;

    private CanvasGroup currentGroup;
    private CanvasGroup bufferGroup;

    private int selectedIndex;
    private int requestedDirection;
    private int lastRequestFrame = -10;

    private bool isMoving;
    private UIGroupController groupController;
    private int presentationVersion;
    private Image[] previewImages;
    private Vector3[] previewPositions;
    private Vector2[] previewSizes;
    private Color[] previewColors;

    private bool UsesPreviewImages => selectedItemImage != null && previousItemImage != null && nextItemImage != null;

    public bool IsOpen => inventoryPanel != null && inventoryPanel.activeSelf;
    internal bool IsMoving => isMoving;
    private int ItemCount => groupController != null ? groupController.Data.ItemCount : itemSprites?.Length ?? 0;


    private void Awake()
    {
        if (!EnsureReferences() || groupController != null)
        {
            return;
        }

        inventoryPanel.SetActive(startOpen);
        RefreshImmediate();
    }

    internal void Initialize(UIGroupController controller)
    {
        groupController = controller;
        EnsureReferences();
    }

    private bool EnsureReferences()
    {
        if (UsesPreviewImages)
        {
            if (previewImages == null)
            {
                previewImages = new[] { previousItemImage, selectedItemImage, nextItemImage };
                previewPositions = new Vector3[previewImages.Length];
                previewSizes = new Vector2[previewImages.Length];
                previewColors = new Color[previewImages.Length];
                for (int i = 0; i < previewImages.Length; i++)
                {
                    previewPositions[i] = previewImages[i].rectTransform.localPosition;
                    previewSizes[i] = previewImages[i].rectTransform.sizeDelta;
                    previewColors[i] = previewImages[i].color;
                }
            }

            return inventoryPanel != null;
        }

        if (currentView != null && bufferView != null)
        {
            return true;
        }

        if (inventoryPanel == null || slotViews == null || slotViews.Length < 2 || slotViews[0] == null || slotViews[1] == null || slotViews[0] == slotViews[1])
        {
            return false;
        }

        currentView = slotViews[0];
        bufferView = slotViews[1];

        currentGroup = GetCanvasGroup(currentView);
        bufferGroup = GetCanvasGroup(bufferView);

        return true;
    }


    public void ToggleOpen()
    {
        SetOpen(!IsOpen);
    }


    //인벤토리 UI를 열거나 닫음
    public void SetOpen(bool isOpen)
    {
        if (groupController != null)
        {
            groupController.RequestOpen(isOpen);
            return;
        }

        if (inventoryPanel == null)
        {
            return;
        }

        StopPresentation();

        inventoryPanel.SetActive(isOpen);

        if (isOpen)
        {
            RefreshImmediate();
        }
    }


    public void MoveLeft()
    {
        Move(-1);
    }

    internal void StopPresentation()
    {
        presentationVersion++;
        StopAllCoroutines();
        isMoving = false;
        requestedDirection = 0;
        lastRequestFrame = -10;
        RestorePreviewImages();
    }

    private void OnDisable()
    {
        StopPresentation();
    }

    internal void RefreshSelection()
    {
        StopPresentation();
        selectedIndex = groupController.SelectedIndex;
        RefreshImmediate();
    }

    internal bool AnimateSelection(int itemIndex, int direction, Action onCompleted)
    {
        if (!isActiveAndEnabled || !IsOpen || isMoving || !EnsureReferences() || itemIndex < 0 || itemIndex >= ItemCount)
        {
            return false;
        }

        StopAllCoroutines();
        StartCoroutine(UsesPreviewImages
            ? MovePreviewRoutine(direction, itemIndex, onCompleted)
            : MoveRoutine(direction, itemIndex, onCompleted));
        return true;
    }

    public void MoveRight()
    {
        Move(1);
    }


    /// <summary>
    /// 이동 방향을 저장하고 가능한 경우 이동을 시작함
    /// </summary>
    private void Move(int direction)
    {
        if (groupController != null)
        {
            groupController.RequestMove(direction);
            return;
        }

        if (!isActiveAndEnabled || !IsOpen || ItemCount <= 1 || !EnsureReferences()) return;
        

        requestedDirection = direction;
        lastRequestFrame = Time.frameCount;

        if (isMoving) return;
    

        // 대기 슬롯 페이드가 실행 중이면 종료함
        StopAllCoroutines();

        int targetIndex = WrapIndex(selectedIndex + direction);
        StartCoroutine(UsesPreviewImages
            ? MovePreviewRoutine(direction, targetIndex, null)
            : MoveRoutine(direction, targetIndex));
    }


    /// <summary>
    /// 두 슬롯의 이동, 확대, 페이드, 역할 교체를 처리함
    /// </summary>
    private IEnumerator MoveRoutine(int direction, int targetIndex, Action onCompleted = null)
    {
        isMoving = true;
        int version = ++presentationVersion;

        bufferView.SetSprite(GetSprite(targetIndex));
        ApplyView(bufferView, direction * slotSpacing);

        bufferGroup.alpha = 0f;

        float elapsedTime = 0f;
        float fadeStart = 1f - Mathf.Clamp01(fadePortion);
        float fadeEnd = Mathf.Max(0.01f, fadePortion);

        while (elapsedTime < moveDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;

            float progress = Mathf.Clamp01(elapsedTime / moveDuration);

            float moveProgress = moveCurve.Evaluate(progress);

            float currentX = Mathf.Lerp(0f, -direction * slotSpacing, moveProgress);

            float bufferX = Mathf.Lerp(direction * slotSpacing, 0f, moveProgress);

            ApplyView(currentView, currentX);
            ApplyView(bufferView, bufferX);

            bufferGroup.alpha = Mathf.Clamp01(progress / fadeEnd);

            if (progress > fadeStart)
            {
                currentGroup.alpha = 1f - Mathf.InverseLerp(fadeStart, 1f, progress);
            }

            yield return null;
        }

        selectedIndex = targetIndex;

        InventorySlotView oldView = currentView;
        currentView = bufferView;
        bufferView = oldView;

        CanvasGroup oldGroup = currentGroup;
        currentGroup = bufferGroup;
        bufferGroup = oldGroup;

        ApplyView(currentView, 0f);

        currentGroup.alpha = 1f;
        bufferGroup.alpha = 0f;

        isMoving = false;
        onCompleted?.Invoke();
        if (version != presentationVersion || !IsOpen || !isActiveAndEnabled)
        {
            yield break;
        }

        // 키가 계속 눌려 있으면 바로 다음 이동을 시작함
        if (groupController == null && moveDuration > 0f && Time.frameCount - lastRequestFrame <= 1)
        {
            StartCoroutine(MoveRoutine(requestedDirection, WrapIndex(selectedIndex + requestedDirection)));
            yield break;
        }

        int nextIndex = GetAdjacentIndex(selectedIndex, direction);

        bufferView.SetSprite(GetSprite(nextIndex));
        ApplyView(bufferView, direction * slotSpacing);

        if (restFadeDuration <= 0f)
        {
            bufferGroup.alpha = 1f;
            yield break;
        }

        elapsedTime = 0f;

        while (elapsedTime < restFadeDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;

            bufferGroup.alpha =Mathf.Clamp01(elapsedTime / restFadeDuration);

            yield return null;
        }

        bufferGroup.alpha = 1f;
    }


    /// <summary>
    /// 슬롯 위치와 중앙 거리 기반 크기를 적용함
    /// </summary>
    private void ApplyView(InventorySlotView view, float positionX)
    {
        view.SetPosition(new Vector2(positionX, 0f));

        float distance = Mathf.Clamp01(Mathf.Abs(positionX) / Mathf.Max(0.01f, slotSpacing));

        float centerWeight = Mathf.SmoothStep(1f, 0f, distance);

        view.SetScale(Mathf.Lerp(sideScale, centerScale, centerWeight));
    }


    /// <summary>
    /// 현재 선택 상태에 맞게 두 슬롯을 배치함
    /// </summary>
    private void RefreshImmediate()
    {
        if (!EnsureReferences()) return;
        if (UsesPreviewImages)
        {
            RestorePreviewImages();
            SetPreviewSprite(previousItemImage, GetAdjacentIndex(selectedIndex, -1));
            SetPreviewSprite(selectedItemImage, selectedIndex);
            SetPreviewSprite(nextItemImage, GetAdjacentIndex(selectedIndex, 1));
            return;
        }
        

        currentView.SetSprite(GetSprite(selectedIndex));

        bufferView.SetSprite(GetSprite(GetAdjacentIndex(selectedIndex, 1)));

        ApplyView(currentView, 0f);
        ApplyView(bufferView, slotSpacing);

        currentGroup.alpha = 1f;
        bufferGroup.alpha = 1f;
    }

    /// <summary>
    /// 슬롯의 CanvasGroup을 가져오거나 생성함
    /// </summary>
    private CanvasGroup GetCanvasGroup(InventorySlotView view)
    {
        CanvasGroup group = view.GetComponent<CanvasGroup>();

        if (group == null)
        {
            group = view.gameObject.AddComponent<CanvasGroup>();
        }

        return group;
    }


    // 배열 끝을 넘어가면 반대쪽으로 순환시킴
    private int WrapIndex(int index)
    {
        int count = ItemCount;

        return count > 0 ? (index % count + count) % count : -1;
    }

    private int GetAdjacentIndex(int index, int direction)
    {
        return groupController != null ? groupController.GetAdjacentIndex(index, direction) : WrapIndex(index + direction);
    }

    private Sprite GetSprite(int index)
    {
        if (index < 0 || index >= ItemCount)
        {
            return null;
        }

        return groupController != null ? groupController.GetItemSprite(index) : itemSprites[index];
    }

    private IEnumerator MovePreviewRoutine(int direction, int targetIndex, Action onCompleted)
    {
        RefreshImmediate();
        isMoving = true;
        int version = ++presentationVersion;
        int incoming = direction > 0 ? 2 : 0;
        int outgoing = direction > 0 ? 0 : 2;
        SetPreviewSprite(previewImages[incoming], targetIndex);
        float elapsed = 0f;

        while (elapsed < moveDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / moveDuration);
            float eased = Mathf.SmoothStep(0f, 1f, progress);
            for (int i = 0; i < previewImages.Length; i++)
            {
                int destination = i - direction;
                Vector3 position = destination < 0 ? previewPositions[0] + previewPositions[0] - previewPositions[1] :
                    destination >= previewImages.Length ? previewPositions[2] + previewPositions[2] - previewPositions[1] :
                    previewPositions[destination];
                Vector2 size = previewSizes[Mathf.Clamp(destination, 0, previewImages.Length - 1)];
                previewImages[i].rectTransform.localPosition = Vector3.Lerp(previewPositions[i], position, eased);
                previewImages[i].rectTransform.sizeDelta = Vector2.Lerp(previewSizes[i], size, eased);
            }

            Color color = previewColors[outgoing];
            color.a *= 1f - eased;
            previewImages[outgoing].color = color;
            yield return null;
        }

        selectedIndex = targetIndex;
        RefreshImmediate();
        isMoving = false;
        onCompleted?.Invoke();
        if (version != presentationVersion || !IsOpen || !isActiveAndEnabled)
        {
            yield break;
        }

        elapsed = 0f;
        while (elapsed < restFadeDuration)
        {
            Color color = previewColors[incoming];
            color.a *= elapsed / restFadeDuration;
            previewImages[incoming].color = color;
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        previewImages[incoming].color = previewColors[incoming];
    }

    private void RestorePreviewImages()
    {
        if (previewImages == null) return;
        for (int i = 0; i < previewImages.Length; i++)
        {
            if (previewImages[i] == null) continue;
            previewImages[i].rectTransform.localPosition = previewPositions[i];
            previewImages[i].rectTransform.sizeDelta = previewSizes[i];
            previewImages[i].color = previewColors[i];
        }
    }

    private void SetPreviewSprite(Image image, int index)
    {
        image.sprite = GetSprite(index);
        image.enabled = image.sprite != null;
    }
}
