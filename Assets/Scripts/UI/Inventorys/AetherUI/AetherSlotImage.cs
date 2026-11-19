using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 장착 슬롯 아이콘 표시와 클릭 상세 조회 및 슬롯 간 드래그 이동과 교환 처리
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public sealed class AetherSlotImage : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler, IPointerClickHandler
{
    // 장착 슬롯 표시 설정
    [Range(0, PlayerBattleData.EquipmentCapacity - 1)]
    [SerializeField] private int slotIndex;             // 표시할 장착 슬롯의 0부터 시작하는 번호
    [SerializeField] private AetherDetailsUI detailsUI;

    // 장착 데이터 및 UI 참조
    private PlayerBattleData playerData;                // 이 UI를 소유한 플레이어의 장착 데이터
    private Image iconImage;                            // 에테르 원본 아이콘 표시
    private Canvas canvas;                              // 드래그 아이콘을 표시할 최상위 캔버스

    // 드래그 표시 및 시작 항목 보관
    private Image dragImage;                            // 포인터를 따라가는 별도 아이콘
    private AetherData draggedAether;                   // 드래그 도중 장착 데이터 변경을 판별할 시작 항목


    private void Awake()
    {
        // 같은 오브젝트의 이미지와 부모 데이터 및 캔버스 참조 확보
        iconImage = GetComponent<Image>();
        playerData = GetComponentInParent<PlayerBattleData>();
        canvas = GetComponentInParent<Canvas>();

        if (canvas != null)
        {
            canvas = canvas.rootCanvas;
        }

        // 슬롯 드롭 입력 활성화 및 필수 플레이어 데이터 누락 시 표시와 동작 중단
        iconImage.raycastTarget = true;

        if (playerData == null)
        {
            Debug.LogError("에테르 슬롯의 부모에서 PlayerBattleData를 찾을 수 없습니다.", this);
            iconImage.enabled = false;
            enabled = false;
        }
    }


    private void OnEnable()
    {
        if (playerData == null)
        {
            return;
        }

        // 활성 상태에서 장착 변경 구독 및 현재 슬롯 표시 반영
        playerData.EquipmentChanged += Refresh;
        Refresh();
    }


    private void Start()
    {
        // 플레이어의 Awake 정규화 이후 최초 활성 슬롯 표시 갱신
        Refresh();
    }


    private void OnDisable()
    {
        // 비활성화 시 드래그 취소 및 장착 변경 구독 해제
        ClearDrag();

        if (playerData != null)
        {
            playerData.EquipmentChanged -= Refresh;
        }
    }


    private void OnDestroy()
    {
        // 캔버스 아래에 별도 생성한 드래그 이미지 제거
        if (dragImage != null)
        {
            Destroy(dragImage.gameObject);
        }
    }


    // 왼쪽 클릭한 장착 슬롯의 상세 정보 표시
    public void OnPointerClick(PointerEventData eventData)
    {
        // 드래그 입력과 상세 화면이 없는 슬롯의 클릭 제외
        if (!isActiveAndEnabled || eventData.button != PointerEventData.InputButton.Left ||
            eventData.dragging || !eventData.eligibleForClick || detailsUI == null)
        {
            return;
        }

        // 슬롯 번호를 상세 화면에 전달해 현재 장착 데이터 조회
        detailsUI.ShowSlot(slotIndex);
    }


    public void OnBeginDrag(PointerEventData eventData)
    {
        // 편집 불가 상태와 빈 아이콘 제외 및 전투 시작용 1번 슬롯의 드래그 출발 제한
        if (eventData.button != PointerEventData.InputButton.Left || canvas == null || playerData == null ||
            !playerData.CanEditEquipment || playerData.EquippedAetherCount < 2 || slotIndex == 0 ||
            iconImage.sprite == null)
        {
            return;
        }

        // 출발 슬롯 항목 보관 및 빈 데이터의 드래그 중단
        draggedAether = playerData.GetEquippedAether(slotIndex);

        if (draggedAether == null)
        {
            return;
        }

        // 최초 드래그에서 표시용 이미지 생성 후 재사용
        if (dragImage == null)
        {
            GameObject ghost = new GameObject("AetherSwapDrag", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            ghost.transform.SetParent(canvas.transform, false);
            dragImage = ghost.GetComponent<Image>();
            dragImage.raycastTarget = false;
            dragImage.preserveAspect = true;
        }

        // 원본 아이콘 크기와 모양을 복사하고 최상위 표시 위치 갱신
        dragImage.sprite = iconImage.sprite;
        dragImage.rectTransform.sizeDelta = iconImage.rectTransform.rect.size;
        dragImage.gameObject.SetActive(true);
        dragImage.transform.SetAsLastSibling();
        OnDrag(eventData);
    }


    public void OnDrag(PointerEventData eventData)
    {
        // 드래그 미시작 상태의 이동 입력 제외
        if (draggedAether == null || dragImage == null)
        {
            return;
        }

        // 편집 상태나 출발 항목 변경 시 진행 중 드래그 취소
        if (!playerData.CanEditEquipment || playerData.GetEquippedAether(slotIndex) != draggedAether)
        {
            ClearDrag();
            return;
        }

        // 캔버스 렌더 모드에 맞춰 포인터 좌표를 월드 위치로 변환
        Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

        if (RectTransformUtility.ScreenPointToWorldPointInRectangle((RectTransform)canvas.transform,
            eventData.position, camera, out Vector3 position))
        {
            dragImage.rectTransform.position = position;
        }
    }


    public void OnEndDrag(PointerEventData eventData)
    {
        // 드롭 여부와 무관하게 드래그 종료 상태 정리
        ClearDrag();
    }


    public void OnDrop(PointerEventData eventData)
    {
        // 왼쪽 버튼 드롭과 출발 오브젝트 존재 여부 확인
        if (playerData == null || eventData.pointerDrag == null ||
            eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        // 동일 플레이어의 유효한 출발 슬롯과 드래그 시작 항목 확인
        AetherSlotImage source = eventData.pointerDrag.GetComponent<AetherSlotImage>();

        if (source == null || source == this || source.playerData != playerData || source.draggedAether == null ||
            playerData.GetEquippedAether(source.slotIndex) != source.draggedAether)
        {
            return;
        }

        // 플레이어 데이터에 교환 요청 후 성공 여부와 무관하게 드래그 표시 정리
        playerData.TrySwapAethers(source.slotIndex, slotIndex);
        source.ClearDrag();
    }


    // 화면 비활성화와 드롭 취소에 사용한 드래그 표시 및 임시 참조 정리
    private void ClearDrag()
    {
        // 시작 항목 참조 해제 및 재사용할 드래그 이미지 숨김
        draggedAether = null;

        if (dragImage != null)
        {
            dragImage.gameObject.SetActive(false);
        }
    }


    // 아이콘 표시 갱신 및 빈 슬롯의 투명한 드롭 영역 유지
    private void Refresh()
    {
        if (playerData == null)
        {
            return;
        }

        // 장착 데이터의 아이콘을 슬롯 이미지에 전달하고 빈 슬롯은 투명 처리
        AetherData aether = playerData.GetEquippedAether(slotIndex);
        iconImage.sprite = aether != null ? aether.Icon : null;
        iconImage.color = iconImage.sprite != null ? Color.white : Color.clear;
        iconImage.preserveAspect = true;

        // 빈 슬롯도 GraphicRaycaster의 드롭 대상으로 유지
        iconImage.enabled = true;
    }
}
