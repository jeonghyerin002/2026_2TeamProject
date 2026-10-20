using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class StartMenuController : MonoBehaviour
{
    [SerializeField] private Button startButton;
    [SerializeField] private Button endButton;
    // 공통 슬롯 미리보기를 여는 시작 화면의 불러오기 버튼.
    [SerializeField] private Button loadButton;

    private void OnEnable()
    {
        startButton.onClick.AddListener(StartNewGame);
        endButton.onClick.AddListener(QuitGame);
        if (loadButton != null)
        {
            loadButton.onClick.AddListener(GameUIController.ShowSavedGame);
        }
    }

    private void OnDisable()
    {
        startButton.onClick.RemoveListener(StartNewGame);
        endButton.onClick.RemoveListener(QuitGame);
        if (loadButton != null)
        {
            loadButton.onClick.RemoveListener(GameUIController.ShowSavedGame);
        }
    }

    public void StartNewGame()
    {
        // 세이브 파일은 유지하고 이전 게임의 런타임 진행도만 지운다.
        SceneFadeIn.LoadScene("Prologue", GameSaveSystem.PrepareNewGame);
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
