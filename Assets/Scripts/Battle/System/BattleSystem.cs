using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>플레이어와 적의 전투 상태, 턴 진행 및 전투 종료를 관리한다</summary>
[DisallowMultipleComponent]
public class BattleSystem : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private CharacterData playerCharacter;
    [SerializeField] private AetherData playerAether;
    [Tooltip("전투에서 교체할 수 있는 보유 에테르 목록. 시작 장착 에테르는 자동으로 포함한다.")]
    [SerializeField] private AetherData[] ownedAethers = Array.Empty<AetherData>();
    [SerializeField] private WeaponData playerWeapon;
    [SerializeField, Range(1, 100)] private int playerLevel = 1;

    [Header("Enemy")]
    [SerializeField] private CharacterData enemyCharacter;
    [SerializeField] private AetherData enemyAether;
    [SerializeField] private WeaponData enemyWeapon;
    [SerializeField, Range(1, 100)] private int enemyLevel = 1;
    [SerializeField, Min(0)] private int experienceReward = 50;

    private readonly BattleTurnSystem turnSystem = new();
    private readonly HashSet<BattleState> fainted = new();
    private readonly List<AetherData> availableAethers = new();
    private IReadOnlyList<AetherData> aetherView;
    private BattleState playerState;
    private BattleState enemyState;
    private BattleState playerActor;
    private BattleState enemyActor;
    private SkillData playerSkill;
    private SkillData enemySkill;
    private bool actionPending;

    public event Action<string> Message;
    public event Action StateChanged;
    public event Action<BattleSide?> BattleEnded;

    public BattleState PlayerState => playerState;
    public BattleState EnemyState => enemyState;
    public IReadOnlyList<AetherData> OwnedAethers => aetherView ??= availableAethers.AsReadOnly();
    public TurnPhase Phase => turnSystem.Phase;
    public int TurnNumber => turnSystem.TurnNumber;
    public BattleSide? Winner { get; private set; }

    // 외부 플레이어 전투 데이터를 설정한다
    public void SetPlayer(CharacterData character, AetherData aether, AetherData[] aethers, WeaponData weapon, int level)
    {
        playerCharacter = character;
        playerAether = aether;
        ownedAethers = aethers ?? Array.Empty<AetherData>();
        playerWeapon = weapon;
        playerLevel = Mathf.Clamp(level, 1, 100);
    }

    // 외부 NPC 전투 데이터를 설정한다
    public void SetEnemy(CharacterData character, AetherData aether, WeaponData weapon, int level, int reward)
    {
        enemyCharacter = character;
        enemyAether = aether;
        enemyWeapon = weapon;
        enemyLevel = Mathf.Clamp(level, 1, 100);
        experienceReward = Mathf.Max(0, reward);
    }

    // 플레이어와 적의 전투 상태를 생성하고 전투를 시작한다
    public void StartBattle()
    {
        StopBattle();

        if (playerCharacter == null || playerAether == null || enemyCharacter == null || enemyAether == null)
        {
            Debug.LogError("BattleSystem: 양측 CharacterData와 AetherData를 연결하세요.");
            return;
        }

        playerState = new BattleState(playerCharacter, playerAether, playerWeapon, playerLevel);
        enemyState = new BattleState(enemyCharacter, enemyAether, enemyWeapon, enemyLevel);

        BuildAethers();
        fainted.Clear();

        playerSkill = null;
        enemySkill = null;
        playerActor = null;
        enemyActor = null;
        actionPending = false;
        Winner = null;

        turnSystem.StartBattle();
        NotifyState();
    }

    // 중단된 행동을 폐기하여 연출 재시작 시 이중 적용을 방지한다
    public void StopBattle()
    {
        turnSystem.EndBattle();
        actionPending = false;
    }

    // 시작 장비와 보유한 에테르를 중복 없이 준비한다
    private void BuildAethers()
    {
        availableAethers.Clear();
        AddAether(playerState.Aether);

        if (ownedAethers == null)
            return;

        foreach (AetherData aether in ownedAethers)
            AddAether(aether);
    }

    // 같은 ID의 에테르가 중복 등록되지 않도록 추가한다
    private void AddAether(AetherData aether)
    {
        if (aether == null)
            return;

        foreach (AetherData owned in availableAethers)
        {
            if (owned.itemId == aether.itemId)
                return;
        }

        availableAethers.Add(aether);
    }

    // 보유 에테르를 교체하고 자신의 공격 대신 적 행동을 진행한다
    public bool SelectPlayerAether(int index)
    {
        if (Phase != TurnPhase.WaitingPlayer || playerState == null || enemyState == null || enemyState.IsDead ||
            index < 0 || index >= availableAethers.Count || !playerState.EquipAether(availableAethers[index]))
            return false;

        playerActor = null;
        playerSkill = null;
        enemyActor = enemyState;
        enemySkill = enemyState.GetSkill(enemyState.GetAvailableSkill());

        turnSystem.SetEnemyResponse(new BattleTurnAction(
            BattleSide.Enemy,
            enemySkill != null ? enemySkill.Priority : 0,
            enemyState.Speed));

        Publish($"{playerState.Aether.AetherName}을(를) 장착했다!");
        NotifyState();
        return true;
    }

    // 플레이어가 선택한 기술을 현재 턴에 등록한다
    public bool SelectPlayerSkill(int skillIndex)
    {
        if (playerState == null || Phase != TurnPhase.WaitingPlayer || !CanSelect(playerState, skillIndex))
            return false;

        playerActor = playerState;
        playerSkill = playerState.GetSkill(skillIndex);

        turnSystem.SetPlayerAction(new BattleTurnAction(
            BattleSide.Player,
            playerSkill != null ? playerSkill.Priority : 0,
            playerState.Speed));

        return true;
    }

    // 적이 선택한 기술을 등록하고 행동 순서를 확정한다
    public bool SelectEnemySkill(int skillIndex)
    {
        if (enemyState == null || Phase != TurnPhase.WaitingEnemy || !CanSelect(enemyState, skillIndex))
            return false;

        enemyActor = enemyState;
        enemySkill = enemyState.GetSkill(skillIndex);

        turnSystem.SetEnemyAction(new BattleTurnAction(
            BattleSide.Enemy,
            enemySkill != null ? enemySkill.Priority : 0,
            enemyState.Speed));

        return true;
    }

    // 현재 전투 상태에서 선택 가능한 기술인지 확인한다
    private static bool CanSelect(BattleState state, int index)
    {
        if (state.IsDead)
            return false;

        return index == -1 ?
            state.GetAvailableSkill() < 0 :
            state.GetCurrentPp(state.GetSkill(index)) > 0;
    }

    // 현재 행동 순서에 해당하는 전투 행동을 반환한다
    public bool TryGetNextAction(out BattleState actor, out BattleState target, out SkillData skill)
    {
        actor = null;
        target = null;
        skill = null;

        if (Phase != TurnPhase.Resolving ||
            !turnSystem.TryGetNextAction(out BattleTurnAction action))
            return false;

        bool isPlayer = action.Side == BattleSide.Player;

        actor = isPlayer ? playerActor : enemyActor;
        target = isPlayer ? enemyState : playerState;
        skill = isPlayer ? playerSkill : enemySkill;
        actionPending = true;
        return true;
    }

    // 행동 종료 후 기절과 지속 피해를 확인하고 다음 상태로 진행한다
    public void CompleteAction()
    {
        if (!actionPending || playerState == null || enemyState == null)
            return;

        actionPending = false;

        ReportFaint(playerState, enemyState, false);
        ReportFaint(enemyState, playerState, true);

        bool ended = playerState.IsDead || enemyState.IsDead;

        if (!ended && turnSystem.IsLastAction)
        {
            ApplyResidual(playerState);
            ApplyResidual(enemyState);

            ReportFaint(playerState, enemyState, false);
            ReportFaint(enemyState, playerState, true);

            ended = playerState.IsDead || enemyState.IsDead;
        }

        turnSystem.CompleteAction(ended);

        if (ended)
        {
            FinishBattle();
            return;
        }

        NotifyState();
    }

    // 턴 종료 상태이상 피해를 적용한다
    private void ApplyResidual(BattleState state)
    {
        if (state.ApplyResidualDamage() > 0)
            Publish($"{state.Character.Charactername}이(가) {state.Status} 피해를 받았다!");
    }

    // 기절 문구와 적 격파 경험치를 한 번만 처리한다
    private void ReportFaint(BattleState state, BattleState recipient, bool reward)
    {
        if (!state.IsDead || !fainted.Add(state))
            return;

        Publish($"{state.Character.Charactername}이(가) 쓰러졌다!");

        if (!reward || recipient.IsDead)
            return;

        recipient.GainExperience(experienceReward);
        Publish($"{recipient.Character.Charactername}: 경험치 {experienceReward} 획득!");
    }

    // 승패를 결정하고 전투 종료 상태를 전달한다
    private void FinishBattle()
    {
        Winner = playerState.IsDead && enemyState.IsDead ? null :
            enemyState.IsDead ? BattleSide.Player :
            BattleSide.Enemy;

        playerState.FinishBattle();
        enemyState.FinishBattle();

        Publish(Winner == BattleSide.Player ? "전투에서 승리했다!" :
            Winner == BattleSide.Enemy ? "전투에서 패배했다!" :
            "전투가 무승부로 끝났다!");

        NotifyState();
        BattleEnded?.Invoke(Winner);
    }

    // 전투 문구를 표시 계층에 전달한다
    public void Publish(string message)
    {
        if (!string.IsNullOrEmpty(message))
            Message?.Invoke(message);
    }

    // HP와 전투 상태 변경을 표시 계층에 전달한다
    public void NotifyState()
    {
        StateChanged?.Invoke();
    }
}