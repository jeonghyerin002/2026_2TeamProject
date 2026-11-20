using UnityEngine;
using System.Collections.Generic;

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

    // 이 출입구로 되돌아올 때 사용할 현재 씬의 도착 지점
    [SerializeField] private string returnSpawnID;

    // 씬 경로별로 가장 최근 출발한 출입구의 복귀 지점 보관
    private static readonly Dictionary<(string fromScene, string toScene), string> ReturnSpawnPoints =
        new Dictionary<(string fromScene, string toScene), string>();

    [Header("Weapon Requirement")]
    // 지정한 출구에서만 무기 장착을 필수로 검사, 기존 출구는 기본값 false로 유지
    [SerializeField] private bool requiresEquippedWeapon;
    // 통행을 거절할 때 첫 안내 또는 재대화를 요청하는 선택 경비 참조
    [SerializeField] private WeaponGate weaponGate;
    // 같은 프레임의 여러 콜라이더가 씬 이동을 중복 요청하지 않도록 보호
    private bool isChangingScene;


    // 새 플레이 세션과 새 게임 및 저장 불러오기 시 이전 이동 경로 초기화
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void ClearReturnPoints()
    {
        // 이전 진행의 출입구 기록 제거
        ReturnSpawnPoints.Clear();
    }


    // 반대 방향 이동 기록이 있으면 해당 출입구로 복귀하고 없으면 기본 지점 사용
    private static string GetTargetSpawnID(string sourceScene, string destinationScene, string defaultSpawnID)
    {
        // 현재 이동 방향에 대응하는 최근 복귀 지점 조회
        return ReturnSpawnPoints.TryGetValue((sourceScene, destinationScene), out var spawnID)
            ? spawnID
            : defaultSpawnID;
    }


    // 수락한 씬 이동의 반대 방향에 현재 출입구의 복귀 지점 등록
    private static void RegisterReturnPoint(string sourceScene, string destinationScene, string spawnID)
    {
        // 복귀 지점이 없는 기존 출입구는 이전 연결 방식 유지
        if (string.IsNullOrWhiteSpace(spawnID))
        {
            return;
        }

        ReturnSpawnPoints[(destinationScene, sourceScene)] = spawnID;
    }


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

        // 씬 교체 전 출입구별 도착 ID와 복귀 ID 및 씬 이름 확보
        var sourceScene = gameObject.scene.name;
        var destinationScene = sceneName;
        var spawnID = GetTargetSpawnID(sourceScene, destinationScene, targetSpawnID);
        var sourceSpawnID = returnSpawnID;
        isChangingScene = SceneFadeIn.LoadScene(sceneName,
            () =>
            {
                // 실제 로딩 직전에만 반대 방향 복귀 경로와 목적지 위치 지정
                RegisterReturnPoint(sourceScene, destinationScene, sourceSpawnID);
                SceneSpawnManager.nextSpawnID = spawnID;
            },
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
