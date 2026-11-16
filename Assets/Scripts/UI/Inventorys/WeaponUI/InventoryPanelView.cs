using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// 프리팹에 저장된 카테고리 인벤토리의 슬롯 표시와 버튼 요청 전달
/// </summary>
[DisallowMultipleComponent]
public sealed class InventoryPanelView : MonoBehaviour
{
    // 프리팹의 카테고리와 스크롤 참조
    private const int SlotCount = 50;
    private const int CategoryCount = 2;
    [SerializeField] private ScrollRect scroll;
    [SerializeField] private Image[] categoryBackgrounds;
    [SerializeField] private Button[] categoryButtons;
    [SerializeField] private SlotView[] slots;

    // 카테고리 아이콘의 확대와 선택 밑줄
    [SerializeField] private RectTransform[] categoryIcons;
    [SerializeField] private Image[] categoryUnderlines;
    [SerializeField, Min(1f)] private float selectedIconScale = 1.2f;
    [SerializeField, Min(0.01f)] private float categoryTransitionDuration = 0.12f;
    private readonly Vector3[] normalIconScales = new Vector3[CategoryCount];
    private readonly Vector3[] transitionStartScales = new Vector3[CategoryCount];
    private int presentedCategory = -1;
    private Coroutine categoryRoutine;

    // 프리팹의 목록 및 선택 정보 참조
    [SerializeField] private TMP_Text categoryTitle;
    [SerializeField] private TMP_Text itemCount;
    [SerializeField] private TMP_Text actionText;
    [SerializeField] private Button equipButton;

    // 선택 슬롯 왼쪽에 표시할 아이템 상세창
    [SerializeField] private GameObject detailsPanel;
    [SerializeField] private TMP_Text itemName;
    [SerializeField] private TMP_Text itemDescription;
    [SerializeField] private TMP_Text itemStats;
    [SerializeField, Min(0f)] private float detailsGap = 12f;
    private RectTransform detailsRect;
    private Transform detailsParent;
    private RectTransform selectedSlot;
    private readonly Vector3[] slotCorners = new Vector3[4];

    // 빈 공간 클릭 판정에 재사용할 포인터와 UI 검색 결과
    private readonly List<RaycastResult> pointerHits = new List<RaycastResult>(16);
    private EventSystem pointerEventSystem;
    private PointerEventData pointerData;
    private Vector2 pointerPressPosition;
    private bool hasPointerPress;

    // Inspector에서 조정할 수 있는 흑백 선택 색상
    [SerializeField] private Color accent = Color.white;
    [SerializeField] private Color border = new Color32(90, 90, 90, 255);

    // 버튼 연결 상태와 컨트롤러에 전달할 요청
    private bool initialized;
    internal event Action<int> CategorySelected;
    internal event Action<int> SlotSelected;
    internal event Action DismissRequested;
    internal event Action EquipmentRequested;


