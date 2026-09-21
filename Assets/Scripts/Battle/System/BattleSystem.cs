using UnityEngine;

/// <summary>플레이어와 적의 BattleState를 만들고 BattleTurnSystem과 전투 행동을 연결한다.</summary>
[DisallowMultipleComponent]
public class BattleSystem : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private CharacterData playerCharacter;
    [SerializeField] private AetherData playerAether;

    [Header("Enemy")]
    [SerializeField] private CharacterData enemyCharacter;
    [SerializeField] private AetherData enemyAether;

    private readonly BattleTurnSystem turnSystem = new();

    private BattleState playerState;
    private BattleState enemyState;
    private SkillData playerSkill;
    private SkillData enemySkill;

    public BattleState PlayerState => playerState;
    public BattleState EnemyState => enemyState;
    public TurnPhase Phase => turnSystem.Phase;
    public int TurnNumber => turnSystem.TurnNumber;

    // 원본 데이터를 기반으로 전투 상태를 만들고 전투 시작
    public void StartBattle()
    {
        if (playerCharacter == null ||
            playerAether == null ||
            enemyCharacter == null ||
            enemyAether == null)
        {
            Debug.LogError("BattleSystem: CharacterData와 AetherData를 모두 연결하세요.");
            return;
        }

        playerState = new BattleState(playerCharacter, playerAether);
        enemyState = new BattleState(enemyCharacter, enemyAether);
        playerSkill = null;
        enemySkill = null;

        turnSystem.StartBattle();
    }

    // 플레이어 스킬 선택을 확정하고 턴 시스템에 행동 등록
    public bool SelectPlayerSkill(int skillIndex)
    {
        if (playerState == null || Phase != TurnPhase.WaitingPlayer)
            return false;

        SkillData skill = playerState.GetSkill(skillIndex);

        if (!playerState.TryConsumePp(skill))
            return false;

        playerSkill = skill;

        turnSystem.SetPlayerAction(
            new BattleTurnAction(
                BattleSide.Player,
                skill.Priority,
                playerState.Speed));

        return true;
    }

    // 적 스킬 선택을 확정하고 턴 시스템에 행동 등록
    public bool SelectEnemySkill(int skillIndex)
    {
        if (enemyState == null || Phase != TurnPhase.WaitingEnemy)
            return false;

        SkillData skill = enemyState.GetSkill(skillIndex);

        if (!enemyState.TryConsumePp(skill))
            return false;

        enemySkill = skill;

        turnSystem.SetEnemyAction(
            new BattleTurnAction(
                BattleSide.Enemy,
                skill.Priority,
                enemyState.Speed));

        return true;
    }

    // 다음 행동의 사용자, 대상, 스킬 반환
    public bool TryGetNextAction(
        out BattleState actor,
        out BattleState target,
        out SkillData skill)
    {
        actor = null;
        target = null;
        skill = null;

        if (!turnSystem.TryGetNextAction(out BattleTurnAction action))
            return false;

        bool isPlayer = action.Side == BattleSide.Player;

        actor = isPlayer ? playerState : enemyState;
        target = isPlayer ? enemyState : playerState;
        skill = isPlayer ? playerSkill : enemySkill;

        return true;
    }

    // 현재 행동 완료 후 전투 종료 또는 다음 행동/턴으로 진행
    public void CompleteAction()
    {
        if (playerState == null || enemyState == null)
            return;

        bool battleEnded = playerState.IsDead || enemyState.IsDead;
        turnSystem.CompleteAction(battleEnded);

        if (battleEnded || Phase == TurnPhase.WaitingPlayer)
        {
            playerSkill = null;
            enemySkill = null;
        }
    }
}
