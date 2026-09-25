using UnityEngine;
using UnityEngine.SceneManagement;

public class Door : MonoBehaviour
{
    [Header("Scene Setting")]
    public string sceneName;

    [Header("Trigger Setting")]
    bool useTagCheck = true;
    string playerTag = "Player";

    [Header("Spaawn Setting")]
    public string targetSpawnID;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("Scene name is not set for the door.");
            return;
        }

        if (useTagCheck)
        {
            if (collision.CompareTag(playerTag))
            {
                ChangeScene();
            }

        }
    }
    void ChangeScene()
    {
        SceneSpawnManager.nextSpawnID = targetSpawnID;
        SceneManager.LoadScene(sceneName);
    }
}
