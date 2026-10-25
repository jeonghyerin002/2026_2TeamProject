using System;
using System.Collections;
using UnityEngine;

public class BattleSceneDirector : MonoBehaviour
{
    [Header("연결")]
    [SerializeField] BattleSystem battleSystem;
    [SerializeField] Transform playerVisual;
    [SerializeField] Transform enemyVisual;

    [Header("플레이어 턴")]
    [SerializeField] float playerTurnMove = 5f;

    [Header("적 턴")]
    [SerializeField] float enemyTurnMove = 5f;

    [Header("공통")]
    [SerializeField] float focusScale = 1.5f;
    [SerializeField] float otherScale = 0.5f;
    [SerializeField, Min(0.01f)] float turnDuration = 0.5f;

    [Header("피격")]
    [SerializeField] float hitFirstMove = -0.5f;
    [SerializeField] float hitSecondMove = 0.7f;
    [SerializeField, Min(0.01f)] float hitStepDuration = 0.06f;

    [Header("기절")]
    [SerializeField, Range(0f, 1f)] float blinkAlpha = 0.5f;
    [SerializeField, Min(1)] int blinkCount = 2;
    [SerializeField, Min(0.01f)] float blinkHold = 0.1f;
    [SerializeField, Min(0.01f)] float fadeDuration = 0.5f;


    Actor player;
    Actor enemy;
    Coroutine turnRoutine;

    private void Awake()
    {
        if (player != null)
        {
            player = new Actor(playerVisual);
        }
        if (enemy != null)
        {
            enemy = new Actor(enemyVisual);
        }
    }

    private void OnEnable()
    {
        if (battleSystem != null)
        {
            battleSystem.StateChanged += OnStateChanged;
        }
    }

    private void OnDisable()
    {
        if (battleSystem != null)
        {
            battleSystem.StateChanged -= OnStateChanged;
        }

        StopAllCoroutines();
        turnRoutine = null;
        player?.Clear();
        enemy?.Clear();
    }

    public void OnStateChanged()
    {
        if (battleSystem == null)
        {
            return;
        }
        Track(player, battleSystem.PlayerState, true);
        Track(enemy, battleSystem.EnemyState, false);
    }

    //플레이어 턴 연출
    public void PlayPlayerTurn()
    {
        PlayTurn(player, enemy, playerTurnMove);
    }

    //적 턴 연출
    public void PlayEnemyTurn()
    {
        PlayTurn(enemy, player, enemyTurnMove);
    }

    //피격 연출
    public void PlayHit(bool targetIsPlayer)
    {
        Actor actor = targetIsPlayer ? player : enemy;
        if (actor == null || !isActiveAndEnabled)
            return;

        if (actor.HitRoutine != null)
            StopCoroutine(actor.HitRoutine);

        actor.HitRoutine = StartCoroutine(HitRoutine(actor));
    }

    //기절 연출
    public void PlayFaint(bool targetIsPlayer)
    {
        Actor actor = targetIsPlayer ? player : enemy;
        if (actor == null || !isActiveAndEnabled)
            return;

        if (actor.FaintRoutine != null)
            StopCoroutine(actor.FaintRoutine);

        actor.FaintRoutine = StartCoroutine(FaintRoutine(actor));
    }

    //PP 소비는 행동 시작, HP 감소는 피격, HP 0은 기절로 판단한다.
    private void Track(Actor actor, BattleState state, bool isPlayer)
    {
        if (actor == null || state == null)
            return;

        int hp = state.CurrentHp;
        int pp = GetTotalPp(state);


        if (actor.State != state || actor.Aether != state.Aether)
        {
            if (actor.State != state)
                ResetActor(actor);

            actor.State = state;
            actor.Aether = state.Aether;
            actor.Hp = hp;
            actor.Pp = pp;
            return;
        }

        if (pp < actor.Pp)
        {
            if (isPlayer)
                PlayPlayerTurn();
            else
                PlayEnemyTurn();
        }

        if (hp < actor.Hp)
        {
            PlayHit(isPlayer);

            if (hp <= 0)
                PlayFaint(isPlayer);
        }

        actor.Hp = hp;
        actor.Pp = pp;
    }

    //현재 장착 기술의 PP 합계를 반환한다.
    private static int GetTotalPp(BattleState state)
    {
        int total = 0;
        for (int i = 0; i < state.SkillCount; i++)
            total += state.GetCurrentPp(state.GetSkill(i));
        return total;
    }

    //이전 턴 연출을 정리하고 새 턴 연출을 시작한다.

    private void PlayTurn(Actor focus, Actor other, float move)
    {
        if (!isActiveAndEnabled)
            return;

        if (turnRoutine != null)
            StopCoroutine(turnRoutine);

        player?.SetTurn(Vector3.zero, 1f);
        enemy?.SetTurn(Vector3.zero, 1f);
        turnRoutine = StartCoroutine(TurnRoutine(focus, other, move));
    }

