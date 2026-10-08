using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

/// <summary>NPC 대화와 전투 진입을 연결한다</summary>
[RequireComponent(typeof(DialogManager))]
public class NPCInteraction : MonoBehaviour
{
    [Header("NPC")]
    [SerializeField] private string npcId;
    [SerializeField] private NpcBattleData battleData;

    [Header("Dialogue")]
    [FormerlySerializedAs("startingDialogueId")]
    [SerializeField] private int beforeBattleDialogueId = 1;
    [SerializeField] private int afterVictoryDialogueId;

    [Header("Battle")]
    [SerializeField] private string battleSceneName = "Battle";
    [SerializeField] private AetherEquipmentUI equipmentUI;

    private DialogManager dialogManager;
    private bool isPlayerNearby;
    private bool isInteracting;
    private bool battlePending;

    // 필요한 컴포넌트를 캐시한다
    private void Awake()
    {
        dialogManager = GetComponent<DialogManager>();
    }

    // 대화 종료 이벤트를 구독한다
    private void OnEnable()
    {
        dialogManager.OnDialogFinshed += OnDialogCompleted;
    }

    // 대화 종료 이벤트 구독을 해제한다
    private void OnDisable()
    {
        if (dialogManager != null)
            dialogManager.OnDialogFinshed -= OnDialogCompleted;
    }

    // 플레이어가 가까이 있을 때 상호작용 입력을 확인한다
    private void Update()
    {
        if (isInteracting || dialogManager.IsOpen || !isPlayerNearby || Keyboard.current == null || (equipmentUI != null && equipmentUI.BlocksWorldInput))
            return;

        if (Keyboard.current.eKey.wasPressedThisFrame)
            StartInteraction();
    }

    // 승리 여부에 따라 전투 전 또는 승리 후 대화를 시작한다
    private void StartInteraction()
    {
        isInteracting = true;
        battlePending = !NpcBattleProgress.IsDefeated(npcId);

        int dialogId = battlePending ? beforeBattleDialogueId : afterVictoryDialogueId;

        if (dialogId > 0)
        {
            dialogManager.StartDialog(dialogId);
            return;
        }

        if (battlePending)
            TransitionToBattle();
        else
            isInteracting = false;
    }

    // 대화 종료 후 필요한 경우 전투를 시작한다
    private void OnDialogCompleted()
    {
        if (!isInteracting)
            return;

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
