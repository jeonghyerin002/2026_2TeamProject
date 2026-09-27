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
    Coroutine typingCoroutine;

    private void Awake()
    {
        if (dialogDatabase != null)
        {
            dialogDatabase.Initialize();
        }
    }

    void Start()
    {
        if (dialogPanel != null)
        {
            dialogPanel.SetActive(false);
        }
    }


    void Update()
    {
        if (!isAction) return;

        if (Keyboard.current != null &&
           (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame))
        {
            HandleInput();
        }
    }

    public void StartDialog (int dialogID)
    {
        if (dialogDatabase == null)
        {
            OnDialogFinshed?.Invoke();
            return;
        }

        DialogSO dialog = dialogDatabase.GetDialogById(dialogID);

        if (dialog != null)
        {
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
            dialogText.text = currentDialog.text;
            isTyping = false;
        }
        else
        {
            if (currentDialog.nextId > 0)
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
        isAction = false;
        dialogPanel.SetActive(false);
        currentDialog = null;

        OnDialogFinshed?.Invoke();
    }
    void StartTypingEffect(string text)
    {
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
            yield return new WaitForSeconds(typeSpeed);
        }
        isTyping = false;
    }
}
