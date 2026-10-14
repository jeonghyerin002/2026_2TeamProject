using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Scripting.APIUpdating;

/// <summary>모든 Scene에서 공통 저장/불러오기 UI를 제공하고 시작 화면에서는 Esc 메뉴를 숨긴다.</summary>
[MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp")]
[DisallowMultipleComponent]
public sealed class GameUIController : MonoBehaviour
{
    // 공통 Canvas 소유자와 시작 화면 이름.
    private static GameUIController instance;
    private const string StartMenuSceneName = "StartMenu";
    // 기존 Scene의 연결 필드는 그대로 유지한다.
    [SerializeField] private GameObject gameUIPanel;
    [SerializeField] private GameObject exitButtonObject;
    // 직접 등록하는 저장/불러오기 버튼과 공통 슬롯 패널.
    [SerializeField] private Button saveButton;
    [SerializeField] private Button loadButton;
    [SerializeField] private Button loadSlotButton;
    [SerializeField] private Button closeLoadButton;
    // 슬롯 데이터와 백업을 함께 삭제하는 버튼.
    [SerializeField] private Button deleteSaveButton;
    [SerializeField] private GameObject loadPanel;
    [SerializeField] private TMP_Text loadSummary;
    [SerializeField] private TMP_Text saveMessage;
    // 런타임 캐시와 Scene 전환 상태.
    private Button exitButton;
    private bool canOpenMenu;
    private bool isReturningToStartMenu;

    // Domain Reload 없이 재실행할 때 기존 정적 참조를 초기화한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetInstance() => instance = null;

    // 프리팹으로 생성된 공통 메뉴를 소유하고 Scene 안의 중복 Canvas를 제거한다.
    private void Awake()
    {
        if (instance != null && instance != this)
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
            return;
        }
        if (gameUIPanel == null || exitButtonObject == null ||
            !exitButtonObject.TryGetComponent(out exitButton) || saveButton == null || loadButton == null ||
            loadSlotButton == null || closeLoadButton == null || deleteSaveButton == null || loadPanel == null || loadSummary == null || saveMessage == null)
        {
            Debug.LogError("GameUIController: 공통 메뉴의 패널, 버튼과 텍스트를 연결하세요.", this);
            enabled = false;
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
        RefreshMenuForScene(SceneManager.GetActiveScene());
    }

    // 이 컴포넌트가 소유한 이벤트만 등록한다.
    private void OnEnable()
    {
        if (instance != this)
        {
            return;
        }
        SceneManager.activeSceneChanged += HandleActiveSceneChanged;
        exitButton.onClick.AddListener(ReturnToStartMenu);
        saveButton.onClick.AddListener(SaveGame);
        loadButton.onClick.AddListener(OpenLoadPanel);
        loadSlotButton.onClick.AddListener(LoadSelectedSave);
        closeLoadButton.onClick.AddListener(CloseLoadPanel);
        deleteSaveButton.onClick.AddListener(DeleteSelectedSave);
    }

    // 종료와 Scene 재진입 시 중복 이벤트를 방지한다.
    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
        if (instance != this)
        {
            return;
        }
        exitButton.onClick.RemoveListener(ReturnToStartMenu);
        saveButton.onClick.RemoveListener(SaveGame);
        loadButton.onClick.RemoveListener(OpenLoadPanel);
        loadSlotButton.onClick.RemoveListener(LoadSelectedSave);
        closeLoadButton.onClick.RemoveListener(CloseLoadPanel);
        deleteSaveButton.onClick.RemoveListener(DeleteSelectedSave);
    }

    // 새 Scene에 들어갈 때 슬롯 미리보기와 Esc 메뉴를 닫는다.
    private void HandleActiveSceneChanged(Scene previousScene, Scene nextScene) => RefreshMenuForScene(nextScene);
    private void RefreshMenuForScene(Scene scene)
    {
        canOpenMenu = scene.name != StartMenuSceneName;
        isReturningToStartMenu = false;
        gameUIPanel.SetActive(false);
        loadPanel.SetActive(false);
        saveMessage.text = "";
    }

    // 시작 화면의 불러오기 버튼도 같은 슬롯 미리보기를 연다.
    public static void ShowSavedGame()
    {
        if (instance != null)
        {
            instance.OpenLoadPanel();
        }
    }

    // 저장 성공/실패를 콘솔 대신 메뉴 텍스트에 표시한다.
    private void SaveGame()
    {
        GameSaveSystem.TrySave(out var message);
        saveMessage.text = message;
    }

    // 파일이 없거나 손상되었으면 내용을 안내하고 슬롯 클릭을 비활성화한다.
    private void OpenLoadPanel()
    {
        loadPanel.SetActive(true);
        loadPanel.transform.SetAsLastSibling();
        RefreshSaveSlot();
    }

    // 삭제 직후에도 빈 슬롯 안내와 버튼 상태를 같은 규칙으로 갱신한다.
    private void RefreshSaveSlot()
    {
        loadSlotButton.interactable = GameSaveSystem.TryRead(out var save, out var message);
        deleteSaveButton.interactable = !GameSaveSystem.IsBusy && GameSaveSystem.HasSaveFiles;
        if (save == null)
        {
            loadSummary.text = message;
            return;
        }
        // 파일은 UTC로 보존하고 사용자에게는 운영체제의 현지 시각으로 표시한다.
        var savedTime = DateTimeOffset.Parse(save.savedAtUtc).ToLocalTime();
        loadSummary.text = $"저장 시각: {savedTime:yyyy-MM-dd HH:mm:ss}\nScene: {save.sceneName}\n레벨: {save.level}";
    }

    // 클릭 직전에 파일을 다시 검증하므로 미리보기 이후 파일 변경도 안전하게 처리한다.
    private void LoadSelectedSave()
    {
        if (GameSaveSystem.TryLoad(out var message))
        {
            loadSlotButton.interactable = false;
            deleteSaveButton.interactable = false;
        }
        loadSummary.text = message;
    }

    // 패널을 닫으면 기존 메뉴 또는 시작 화면으로 돌아간다.
    private void CloseLoadPanel() => loadPanel.SetActive(false);

    // 삭제 결과를 표시하고 파일이 없는 슬롯은 불러오기와 삭제 모두 비활성화한다.
    private void DeleteSelectedSave()
    {
        var deleted = GameSaveSystem.TryDeleteSave(out var message);
        RefreshSaveSlot();
        saveMessage.text = message;
        if (!deleted)
        {
            loadSummary.text = message;
        }
    }

    // 시작 화면 카메라가 준비된 뒤 기존 플레이어를 정리해 카메라 없는 프레임을 방지한다.
    private void ReturnToStartMenu()
    {
        if (!canOpenMenu || isReturningToStartMenu || GameSaveSystem.IsBusy)
        {
            return;
        }
        isReturningToStartMenu = true;
        var player = PlayerController.Instance;
        if (player != null)
        {
            foreach (var listener in player.GetComponentsInChildren<AudioListener>(true))
            {
                listener.enabled = false;
            }
        }
        var operation = SceneManager.LoadSceneAsync(StartMenuSceneName);
        operation.completed += _ =>
        {
            if (player != null)
            {
                Destroy(player.gameObject);
            }
            // 전투 메뉴에서 복귀한 경우에도 임시 전투 상태를 남기지 않는다.
            BattleSession.Clear();
        };
    }

    // 키를 누른 프레임마다 한 번만 토글한다. 시작 화면에서는 Esc 메뉴를 사용하지 않는다.
    private void Update()
    {
        var keyboard = Keyboard.current;
        if (!canOpenMenu || isReturningToStartMenu || GameSaveSystem.IsBusy || keyboard == null)
        {
            return;
        }
        if (keyboard.escapeKey.wasPressedThisFrame)
        {
            if (loadPanel.activeSelf)
            {
                CloseLoadPanel();
                return;
            }
            gameUIPanel.SetActive(!gameUIPanel.activeSelf);
        }
    }

    // 현재 소유자가 파괴될 때만 정적 참조를 비운다.
    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }
}
