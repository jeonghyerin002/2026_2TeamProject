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
    [SerializeField] private BattleSkillPresenter skillPresenter; // 이미지와 효과음의 재생 담당
    [SerializeField] private TMP_Text battleText;
    [SerializeField] private Slider playerHp;
    [SerializeField] private Slider enemyHp;
    [SerializeField, Min(0f)] private float textDuration = 1.25f; // 양측 전투 문구의 공통 표시 시간(초)
    [SerializeField, Min(0f)] private float attackDelay = 0.2f; // 대화창이 사라진 뒤 양측 공격 연출을 시작하기까지의 시간(초)
    [SerializeField, Min(0f)] private float animationDuration = 0.5f;
    [SerializeField, Min(0f)] private float enemyHudHoldDuration = 0.7f; // 플레이어 타격 후 적 HP를 확인할 유지 시간(초)
    [SerializeField, Min(0f)] private float playerHudHoldDuration = 0.7f; // NPC 타격 후 플레이어 HP를 확인할 유지 시간(초)
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
    public bool IsShowingMessage { get; private set; } // 문구 표시 시간 동안만 대화창을 활성화하기 위한 상태
    public BattleSide? ActingSide { get; private set; } // 현재 연출 중인 행동 주체이며 행동 시작 전에는 null

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

        if (skillPresenter != null)
            skillPresenter.Cancel();

        turnRoutine = null;
        running = false;
        ActingSide = null;
        IsShowingMessage = false;

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

        if (skillPresenter != null)
            skillPresenter.Cancel();

        turnRoutine = null;
        running = false;
        ActingSide = null;
        IsShowingMessage = false;
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
        ActingSide = null;

        RefreshState();

        // 최종 승패 문구까지 모두 표시된 후 종료 연출 완료를 알린다
        if (battleSystem.Phase == TurnPhase.Ended)
            PresentationEnded?.Invoke();
    }

    // 행동 가능 여부부터 피해와 부가효과까지 순서대로 처리한다
    private IEnumerator ResolveAction(BattleState actor, BattleState target, SkillData skill)
    {
        // HP와 PP가 바뀌기 전에 행동 주체를 정해 피격 대상의 HUD로 전환한다.
        ActingSide = actor == battleSystem.PlayerState ? BattleSide.Player : BattleSide.Enemy;
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

        // 대화창이 사라진 뒤 짧은 간격을 두고 양측 공격 연출을 시작한다.
        yield return new WaitForSeconds(attackDelay);

        if (!BattleResolver.IsHit(skill))
        {
            yield return ShowText("그러나 공격은 빗나갔다!");
            yield break;
        }

        BattleResolveResult result = default;
        string effect = null;
        if (skillPresenter != null)
        {
            yield return skillPresenter.Play(actor == battleSystem.PlayerState,
                skill != null ? skill.Presentation : null, animationDuration, () =>
                {
                    result = BattleResolver.ApplyDamage(actor, target, skill);
                    effect = BattleResolver.ApplyEffects(actor, target, skill, result);
                    RefreshState();
                    return result;
                }, battleSystem.PlayerState.Character, battleSystem.EnemyState.Character,
                skill != null ? skill.Type : ElementType.Normal);

            if (!skillPresenter.Completed)
            {
                battleSystem.StopBattle();
                yield break;
            }
        }
        else
        {
            // 재생 담당을 연결하지 않은 기존 씬은 이전 이벤트 경로를 유지한다.
            AnimationRequested?.Invoke(actor, target, skill);

            if (actor == battleSystem.PlayerState)
                onPlayerAnimation.Invoke();
            else
                onEnemyAnimation.Invoke();

            if (animationDuration > 0f)
                yield return new WaitForSeconds(animationDuration);

            result = BattleResolver.ApplyDamage(actor, target, skill);
            effect = BattleResolver.ApplyEffects(actor, target, skill, result);
        }

        RefreshState();

        // 다음 행동에서 HUD가 전환되기 전에 피격 대상의 감소한 HP를 읽을 시간을 확보한다.
        float hudHoldDuration = actor == battleSystem.PlayerState ? enemyHudHoldDuration : playerHudHoldDuration;
        if (result.Damage > 0 && hudHoldDuration > 0f)
            yield return new WaitForSeconds(hudHoldDuration);

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

        if (!string.IsNullOrEmpty(effect))
            yield return ShowText(effect);
    }

    // 기절과 경험치 및 전투 종료 문구를 순서대로 표시한다
    private IEnumerator ShowMessages()
    {
        while (messages.Count > 0)
            yield return ShowText(messages.Dequeue());
    }

    // 전투 문구를 설정된 시간만큼 표시한 뒤 다음 공격 연출 전에 대화창을 숨긴다
    private IEnumerator ShowText(string message)
    {
        if (battleText != null)
            battleText.text = message;

        IsShowingMessage = true;
        RefreshState();
        onMessage.Invoke(message);

        if (textDuration > 0f)
            yield return new WaitForSeconds(textDuration);

        IsShowingMessage = false;
        RefreshState();
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
