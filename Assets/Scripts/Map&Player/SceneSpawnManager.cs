using UnityEngine;
using UnityEngine.SceneManagement;

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

        nextSpawnID = null;
        BattleSession.Clear();
        return true;
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