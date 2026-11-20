using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Scripting.APIUpdating;

/// <summary>플레이어 설정 메뉴와 시작 화면의 저장 및 불러오기 요청 처리</summary>
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

    // 플레이어 카테고리에서 사용하는 설정 및 불러오기 그룹
    [SerializeField] private UINavigationManager navigation;
    [SerializeField] private UIGroupData settingsGroup;
    [SerializeField] private UIGroupData loadGroup;

    // Domain Reload 없이 재실행할 때 기존 정적 참조를 초기화한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetInstance() => instance = null;

    // 프리팹으로 생성된 공통 메뉴를 소유하고 Scene 안의 중복 Canvas를 제거한다.
    private void Awake()
    {
        if (instance != null && instance != this)
        {
            // 플레이어 자식 메뉴를 우선하고 시작 화면용 독립 메뉴 정리
            if (GetComponentInParent<PlayerController>() != null && instance.GetComponentInParent<PlayerController>() == null)
            {
                instance.gameObject.SetActive(false);
                Destroy(instance.gameObject);
            }
            else
            {
                gameObject.SetActive(false);
                Destroy(gameObject);
                return;
            }
        }
        if (gameUIPanel == null || exitButtonObject == null ||
            !exitButtonObject.TryGetComponent(out exitButton) || saveButton == null || loadButton == null ||
            loadSlotButton == null || closeLoadButton == null || deleteSaveButton == null || loadPanel == null || loadSummary == null || saveMessage == null ||
            (GetComponentInParent<PlayerController>() != null && (navigation == null || settingsGroup == null || loadGroup == null)))
        {
            Debug.LogError("GameUIController: 공통 메뉴의 패널, 버튼과 텍스트를 연결하세요.", this);
            enabled = false;
            return;
        }
        instance = this;
        // 자식 메뉴는 플레이어의 영속 수명을 공유하고 독립 메뉴만 별도 유지
        if (transform.parent == null)
        {
            DontDestroyOnLoad(gameObject);
        }

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

    // 새 씬 진입 시 슬롯 미리보기와 설정 메뉴 초기화
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
        // 시작 화면 복귀로 플레이어 메뉴가 파괴된 경우 독립 메뉴 재생성
        if (instance == null)
        {
            var menu = Resources.Load<GameObject>("GameMenu");
            if (menu == null)
            {
                Debug.LogError("GameUIController: 시작 화면용 GameMenu 프리팹이 없습니다.");
                return;
            }

            Instantiate(menu);
        }

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
        // 플레이어 메뉴에서는 기존 그룹 이동 기록과 월드 입력 차단 재사용
        if (navigation != null)
        {
            if (!navigation.TryOpenGroup(loadGroup))
            {
                return;
            }
        }
        else
        {
            loadPanel.SetActive(true);
        }

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
        // 저장된 씬 식별자를 표시용 장소명으로 변환 후 세 줄 요약 전달
        var sceneDisplayName = GetSceneDisplayName(save.sceneName);
        loadSummary.text = $"저장 시각: {savedTime:yyyy-MM-dd HH:mm:ss}\n{sceneDisplayName}\n레벨: {save.level}";
    }


    // 저장 요약에 사용할 씬별 한글 장소명 조회
    private static string GetSceneDisplayName(string sceneName)
    {
        // 지정된 맵의 표시명 반환 및 미등록 씬의 기존 이름 보존
        return sceneName switch
        {
            "PalletTown" => "태초마을",
            "Route_1" => "1번 도로",
            "Shrine" => "신사",
            "RoadOfMudo" => "무도의 길",
            "BrownCity" => "갈색마을",
            "JinmuHouse" => "진무가문 저택",
            "M_1" => "무령산",
            "Level_Design" => "개발자맵",
            _ => sceneName
        };
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
    private void CloseLoadPanel()
    {
        // 시작 화면은 패널만 닫고 플레이어 메뉴는 이전 설정 그룹으로 복귀
        if (navigation != null && navigation.CurrentGroup == loadGroup)
        {
            navigation.Back();
        }
        else
        {
            loadPanel.SetActive(false);
        }
    }

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
        if (!canOpenMenu || isReturningToStartMenu || GameSaveSystem.IsBusy || SceneFadeIn.IsTransitioning)
        {
            return;
        }
        // 암전 후 오디오를 정리하고 로딩 완료 후 기존 플레이어를 제거
        var player = PlayerController.Instance;
        isReturningToStartMenu = SceneFadeIn.LoadScene(StartMenuSceneName, () =>
        {
            if (player != null)
            {
                foreach (var listener in player.GetComponentsInChildren<AudioListener>(true))
                {
                    listener.enabled = false;
                }
            }
        }, () =>
        {
            if (player != null)
            {
                Destroy(player.gameObject);
            }
            // 전투 메뉴에서 복귀한 경우에도 임시 전투 상태를 남기지 않는다.
            BattleSession.Clear();
        });
    }


    // 설정 그룹에서 선택한 기존 버튼 동작 실행
    public void InvokeSettingsItem(int index)
    {
        // 다른 그룹의 지연 확인 요청과 전환 중 중복 실행 차단
        if (navigation == null || navigation.CurrentGroup != settingsGroup || isReturningToStartMenu ||
            GameSaveSystem.IsBusy || SceneFadeIn.IsTransitioning)
        {
            return;
        }

        var button = index == 0 ? saveButton : index == 1 ? loadButton : index == 2 ? exitButton : null;
        if (button != null && button.IsInteractable())
        {
            button.onClick.Invoke();
        }
    }


    // 불러오기 그룹에서 선택한 슬롯과 삭제 및 닫기 버튼 실행
    public void InvokeLoadItem(int index)
    {
        // 빈 슬롯과 저장 처리 중 비활성 버튼 실행 차단
        if (navigation == null || navigation.CurrentGroup != loadGroup || GameSaveSystem.IsBusy || SceneFadeIn.IsTransitioning)
        {
            return;
        }

        var button = index == 0 ? loadSlotButton : index == 1 ? deleteSaveButton : index == 2 ? closeLoadButton : null;
        if (button != null && button.IsInteractable())
        {
            button.onClick.Invoke();
        }
    }


    // 설정 그룹의 키보드 선택 상태를 기존 버튼 강조 표시로 전달
    public void SelectSettingsItem(int index)
    {
        // 그룹의 선택 순서와 저장 및 불러오기 및 종료 버튼 대응
        var button = index == 0 ? saveButton : index == 1 ? loadButton : index == 2 ? exitButton : null;
        if (button != null)
        {
            button.Select();
        }
    }


    // 불러오기 그룹의 키보드 선택 상태를 기존 버튼 강조 표시로 전달
    public void SelectLoadItem(int index)
    {
        // 그룹의 선택 순서와 슬롯 및 삭제 및 닫기 버튼 대응
        var button = index == 0 ? loadSlotButton : index == 1 ? deleteSaveButton : index == 2 ? closeLoadButton : null;
        if (button != null)
        {
            button.Select();
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
