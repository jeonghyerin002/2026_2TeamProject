using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Scripting.APIUpdating;

/// <summary>Scene 이동 후에도 유지되는 공통 게임 메뉴. 시작 화면에서는 입력을 받지 않는다.</summary>
[MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp")]
[DisallowMultipleComponent]
public sealed class GameUIController : MonoBehaviour
{
    // 게임 메뉴 Canvas가 Scene 재진입 시 중복 생성되지 않도록 소유자를 유지한다.
    private static GameUIController instance;

    // 시작 화면 이름을 입력 차단과 메뉴 복귀에서 함께 사용한다.
    private const string StartMenuSceneName = "StartMenu";

    // 현재 활성 Scene에서 게임 메뉴를 사용할 수 있는지 캐시한다.
    private bool canOpenMenu;

    // UI_Canvas 아래의 메뉴 패널. 입력 처리는 항상 활성인 Canvas에서 수행한다.
    [SerializeField] private GameObject gameUIPanel;

    // Inspector에서 등록하는 시작 화면 이동 버튼 오브젝트.
    [SerializeField] private GameObject exitButtonObject;

    // 메뉴 패널의 시작 화면 이동 버튼과 이 컴포넌트가 등록한 클릭 이벤트를 관리한다.
    private Button exitButton;

    // 버튼 연타로 같은 Scene을 중복 로딩하지 않도록 한다.
    private bool isReturningToStartMenu;

    // Play 모드 재실행 시 정적 참조를 초기화한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetInstance()
    {
        instance = null;
    }

    // 공통 메뉴를 준비하고 Canvas와 자식 UI를 Scene 이동 후에도 유지한다.
    private void Awake()
    {
        if (instance != null && instance != this)
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
            return;
        }

        if (gameUIPanel == null)
        {
            Debug.LogError("GameUIController: GameUIPanel을 연결하세요.", this);
            enabled = false;
            return;
        }

        gameUIPanel.SetActive(false);
        instance = this;
        DontDestroyOnLoad(gameObject);

        // 등록된 오브젝트의 Button을 초기화 시 한 번 가져와 캐시한다.
        if (exitButtonObject == null || !exitButtonObject.TryGetComponent(out exitButton))
        {
            Debug.LogError("GameUIController: Exit Button Object에 Button 컴포넌트가 있는 오브젝트를 연결하세요.", this);
        }
    }

    // Canvas가 활성화되어 있는 동안 시작 화면 이동 요청을 구독한다.
    private void OnEnable()
    {
        if (instance != this)
        {
            return;
        }

        SceneManager.activeSceneChanged += HandleActiveSceneChanged;
        RefreshMenuForScene(SceneManager.GetActiveScene());
        if (exitButton != null)
        {
            exitButton.onClick.AddListener(ReturnToStartMenu);
        }
    }

    // 비활성화 및 Scene 전환 시 이 컴포넌트가 등록한 이벤트만 정리한다.
    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
        if (exitButton != null)
        {
            exitButton.onClick.RemoveListener(ReturnToStartMenu);
        }
    }

    // 새 Scene에서는 메뉴를 닫고 시작 화면 여부를 다시 확인한다.
    private void HandleActiveSceneChanged(Scene previousScene, Scene nextScene)
    {
        RefreshMenuForScene(nextScene);
    }

    // 시작 화면에서 메뉴를 숨기고 게임 Scene에 재진입하면 입력을 허용한다.
    private void RefreshMenuForScene(Scene scene)
    {
        canOpenMenu = scene.name != StartMenuSceneName;
        isReturningToStartMenu = false;
        if (gameUIPanel != null)
        {
            gameUIPanel.SetActive(false);
        }
    }

    // 소유한 메뉴가 파괴될 때만 정적 참조를 정리한다.
    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    // 종료 버튼은 앱을 종료하는 대신 시작 화면 Scene을 연다.
    private void ReturnToStartMenu()
    {
        if (!canOpenMenu || isReturningToStartMenu)
        {
            return;
        }

        isReturningToStartMenu = true;
        // Scene 이동 후에도 유지되는 플레이어와 자식 카메라를 메뉴 복귀 시 정리한다.
        // 카메라는 전환까지 렌더링을 유지하고 리스너만 꺼 메뉴의 리스너와 겹치지 않게 한다.
        var player = PlayerController.Instance;
        if (player != null)
        {
            // 플레이어 자식 카메라의 리스너를 한 번 조회해 종료 직전에 비활성화한다.
            var listeners = player.GetComponentsInChildren<AudioListener>(true);
            foreach (var listener in listeners)
            {
                listener.enabled = false;
            }

        }

        // 시작 화면 카메라가 준비될 때까지 기존 플레이어의 카메라를 유지한다.
        var loadOperation = SceneManager.LoadSceneAsync(StartMenuSceneName);
        // 시작 화면의 카메라가 활성화된 뒤 기존 플레이어를 정리한다.
        loadOperation.completed += _ =>
        {
            if (player != null)
            {
                Destroy(player.gameObject);
            }
        };
    }

    // 키를 누른 프레임에만 상태를 반전해 길게 눌러도 한 번만 전환한다.
    private void Update()
    {
        // 키보드가 없는 환경에서는 입력을 처리하지 않는다.
        var keyboard = Keyboard.current;
        if (!canOpenMenu || isReturningToStartMenu || keyboard == null || gameUIPanel == null)
        {
            return;
        }

        if (keyboard.escapeKey.wasPressedThisFrame)
        {
            gameUIPanel.SetActive(!gameUIPanel.activeSelf);
        }
    }
}
