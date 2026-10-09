using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>2D 트리거로 시작하며 대사 입력과 한 축 이동을 순서대로 실행한다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class TriggerCutscene : MonoBehaviour
{
    public enum StepType
    {
        SetTarget = 0,
        Dialogue = 1,
        Move = 2
    }

    public enum MoveAxis
    {
        X = 0,
        Y = 1
    }

    [Serializable]
    public sealed class Step
    {
        public StepType type;
        [Tooltip("Hierarchy의 이동할 GameObject를 지정합니다. 비워 두면 트리거에 들어온 플레이어를 선택합니다. Rigidbody2D가 있으면 그 루트 오브젝트를 지정하세요.")]
        public GameObject target;
        [TextArea(3, 6)] public string text;
        public MoveAxis axis;
        [Tooltip("이 이동 단계 시작 시의 위치에서 선택한 월드 축으로 이동할 거리입니다. 양수는 오른쪽/위쪽, 음수는 왼쪽/아래쪽이며 다른 축과 Z는 유지합니다.")]
        public float coordinate;
        [Min(0.01f)] public float speed = 2f;
    }

    private sealed class ActorState
    {
        public GameObject target;
        public Transform actorTransform;
        public Rigidbody2D body;
        public RigidbodyType2D bodyType;
        public RigidbodyConstraints2D constraints;
        public PlayerController player;
        public bool playerEnabled;
        public bool movementEnabled;
    }

    [Header("Trigger")]
    [SerializeField] private LayerMask triggeringLayers = ~0;
    [SerializeField] private bool playOnce = true;

    [Header("Dialogue UI")]
    [SerializeField] private DialogManager dialogManager;

    [Header("Ordered Steps")]
    [SerializeField] private List<Step> steps = new List<Step>();

    private readonly List<ActorState> actors = new List<ActorState>();
    private DialogManager activeDialogManager;
    private ActorState currentActor;
    private int stepIndex;
    private Vector3 moveDestination;
    private float moveSpeed;
    private MoveAxis movingAxis;
    private bool isPlaying;
    private bool isWaitingForDialogue;
    private bool isMoving;
    private bool hasPlayed;

    public bool IsPlaying => isPlaying;

    private void OnEnable()
    {
        Collider2D trigger = GetComponent<Collider2D>();
        if (trigger == null || !trigger.isTrigger || dialogManager == null || steps == null || steps.Count == 0)
        {
            Fail("Is Trigger를 켠 Collider2D, Dialog Manager와 한 개 이상의 단계를 설정하세요.");
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isActiveAndEnabled || isPlaying || (playOnce && hasPlayed) ||
            (triggeringLayers.value & (1 << other.gameObject.layer)) == 0)
        {
            return;
        }

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null || !player.isActiveAndEnabled)
        {
            return;
        }

        TryPlay(player);
    }

    private void Update()
    {
        if (!isPlaying)
        {
            return;
        }

        if (activeDialogManager == null || !activeDialogManager.IsCutsceneOwner(this))
        {
            StopCutscene();
            return;
        }

        if (isWaitingForDialogue)
        {
            if (activeDialogManager.HasCutsceneMessage(this))
            {
                return;
            }

            isWaitingForDialogue = false;
            stepIndex++;
        }

        if (isMoving)
        {
            if (!IsActorAvailable(currentActor))
            {
                Fail("이동 대상이 파괴되었거나 비활성화되었습니다.");
                return;
            }

            // Rigidbody2D 이동은 FixedUpdate에서만 처리한다.
            if (currentActor.body == null)
            {
                Vector3 position = currentActor.actorTransform.position;
                float coordinate = movingAxis == MoveAxis.X ? position.x : position.y;
                float destination = movingAxis == MoveAxis.X ? moveDestination.x : moveDestination.y;
                coordinate = Mathf.MoveTowards(coordinate, destination, moveSpeed * Time.deltaTime);
                if (movingAxis == MoveAxis.X)
                {
                    position.x = coordinate;
                }
                else
                {
                    position.y = coordinate;
                }
                currentActor.actorTransform.position = position;
                if (coordinate == destination)
                {
                    isMoving = false;
                    stepIndex++;
                }
            }

            if (isMoving)
            {
                return;
            }
        }

        RunNextSteps();
    }

    private void FixedUpdate()
    {
        if (!isPlaying || !isMoving || currentActor == null || currentActor.body == null)
        {
            return;
        }

        Rigidbody2D body = currentActor.body;
        Vector2 destination = moveDestination;
        if (body.position == destination)
        {
            body.linearVelocity = Vector2.zero;
            isMoving = false;
            stepIndex++;
            return;
        }

        body.MovePosition(Vector2.MoveTowards(body.position, destination, moveSpeed * Time.fixedDeltaTime));
    }

    private void OnDisable()
    {
        StopCutscene();
    }

    /// <summary>중단 시 현재 위치를 유지하고 대화 예약과 플레이어/물리 제어를 반환한다.</summary>
    public void StopCutscene()
    {
        isPlaying = false;
        isMoving = false;
        isWaitingForDialogue = false;
        currentActor = null;

        if (activeDialogManager != null)
        {
            activeDialogManager.EndCutscene(this);
        }
        activeDialogManager = null;

        foreach (ActorState actor in actors)
        {
            if (actor.body != null)
            {
                actor.body.linearVelocity = Vector2.zero;
                actor.body.angularVelocity = 0f;
                actor.body.constraints = actor.constraints;
                actor.body.bodyType = actor.bodyType;
            }

            if (actor.player != null)
            {
                actor.player.SetMovementEnabled(actor.movementEnabled);
                actor.player.enabled = actor.playerEnabled;
            }
        }
        actors.Clear();
    }

    private void TryPlay(PlayerController player)
    {
        if (dialogManager == null || !dialogManager.CanShowMessage)
        {
            // 다른 대화/컷신 중이면 소비하지 않는다. 다시 들어오면 재시도한다.
            return;
        }

        if (!ValidateSteps(player))
        {
            return;
        }

        activeDialogManager = dialogManager;
        if (!activeDialogManager.TryBeginCutscene(this))
        {
            activeDialogManager = null;
            return;
        }

        isPlaying = true;
        stepIndex = 0;
        HoldActor(player.gameObject);
        foreach (Step step in steps)
        {
            if (step.type == StepType.SetTarget)
            {
                HoldActor(step.target != null ? step.target : player.gameObject);
            }
        }

        // 첫 대상 선택 전의 이동은 진입한 플레이어에게 적용한다.
        currentActor = actors[0];
        RunNextSteps();
    }

    private bool ValidateSteps(PlayerController player)
    {
        if (Physics2D.simulationMode != SimulationMode2D.FixedUpdate)
        {
            Fail("이 컷신은 Physics2D Simulation Mode가 Fixed Update일 때 사용하세요.");
            return false;
        }

        if (!ValidateTarget(player.gameObject))
        {
            return false;
        }

        for (int i = 0; i < steps.Count; i++)
        {
            Step step = steps[i];
            if (step == null || !Enum.IsDefined(typeof(StepType), step.type))
            {
                Fail($"단계 {i + 1}: 종류를 올바르게 지정하세요.");
                return false;
            }

            if (step.type == StepType.SetTarget &&
                !ValidateTarget(step.target != null ? step.target : player.gameObject))
            {
                return false;
            }

            if (step.type == StepType.Dialogue && string.IsNullOrWhiteSpace(step.text))
            {
                Fail($"단계 {i + 1}: 출력할 대사를 입력하세요.");
                return false;
            }

            if (step.type == StepType.Move &&
                (!Enum.IsDefined(typeof(MoveAxis), step.axis) || !IsFinite(step.coordinate) ||
                !IsFinite(step.speed) || step.speed <= 0f))
            {
                Fail($"단계 {i + 1}: X/Y 축, 유효한 이동 거리와 0보다 큰 속도를 지정하세요.");
                return false;
            }
        }
        return true;
    }

    private bool ValidateTarget(GameObject target)
    {
        if (target == null || !target.scene.IsValid() || !target.scene.isLoaded || !target.activeInHierarchy)
        {
            Fail("Hierarchy에서 로드된 씬의 활성화된 이동 대상 GameObject를 지정하세요.");
            return false;
        }

        Rigidbody2D body = target.GetComponentInParent<Rigidbody2D>();
        Rigidbody2D[] childBodies = target.GetComponentsInChildren<Rigidbody2D>(true);
        if (target.GetComponentInParent<Rigidbody>() != null || target.GetComponentInChildren<Rigidbody>(true) != null ||
            childBodies.Length > (body != null && body.gameObject == target ? 1 : 0) ||
            (body != null && (body.gameObject != target || !body.simulated)))
        {
            Fail("3D Rigidbody는 지원하지 않습니다. 2D 물리 대상은 Simulated를 켠 Rigidbody2D 루트를 지정하세요.");
            return false;
        }
        return true;
    }

    private void HoldActor(GameObject target)
    {
        foreach (ActorState actor in actors)
        {
            if (actor.target == target)
            {
                return;
            }
        }

        var state = new ActorState
        {
            target = target,
            actorTransform = target.transform,
            body = target.GetComponent<Rigidbody2D>(),
            player = target.GetComponent<PlayerController>()
        };

        if (state.player != null)
        {
            state.playerEnabled = state.player.enabled;
            state.movementEnabled = state.player.IsMovementEnabled;
            state.player.SetMovementEnabled(false);
            state.player.enabled = false;
        }

        if (state.body != null)
        {
            state.bodyType = state.body.bodyType;
            state.constraints = state.body.constraints;
            // 컷신이 경로를 소유한다. 중력/충돌 반응에 의한 대각선 이동을 방지한다.
            state.body.bodyType = RigidbodyType2D.Kinematic;
            state.body.constraints = RigidbodyConstraints2D.FreezeRotation;
            state.body.linearVelocity = Vector2.zero;
            state.body.angularVelocity = 0f;
        }

        actors.Add(state);
    }

    private void RunNextSteps()
    {
        while (isPlaying && stepIndex < steps.Count)
        {
            Step step = steps[stepIndex];
            if (step.type == StepType.SetTarget)
            {
                // 비어 있는 설정과 실행 중 파괴된 Unity 참조를 구분한다.
                if (ReferenceEquals(step.target, null))
                {
                    currentActor = actors[0];
                }
                else
                {
                    if (step.target == null)
                    {
                        Fail($"단계 {stepIndex + 1}: 이동 대상 참조가 파괴되었거나 누락되었습니다.");
                        return;
                    }

                    // Unity 오브젝트는 동일한 엔진 객체를 나타내는 관리 참조가 다를 수 있다.
                    currentActor = actors.Find(actor => actor.target != null && actor.target == step.target);
                }

                if (currentActor == null)
                {
                    Fail($"단계 {stepIndex + 1}: 선택한 대상을 컷신 시작 시 확보한 목록에서 찾을 수 없습니다. 실행 중 대상 변경 여부를 확인하세요.");
                    return;
                }
                if (currentActor.target == null || currentActor.actorTransform == null)
                {
                    Fail($"단계 {stepIndex + 1}: 선택한 이동 대상이 파괴되었습니다.");
                    return;
                }
                if (!currentActor.target.activeInHierarchy)
                {
                    Fail($"단계 {stepIndex + 1}: 이동 대상 '{currentActor.target.name}'이 비활성화되었습니다. 대상과 부모 오브젝트의 활성 상태를 확인하세요.");
                    return;
                }
                if (!IsActorAvailable(currentActor))
                {
                    Fail($"단계 {stepIndex + 1}: 이동 대상 '{currentActor.target.name}'의 Rigidbody2D가 제거되었거나 Simulated가 꺼졌습니다.");
                    return;
                }
                stepIndex++;
                continue;
            }

            if (step.type == StepType.Dialogue)
            {
                if (!activeDialogManager.TryShowCutsceneMessage(this, step.text))
                {
                    Fail($"단계 {stepIndex + 1}: 대화 UI를 사용할 수 없습니다.");
                    return;
                }

                isWaitingForDialogue = true;
                return;
            }

            if (!IsActorAvailable(currentActor))
            {
                Fail($"단계 {stepIndex + 1}: 이동 대상을 사용할 수 없습니다.");
                return;
            }

            moveDestination = currentActor.actorTransform.position;
            if (currentActor.body != null)
            {
                Vector2 position = currentActor.body.position;
                moveDestination.x = position.x;
                moveDestination.y = position.y;
            }

            if (step.axis == MoveAxis.X)
            {
                moveDestination.x += step.coordinate;
            }
            else
            {
                moveDestination.y += step.coordinate;
            }

            moveSpeed = step.speed;
            movingAxis = step.axis;
            isMoving = true;
            return;
        }

        if (isPlaying)
        {
            hasPlayed = true;
            StopCutscene();
        }
    }

    private static bool IsActorAvailable(ActorState actor)
    {
        return actor != null && actor.target != null && actor.actorTransform != null && actor.target.activeInHierarchy &&
            (ReferenceEquals(actor.body, null) || (actor.body != null && actor.body.simulated));
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private void Fail(string message)
    {
        Debug.LogError($"TriggerCutscene: {message}", this);
        StopCutscene();
        enabled = false;
    }
}