    // 비활성 프리팹의 필수 참조 확인과 버튼 이벤트 초기 연결
    internal bool Initialize()
    {
        // 재진입 시 기존 버튼 연결 유지
        if (initialized)
        {
            return true;
        }

        if (scroll == null || scroll.content == null || scroll.viewport == null ||
            categoryBackgrounds == null || categoryBackgrounds.Length != CategoryCount ||
            categoryButtons == null || categoryButtons.Length != CategoryCount ||
            categoryIcons == null || categoryIcons.Length != CategoryCount ||
            categoryUnderlines == null || categoryUnderlines.Length != CategoryCount ||
            slots == null || slots.Length != SlotCount || categoryTitle == null || itemCount == null ||
            actionText == null || equipButton == null || detailsPanel == null || itemName == null ||
            itemDescription == null || itemStats == null)
        {
            Debug.LogError("InventoryPanelView: 프리팹의 탭, 50칸 슬롯과 UI 참조를 연결하세요", this);
            return false;
        }

        // 부분 초기화로 인한 중복 연결을 막기 위해 전체 참조를 먼저 검사
        for (int i = 0; i < CategoryCount; i++)
        {
            if (categoryBackgrounds[i] == null || categoryButtons[i] == null ||
                categoryIcons[i] == null || categoryUnderlines[i] == null)
            {
                Debug.LogError("InventoryPanelView: 카테고리 아이콘과 버튼을 연결하세요", this);
                return false;
            }
        }

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null || !slots[i].IsConfigured)
            {
                Debug.LogError($"InventoryPanelView: {i + 1}번 슬롯의 UI 참조를 연결하세요", this);
                return false;
            }
        }

        // 프리팹에 이미 배치된 버튼의 요청만 컨트롤러로 전달
        for (int i = 0; i < CategoryCount; i++)
        {
            normalIconScales[i] = categoryIcons[i].localScale;
            int index = i;
            categoryButtons[i].onClick.AddListener(() => CategorySelected?.Invoke(index));
        }

        for (int i = 0; i < slots.Length; i++)
        {
            int index = i;
            slots[i].Button.onClick.AddListener(() => SlotSelected?.Invoke(index));
        }

        // 프리팹 상세창의 위치 계산 참조 캐싱
        detailsRect = (RectTransform)detailsPanel.transform;
        detailsParent = detailsRect.parent;
        equipButton.onClick.AddListener(RequestEquipment);
        initialized = true;
        return true;
    }


    // 기존 데이터의 목록과 선택 상태를 프리팹 UI에 표시
    internal void Refresh(WeaponData[] weapons, WeaponData selected, WeaponData equipped, int categoryIndex, string statComparison)
    {
        // 초기화에 실패한 화면 갱신 차단
        if (!initialized)
        {
            return;
        }

        // 현재 탭의 제목과 보유 개수 갱신
        categoryTitle.text = categoryIndex == 0 ? "무기 아이템" : "회복 아이템";
        itemCount.text = $"{weapons.Length}개 / {SlotCount}칸";

        for (int i = 0; i < categoryBackgrounds.Length; i++)
        {
            // 사각형 배경 없이 기존 버튼의 클릭 영역 유지
            categoryBackgrounds[i].color = Color.clear;
        }

        RefreshCategorySelection(categoryIndex);
        selectedSlot = null;

        for (int i = 0; i < slots.Length; i++)
        {
            WeaponData item = i < weapons.Length ? weapons[i] : null;
            SlotView slot = slots[i];
            slot.Button.interactable = item != null;
            slot.Background.color = item != null && item == selected ? accent : border;
            slot.Icon.sprite = item != null ? item.Icon : null;
            slot.Icon.enabled = slot.Icon.sprite != null;
            slot.Name.text = item != null ? item.WeaponName : "";
            slot.Badge.text = item != null && equipped != null && equipped.ItemId == item.ItemId ? "장착" : "";

            if (item != null && item == selected)
            {
                selectedSlot = (RectTransform)slot.Button.transform;
            }
        }

        // 선택한 무기의 원본 이름과 설명을 슬롯 옆 상세창에 표시
        detailsPanel.SetActive(selectedSlot != null);
        itemName.text = selected != null ? selected.WeaponName : "";
        itemDescription.text = selected != null ?
            (string.IsNullOrWhiteSpace(selected.Description) ? "등록된 설명이 없습니다" : selected.Description) : "";

        // 아이템 설명 아래에 변경된 스텟이 있는 경우에만 표시
        itemStats.text = selected != null ? statComparison : "";
        itemStats.gameObject.SetActive(!string.IsNullOrEmpty(itemStats.text));

        // 선택한 무기가 있는 경우에만 장비 변경 버튼 표시
        equipButton.gameObject.SetActive(selected != null);
        actionText.text = selected != null && equipped != null && equipped.ItemId == selected.ItemId ? "해제" : "장착";
        UpdateDetailsPosition();
    }


    // 스크롤 이동 후 선택한 슬롯의 위치 추적
    private void LateUpdate()
    {
        // 열린 화면에서 선택된 슬롯이 있는 동안만 위치 계산
        if (initialized && selectedSlot != null)
        {
            ProcessBackgroundClick();
            UpdateDetailsPosition();
        }
    }


    // UI 클릭 처리 이후 빈 영역의 클릭을 선택 해제 요청으로 전달
    private void ProcessBackgroundClick()
    {
        // 마우스와 EventSystem이 없는 환경의 클릭 판정 생략
        Mouse mouse = Mouse.current;
        EventSystem eventSystem = EventSystem.current;

        if (mouse == null || eventSystem == null)
        {
            return;
        }

        Vector2 position = mouse.position.ReadValue();

        if (mouse.leftButton.wasPressedThisFrame)
        {
            pointerPressPosition = position;
            hasPointerPress = true;
        }

        if (!mouse.leftButton.wasReleasedThisFrame || !hasPointerPress)
        {
            return;
        }

        hasPointerPress = false;

        // 스크롤 드래그의 종료를 빈 공간 클릭과 구분
        if (Vector2.Distance(position, pointerPressPosition) > eventSystem.pixelDragThreshold)
        {
            return;
        }

        if (pointerEventSystem != eventSystem)
        {
            pointerEventSystem = eventSystem;
            pointerData = new PointerEventData(eventSystem);
        }

        // 실제 최상단 UI를 확인해 설명창과 유효한 버튼 클릭 보호
        pointerData.Reset();
        pointerData.position = position;
        pointerHits.Clear();
        eventSystem.RaycastAll(pointerData, pointerHits);

        if (pointerHits.Count > 0)
        {
            Transform hit = pointerHits[0].gameObject.transform;

            if (hit.IsChildOf(detailsRect) ||
                (scroll.verticalScrollbar != null && hit.IsChildOf(scroll.verticalScrollbar.transform)))
            {
                return;
            }

            for (int i = 0; i < CategoryCount; i++)
            {
                if (hit.IsChildOf(categoryButtons[i].transform))
                {
                    return;
                }
            }

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].Button.interactable && hit.IsChildOf(slots[i].Button.transform))
                {
                    return;
                }
            }
        }

        // 빈 슬롯과 패널 여백 및 전체 화면 배경에서 선택 해제
        DismissRequested?.Invoke();
    }


    // 선택 슬롯의 왼쪽 위에 상세창의 오른쪽 위 배치
    private void UpdateDetailsPosition()
    {
        // 선택 해제 시 상세창 숨김
        if (selectedSlot == null)
        {
            detailsPanel.SetActive(false);
            return;
        }

        // 스크롤 영역을 벗어난 슬롯의 설명은 숨기고 재진입 시 복원
        selectedSlot.GetWorldCorners(slotCorners);
        RectTransform viewport = scroll.viewport;
        Vector3 bottom = viewport.InverseTransformPoint(slotCorners[0]);
        Vector3 top = viewport.InverseTransformPoint(slotCorners[2]);
        Rect bounds = viewport.rect;
        bool isVisible = top.y > bounds.yMin && bottom.y < bounds.yMax &&
            top.x > bounds.xMin && bottom.x < bounds.xMax;
        detailsPanel.SetActive(isVisible);

        if (!isVisible)
        {
            return;
        }

        // 같은 부모 좌표로 변환해 열과 행에 관계없이 슬롯 왼쪽에 배치
        Vector3 position = detailsParent.InverseTransformPoint(slotCorners[1]);
        position.x -= detailsGap;
        position.z = detailsRect.localPosition.z;
        detailsRect.localPosition = position;
    }


    // 선택 카테고리 변경 시 밑줄과 확대 전환 시작
    private void RefreshCategorySelection(int categoryIndex)
    {
        // 아이템 정보만 갱신된 경우 진행 중인 카테고리 연출 유지
        if (categoryIndex == presentedCategory)
        {
            return;
        }

        StopCategoryTransition();
        presentedCategory = categoryIndex;

        for (int i = 0; i < CategoryCount; i++)
        {
            transitionStartScales[i] = categoryIcons[i].localScale;
            categoryUnderlines[i].gameObject.SetActive(i == presentedCategory);
        }

        // 닫힌 프리팹의 초기화에는 코루틴 없이 최종 상태 적용
        if (!isActiveAndEnabled)
        {
            ApplyCategoryScales(1f);
            return;
        }

        categoryRoutine = StartCoroutine(AnimateCategorySelection());
    }


    // 현재 크기에서 선택 크기까지 실시간 기준 보간
    private IEnumerator AnimateCategorySelection()
    {
        // 빠른 재전환도 현재 크기에서 연결
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, categoryTransitionDuration);

        while (elapsed < duration)
        {
            yield return null;
            elapsed += Time.unscaledDeltaTime;
            ApplyCategoryScales(Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration)));
        }

        ApplyCategoryScales(1f);
        categoryRoutine = null;
    }


    // 선택 아이콘과 비선택 아이콘의 크기 적용
    private void ApplyCategoryScales(float progress)
    {
        // 프리팹의 기본 크기를 기준으로 선택 아이콘만 확대
        for (int i = 0; i < CategoryCount; i++)
        {
            Vector3 target = normalIconScales[i] * (i == presentedCategory ? selectedIconScale : 1f);
            categoryIcons[i].localScale = Vector3.LerpUnclamped(transitionStartScales[i], target, progress);
        }
    }


    // 진행 중인 카테고리 전환 취소
    private void StopCategoryTransition()
    {
        // 중복 코루틴과 닫힌 화면의 위치 갱신 방지
        if (categoryRoutine != null)
        {
            StopCoroutine(categoryRoutine);
            categoryRoutine = null;
        }
    }


    // 패널 닫기 및 씬 전환 시 선택 연출 정리
    private void OnDisable()
    {
        // 다음 열기에서 중간 크기가 남지 않도록 선택 상태 복원
        hasPointerPress = false;
        StopCategoryTransition();

        if (initialized && presentedCategory >= 0)
        {
            ApplyCategoryScales(1f);
        }
    }


    // 카테고리 전환 시 첫 줄로 스크롤 복원
    internal void ResetScroll()
    {
        // 이전 탭의 관성 제거 후 상단 위치 적용
        scroll.StopMovement();
        scroll.verticalNormalizedPosition = 1f;
    }


    // 선택 무기의 장비 변경 요청 전달
    private void RequestEquipment()
    {
        // 실제 장착과 해제 판단은 기존 컨트롤러에서 처리
        EquipmentRequested?.Invoke();
    }


    /// <summary>
    /// Inspector에 저장할 한 슬롯의 이미지와 텍스트 및 버튼 연결
    /// </summary>
    [Serializable]
    private sealed class SlotView
    {
        // 프리팹 슬롯 표시 요소
        [SerializeField] private Image background;
        [SerializeField] private Button button;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text name;
        [SerializeField] private TMP_Text badge;

        // 외부에서 교체하지 않는 UI 참조
        public Image Background => background;
        public Button Button => button;
        public Image Icon => icon;
        public TMP_Text Name => name;
        public TMP_Text Badge => badge;
        public bool IsConfigured => background != null && button != null && icon != null && name != null && badge != null;
    }
}
