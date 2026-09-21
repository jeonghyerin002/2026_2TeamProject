using System.Collections.Generic;
using UnityEngine;

public enum BattleSide
{
    Player,
    Enemy
}

public enum TurnPhase
{
    WaitingPlayer,
    WaitingEnemy,
    Resolving,
    Ended
}

public sealed class BattleTurnAction
{
    public BattleSide Side { get; }
    public int Priority { get; }
    public int Speed { get; }

    public BattleTurnAction(BattleSide side, int priority, int speed)
    {
        Side = side;
        Priority = priority;
        Speed = speed;
    }
}

/// <summary>
/// 플레이어와 적의 행동 순서를 결정하고 턴 진행 상태를 관리
/// </summary>
public class BattleTurnSystem
{
    private readonly Queue<BattleTurnAction> actionQueue = new();

    private BattleTurnAction playerAction;
    private BattleTurnAction enemyAction;
    private bool actionInProgress;

    public int TurnNumber { get; private set; }
    public TurnPhase Phase { get; private set; } = TurnPhase.Ended;

    // 전투를 시작하고 첫 번째 턴을 준비
    public void StartBattle()
    {
        TurnNumber = 1;
        StartTurn();
    }

    // 플레이어가 선택한 행동 저장
    public void SetPlayerAction(BattleTurnAction action)
    {
        if (Phase != TurnPhase.WaitingPlayer || action.Side != BattleSide.Player)
            return;

        playerAction = action;
        Phase = TurnPhase.WaitingEnemy;
    }

    // 적 행동을 저장하고 행동 순서 결정
    public void SetEnemyAction(BattleTurnAction action)
    {
        if (Phase != TurnPhase.WaitingEnemy || action.Side != BattleSide.Enemy)
            return;

        enemyAction = action;
        BuildActionOrder();
        Phase = TurnPhase.Resolving;
    }

    // 다음에 실행할 행동 반환
    public bool TryGetNextAction(out BattleTurnAction action)
    {
        action = null;

        if (Phase != TurnPhase.Resolving ||
            actionInProgress ||
            actionQueue.Count == 0)
            return false;

        action = actionQueue.Dequeue();
        actionInProgress = true;

        return true;
    }

    // 현재 행동 종료 후 다음 행동 또는 다음 턴 진행
    public void CompleteAction(bool battleEnded)
    {
        if (!actionInProgress)
            return;

        actionInProgress = false;

        if (battleEnded)
        {
            EndBattle();
            return;
        }

        if (actionQueue.Count == 0)
        {
            TurnNumber++;
            StartTurn();
        }
    }

    // Priority → Speed → 50% 순으로 선공 결정
    private void BuildActionOrder()
    {
        bool playerFirst;

        if (playerAction.Priority != enemyAction.Priority)
            playerFirst = playerAction.Priority > enemyAction.Priority;
        else if (playerAction.Speed != enemyAction.Speed)
            playerFirst = playerAction.Speed > enemyAction.Speed;
        else
            playerFirst = Random.value < 0.5f;

        if (playerFirst)
        {
            actionQueue.Enqueue(playerAction);
            actionQueue.Enqueue(enemyAction);
        }
        else
        {
            actionQueue.Enqueue(enemyAction);
            actionQueue.Enqueue(playerAction);
        }
    }

    // 새로운 턴 준비
    private void StartTurn()
    {
        playerAction = null;
        enemyAction = null;
        actionQueue.Clear();
        actionInProgress = false;
        Phase = TurnPhase.WaitingPlayer;
    }

    // 전투 종료
    private void EndBattle()
    {
        actionQueue.Clear();
        playerAction = null;
        enemyAction = null;
        actionInProgress = false;
        Phase = TurnPhase.Ended;
    }
}