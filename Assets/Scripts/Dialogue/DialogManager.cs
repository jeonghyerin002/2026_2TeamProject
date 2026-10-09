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

    void Start()
    {
        if (dialogPanel != null && !isAction)
        {
            dialogPanel.SetActive(false);
        }
    }


    void Update()
    {
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
        if (!isActiveAndEnabled || cutsceneOwner != null)
        {
            return;
        }

        if (dialogDatabase == null)
        {
            OnDialogFinshed?.Invoke();
            return;
        }

        DialogSO dialog = dialogDatabase.GetDialogById(dialogID);

        if (dialog != null)
        {
            if (dialogPanel == null || dialogText == null)
            {
                Debug.LogError("DialogManager: Dialog Panel과 Dialog Text를 연결하세요.", this);
                enabled = false;
                return;
            }

            isAction = true;
            dialogPanel.SetActive(true);
            PlayDialog(dialog);
        }
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

        isAction = false;
        dialogPanel.SetActive(false);
        currentDialog = null;

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

    private void ClearDialog()
    {
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
    }
}
