using System.Collections;
using UnityEngine;

/// <summary>
/// 두 슬롯을 재사용해 인벤토리 순환 이동과 중앙 확대를 관리함
/// 아직 확장성 없음
/// </summary>
public class InventoryCarouselUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject inventoryPanel;
    [SerializeField] private InventorySlotView[] slotViews;

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

    private bool startOpen;

    private InventorySlotView currentView;
    private InventorySlotView bufferView;

    private CanvasGroup currentGroup;
    private CanvasGroup bufferGroup;

    private int selectedIndex;
    private int requestedDirection;
    private int lastRequestFrame = -10;

    private bool isMoving;

    public bool IsOpen => inventoryPanel != null && inventoryPanel.activeSelf;


    private void Awake()
    {
        currentView = slotViews[0];
        bufferView = slotViews[1];

        currentGroup = GetCanvasGroup(currentView);
        bufferGroup = GetCanvasGroup(bufferView);

        inventoryPanel.SetActive(startOpen);

        RefreshImmediate();
    }


    public void ToggleOpen()
    {
        SetOpen(!IsOpen);
    }


    //인벤토리 UI를 열거나 닫음
    public void SetOpen(bool isOpen)
    {
        if (inventoryPanel == null)
        {
            return;
        }

        StopAllCoroutines();

        isMoving = false;
        requestedDirection = 0;
        lastRequestFrame = -10;

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

    public void MoveRight()
    {
        Move(1);
    }


    /// <summary>
    /// 이동 방향을 저장하고 가능한 경우 이동을 시작함
    /// </summary>
    private void Move(int direction)
    {
        if (!IsOpen || itemSprites == null || itemSprites.Length <= 1) return;
        

        requestedDirection = direction;
        lastRequestFrame = Time.frameCount;

        if (isMoving) return;
    

        // 대기 슬롯 페이드가 실행 중이면 종료함
        StopAllCoroutines();

        StartCoroutine(MoveRoutine(direction));
    }


    /// <summary>
    /// 두 슬롯의 이동, 확대, 페이드, 역할 교체를 처리함
    /// </summary>
    private IEnumerator MoveRoutine(int direction)
    {
        isMoving = true;

        int targetIndex = WrapIndex(selectedIndex + direction);

        bufferView.SetSprite(itemSprites[targetIndex]);
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

        // 키가 계속 눌려 있으면 바로 다음 이동을 시작함
        if (Time.frameCount - lastRequestFrame <= 1)
        {
            StartCoroutine(MoveRoutine(requestedDirection));
            yield break;
        }

        int nextIndex = WrapIndex(selectedIndex + direction);

        bufferView.SetSprite(itemSprites[nextIndex]);
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

        float distance = Mathf.Clamp01(Mathf.Abs(positionX) / slotSpacing);

        float centerWeight = Mathf.SmoothStep(1f, 0f, distance);

        view.SetScale(Mathf.Lerp(sideScale, centerScale, centerWeight));
    }


    /// <summary>
    /// 현재 선택 상태에 맞게 두 슬롯을 배치함
    /// </summary>
    private void RefreshImmediate()
    {
        if (currentView == null || 
            bufferView == null || 
            itemSprites == null || 
            itemSprites.Length == 0) return;
        

        currentView.SetSprite(itemSprites[selectedIndex]);

        bufferView.SetSprite(itemSprites[WrapIndex(selectedIndex + 1)]);

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
        int count = itemSprites.Length;

        return (index % count + count) % count;
    }

}