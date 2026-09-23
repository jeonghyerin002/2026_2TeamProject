using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>기존 선발 SO 연결과 예비 멤버, 턴 진행 및 전투 종료를 관리한다</summary>
[DisallowMultipleComponent]
public class BattleSystem : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private CharacterData playerCharacter;
    [SerializeField] private AetherData playerAether;
    [SerializeField] private WeaponData playerWeapon;
    [SerializeField, Range(1, 100)] private int playerLevel = 1;
    [SerializeField] private BattleMember[] playerReserves = Array.Empty<BattleMember>();

    [Header("Enemy")]
    [SerializeField] private CharacterData enemyCharacter;
    [SerializeField] private AetherData enemyAether;
    [SerializeField] private WeaponData enemyWeapon;
    [SerializeField, Range(1, 100)] private int enemyLevel = 1;
    [SerializeField] private BattleMember[] enemyReserves = Array.Empty<BattleMember>();
    [SerializeField, Min(0)] private int experienceReward = 50;

    private readonly BattleTurnSystem turnSystem = new();
    private readonly List<BattleState> playerParty = new();
    private readonly List<BattleState> enemyParty = new();
    private readonly HashSet<BattleState> fainted = new();
    private IReadOnlyList<BattleState> playerView;
    private BattleState playerState;
    private BattleState enemyState;
    private BattleState playerActor;
    private BattleState enemyActor;
    private SkillData playerSkill;
    private SkillData enemySkill;
    private bool waitingReplacement;
    private bool actionPending;

    public event Action<string> Message;
    public event Action StateChanged;
    public event Action<BattleSide?> BattleEnded;
    public BattleState PlayerState => playerState;
    public BattleState EnemyState => enemyState;
    public IReadOnlyList<BattleState> PlayerParty => playerView ??= playerParty.AsReadOnly();
    public TurnPhase Phase => waitingReplacement ? TurnPhase.WaitingReplacement : turnSystem.Phase;
    public int TurnNumber => turnSystem.TurnNumber;
    public BattleSide? Winner { get; private set; }

    // 기존 선발 참조와 예비 멤버로 새 전투를 시작한다
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
        BuildParty(playerParty, playerState, playerReserves);
        BuildParty(enemyParty, enemyState, enemyReserves);
        fainted.Clear();
        playerSkill = null;
        enemySkill = null;
        playerActor = null;
        enemyActor = null;
        waitingReplacement = false;
        actionPending = false;
        Winner = null;
        turnSystem.StartBattle();
        NotifyState();
    }

    // 중단된 행동을 폐기하여 연출 재시작 시 이중 적용을 방지한다
    public void StopBattle()
    {
        turnSystem.EndBattle();
        waitingReplacement = false;
        actionPending = false;
    }

    // 선발과 유효한 예비 멤버의 전투 상태를 생성한다
    private static void BuildParty(List<BattleState> party, BattleState first, BattleMember[] reserves)
    {
        party.Clear();
        party.Add(first);
        if (reserves == null)
            return;
        foreach (BattleMember member in reserves)
        {
            if (member != null && member.Character != null && member.Aether != null)
                party.Add(new BattleState(member.Character, member.Aether, member.Weapon, member.Level));
        }
    }

    // 플레이어 선택을 등록하며 PP는 실행 시까지 보존한다
    public bool SelectPlayerSkill(int skillIndex)
    {
        if (playerState == null || Phase != TurnPhase.WaitingPlayer || !CanSelect(playerState, skillIndex))
            return false;
        playerActor = playerState;
        playerSkill = playerState.GetSkill(skillIndex);
        turnSystem.SetPlayerAction(new BattleTurnAction(BattleSide.Player, playerSkill != null ? playerSkill.Priority : 0, playerState.Speed));
        return true;
    }

    // 적 선택을 등록하고 우선도와 스피드로 행동 순서를 확정한다
    public bool SelectEnemySkill(int skillIndex)
    {
        if (enemyState == null || Phase != TurnPhase.WaitingEnemy || !CanSelect(enemyState, skillIndex))
            return false;
        enemyActor = enemyState;
        enemySkill = enemyState.GetSkill(skillIndex);
        turnSystem.SetEnemyAction(new BattleTurnAction(BattleSide.Enemy, enemySkill != null ? enemySkill.Priority : 0, enemyState.Speed));
        return true;
    }

    // 전체 PP 소진 시에만 발버둥 슬롯 -1을 허용한다
    private static bool CanSelect(BattleState state, int index)
    {
        if (state.IsDead)
            return false;
        return index == -1 ? state.GetAvailableSkill() < 0 : state.GetCurrentPp(state.GetSkill(index)) > 0;
    }

    // 턴 시작 시 선택했던 행동 주체를 보존하여 다음 행동을 반환한다
    public bool TryGetNextAction(out BattleState actor, out BattleState target, out SkillData skill)
    {
        actor = null;
        target = null;
        skill = null;
        if (Phase != TurnPhase.Resolving || !turnSystem.TryGetNextAction(out BattleTurnAction action))
            return false;
        bool isPlayer = action.Side == BattleSide.Player;
        actor = isPlayer ? playerActor : enemyActor;
        target = isPlayer ? enemyState : playerState;
        skill = isPlayer ? playerSkill : enemySkill;
        actionPending = true;
        return true;
    }

    // 기절 및 턴 종료 피해를 처리하고 교체나 다음 턴으로 진행한다
    public void CompleteAction()
    {
        if (!actionPending || playerState == null || enemyState == null)
            return;
        actionPending = false;
        ReportFaint(playerState, enemyState, false);
        ReportFaint(enemyState, playerState, true);
        bool ended = !HasLiving(playerParty) || !HasLiving(enemyParty);
        if (!ended && turnSystem.IsLastAction)
        {
            ApplyResidual(playerState);
            ApplyResidual(enemyState);
            ReportFaint(playerState, enemyState, false);
            ReportFaint(enemyState, playerState, true);
            ended = !HasLiving(playerParty) || !HasLiving(enemyParty);
        }
        turnSystem.CompleteAction(ended);
        if (ended)
        {
            FinishBattle();
            return;
        }
        if (enemyState.IsDead)
        {
            enemyState.ResetVolatileState();
            enemyState = enemyParty.Find(unit => !unit.IsDead);
            Publish($"{enemyState.Character.Charactername}이(가) 전투에 나왔다!");
        }
        waitingReplacement = playerState.IsDead;
        if (waitingReplacement)
            Publish("다음에 나올 아군을 선택하세요.");
        NotifyState();
    }

    // 지속 피해와 HP 변경 결과를 전달한다
    private void ApplyResidual(BattleState state)
    {
        if (state.ApplyResidualDamage() > 0)
            Publish($"{state.Character.Charactername}이(가) {state.Status} 피해를 받았다!");
    }

    // 기절 메시지와 적 격파 경험치를 한 번만 처리한다
    private void ReportFaint(BattleState state, BattleState recipient, bool reward)
    {
        if (!state.IsDead || !fainted.Add(state))
            return;
        Publish($"{state.Character.Charactername}이(가) 쓰러졌다!");
        if (reward && !recipient.IsDead)
        {
            recipient.GainExperience(experienceReward);
            Publish($"{recipient.Character.Charactername}: 경험치 {experienceReward} 획득!");
        }
    }

    // 살아 있는 멤버가 남았는지 확인한다
    private static bool HasLiving(List<BattleState> party)
    {
        return party.Exists(unit => !unit.IsDead);
    }

    // 기절한 플레이어 멤버를 선택한 파티 슬롯으로 교체한다
    public bool SelectReplacement(int partyIndex)
    {
        if (!waitingReplacement || partyIndex < 0 || partyIndex >= playerParty.Count || playerParty[partyIndex].IsDead)
            return false;
        playerState.ResetVolatileState();
        playerState = playerParty[partyIndex];
        waitingReplacement = false;
        Publish($"{playerState.Character.Charactername}이(가) 전투에 나왔다!");
        NotifyState();
        return true;
    }

    // 승패와 일시 상태 정리를 완료한다
    private void FinishBattle()
    {
        bool playerAlive = HasLiving(playerParty);
        bool enemyAlive = HasLiving(enemyParty);
        Winner = playerAlive ? BattleSide.Player : enemyAlive ? BattleSide.Enemy : (BattleSide?)null;
        foreach (BattleState state in playerParty)
            state.FinishBattle();
        foreach (BattleState state in enemyParty)
            state.FinishBattle();
        waitingReplacement = false;
        Publish(Winner == BattleSide.Player ? "전투에서 승리했다!" : Winner == BattleSide.Enemy ? "전투에서 패배했다!" : "전투가 무승부로 끝났다!");
        NotifyState();
        BattleEnded?.Invoke(Winner);
    }

    // 전투 문구를 표시 계층에 전달한다
    public void Publish(string message)
    {
        if (!string.IsNullOrEmpty(message))
            Message?.Invoke(message);
    }

    // HP, PP 및 교체 상태 변경을 표시 계층에 전달한다
    public void NotifyState()
    {
        StateChanged?.Invoke();
    }
}

/// <summary>Inspector에서 예비 멤버의 원본 데이터와 레벨을 지정한다</summary>
[Serializable]
public sealed class BattleMember
{
    [SerializeField] private CharacterData character;
    [SerializeField] private AetherData aether;
    [SerializeField] private WeaponData weapon;
    [SerializeField, Range(1, 100)] private int level = 1;

    public CharacterData Character => character;
    public AetherData Aether => aether;
    public WeaponData Weapon => weapon;
    public int Level => level;
}
