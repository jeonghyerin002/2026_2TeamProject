using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>태초마을에서 Esc 입력으로 게임 메뉴 패널을 열고 닫는다.</summary>
[DisallowMultipleComponent]
public sealed class PalletTownUIController : MonoBehaviour
{
    // UI_Canvas 아래의 메뉴 패널. 입력 처리는 항상 활성인 Canvas에서 수행한다.
    [SerializeField] private GameObject gameUIPanel;

    // Inspector에서 등록하는 시작 화면 이동 버튼 오브젝트.
    [SerializeField] private GameObject exitButtonObject;

    // 메뉴 패널의 시작 화면 이동 버튼과 이 컴포넌트가 등록한 클릭 이벤트를 관리한다.
    private Button exitButton;

    // 버튼 연타로 같은 Scene을 중복 로딩하지 않도록 한다.
    private bool isReturningToStartMenu;

    // 마을에 진입하면 메뉴를 닫은 상태로 시작한다.
    private void Awake()
    {
        if (gameUIPanel == null)
        {
            Debug.LogError("PalletTownUIController: GameUIPanel을 연결하세요.", this);
            enabled = false;
            return;
        }

        gameUIPanel.SetActive(false);

        // 등록된 오브젝트의 Button을 초기화 시 한 번 가져와 캐시한다.
        if (exitButtonObject == null || !exitButtonObject.TryGetComponent(out exitButton))
        {
            Debug.LogError("PalletTownUIController: Exit Button Object에 Button 컴포넌트가 있는 오브젝트를 연결하세요.", this);
        }
    }

    // Canvas가 활성화되어 있는 동안 시작 화면 이동 요청을 구독한다.
    private void OnEnable()
    {
        if (exitButton != null)
        {
            exitButton.onClick.AddListener(ReturnToStartMenu);
        }
    }

    // 비활성화 및 Scene 전환 시 이 컴포넌트가 등록한 이벤트만 정리한다.
    private void OnDisable()
    {
        if (exitButton != null)
        {
            exitButton.onClick.RemoveListener(ReturnToStartMenu);
        }
    }

    // 종료 버튼은 앱을 종료하는 대신 시작 화면 Scene을 연다.
    private void ReturnToStartMenu()
    {
        if (isReturningToStartMenu)
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
        var loadOperation = SceneManager.LoadSceneAsync("StartMenu");
        // Canvas는 Scene 전환 시 파괴되므로 로딩 완료 알림에서 기존 플레이어를 정리한다.
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
        if (keyboard == null || gameUIPanel == null)
        {
            return;
        }

        if (keyboard.escapeKey.wasPressedThisFrame)
        {
            gameUIPanel.SetActive(!gameUIPanel.activeSelf);
        }
    }
}
