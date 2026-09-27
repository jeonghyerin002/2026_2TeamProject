using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class NPCInteraction : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] private int startingDialogueId = 1;
    [SerializeField] private string battleSceneName = "Battle";

    DialogManager dialogManager;

    bool isPlayerNearby = false;
    bool isInteracting = false;

    private void Awake()
    {
        dialogManager = GetComponent<DialogManager>();

        //대화 매니저의종료 이벤트 구독
        dialogManager.OnDialogFinshed += OnDialogCompleted;
    }
    void Update()
    {
        if (isInteracting) return; //이걸 업데이트에서 하는거 맞나 잘 모르겠슨

        if (isPlayerNearby && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            Debug.Log("대화시작");
            StartInteraction();
        }
    }
    
    void StartInteraction()
    {
        isInteracting = true;
        
        if (startingDialogueId > 0)
        {
            dialogManager.StartDialog(startingDialogueId);
        }
        else
        {
            //다이얼로그 ID가 없거나 0이면 즉시 배틀씬 진입
            TransitionToBattle();
        }
    }

    void OnDialogCompleted()
    {
        isInteracting = false;
        TransitionToBattle();
    }
    void TransitionToBattle()
    {
        //배틀씬으로 이동
        SceneManager.LoadScene(battleSceneName);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            isPlayerNearby = true;
            Debug.Log("플레이어 감지됨");
        }
    }

    void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            isPlayerNearby = false;
            Debug.Log("플레이어 이탈함");
        }
    }

    private void OnDestroy()
    {
        //메모리 누수 방지를 위한 이벤트 구독 해제
        if (dialogManager != null)
        {
            dialogManager.OnDialogFinshed -= OnDialogCompleted;
        }
    }
}
