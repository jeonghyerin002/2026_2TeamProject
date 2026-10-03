using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>입력과 전투 규칙을 연결하고 문구, 애니메이션, HP 변화를 순서대로 연출한다</summary>
public class BattleFlow : MonoBehaviour
{
    [SerializeField] private BattleSystem battleSystem;
    [SerializeField] private TMP_Text battleText;
    [SerializeField] private Slider playerHp;
    [SerializeField] private Slider enemyHp;
    [SerializeField, Min(0f)] private float textDuration = 0.6f;
    [SerializeField, Min(0f)] private float animationDuration = 0.5f;
    [SerializeField] private UnityEvent<string> onMessage = new();
    [SerializeField] private UnityEvent onPlayerAnimation = new();
    [SerializeField] private UnityEvent onEnemyAnimation = new();
    [SerializeField] private UnityEvent onStateChanged = new();

    private readonly Queue<string> messages = new();
    private Coroutine turnRoutine;
    private bool running;

    public event Action<BattleState, BattleState, SkillData> AnimationRequested;
    public event Action PresentationEnded;
    public bool IsBusy => running;

    // 표시 이벤트를 구독한다
    private void OnEnable()
    {
        if (battleSystem == null)
            return;

        battleSystem.Message += QueueMessage;
        battleSystem.StateChanged += RefreshState;
    }

    // 진행 중 Coroutine과 이벤트 구독을 정리한다
    private void OnDisable()
    {
        bool interrupted = running;

        if (turnRoutine != null)
            StopCoroutine(turnRoutine);

        turnRoutine = null;
        running = false;

        if (battleSystem == null)
            return;

        if (interrupted)
            battleSystem.StopBattle();

        battleSystem.Message -= QueueMessage;
        battleSystem.StateChanged -= RefreshState;
    }

    // 준비된 원본 데이터로 전투를 시작한다
    private void Start()
    {
        StartBattle();
    }

    // 진행 중인 연출을 정리하고 전투를 초기화한다
    public void StartBattle()
    {
        if (battleSystem == null)
        {
            Debug.LogError("BattleFlow: BattleSystem을 연결하세요.");
            return;
        }

        if (turnRoutine != null)
            StopCoroutine(turnRoutine);

        turnRoutine = null;
        running = false;
        messages.Clear();

        battleSystem.StartBattle();
    }

    // 플레이어와 적의 기술을 선택하고 한 턴을 실행한다
    public void SelectPlayerSkill(int skillIndex)
    {
        if (running || battleSystem == null || !battleSystem.SelectPlayerSkill(skillIndex))
            return;

        if (!battleSystem.SelectEnemySkill(battleSystem.EnemyState.GetAvailableSkill()))
            return;

        BeginResolution();
    }

    // 에테르 교체 후 적 행동을 한 턴으로 실행한다
    public bool SelectPlayerAether(int index)
    {
        if (running || battleSystem == null || !battleSystem.SelectPlayerAether(index))
            return false;

        BeginResolution();
        return true;
    }

    // 전체 PP 소진 상태에서 발버둥을 사용한다
    public void SelectStruggle()
    {
        SelectPlayerSkill(-1);
    }

    // 중복 실행을 막고 전투 행동 연출을 시작한다
    private void BeginResolution()
    {
        running = true;
        RefreshState();
        turnRoutine = StartCoroutine(ResolveTurn());
    }

    // 선택된 행동과 전투 메시지를 순서대로 처리한다
    private IEnumerator ResolveTurn()
    {
        yield return ShowMessages();

        while (battleSystem.Phase == TurnPhase.Resolving)
        {
            if (!battleSystem.TryGetNextAction(out BattleState actor, out BattleState target, out SkillData skill))
                break;

            yield return ResolveAction(actor, target, skill);

            battleSystem.CompleteAction();

            yield return ShowMessages();
        }

        turnRoutine = null;
        running = false;

        RefreshState();

        // 최종 승패 문구까지 모두 표시된 후 종료 연출 완료를 알린다
        if (battleSystem.Phase == TurnPhase.Ended)
            PresentationEnded?.Invoke();
    }

    // 행동 가능 여부부터 피해와 부가효과까지 순서대로 처리한다
    private IEnumerator ResolveAction(BattleState actor, BattleState target, SkillData skill)
    {
        bool ready = BattleResolver.TryBegin(actor, target, skill, out BattleResolveResult blocked);

        RefreshState();

        if (!ready)
        {
            if (!string.IsNullOrEmpty(blocked.Message))
                yield return ShowText($"{actor.Character.Charactername}: {blocked.Message}");

            yield break;
        }

        yield return ShowText(
            $"{actor.Character.Charactername}이(가) {(skill != null ? skill.Skillname : "발버둥")}을(를) 사용했다!");

        if (!BattleResolver.IsHit(skill))
        {
            yield return ShowText("그러나 공격은 빗나갔다!");
            yield break;
        }

        AnimationRequested?.Invoke(actor, target, skill);

        if (actor == battleSystem.PlayerState)
            onPlayerAnimation.Invoke();
        else
            onEnemyAnimation.Invoke();

        if (animationDuration > 0f)
            yield return new WaitForSeconds(animationDuration);

        BattleResolveResult result = BattleResolver.ApplyDamage(actor, target, skill);

        RefreshState();

        if (skill == null || skill.Damage > 0)
        {
            if (result.Effectiveness == 0f)
                yield return ShowText("효과가 없는 듯하다...");
            else if (result.Effectiveness > 1f)
                yield return ShowText("효과가 굉장했다!");
            else if (result.Effectiveness < 1f)
                yield return ShowText("효과가 별로인 듯하다...");

            if (result.Critical)
                yield return ShowText("급소에 맞았다!");
        }

        string effect = BattleResolver.ApplyEffects(actor, target, skill, result);

        RefreshState();

        if (!string.IsNullOrEmpty(effect))
            yield return ShowText(effect);
    }

    // 기절과 경험치 및 전투 종료 문구를 순서대로 표시한다
    private IEnumerator ShowMessages()
    {
        while (messages.Count > 0)
            yield return ShowText(messages.Dequeue());
    }

    // 전투 문구를 표시하고 설정된 시간만큼 유지한다
    private IEnumerator ShowText(string message)
    {
        if (battleText != null)
            battleText.text = message;

        onMessage.Invoke(message);

        if (textDuration > 0f)
            yield return new WaitForSeconds(textDuration);
    }

    // 규칙 계층에서 발생한 문구를 표시 대기열에 등록한다
    private void QueueMessage(string message)
    {
        messages.Enqueue(message);
    }

    // 현재 HP와 연결된 외부 UI를 갱신한다
    private void RefreshState()
    {
        if (battleSystem == null)
            return;

        SetHp(playerHp, battleSystem.PlayerState);
        SetHp(enemyHp, battleSystem.EnemyState);
        onStateChanged.Invoke();
    }

    // 연결된 HP Slider를 현재 전투 상태로 갱신한다
    private static void SetHp(Slider slider, BattleState state)
    {
        if (slider == null || state == null)
            return;

        slider.maxValue = state.MaxHp;
        slider.value = state.CurrentHp;
    }
}