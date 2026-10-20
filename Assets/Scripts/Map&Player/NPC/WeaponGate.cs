using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>무기 장착 전 통행을 막고 첫 안내와 재대화를 공용 UI에 표시</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class WeaponGate : MonoBehaviour
{
    // 경비의 접근 감지 트리거와 분리된 실제 길막 콜라이더
    [SerializeField] private Collider2D passageBlocker;
    // 씬에서 공유하는 대화 서비스와 경비 안내 ID
    [SerializeField] private DialogManager dialogManager;
    [SerializeField, Min(1)] private int firstDialogueId;
    [SerializeField, Min(1)] private int repeatDialogueId;
    // 무기 장착 조건을 충족한 뒤 표시할 통행 허가 대화 ID
    [SerializeField, Min(1)] private int equippedDialogueId;
    // 장비 UI가 입력을 사용하는 동안 경비가 대화를 시작하지 않도록 하는 선택 참조
    [SerializeField] private AetherEquipmentUI equipmentUI;
    // 실제로 이벤트를 구독한 플레이어 데이터와 현재 대화를 수락한 서비스
    private PlayerBattleData subscribedData;
    private DialogManager activeDialogManager;
    // 여러 플레이어 콜라이더가 접근 범위를 공유할 때 마지막 이탈까지 범위 유지
    private readonly HashSet<Collider2D> nearbyColliders = new HashSet<Collider2D>();
    // 첫 안내 완료 상태와 실제 길막 접촉에서 발생한 통행 시도
    private bool hasWarnedPlayer;
    private bool pendingPassageAttempt;
    private bool wasTouchingBlocker;
    private bool hasPassagePosition;
    private float previousPlayerFrontY;
    private bool isInteracting;
    // 진행 중인 대사가 시작될 때의 장착 조건, 조건이 달라지면 해당 대화 취소
    private bool activeDialogueHasWeapon;

    // 필수 참조가 누락되면 길을 닫아 두고 구성 오류 안내
    private void OnEnable()
    {
        var sensor = GetComponent<BoxCollider2D>();
        if (sensor == null || !sensor.isTrigger || passageBlocker == null ||
            passageBlocker == sensor || passageBlocker.isTrigger || dialogManager == null ||
            firstDialogueId <= 0 || repeatDialogueId <= 0 || equippedDialogueId <= 0)
        {
            Debug.LogError("WeaponGate: 접근용 Trigger, 별도 길막 Collider, 공용 DialogManager와 안내 ID를 연결하세요.", this);
            enabled = false;
            return;
        }
        passageBlocker.enabled = true;
    }

    // 씬의 플레이어 Awake 이후 구독과 초기 장착 상태 동기화
    private void Start()
    {
        RefreshPlayerBinding();
    }

    // 늦게 생성·교체된 플레이어는 매 프레임 바인딩 시도로 감지
    private void Update()
    {
        RefreshPlayerBinding();
        nearbyColliders.RemoveWhere(collider => collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy);
        if (nearbyColliders.Count == 0)
        {
            pendingPassageAttempt = false;
            wasTouchingBlocker = false;
            hasPassagePosition = false;
            return;
        }
        var isTouchingBlocker = false;
        var playerFrontY = float.NegativeInfinity;
        foreach (var collider in nearbyColliders)
        {
            playerFrontY = Mathf.Max(playerFrontY, collider.bounds.max.y);
            if (passageBlocker != null && passageBlocker.enabled && collider.IsTouching(passageBlocker))
            {
                isTouchingBlocker = true;
            }
        }
        var passageY = passageBlocker != null ? passageBlocker.transform.TransformPoint(passageBlocker.offset).y : 0f;
        var isCrossingPassage = hasPassagePosition && previousPlayerFrontY < passageY && playerFrontY >= passageY;
        previousPlayerFrontY = playerFrontY;
        hasPassagePosition = true;
        // 닫힌 길막에 닿거나 열린 통로를 마을에서 도로 방향으로 넘을 때만 자동 안내
        if (isCrossingPassage || (isTouchingBlocker && !wasTouchingBlocker))
        {
            pendingPassageAttempt = true;
        }
        else if (!isTouchingBlocker && playerFrontY < passageY)
        {
            pendingPassageAttempt = false;
        }
        wasTouchingBlocker = isTouchingBlocker;
        if (Time.timeScale == 0f || subscribedData == null ||
            (equipmentUI != null && equipmentUI.BlocksWorldInput))
        {
            return;
        }
        if (pendingPassageAttempt || (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame))
        {
            TryStartBlockedDialogue();
        }
    }

    // 활성 수명이 끝나면 실제 구독 대상과 자신의 대화만 정리하고 통행 차단 유지
    private void OnDisable()
    {
        Unsubscribe();
        if (activeDialogManager != null)
        {
            activeDialogManager.CancelDialog(this);
        }
        activeDialogManager = null;
        isInteracting = false;
        pendingPassageAttempt = false;
        wasTouchingBlocker = false;
        hasPassagePosition = false;
        nearbyColliders.Clear();
        if (passageBlocker != null)
        {
            passageBlocker.enabled = true;
        }
    }

    // 기존 출구 호출을 유지하며 통행 시도·E 입력 시 현재 장착 조건에 맞는 대화 요청
    public bool TryStartBlockedDialogue()
    {
        if (!isActiveAndEnabled || isInteracting || Time.timeScale == 0f || dialogManager == null ||
            !dialogManager.CanShowMessage || (equipmentUI != null && equipmentUI.BlocksWorldInput))
        {
            return false;
        }
        RefreshPlayerBinding();
        if (subscribedData == null)
        {
            return false;
        }

        // 시작 실패나 취소는 첫 안내 완료로 기록하지 않음
        // 장착 상태에서는 첫 안내와 반복 경고 대신 통행 허가 대화만 선택
        activeDialogueHasWeapon = subscribedData.Weapon != null;
        var dialogueId = activeDialogueHasWeapon ? equippedDialogueId :
            (hasWarnedPlayer ? repeatDialogueId : firstDialogueId);
        isInteracting = true;
        activeDialogManager = dialogManager;
        if (!activeDialogManager.TryStartDialog(dialogueId, this, HandleDialogueEnded))
        {
            activeDialogManager = null;
            isInteracting = false;
            return false;
        }
        pendingPassageAttempt = false;
        return true;
    }

    // 플레이어 데이터의 교체·제거를 감지하여 구독 후 현재 장착 상태 동기화
    private void RefreshPlayerBinding()
    {
        var player = PlayerController.Instance;
        var data = player != null ? player.GetComponent<PlayerBattleData>() : null;
        if (ReferenceEquals(subscribedData, data))
        {
            return;
        }
        Unsubscribe();
        subscribedData = data;
        if (subscribedData != null)
        {
            subscribedData.EquipmentChanged += HandleEquipmentChanged;
        }
        HandleEquipmentChanged();
    }

    // field-like managed 이벤트만 해제하여 파괴된 Unity 래퍼도 안전하게 정리
    private void Unsubscribe()
    {
        if (!ReferenceEquals(subscribedData, null))
        {
            subscribedData.EquipmentChanged -= HandleEquipmentChanged;
        }
        subscribedData = null;
    }

    // 실제 장착 여부로 통로를 갱신하고 현재 조건과 맞지 않는 진행 중 대화 취소
    private void HandleEquipmentChanged()
    {
        var hasEquippedWeapon = subscribedData != null && subscribedData.Weapon != null;
        if (passageBlocker != null)
        {
            passageBlocker.enabled = !hasEquippedWeapon;
        }
        if (hasEquippedWeapon)
        {
            pendingPassageAttempt = false;
            wasTouchingBlocker = false;
            hasPassagePosition = false;
        }
        if (activeDialogManager != null && activeDialogueHasWeapon != hasEquippedWeapon)
        {
            activeDialogManager.CancelDialog(this);
        }
    }

    // 정상 완료된 안내만 재대화 상태로 전환
    private void HandleDialogueEnded(bool completed)
    {
        activeDialogManager = null;
        isInteracting = false;
        if (completed && !activeDialogueHasWeapon)
        {
            hasWarnedPlayer = true;
        }
    }

    // 접근 범위는 직접 E 대화와 통행 경계 감지에만 사용
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isActiveAndEnabled)
        {
            return;
        }
        var player = other.GetComponentInParent<PlayerController>();
        if (player == null || player != PlayerController.Instance)
        {
            return;
        }
        nearbyColliders.Add(other);
    }

    // 범위 안에서 컴포넌트를 다시 켜도 다음 물리 스텝에 접근 상태 복구
    private void OnTriggerStay2D(Collider2D other)
    {
        OnTriggerEnter2D(other);
    }

    // 마지막 플레이어 콜라이더가 이탈하면 자동 안내 예약 제거
    private void OnTriggerExit2D(Collider2D other)
    {
        nearbyColliders.Remove(other);
        if (nearbyColliders.Count == 0)
        {
            pendingPassageAttempt = false;
            wasTouchingBlocker = false;
            hasPassagePosition = false;
        }
    }
}
