using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

/// <summary>NPC 대화 완료 후 선택한 전투, 대화 종료 또는 아이템 지급 실행</summary>
public class NPCInteraction : MonoBehaviour
{
    // 기존 NPC의 전투 동작을 기본값 0으로 보존하는 대화 후 동작
    public enum InteractionType
    {
        [InspectorName("대화 후 전투")] Battle = 0,
        [InspectorName("대화만")] DialogueOnly = 1,
        [InspectorName("대화 후 아이템 지급")] GiveItem = 2
    }

    [Header("NPC")]
    [SerializeField] private string npcId;
    // 오브젝트 복제 시 원본과 사본의 저장 ID를 구분하는 에디터용 소유 정보.
    [SerializeField, HideInInspector] private string saveIdentityOwner;
    [SerializeField] private CharacterData battleData;

    [Header("Interaction")]
    // 이번 NPC가 대화 완료 후 실행할 동작, 기존 에셋은 전투로 유지
    [SerializeField, InspectorName("대화 후 동작")]
    private InteractionType interactionType = InteractionType.Battle;
    // 기존 보상 필드를 첫 번째 아이템으로 유지, 종류별 지급 수량은 1개
    [SerializeField, Min(0), InspectorName("보상 ItemId")]
    [Tooltip("아이템 지급 모드에서 사용합니다. 기존 카탈로그의 무기·에테르 ID를 입력하세요. 이미 보유한 아이템은 중복 지급하지 않습니다.")]
    private int rewardItemId;
    // 첫 번째 보상 뒤에 입력한 순서대로 지급할 추가 무기·에테르 ID
    [SerializeField]
    [Tooltip("첫 번째 Reward Item Id 다음에 지급할 아이템 ID 목록입니다. 같은 ID는 한 번만 처리합니다.")]
    private int[] additionalRewardItemIds = System.Array.Empty<int>();

    [Header("Dialogue")]
    // 씬의 공용 대화 UI 서비스
    [SerializeField] private DialogManager dialogManager;
    // 대화 전용 모드의 시작 ID, 0이면 이전 시작 ID를 사용하여 기존 NPC 설정 보존
    [SerializeField, Min(0)]
    [Tooltip("대화만 모드의 대화 ID입니다. 0이면 기존 시작 대화 ID를 사용합니다.")]
    private int dialogueOnlyId;
    [FormerlySerializedAs("startingDialogueId")]
    [InspectorName("시작 대화 ID")]
    [SerializeField] private int beforeBattleDialogueId = 1;
    [Tooltip("전투 모드에서 해당 NPC에게 승리한 뒤 사용할 대화 ID입니다.")]
    [SerializeField] private int afterVictoryDialogueId;
    // 아이템 모드의 수령 전 대사, 0이면 기존 시작 대화 ID를 사용하여 설정 보존
    [SerializeField, Min(0)]
    [Tooltip("아이템을 받기 전의 대화 ID입니다. 0이면 기존 Before Battle Dialogue Id를 사용합니다.")]
    private int beforeRewardDialogueId;
    // 모든 보상 아이템을 보유한 상태에서 다시 상호작용할 때의 대사, 0이면 대화 생략
    [SerializeField, Min(0)]
    [Tooltip("첫 번째와 추가 보상 아이템을 모두 보유한 상태에서 사용할 대화 ID입니다. 0이면 대화하지 않습니다.")]
    private int afterRewardDialogueId;

    [Header("Battle")]
    [SerializeField] private string battleSceneName = "Battle";
    [SerializeField] private AetherEquipmentUI equipmentUI;

    // 현재 요청을 수락한 매니저로 취소 대상을 고정
    private DialogManager activeDialogManager;
    private bool isPlayerNearby;
    private bool isInteracting;
    private bool battlePending;
    // 이번 대화를 정상 완료한 뒤 아이템 지급을 시도할지 나타내는 상태
    private bool rewardPending;
    // 대화 도중 Inspector 설정이 바뀌어도 시작 시 선택한 동작으로 완료
    private InteractionType pendingInteractionType;

#if UNITY_EDITOR
    // 신규 NPC는 별도 ID 입력 없이 시작하며 기존 NPC의 ID는 보존한다.
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(npcId))
        {
            npcId = System.Guid.NewGuid().ToString("N");
        }
    }
