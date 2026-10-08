using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Battle Scene의 전투 데이터 연결과 전투 종료 후 맵 복귀를 관리한다</summary>
[DisallowMultipleComponent]
public sealed class BattleSceneController : MonoBehaviour
{
    [SerializeField] private BattleSystem battleSystem;
    [SerializeField] private BattleFlow battleFlow;
    [SerializeField, Min(0f)] private float returnDelay = 2f;

    private PlayerBattleData playerData;
    private Coroutine returnRoutine;
    private bool resultHandled;

    // Battle Scene 진입 시 현재 NPC 전투 데이터를 연결한다
    private void Awake()
    {
        if (!BattleSession.IsActive)
            return;

        if (!SetupBattle())
        {
            if (battleFlow != null)
                battleFlow.enabled = false;

            enabled = false;
        }
    }

    // 전투 종료 이벤트와 최종 연출 완료 이벤트를 구독한다
    private void OnEnable()
    {
        if (!BattleSession.IsActive || battleSystem == null || battleFlow == null)
            return;

        battleSystem.BattleEnded += OnBattleEnded;
        battleFlow.PresentationEnded += OnPresentationEnded;
    }

    // 이벤트와 복귀 Coroutine을 정리한다
    private void OnDisable()
    {
        if (battleSystem != null)
            battleSystem.BattleEnded -= OnBattleEnded;

        if (battleFlow != null)
            battleFlow.PresentationEnded -= OnPresentationEnded;

        if (returnRoutine != null)
            StopCoroutine(returnRoutine);

        returnRoutine = null;
    }

    // 플레이어와 NPC 데이터를 기존 전투 시스템에 전달한다
    private bool SetupBattle()
    {
        if (battleSystem == null || battleFlow == null || BattleSession.Enemy == null)
        {
            Debug.LogError("BattleSceneController: 전투 참조 또는 NPC 전투 데이터가 없습니다.");
            return false;
        }

        PlayerController player = PlayerController.Instance;
        if (player == null)
        {
            Debug.LogError("BattleSceneController: PlayerController가 없습니다.");
            return false;
        }

        playerData = player.GetComponent<PlayerBattleData>();
        if (playerData == null || !playerData.IsValid)
        {
            Debug.LogError("BattleSceneController: PlayerBattleData가 없거나 설정되지 않았습니다.");
            return false;
        }

        NpcBattleData enemy = BattleSession.Enemy;

        // 양측 원본 데이터를 전투 시스템에 전달한다
        battleSystem.SetPlayer(playerData.Character, playerData.EquippedAether, playerData.GetBattleAethers(), playerData.Weapon, playerData.Level);
        battleSystem.SetEnemy(enemy.Character, enemy.Aether, enemy.Weapon, enemy.Level, enemy.ExperienceReward);

        // Battle Scene에서 맵 이동 입력이 실행되지 않도록 잠근다
        player.SetMovementEnabled(false);
        return true;
    }

    // 승패 결과를 플레이어 데이터와 NPC 진행도에 반영한다
    private void OnBattleEnded(BattleSide? winner)
    {
        if (!BattleSession.IsActive || resultHandled || playerData == null)
            return;

        playerData.ApplyBattleResult(battleSystem.PlayerState);

        if (winner == BattleSide.Player)
            NpcBattleProgress.MarkDefeated(BattleSession.NpcId);

        resultHandled = true;
    }

    // 최종 승패 문구 출력이 끝나면 맵 복귀를 시작한다
    private void OnPresentationEnded()
    {
        if (!BattleSession.IsActive || !resultHandled || returnRoutine != null)
            return;

        returnRoutine = StartCoroutine(ReturnToMap());
    }

    // 지정한 시간 후 원래 맵으로 돌아간다
    private IEnumerator ReturnToMap()
    {
        if (returnDelay > 0f)
            yield return new WaitForSeconds(returnDelay);

        string scene = BattleSession.ReturnScene;
        if (string.IsNullOrEmpty(scene))
        {
            Debug.LogError("BattleSceneController: 복귀할 Scene이 없습니다.");
            yield break;
        }

        SceneManager.LoadScene(scene);
    }
}
