using UnityEngine;

/// <summary>
/// 출입 조건 확인과 플레이어 이동 정지를 포함한 씬 전환 요청
/// </summary>
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
        if (isChangingScene || SceneFadeIn.IsTransitioning)
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


    // 감지 직후 이동 정지 후 목적지 씬 로딩 완료 시 기존 이동 상태 복구
    private void ChangeScene()
    {
        // 지속 플레이어의 기존 이동 상태 보관 후 입력과 물리 속도 즉시 초기화
        var player = PlayerController.Instance;
        if (player == null)
        {
            Debug.LogError("Door: 씬을 이동할 PlayerController가 없습니다.", this);
            return;
        }

        var wasMovementEnabled = player.IsMovementEnabled;
        player.SetMovementEnabled(false);

        // 출발 씬의 Door 파괴 후에도 사용할 도착 ID와 플레이어 참조 전달
        var spawnID = targetSpawnID;
        isChangingScene = SceneFadeIn.LoadScene(sceneName,
            () => SceneSpawnManager.nextSpawnID = spawnID,
            () =>
            {
                // 목적지 로딩 완료 후 이번 전환이 제한한 이동 상태 복구
                if (player != null)
                {
                    player.SetMovementEnabled(wasMovementEnabled);
                }
            });

        // 잘못된 씬 설정이나 중복 요청 거절 시 출발 씬에서 이동 상태 복구
        if (!isChangingScene)
        {
            player.SetMovementEnabled(wasMovementEnabled);
        }
    }
}