#endif

    // 활성화마다 Inspector에 연결된 공용 매니저 검증
    private void OnEnable()
    {
        if (dialogManager == null)
        {
            Debug.LogError("NPCInteraction: 씬의 공용 DialogManager를 연결하세요.", this);
            enabled = false;
        }
    }

    // 활성 수명이 끝나면 자신이 시작한 대화만 취소
    private void OnDisable()
    {
        if (activeDialogManager != null)
        {
            activeDialogManager.CancelDialog(this);
        }
        activeDialogManager = null;
        CancelInteraction();
    }

    // 플레이어가 가까이 있을 때 상호작용 입력을 확인한다
    private void Update()
    {
        if (isInteracting || dialogManager == null || !dialogManager.CanShowMessage || !isPlayerNearby || Keyboard.current == null || (equipmentUI != null && equipmentUI.BlocksWorldInput))
            return;

        if (Keyboard.current.eKey.wasPressedThisFrame)
            StartInteraction();
    }

    // 동작 모드를 캡처하고 전투 승리 또는 보상 보유 여부에 따라 대화 선택
    private void StartInteraction()
    {
        if (!isActiveAndEnabled || dialogManager == null || !dialogManager.CanShowMessage)
        {
            return;
        }

        isInteracting = true;
        pendingInteractionType = interactionType;
        battlePending = pendingInteractionType == InteractionType.Battle && !NpcBattleProgress.IsDefeated(npcId);
        rewardPending = pendingInteractionType == InteractionType.GiveItem && !HasRewardItem();

        // 일반 대화와 전투의 기존 시작 ID는 유지하고 아이템 모드만 별도 ID로 분기
        int dialogId = pendingInteractionType == InteractionType.Battle && !battlePending
            ? afterVictoryDialogueId : beforeBattleDialogueId;
        if (pendingInteractionType == InteractionType.DialogueOnly)
        {
            dialogId = dialogueOnlyId > 0 ? dialogueOnlyId : beforeBattleDialogueId;
        }
        else if (pendingInteractionType == InteractionType.GiveItem)
        {
            dialogId = rewardPending
                ? (beforeRewardDialogueId > 0 ? beforeRewardDialogueId : beforeBattleDialogueId)
                : afterRewardDialogueId;
        }

        if (dialogId > 0)
        {
            activeDialogManager = dialogManager;
            if (!activeDialogManager.TryStartDialog(dialogId, this, OnDialogCompleted))
            {
                activeDialogManager = null;
                CancelInteraction();
                Debug.LogWarning($"NPCInteraction: 대화 {dialogId}을 시작하지 못했습니다. 공용 매니저의 DB와 대화 ID를 확인하세요.", this);
            }
            return;
        }

        // 보상은 유효한 대화를 정상 완료한 경우에만 지급하며 ID 누락 시 생략
        if (rewardPending)
        {
            Debug.LogWarning("NPCInteraction: 아이템 지급에는 양수의 시작 대화 ID가 필요합니다.", this);
            CancelInteraction();
            return;
        }

        if (battlePending)
        {
            TransitionToBattle();
        }
        else
        {
            CancelInteraction();
        }
    }

    // 자신의 대화 정상 완료 시에만 캡처한 동작 실행, 취소 시 보상과 전투 생략
    private void OnDialogCompleted(bool completed)
    {
        activeDialogManager = null;
        if (!completed || !isActiveAndEnabled || !isInteracting)
        {
            CancelInteraction();
            return;
        }

        if (battlePending)
        {
            TransitionToBattle();
            return;
        }

        // 인벤토리 변경 알림의 재진입 전에 이전 상호작용 상태 해제
        var shouldGiveItem = pendingInteractionType == InteractionType.GiveItem && rewardPending;
        CancelInteraction();
        if (shouldGiveItem)
        {
            GiveItemReward();
        }
    }

    // 첫 번째 보상부터 추가 목록까지 같은 ID는 한 번만 처리하는 순서 제공
    private IEnumerable<int> GetRewardItemIds()
    {
        // 인벤토리의 중복 보유 제한에 맞춰 목록의 첫 등장 순서 유지
        var seenIds = new HashSet<int> { rewardItemId };
        yield return rewardItemId;
        if (additionalRewardItemIds == null)
        {
            yield break;
        }
        foreach (var itemId in additionalRewardItemIds)
        {
            if (seenIds.Add(itemId))
            {
                yield return itemId;
            }
        }
    }

    // 저장·복원되는 인벤토리에서 모든 보상을 보유해야 수령 후 대화로 분기
    private bool HasRewardItem()
    {
        // 플레이어나 카탈로그가 준비되지 않으면 수령 전 상태를 유지하여 지급 시 재검증
        var player = PlayerController.Instance;
        var catalog = Resources.Load<GameSaveCatalog>("GameSaveCatalog");
        if (player == null || catalog == null ||
            !player.TryGetComponent<PlayerBattleData>(out var playerData))
        {
            return false;
        }

        // 유효하지 않거나 아직 받지 못한 보상이 하나라도 있으면 수령 전 상태 유지
        foreach (var itemId in GetRewardItemIds())
        {
            if (itemId <= 0)
            {
                return false;
            }
            var aether = catalog.Aether(itemId);
            var weapon = catalog.Weapon(itemId);
            if (aether != null && weapon == null)
            {
                if (!playerData.OwnsAether(aether))
                {
                    return false;
                }
            }
            else if (weapon == null || aether != null || !playerData.OwnsWeapon(weapon))
            {
                return false;
            }
        }
        return true;
    }

    // 첫 보상과 추가 목록을 순서대로 처리하고 결과 안내를 한 묶음으로 표시
    private void GiveItemReward()
    {
        // 플레이어가 사라지거나 전투 중이면 지급을 보류하고 다음 대화에서 재시도
        var player = PlayerController.Instance;
        if (player == null || !player.TryGetComponent<PlayerBattleData>(out var playerData) || !playerData.CanEditEquipment)
        {
            ShowRewardMessage("아이템을 지급할 수 없습니다. 플레이어 상태를 확인하세요.");
            return;
        }

        // 저장 시스템과 동일한 원본 카탈로그를 사용하여 빌드에서도 ID 해석
        var catalog = Resources.Load<GameSaveCatalog>("GameSaveCatalog");
        if (catalog == null)
        {
            Debug.LogWarning("NPCInteraction: GameSaveCatalog와 양수의 보상 ItemId를 확인하세요.", this);
            ShowRewardMessage("보상 아이템 설정을 확인하세요.");
            return;
        }

        // 지급 실패 항목도 원래 입력 위치에서 안내하며 다른 유효한 보상은 계속 처리
        var messages = new List<string>();
        foreach (var itemId in GetRewardItemIds())
        {
            messages.Add(TryGiveRewardItem(playerData, catalog, itemId));
        }
        if (dialogManager == null || !dialogManager.TryShowMessages(messages))
        {
            foreach (var message in messages)
            {
                Debug.LogWarning($"NPCInteraction: {message}", this);
            }
        }
    }

    // 단일 ID의 지급 결과 문장을 반환하며 기존 중복·보관함 제한 유지
    private string TryGiveRewardItem(PlayerBattleData playerData, GameSaveCatalog catalog, int itemId)
    {
        // 종류별 ID가 겹치거나 등록되지 않은 값은 지급하지 않고 해당 항목만 실패 처리
        var aether = itemId > 0 ? catalog.Aether(itemId) : null;
        var weapon = itemId > 0 ? catalog.Weapon(itemId) : null;
        if ((aether == null && weapon == null) || (aether != null && weapon != null))
        {
            Debug.LogWarning($"NPCInteraction: ItemId {itemId}에 해당하는 무기 또는 에테르가 정확히 하나 필요합니다.", this);
            return $"ItemId {itemId}의 보상 설정을 확인하세요.";
        }
        if (aether != null)
        {
            if (playerData.OwnsAether(aether))
            {
                return $"{aether.AetherName}은/는 이미 보유한 에테르입니다.";
            }
            return playerData.TryAddAether(aether)
                ? $"{aether.AetherName} (을/를) 획득했습니다." : $"{aether.AetherName}을/를 받을 공간이 없습니다. 보관함을 확인하세요.";
        }

        if (playerData.OwnsWeapon(weapon))
        {
            return $"{weapon.WeaponName}은/는 이미 보유한 무기입니다.";
        }
        return playerData.TryAddWeapon(weapon)
            ? $"{weapon.WeaponName} (을/를) 획득했습니다." : $"현재 {weapon.WeaponName}을/를 받을 수 없습니다.";
    }

    // 대화가 해제된 공용 UI에 보상 결과 안내, UI 사용 불가 시 컨텍스트 로그로 대체
    private void ShowRewardMessage(string message)
    {
        if (dialogManager == null || !dialogManager.TryShowMessage(message))
        {
            Debug.LogWarning($"NPCInteraction: {message}", this);
        }
    }

    // 현재 NPC 전투와 복귀 정보를 등록하고 Battle Scene으로 이동한다
    private void TransitionToBattle()
    {
        // 다른 전환 중에는 전투 세션과 플레이어 상태를 덮어쓰지 않음
        if (SceneFadeIn.IsTransitioning)
        {
            CancelInteraction();
            return;
        }
        PlayerController player = PlayerController.Instance;

        if (player == null)
        {
            Debug.LogError("NPCInteraction: PlayerController가 없습니다.");
            CancelInteraction();
            return;
        }

        PlayerBattleData playerData = player.GetComponent<PlayerBattleData>();
        if (playerData == null || !playerData.IsValid)
        {
            Debug.LogWarning("전투를 시작하려면 에테르를 하나 이상 장착하세요.");
            if (equipmentUI != null)
            {
                equipmentUI.SetOpen(true);
                equipmentUI.ShowMessage("전투를 시작하려면 에테르를 하나 이상 장착하세요.");
            }
            CancelInteraction();
            return;
        }

        string returnScene = SceneManager.GetActiveScene().name;

        if (!BattleSession.Begin(npcId, battleData, returnScene, player.transform.position, afterVictoryDialogueId))
        {
            Debug.LogError("NPCInteraction: NPC 전투 데이터 설정을 확인하세요.");
            CancelInteraction();
            return;
        }

        player.SetMovementEnabled(false);
        if (!SceneFadeIn.LoadScene(battleSceneName))
        {
            BattleSession.Clear();
            player.SetMovementEnabled(true);
            CancelInteraction();
        }
    }

    // 잘못된 전투 진입 상태를 초기화한다
    private void CancelInteraction()
    {
        isInteracting = false;
        battlePending = false;
        rewardPending = false;
    }

    // 플레이어가 NPC 상호작용 범위에 들어왔는지 기록한다
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
            isPlayerNearby = true;
    }

    // 플레이어가 NPC 상호작용 범위에서 나갔는지 기록한다
    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
            isPlayerNearby = false;
    }
}
