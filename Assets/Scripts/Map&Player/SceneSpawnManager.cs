using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>Scene 진입 시 플레이어를 지정된 위치에 배치한다</summary>
public class SceneSpawnManager : MonoBehaviour
{
    public static string nextSpawnID;

    [System.Serializable]
    public struct SpawnPoint
    {
        public string spawnID;
        public Transform spawnTransform;
    }

    [Header("Spawn Points List")]
    [SerializeField] private SpawnPoint[] spawnPoints;

    private Coroutine victoryDialogueRoutine; // 맵 진입 연출과 공용 UI 준비를 기다리는 승패 자동 대화

    // 전투 복귀 위치 또는 기존 Spawn ID 위치를 적용한다
    private void Start()
    {
        PlayerController player = PlayerController.Instance;

        if (player == null)
        {
            Debug.LogWarning("SceneSpawnManager: PlayerController가 없습니다.");
            return;
        }

        if (TryRestoreBattlePosition(player))
            return;

        ApplySpawnPoint(player);
    }

    // Battle에서 돌아온 경우 이전 맵 위치를 복원한다
    private bool TryRestoreBattlePosition(PlayerController player)
    {
        if (!BattleSession.IsActive ||
            SceneManager.GetActiveScene().name != BattleSession.ReturnScene)
            return false;

        player.transform.position = BattleSession.ReturnPosition;
        player.SetMovementEnabled(true);

        // 세션 초기화 전 이번 전투의 승패 대화 ID 확보 및 재진입 중복 출력 방지
        int victoryDialogueId = BattleSession.VictoryDialogueId;
        int defeatDialogueId = BattleSession.DefeatDialogueId;
        // 대사 미설정 승리와 패배를 구분하고 복귀 NPC 식별자 보관
        bool hasVictory = BattleSession.HasVictory;
        string npcId = BattleSession.NpcId;
        nextSpawnID = null;
        BattleSession.Clear();
        if (hasVictory)
        {
            victoryDialogueRoutine = StartCoroutine(ShowVictoryDialogue(npcId, victoryDialogueId));
        }
        else if (defeatDialogueId > 0)
        {
            // 패배 대화는 승리 보상 처리와 분리하여 한 번만 출력
            victoryDialogueRoutine = StartCoroutine(ShowDefeatDialogue(defeatDialogueId));
        }

        return true;
    }

    // 모든 Start와 맵 진입 연출이 끝나면 승리한 NPC의 대사를 한 번 자동 시작한다.
    private IEnumerator ShowVictoryDialogue(string npcId, int dialogueId)
    {
        yield return null;

        var dialogManager = FindFirstObjectByType<DialogManager>();
        if (dialogManager == null)
        {
            Debug.LogError("SceneSpawnManager: 승리 대사를 출력할 공용 DialogManager가 없습니다.", this);
            victoryDialogueRoutine = null;
            yield break;
        }

        while (SceneFadeIn.IsTransitioning || (dialogManager != null && !dialogManager.CanShowMessage))
            yield return null;

        // 복귀한 NPC에서 기존 승리 대사와 보상 지급을 연속 처리
        if (dialogManager != null && NPCInteraction.TryFindById(npcId, out var npc))
        {
            if (!npc.TryShowVictoryResult())
            {
                Debug.LogWarning("SceneSpawnManager: NPC 승리 대사와 보상 처리를 시작하지 못했습니다. NPC에게 다시 대화하세요.", this);
            }
        }
        else if (dialogManager != null && dialogueId > 0)
        {
            // NPC가 비활성 상태인 맵에서도 기존 승리 대사 출력 유지
            dialogManager.StartDialog(dialogueId);
        }

        victoryDialogueRoutine = null;
    }

    // 맵 연출과 공용 UI 준비 후 플레이어 패배 대화 시작
    private IEnumerator ShowDefeatDialogue(int dialogueId)
    {
        // 맵의 Start 초기화 완료 대기
        yield return null;

        var dialogManager = FindFirstObjectByType<DialogManager>();
        if (dialogManager == null)
        {
            Debug.LogError("SceneSpawnManager: 패배 대사를 출력할 공용 DialogManager가 없습니다.", this);
            victoryDialogueRoutine = null;
            yield break;
        }

        // 암전과 기존 대화 종료 후 예약한 대화 ID 전달
        while (SceneFadeIn.IsTransitioning || (dialogManager != null && !dialogManager.CanShowMessage))
        {
            yield return null;
        }

        if (dialogManager != null)
        {
            dialogManager.StartDialog(dialogueId);
        }

        victoryDialogueRoutine = null;
    }


    private void OnDisable()
    {
        if (victoryDialogueRoutine != null)
            StopCoroutine(victoryDialogueRoutine);
        victoryDialogueRoutine = null;
    }

    // 기존 Door 이동에서 전달받은 Spawn ID 위치를 적용한다
    private void ApplySpawnPoint(PlayerController player)
    {
        if (string.IsNullOrEmpty(nextSpawnID))
            return;

        string spawnId = nextSpawnID;
        nextSpawnID = null;

        foreach (SpawnPoint point in spawnPoints)
        {
            if (point.spawnID != spawnId || point.spawnTransform == null)
                continue;

            player.transform.position = point.spawnTransform.position;
            return;
        }
    }
}
