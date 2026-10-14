using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class StartMenuController : MonoBehaviour
{
    [SerializeField] private Button startButton;
    [SerializeField] private Button endButton;

    private void OnEnable()
    {
        startButton.onClick.AddListener(StartNewGame);
        endButton.onClick.AddListener(QuitGame);
    }

    private void OnDisable()
    {
        startButton.onClick.RemoveListener(StartNewGame);
        endButton.onClick.RemoveListener(QuitGame);
    }

    public void StartNewGame()
    {
        SceneManager.LoadScene("PalletTown");
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
