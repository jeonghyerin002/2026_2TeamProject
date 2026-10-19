using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class DialogManager : MonoBehaviour
{
    public event Action OnDialogFinshed;

    [Header("Dialog Database")]
    [SerializeField] DialogDatabaseSO dialogDatabase;

    [Header("UI Rederences")]
    [SerializeField] GameObject dialogPanel;
    [SerializeField] TextMeshProUGUI dialogText;
    //[SerializeField] TextMeshProUGUI dialogCharacterNameText;

    [Header("Settings")]
    [SerializeField] float typeSpeed = 0.05f;

    bool isAction = false;
    bool isTyping = false;
    DialogSO currentDialog;
    string currentMessage;
    Coroutine typingCoroutine;
    private UnityEngine.Object cutsceneOwner;
    private bool hasCutsceneMessage;
    private int messageShownFrame;
    private int lastInputFrame = -1;
    // 현재 대화를 예약한 소유자와 해당 요청에만 전달할 종료 콜백
    private UnityEngine.Object dialogOwner;
    private Action<bool> dialogCompletion;

    public bool IsOpen => isAction;
    public bool CanShowMessage => isActiveAndEnabled && !isAction && dialogPanel != null && dialogText != null;

    // 컷신 전체 동안 UI를 예약하여 이동 중에도 NPC 대화가 끼어들지 않게 한다.
    public bool TryBeginCutscene(UnityEngine.Object owner)
    {
        if (owner == null || !CanShowMessage)
        {
            return false;
        }

        cutsceneOwner = owner;
        isAction = true;
        currentDialog = null;
        hasCutsceneMessage = false;
        dialogPanel.SetActive(false);
        return true;
    }

    public bool IsCutsceneOwner(UnityEngine.Object owner)
    {
        return owner != null && cutsceneOwner == owner && isActiveAndEnabled &&
            dialogPanel != null && dialogText != null;
    }

    public bool HasCutsceneMessage(UnityEngine.Object owner)
    {
        return IsCutsceneOwner(owner) && hasCutsceneMessage;
    }

    public bool TryShowCutsceneMessage(UnityEngine.Object owner, string text)
    {
        if (!IsCutsceneOwner(owner) || hasCutsceneMessage || string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        hasCutsceneMessage = true;
        dialogPanel.SetActive(true);
        StartTypingEffect(text);
        return true;
    }

    public void EndCutscene(UnityEngine.Object owner)
    {
        if (owner == null || cutsceneOwner != owner)
        {
            return;
        }

        ClearDialog();
    }

    // UI Button의 OnClick에서도 호출할 수 있다. 한 프레임에는 한 번만 진행한다.
    public void AdvanceDialog()
    {
        if (!isActiveAndEnabled || !isAction || dialogPanel == null || dialogText == null ||
            (cutsceneOwner != null && !hasCutsceneMessage) ||
            Time.frameCount == messageShownFrame || Time.frameCount == lastInputFrame)
        {
            return;
        }

        lastInputFrame = Time.frameCount;
        HandleInput();
    }

    // SO 없이 획득 안내 등 한 문장의 대화를 표시함
    public bool TryShowMessage(string text)
    {
        if (!CanShowMessage || string.IsNullOrEmpty(text))
            return false;
        currentDialog = null;
        isAction = true;
        dialogPanel.SetActive(true);
        StartTypingEffect(text);
        return true;
    }

    private void Awake()
    {
        if (dialogDatabase != null)
        {
            dialogDatabase.Initialize();
        }
    }

    // 활성화마다 공용 UI의 필수 참조 검증
    private void OnEnable()
    {
        if (dialogPanel == null || dialogText == null)
        {
            Debug.LogError("DialogManager: 공용 Dialog Panel과 Dialog Text를 연결하세요.", this);
            enabled = false;
        }
    }

    void Start()
    {
        if (dialogPanel != null && !isAction)
        {
            dialogPanel.SetActive(false);
        }
    }


    void Update()
    {
        // 소유자나 필수 UI가 파괴되면 취소로 처리하여 예약 해제
        if (dialogCompletion != null && (dialogOwner == null || dialogPanel == null || dialogText == null))
        {
            ClearDialog();
            return;
        }

        if (!isAction) return;

        if ((Keyboard.current != null &&
           (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame)) ||
           (cutsceneOwner != null && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame))
        {
            AdvanceDialog();
        }
    }

    public void StartDialog (int dialogID)
    {
        // 기존 UnityEvent 진입점을 유지하며 진행 중인 대화 덮어쓰기 방지
        if (!TryStartDialogInternal(dialogID, null, null))
        {
            Debug.LogWarning($"DialogManager: 대화 {dialogID}을 시작할 수 없습니다. UI 사용 상태, DB와 대화 ID를 확인하세요.", this);
        }
    }

    // 성공한 요청만 소유권 획득, 완료 true 또는 취소 false를 한 번 통지
    public bool TryStartDialog(int dialogID, UnityEngine.Object owner, Action<bool> onEnded)
    {
        if (owner == null || onEnded == null)
        {
            return false;
        }
        return TryStartDialogInternal(dialogID, owner, onEnded);
    }

    // 일치하는 소유자의 요청만 취소하며 다른 NPC와 컷신은 보존
    public void CancelDialog(UnityEngine.Object owner)
    {
        if (owner != null && dialogOwner == owner)
        {
            ClearDialog();
        }
    }

    // 사용 가능한 UI와 유효한 양수 대화 ID 확인 후 요청 상태 등록
    private bool TryStartDialogInternal(int dialogID, UnityEngine.Object owner, Action<bool> onEnded)
    {
        if (!CanShowMessage || dialogDatabase == null || dialogID <= 0)
        {
            return false;
        }
        // UI를 예약하기 전에 시작 대화 확인
        var dialog = dialogDatabase.GetDialogById(dialogID);
        if (dialog == null)
        {
            return false;
        }

        dialogOwner = owner;
        dialogCompletion = onEnded;
        isAction = true;
        dialogPanel.SetActive(true);
        PlayDialog(dialog);
        return true;
    }

    void PlayDialog(DialogSO dialog)
    {
        currentDialog = dialog;
        StartTypingEffect(currentDialog.text);
    }

    void HandleInput()
    {
        if(isTyping)
        {
            StopTypingEffect();
            dialogText.text = currentMessage;
            isTyping = false;
        }
        else
        {
            if (currentDialog != null && currentDialog.nextId > 0)
            {
                DialogSO nextDialog = dialogDatabase.GetDialogById(currentDialog.nextId);
                if (nextDialog != null)
                {
                    PlayDialog(nextDialog);
                }
                else
                {
                    EndDialog();
                }
            }
            else
            {
                EndDialog();
            }
        }
    }
    void EndDialog()
    {
        if (cutsceneOwner != null)
        {
            StopTypingEffect();
            isTyping = false;
            hasCutsceneMessage = false;
            dialogPanel.SetActive(false);
            return;
        }

        ClearDialog(true);
        OnDialogFinshed?.Invoke();
    }
    void StartTypingEffect(string text)
    {
        messageShownFrame = Time.frameCount;
        currentMessage = text;
        isTyping = true;
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
        }
        typingCoroutine = StartCoroutine(TypeText(text));
    }

    void StopTypingEffect()
    {
        if(typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }
    }

    IEnumerator TypeText(string text)
    {
        dialogText.text = "";
        foreach (char c in text)
        {
            dialogText.text += c;
            if (cutsceneOwner != null)
            {
                yield return new WaitForSecondsRealtime(Mathf.Max(0f, typeSpeed));
            }
            else
            {
                yield return new WaitForSeconds(typeSpeed);
            }
        }
        isTyping = false;
    }

    private void OnDisable()
    {
        ClearDialog();
    }

    // UI와 요청을 먼저 해제한 후 종료 결과 전달, 기본값은 취소
    private void ClearDialog(bool completed = false)
    {
        // 재진입 시 중복 호출과 새 대화의 상태 손상 방지
        var completion = dialogCompletion;
        dialogCompletion = null;
        dialogOwner = null;
        StopTypingEffect();
        isTyping = false;
        isAction = false;
        hasCutsceneMessage = false;
        cutsceneOwner = null;
        currentDialog = null;
        if (dialogPanel != null)
        {
            dialogPanel.SetActive(false);
        }
        completion?.Invoke(completed);
    }
}
