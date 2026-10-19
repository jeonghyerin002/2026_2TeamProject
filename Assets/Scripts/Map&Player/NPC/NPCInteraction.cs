using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

/// <summary>NPC 대화와 전투 진입을 연결한다</summary>
public class NPCInteraction : MonoBehaviour
{
    [Header("NPC")]
    [SerializeField] private string npcId;
    // 오브젝트 복제 시 원본과 사본의 저장 ID를 구분하는 에디터용 소유 정보.
    [SerializeField, HideInInspector] private string saveIdentityOwner;
    [SerializeField] private CharacterData battleData;

    [Header("Dialogue")]
    // 씬의 공용 대화 UI 서비스
    [SerializeField] private DialogManager dialogManager;
    [FormerlySerializedAs("startingDialogueId")]
    [SerializeField] private int beforeBattleDialogueId = 1;
    [SerializeField] private int afterVictoryDialogueId;

    [Header("Battle")]
    [SerializeField] private string battleSceneName = "Battle";
    [SerializeField] private AetherEquipmentUI equipmentUI;

    // 현재 요청을 수락한 매니저로 취소 대상을 고정
    private DialogManager activeDialogManager;
    private bool isPlayerNearby;
    private bool isInteracting;
    private bool battlePending;

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

    // 승리 여부에 따라 전투 전 또는 승리 후 대화를 시작한다
    private void StartInteraction()
    {
        if (!isActiveAndEnabled || dialogManager == null || !dialogManager.CanShowMessage)
        {
            return;
        }

        isInteracting = true;
        battlePending = !NpcBattleProgress.IsDefeated(npcId);

        int dialogId = battlePending ? beforeBattleDialogueId : afterVictoryDialogueId;

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

        if (battlePending)
            TransitionToBattle();
        else
            isInteracting = false;
    }

    // 자신의 대화가 정상 완료된 경우에만 전투 진입, 취소 시 상태 초기화
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

        isInteracting = false;
    }

    // 현재 NPC 전투와 복귀 정보를 등록하고 Battle Scene으로 이동한다
    private void TransitionToBattle()
    {
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

        if (!BattleSession.Begin(npcId, battleData, returnScene, player.transform.position))
        {
            Debug.LogError("NPCInteraction: NPC 전투 데이터 설정을 확인하세요.");
            CancelInteraction();
            return;
        }

        player.SetMovementEnabled(false);
        SceneManager.LoadScene(battleSceneName);
    }

    // 잘못된 전투 진입 상태를 초기화한다
    private void CancelInteraction()
    {
        isInteracting = false;
        battlePending = false;
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
