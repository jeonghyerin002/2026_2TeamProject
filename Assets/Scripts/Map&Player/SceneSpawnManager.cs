using Unity.VisualScripting;
using UnityEngine;

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
    public SpawnPoint[] spawnPoints;

    void Start()
    {
        Debug.Log($"[SpawnManager] 전달받은 NextSpawnID: {nextSpawnID}");

        if (string.IsNullOrEmpty(nextSpawnID)) return;

        foreach(var point in spawnPoints)
        {
            if(point.spawnID == nextSpawnID)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if(player != null)
                {
                    player.transform.position = point.spawnTransform.position;
                }
                break;
            }
        }
    }
}
