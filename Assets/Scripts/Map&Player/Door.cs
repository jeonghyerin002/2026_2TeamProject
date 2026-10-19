using UnityEngine;
using UnityEngine.SceneManagement;

public class Door : MonoBehaviour
{
    [Header("Scene Setting")]
    public string sceneName;

    [Header("Trigger Setting")]
    bool useTagCheck = true;
    string playerTag = "Player";

    [Header("Spaawn Setting")]
    public string targetSpawnID;

    [Header("Weapon Requirement")]
    // 지정한 출구에서만 무기 장착을 필수로 검사, 기존 출구는 기본값 false로 유지
    [SerializeField] private bool requiresEquippedWeapon;
    // 통행을 거절할 때 첫 안내 또는 재대화를 요청하는 선택 경비 참조
    [SerializeField] private WeaponGate weaponGate;
    // 같은 프레임의 여러 콜라이더가 씬 이동을 중복 요청하지 않도록 보호
    private bool isChangingScene;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        TryEnter(collision);
    }

    // 출구 안에서 무기를 장착한 경우 재진입 없이 장착 상태를 다시 검사
    private void OnTriggerStay2D(Collider2D collision)
    {
        if (requiresEquippedWeapon)
        {
            TryEnter(collision, false);
        }
    }

    // 기존 태그 검사를 유지하고 지정한 출구에서만 실제 무기 장착 상태 확인
    private void TryEnter(Collider2D collision, bool showBlockedDialogue = true)
    {
        if (isChangingScene)
        {
            return;
        }
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("Scene name is not set for the door.");
            return;
        }

        if (useTagCheck)
        {
            if (collision.CompareTag(playerTag))
            {
                if (requiresEquippedWeapon)
                {
                    var player = collision.GetComponentInParent<PlayerBattleData>();
                    if (player == null || player.Weapon == null)
                    {
                        // 경비가 누락되어도 장착 조건 자체는 유지하여 우회 통과 방지
                        if (showBlockedDialogue && weaponGate != null)
                        {
                            weaponGate.TryStartBlockedDialogue();
                        }
                        return;
                    }
                }
                ChangeScene();
            }

        }
    }
    void ChangeScene()
    {
        isChangingScene = true;
        SceneSpawnManager.nextSpawnID = targetSpawnID;
        SceneManager.LoadScene(sceneName);
    }
}
