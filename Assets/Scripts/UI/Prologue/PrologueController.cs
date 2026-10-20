using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>씬 진입 시 컷신 대사를 자동 재생하고 기존 대화 UI의 진행 입력을 재사용</summary>
[DisallowMultipleComponent]
public sealed class PrologueController : MonoBehaviour, ISerializationCallbackReceiver
{
    // 한 문장의 대사와 표시할 배경 이미지 설정
    [Serializable]
    public sealed class Message
    {
        // 해당 순서에서 출력할 대사
        [TextArea(2, 6)] public string text;
        // 비워 두면 직전 배경 이미지를 유지
        public Sprite image;
    }

    // 이 씬에서만 사용하는 대화 UI와 출력 순서대로 편집할 문장
    [SerializeField] private DialogManager dialogManager;
    [SerializeField, InspectorName("Messages")] private Message[] dialogueMessages;
    // 각 문장의 Image를 표시할 씬의 UI Image 컴포넌트
    [SerializeField] private Image backgroundImage;
    // 기존 string 배열의 씬 데이터를 읽기 위한 호환 필드
    [SerializeField, HideInInspector] private string[] messages;
    // 비워 두면 씬을 유지하고 완료 이벤트만 실행
    [SerializeField] private string nextSceneName;
    [SerializeField] private UnityEvent onCompleted = new UnityEvent();

    // 시작 이후 재활성화 여부와 현재 예약의 진행 상태
    private bool hasStarted;
    private bool isPlaying;
    private bool hasCompleted;
    private int messageIndex;
    // 인스펙터 참조가 바뀌어도 실제 예약했던 발행자를 해제하기 위한 참조
    private DialogManager activeDialogManager;

    // 저장 직전에는 데이터를 변경하지 않고 기본 직렬화 유지
    void ISerializationCallbackReceiver.OnBeforeSerialize()
    {
    }

    // 기존 대사를 순서대로 이전하며 직렬화 스레드에서는 Unity API를 호출하지 않음
    void ISerializationCallbackReceiver.OnAfterDeserialize()
    {
        if (messages == null || messages.Length == 0)
        {
            return;
        }
        // 새 형식이 이미 존재하면 기존 호환 데이터로 덮어쓰지 않음
        if (dialogueMessages != null && dialogueMessages.Length > 0)
        {
            messages = Array.Empty<string>();
            return;
        }

        dialogueMessages = new Message[messages.Length];
        for (var index = 0; index < messages.Length; index++)
        {
            dialogueMessages[index] = new Message { text = messages[index] };
        }
        // 사용자가 새 목록을 비워도 옛 대사가 다시 생성되지 않도록 이전 완료 처리
        messages = Array.Empty<string>();
    }

    // 다른 씬 오브젝트의 Awake와 OnEnable 이후 자동 시작
    private void Start()
    {
        hasStarted = true;
        BeginDialogue();
    }

    // 중단 후 재활성화하면 처음부터 재생하며 완료된 대화는 재실행하지 않음
    private void OnEnable()
    {
        if (hasStarted && !hasCompleted)
        {
            BeginDialogue();
        }
    }

    // 클릭과 스페이스바 처리는 DialogManager에 맡기고 문장 종료만 관찰
    private void Update()
    {
        if (!isPlaying)
        {
            return;
        }
        if (activeDialogManager == null || !activeDialogManager.IsCutsceneOwner(this))
        {
            Fail("대화 도중 DialogManager 또는 UI 예약이 해제되었습니다. 씬의 연결을 확인하세요.");
            return;
        }
        if (activeDialogManager.HasCutsceneMessage(this))
        {
            return;
        }

        messageIndex++;
        if (messageIndex < dialogueMessages.Length)
        {
            ShowCurrentMessage();
            return;
        }

        hasCompleted = true;
        ReleaseDialogue();
        onCompleted.Invoke();
        if (this != null && isActiveAndEnabled && !string.IsNullOrWhiteSpace(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
        }
    }

    // 필수 참조와 전체 대사를 검증한 뒤 이 컴포넌트 소유로 UI 예약
    private void BeginDialogue()
    {
        if (dialogManager == null || dialogueMessages == null || dialogueMessages.Length == 0)
        {
            Fail("Dialog Manager와 한 개 이상의 Messages를 연결하세요.");
            return;
        }
        // 빈 문장 때문에 재생 도중 UI가 잠기지 않도록 사전 확인
        foreach (var message in dialogueMessages)
        {
            if (message == null || string.IsNullOrWhiteSpace(message.text))
            {
                Fail("Messages에 빈 문장이 있습니다. 모든 문장을 입력하세요.");
                return;
            }
            if (message.image != null && backgroundImage == null)
            {
                Fail("이미지가 있는 대사를 재생하려면 Background Image에 씬의 UI Image를 연결하세요.");
                return;
            }
        }
        if (!string.IsNullOrWhiteSpace(nextSceneName) && !Application.CanStreamedLevelBeLoaded(nextSceneName))
        {
            Fail("Next Scene Name을 Build Profiles의 Scene List에 등록하거나 비워 두세요.");
            return;
        }
        if (!dialogManager.TryBeginCutscene(this))
        {
            Fail("대화 UI를 사용할 수 없습니다. DialogManager의 활성 상태, UI 참조와 다른 대화 실행 여부를 확인하세요.");
            return;
        }

        activeDialogManager = dialogManager;
        messageIndex = 0;
        isPlaying = true;
        ShowCurrentMessage();
    }

    // 예약한 UI에 현재 문장을 표시하고 실패 시 소유 예약만 해제
    private void ShowCurrentMessage()
    {
        // 이미지를 생략한 문장은 현재 배경을 그대로 유지
        var message = dialogueMessages[messageIndex];
        if (message.image != null && backgroundImage == null)
        {
            Fail("배경 Image가 사라졌습니다. 씬의 Background Image 연결을 확인하세요.");
            return;
        }
        if (!activeDialogManager.TryShowCutsceneMessage(this, message.text))
        {
            Fail("프롤로그 문장을 표시하지 못했습니다. 대화 UI의 연결을 확인하세요.");
            return;
        }
        if (message.image != null)
        {
            backgroundImage.sprite = message.image;
            // 검은 화면용 Image의 기존 색상 때문에 그림이 어둡게 가려지는 현상 방지
            backgroundImage.color = Color.white;
        }
    }

    // 씬 이탈과 비활성화 시 타이핑을 포함한 자신 소유의 예약 정리
    private void OnDisable()
    {
        ReleaseDialogue();
    }

    // 다른 NPC나 컷신 소유의 대화에는 영향을 주지 않는 정리
    private void ReleaseDialogue()
    {
        isPlaying = false;
        if (activeDialogManager != null)
        {
            activeDialogManager.EndCutscene(this);
        }
        activeDialogManager = null;
    }

    // 잘못된 구성은 한 번 알리고 외부 진행을 중지
    private void Fail(string message)
    {
        Debug.LogError("PrologueController: " + message, this);
        ReleaseDialogue();
        enabled = false;
    }
}