    //행동하는 쪽은 좌측 이동과 확대, 상대는 우측 이동과 축소 후 함께 복귀한다.
    private IEnumerator TurnRoutine(Actor focus, Actor other, float move)
    {
        float half = turnDuration * 0.5f;

        yield return Tween(half, t => SetTurn(focus, other, move, t));
        yield return Tween(half, t => SetTurn(focus, other, move, 1f - t));

        turnRoutine = null;
    }

    //진행도에 맞춰 양쪽의 턴 연출 값을 적용한다.
    private void SetTurn(Actor focus, Actor other, float move, float t)
    {
        focus?.SetTurn(Vector3.left * (move * t), Mathf.Lerp(1f, focusScale, t));
        other?.SetTurn(Vector3.right * (move * t), Mathf.Lerp(1f, otherScale, t));
    }

    // -0.5 이동 후 0.7 이동하고 원래 자리로 돌아온다
    private IEnumerator HitRoutine(Actor actor)
    {
        float first = hitFirstMove;
        float second = hitFirstMove + hitSecondMove;

        yield return Tween(hitStepDuration, t => actor.SetHit(Mathf.Lerp(0f, first, t)));
        yield return Tween(hitStepDuration, t => actor.SetHit(Mathf.Lerp(first, second, t)));
        yield return Tween(hitStepDuration, t => actor.SetHit(Mathf.Lerp(second, 0f, t)));

        actor.HitRoutine = null;
    }

    //피격 연출 후 투명도 50/100으로 반복하고 점점 사라진다.
    private IEnumerator FaintRoutine(Actor actor)
    {
        while (actor.HitRoutine != null)
            yield return null;

        WaitForSeconds hold = new WaitForSeconds(blinkHold);
        for (int i = 0; i < blinkCount; i++)
        {
            actor.SetAlpha(blinkAlpha);
            yield return hold;
            actor.SetAlpha(1f);
            yield return hold;
        }

        yield return Tween(fadeDuration, t => actor.SetAlpha(1f - t), false);

        actor.FaintRoutine = null;
    }

    //duration 동안 0에서 1까지 진행도를 전달한다.
    private static IEnumerator Tween(float duration, Action<float> step, bool smooth = true)
    {
        for (float time = 0f; time < duration; time += Time.deltaTime)
        {
            float t = time / duration;
            step(smooth ? Mathf.SmoothStep(0f, 1f, t) : t);
            yield return null;
        }
        step(1f);
    }

    //피격과 기절 연출을 멈추고 모습을 되돌린다
    private void ResetActor(Actor actor)
    {
        if (actor.HitRoutine != null)
        {
            StopCoroutine(actor.HitRoutine);
        }
        if (actor.FaintRoutine != null)
        {
            StopCoroutine(actor.FaintRoutine);
        }

        actor.HitRoutine = null;
        actor.FaintRoutine = null;
        actor.SetHit(0f);
        actor.SetAlpha(1f);
    }

    private sealed class Actor
    {
        private readonly Transform transform;
        private readonly Vector3 basePosition;
        private readonly Vector3 baseScale;
        private readonly CanvasGroup canvasGroup;
        private Vector3 turnOffset;
        private float turnScale = 1f;
        private float hitOffset;

        public Coroutine HitRoutine;
        public Coroutine FaintRoutine;
        public BattleState State;
        public AetherData Aether;
        public int Hp;
        public int Pp;

        //원래 위치와 크기를  저장하고 투명도 조정용 CanvasGroup를 가져온다.
         public Actor(Transform target)
         {
            transform = target;
            basePosition = transform.localPosition;
            baseScale = transform.localScale;

            canvasGroup = target.GetComponent<CanvasGroup>();

            if (canvasGroup == null)
            {
                canvasGroup = target.gameObject.AddComponent<CanvasGroup>();
            }
         }

        //턴 연출의 이동량과 배율을 적용한다.
        public void SetTurn(Vector3 offset, float scale)
        {
            turnOffset = offset;
            turnScale = scale;
            Apply();
        }

        //피격 연출의 이동량을 적용한다.
        public void SetHit(float x)
        {
            hitOffset = x;
            Apply();
        }

        //이미지 전체의 투명도를 적용한다.

        public void SetAlpha(float alpha)
        {
            canvasGroup.alpha = alpha;
        }

        //연출 값과 추적 정보를 모두 초기화 한다
        public void Clear()
        {
            HitRoutine = null;
            FaintRoutine = null;
            State = null;
            Aether = null;
            turnOffset = Vector3.zero;
            turnScale = 1f;
            hitOffset = 0f;
            Apply();
            SetAlpha(1f);
        }

        //원래 값에 턴 연출과 피격 연출을 합성한다.
        private void Apply()
        {
            if (transform == null)
                return;

            transform.localPosition = basePosition + turnOffset + Vector3.right * hitOffset;
            transform.localScale = baseScale * turnScale;
        }
    }

    void Start()
    {
        
    }


    void Update()
    {
        
    }
}
