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

    private Coroutine victoryDialogueRoutine; // 맵 진입 연출과 공용 UI 준비를 기다리는 자동 대화

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

        // 세션을 지우기 전에 이번 전투의 승리 대사만 확보하여 재진입 시 중복 출력을 막는다.
        int victoryDialogueId = BattleSession.VictoryDialogueId;
        nextSpawnID = null;
        BattleSession.Clear();
        if (victoryDialogueId > 0)
            victoryDialogueRoutine = StartCoroutine(ShowVictoryDialogue(victoryDialogueId));
        return true;
    }

    // 모든 Start와 맵 진입 연출이 끝나면 승리한 NPC의 대사를 한 번 자동 시작한다.
    private IEnumerator ShowVictoryDialogue(int dialogueId)
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

        if (dialogManager != null)
            dialogManager.StartDialog(dialogueId);
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
