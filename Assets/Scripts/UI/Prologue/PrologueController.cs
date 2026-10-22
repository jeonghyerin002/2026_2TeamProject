using System;
using System.Collections;
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
    [SerializeField, Min(0f)] private float imageFadeDuration = 2.5f; // 새 이미지가 완전히 겹쳐질 때까지의 실제 시간(초), 0이면 즉시 변경
    [SerializeField, Min(0f)] private float dialogFadeDuration = 0.3f; // 숨겨진 대화창이 나타나는 실제 시간(초), 0이면 즉시 표시
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
    private Coroutine imageFadeRoutine; // 현재 배경 전환 작업
    private Image transitionImage; // 기존 배경 위에 새 이미지를 겹쳐 표시하는 임시 UI

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
        if (imageFadeRoutine != null || activeDialogManager.HasCutsceneMessage(this))
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
            // 대화가 끝난 화면에서 암전한 뒤 다음 씬으로 이동
            SceneFadeIn.LoadScene(nextSceneName);
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
        if (!dialogManager.TryBeginCutscene(this, true))
        {
            Fail("대화 UI를 사용할 수 없습니다. DialogManager의 활성 상태, UI 참조와 다른 대화 실행 여부를 확인하세요.");
            return;
        }

        activeDialogManager = dialogManager;
        messageIndex = 0;
        isPlaying = true;
        ShowCurrentMessage();
    }

    // 배경 전환이 필요하면 문장 출력을 미루고 전환 중에는 대화 UI 예약만 유지
    private void ShowCurrentMessage()
    {
        // 이미지를 생략한 문장은 현재 배경을 그대로 유지
        var message = dialogueMessages[messageIndex];
        if (message.image != null && backgroundImage == null)
        {
            Fail("배경 Image가 사라졌습니다. 씬의 Background Image 연결을 확인하세요.");
            return;
        }
        if (message.image != null)
        {
            if (backgroundImage.sprite == message.image || imageFadeDuration <= 0f)
            {
                backgroundImage.sprite = message.image;
                backgroundImage.color = Color.white;
            }
            else
            {
                if (!activeDialogManager.HideCutscenePanel(this))
                {
                    Fail("이미지 전환 전에 대화창을 숨기지 못했습니다. 대화 UI 예약을 확인하세요.");
                    return;
                }
                imageFadeRoutine = StartCoroutine(FadeImage(message.image));
                return;
            }
        }
        ShowDialogueText();
    }

    // 전환 없이 표시하거나 전환 완료 후 현재 문장의 타이핑을 시작
    private void ShowDialogueText()
    {
        if (activeDialogManager == null ||
            !activeDialogManager.TryShowCutsceneMessage(this, dialogueMessages[messageIndex].text, dialogFadeDuration))
        {
            Fail("프롤로그 문장을 표시하지 못했습니다. 대화 UI의 연결을 확인하세요.");
        }
    }

    // 기존 배경을 유지한 채 새 이미지의 불투명도를 높여 자연스럽게 교체
    private IEnumerator FadeImage(Sprite image)
    {
        var overlay = new GameObject("Background Transition", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        overlay.transform.SetParent(backgroundImage.transform, false);
        // 배경에 자식 UI가 있다면 해당 UI보다 뒤에서 배경만 덮도록 배치
        overlay.transform.SetAsFirstSibling();
        var rect = (RectTransform)overlay.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        transitionImage = overlay.GetComponent<Image>();
        transitionImage.sprite = image;
        transitionImage.material = backgroundImage.material;
        transitionImage.type = backgroundImage.type;
        transitionImage.preserveAspect = backgroundImage.preserveAspect;
        transitionImage.fillCenter = backgroundImage.fillCenter;
        transitionImage.fillMethod = backgroundImage.fillMethod;
        transitionImage.fillAmount = backgroundImage.fillAmount;
        transitionImage.fillClockwise = backgroundImage.fillClockwise;
        transitionImage.fillOrigin = backgroundImage.fillOrigin;
        transitionImage.pixelsPerUnitMultiplier = backgroundImage.pixelsPerUnitMultiplier;
        transitionImage.raycastTarget = false;
        transitionImage.color = new Color(1f, 1f, 1f, 0f);

        var duration = imageFadeDuration;
        var elapsed = 0f;
        while (elapsed < duration)
        {
            yield return null;
            if (backgroundImage == null || transitionImage == null)
            {
                Fail("배경 또는 전환용 Image가 사라졌습니다. 씬의 연결을 확인하세요.");
                yield break;
            }
            elapsed += Time.unscaledDeltaTime;
            transitionImage.color = new Color(1f, 1f, 1f, Mathf.Clamp01(elapsed / duration));
        }

        // 완전히 겹쳐진 뒤 원본을 교체하고 같은 프레임에 임시 이미지를 숨김
        backgroundImage.sprite = image;
        backgroundImage.color = Color.white;
        ClearTransitionImage();
        imageFadeRoutine = null;
        // 전환 중에는 문장을 등록하지 않아 진행 입력을 무시하고, 출력 프레임 입력도 DialogManager에서 차단
        ShowDialogueText();
    }

    // 전환 완료나 중단 시 임시 이미지가 화면과 계층에 남지 않도록 정리
    private void ClearTransitionImage()
    {
        if (transitionImage != null)
        {
            transitionImage.gameObject.SetActive(false);
            Destroy(transitionImage.gameObject);
            transitionImage = null;
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
        if (imageFadeRoutine != null)
        {
            StopCoroutine(imageFadeRoutine);
            imageFadeRoutine = null;
        }
        ClearTransitionImage();
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
