using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>2D 트리거로 시작하는 대사, 한 축 이동 및 NPC ID 기반 전투 실행</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class TriggerCutscene : MonoBehaviour
{
    public enum StepType
    {
        SetTarget = 0,
        Dialogue = 1,
        Move = 2,
        [InspectorName("강제 전투")] Battle = 3
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

        // 이동 목적지 또는 마지막 전투 단계의 NPC 식별 설정
        [Tooltip("Move에서 입력하면 Set Target과 무관하게 플레이어가 NPC 앞으로 X축 후 Y축으로 이동합니다. 비우면 기존 거리 이동을 사용합니다. 강제 전투에서는 전투할 NPC Id입니다.")]
        public string npcId;
        [Tooltip("NPC 위치를 기준으로 한 월드 좌표 간격입니다. 기본값은 NPC 아래쪽 1 유닛입니다.")]
        public Vector2 npcFrontOffset = Vector2.down;

        // Scene 뷰에서 그린 월드 좌표 경유점
        public bool useScenePath;
        [Tooltip("현재 위치에서 각 경유점까지 X축 후 Y축으로 이동합니다. NPC Id가 있으면 마지막에 NPC 앞까지 이동합니다.")]
        public List<Vector2> pathPoints = new List<Vector2>();
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
        public Animator animator;
        public bool hasMoveX;
        public bool hasMoveY;
        public bool hasIsMoving;
        public bool hasWalkSpeed;
    }

    [Header("Trigger")]
    [SerializeField] private LayerMask triggeringLayers = ~0;
    [SerializeField] private bool playOnce = true;

    // 일회성 컷신의 완료 상태를 저장할 때 사용하는 자동 생성 고유 ID.
    [SerializeField] private string persistentId;
    // 복제한 컷신에 원본의 저장 ID가 남지 않도록 소유 오브젝트를 추적한다.
    [SerializeField, HideInInspector] private string saveIdentityOwner;

    [Header("Dialogue UI")]
    [SerializeField] private DialogManager dialogManager;

    [Header("Ordered Steps")]
    [SerializeField] private List<Step> steps = new List<Step>();

    private readonly List<ActorState> actors = new List<ActorState>();
    // 시작 시 확정한 대상 선택으로 직렬화된 빈 참조와 실행 중 파괴 구분
    private readonly Dictionary<Step, ActorState> stepActors = new Dictionary<Step, ActorState>();
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

    // NPC 앞으로 이동할 때의 두 번째 축 목적지와 유효 대상 보관
    private Vector3 npcMoveDestination;
    private NPCInteraction moveNpc;
    private bool isNpcMove;
    private bool hasRemainingNpcAxis;

    // 현재 이동 단계의 경유점 스냅샷과 다음 목적지
    private readonly List<Vector3> movePath = new List<Vector3>();
    private int nextPathPoint;

    // 마우스로 배치한 경유점의 미세한 축 오차 허용 범위
    private const float PathAlignmentTolerance = 0.05f;

    public bool IsPlaying => isPlaying;

#if UNITY_EDITOR
    // OnValidate에서는 Unity 오브젝트를 변경하지 않고 자체 ID 값만 초기화한다.
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(persistentId))
        {
            persistentId = Guid.NewGuid().ToString("N");
        }
    }
#endif

    private void OnEnable()
    {
        // 저장 상태는 Scene 로딩 전에 준비되므로 재생 여부를 먼저 복원한다.
        hasPlayed = WorldSaveProgress.IsCompleted(persistentId);
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
            // 이동 중 NPC가 사라진 경우 전투 없이 컷신 제어 반환
            if (isNpcMove && (moveNpc == null || !moveNpc.isActiveAndEnabled))
            {
                Fail("이동 목적지 NPC가 파괴되었거나 비활성화되었습니다.");
                return;
            }

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
                UpdateMoveAnimation(destination - coordinate);
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
                    FinishMoveAxis();
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
        // 선택한 축만 변경하여 물리 이동에서도 대각선 이동 차단
        Vector2 position = body.position;
        float coordinate = movingAxis == MoveAxis.X ? position.x : position.y;
        float destination = movingAxis == MoveAxis.X ? moveDestination.x : moveDestination.y;
        UpdateMoveAnimation(destination - coordinate);
        if (coordinate == destination)
        {
            body.linearVelocity = Vector2.zero;
            FinishMoveAxis();
            return;
        }

        coordinate = Mathf.MoveTowards(coordinate, destination, moveSpeed * Time.fixedDeltaTime);
        if (movingAxis == MoveAxis.X)
        {
            position.x = coordinate;
        }
        else
        {
            position.y = coordinate;
        }

        body.MovePosition(position);
    }

    private void OnDisable()
    {
        StopCutscene();
    }


    // 중단 시 현재 위치 유지 및 대화 예약과 플레이어 제어 반환
    public void StopCutscene()
    {
        // 일반 중단 시 기존 플레이어 이동 상태 복원
        StopCutscene(false);
    }


    // 컷신 제어 반환 및 전투 전환 중 이동 잠금 유지
    private void StopCutscene(bool keepMovementLocked)
    {
        isPlaying = false;
        isMoving = false;
        isWaitingForDialogue = false;
        currentActor = null;
        moveNpc = null;
        isNpcMove = false;
        hasRemainingNpcAxis = false;
        movePath.Clear();
        nextPathPoint = 0;

        if (activeDialogManager != null)
        {
            activeDialogManager.EndCutscene(this);
        }
        activeDialogManager = null;

        foreach (ActorState actor in actors)
        {
            SetActorAnimation(actor, Vector2.zero);
            if (actor.body != null)
            {
                actor.body.linearVelocity = Vector2.zero;
                actor.body.angularVelocity = 0f;
                actor.body.constraints = actor.constraints;
                actor.body.bodyType = actor.bodyType;
            }

            if (actor.player != null)
            {
                actor.player.SetMovementEnabled(!keepMovementLocked && actor.movementEnabled);
                actor.player.enabled = actor.playerEnabled;
            }
        }
        actors.Clear();
        stepActors.Clear();
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
                // Unity의 빈 참조 판정으로 씬을 넘어온 플레이어를 한 번 선택
                stepActors[step] = HoldActor(step.target != null ? step.target : player.gameObject);
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

            // 빈 경로와 유효하지 않은 좌표를 제어권 확보 전에 검증
            if (step.type == StepType.Move && step.useScenePath)
            {
                if (step.pathPoints == null || step.pathPoints.Count == 0)
                {
                    Fail($"단계 {i + 1}: Scene 경로에 한 개 이상의 이동점을 그리세요.");
                    return false;
                }

                foreach (var point in step.pathPoints)
                {
                    if (!IsFinite(point.x) || !IsFinite(point.y))
                    {
                        Fail($"단계 {i + 1}: 경로 좌표가 유효하지 않습니다.");
                        return false;
                    }
                }
            }

            // NPC 이동 목적지와 간격을 제어권 확보 전에 검증
            if (step.type == StepType.Move && !string.IsNullOrWhiteSpace(step.npcId) &&
                (!IsFinite(step.npcFrontOffset.x) || !IsFinite(step.npcFrontOffset.y) ||
                !NPCInteraction.TryFindById(step.npcId, out _)))
            {
                Fail($"단계 {i + 1}: 유효한 NPC Id와 NPC Front Offset을 지정하세요.");
                return false;
            }

            // 씬 전환 뒤의 단계 유실 방지를 위한 마지막 전투 단계 검증
            if (step.type == StepType.Battle && (string.IsNullOrWhiteSpace(step.npcId) || i != steps.Count - 1))
            {
                Fail($"단계 {i + 1}: 강제 전투에는 NPC Id가 필요하며 마지막 단계에만 배치할 수 있습니다.");
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


    // 이동 제어를 확보한 대상을 재사용하거나 원래 상태와 함께 보관
    private ActorState HoldActor(GameObject target)
    {
        foreach (ActorState actor in actors)
        {
            if (actor.target == target)
            {
                return actor;
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

        // NPC의 기존 애니메이터 파라미터만 사용하고 없는 파라미터는 건너뜀
        state.animator = target.GetComponent<Animator>();
        if (state.player == null && state.animator != null && state.animator.runtimeAnimatorController != null)
        {
            foreach (var parameter in state.animator.parameters)
            {
                state.hasMoveX |= parameter.name == "MoveX" && parameter.type == AnimatorControllerParameterType.Float;
                state.hasMoveY |= parameter.name == "MoveY" && parameter.type == AnimatorControllerParameterType.Float;
                state.hasWalkSpeed |= parameter.name == "WalkSpeed" && parameter.type == AnimatorControllerParameterType.Float;
                state.hasIsMoving |= parameter.name == "IsMoving" && parameter.type == AnimatorControllerParameterType.Bool;
            }
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
        return state;
    }


    // 확보한 대상에 순서대로 대사와 이동 및 전투 단계 적용
    private void RunNextSteps()
    {
        while (isPlaying && stepIndex < steps.Count)
        {
            Step step = steps[stepIndex];
            if (step.type == StepType.SetTarget)
            {
                // 시작 시 확정한 대상을 사용하여 빈 Target의 플레이어 선택 유지
                if (!stepActors.TryGetValue(step, out currentActor) || currentActor == null)
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

            // 컷신 대화 예약 해제 후 NPC ID로 전투 요청
            if (step.type == StepType.Battle)
            {
                activeDialogManager.EndCutscene(this);
                activeDialogManager = null;
                if (!NPCInteraction.TryStartForcedBattle(step.npcId))
                {
                    Debug.LogWarning($"TriggerCutscene: NPC Id '{step.npcId}'의 전투 진입에 실패했습니다. 컷신 완료를 기록하지 않습니다.", this);
                    StopCutscene();
                    return;
                }

                // 전환 수락 시에만 완료 저장 및 플레이어 이동 잠금 유지
                CompleteCutscene(true);
                return;
            }

            // NPC ID 이동은 이전 Set Target과 무관하게 진입한 플레이어 사용
            if (!string.IsNullOrWhiteSpace(step.npcId))
            {
                currentActor = actors[0];
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

            // 편집 중 경로 변경과 실행을 분리하기 위한 월드 좌표 복사
            movePath.Clear();
            nextPathPoint = 0;
            moveSpeed = step.speed;
            if (step.useScenePath)
            {
                foreach (var point in step.pathPoints)
                {
                    movePath.Add(new Vector3(point.x, point.y, moveDestination.z));
                }
            }

            // NPC 앞의 월드 위치를 스냅샷으로 잡고 X축 목적지만 먼저 적용
            if (!string.IsNullOrWhiteSpace(step.npcId))
            {
                if (!NPCInteraction.TryFindById(step.npcId, out moveNpc))
                {
                    Fail($"단계 {stepIndex + 1}: 이동 목적지 NPC를 찾을 수 없습니다.");
                    return;
                }

                npcMoveDestination = moveDestination;
                npcMoveDestination.x = moveNpc.transform.position.x + step.npcFrontOffset.x;
                npcMoveDestination.y = moveNpc.transform.position.y + step.npcFrontOffset.y;
                movePath.Add(npcMoveDestination);
                isNpcMove = true;
            }
            else if (!step.useScenePath && step.axis == MoveAxis.X)
            {
                moveDestination.x += step.coordinate;
            }
            else if (!step.useScenePath)
            {
                moveDestination.y += step.coordinate;
            }

            movingAxis = step.axis;
            isMoving = true;
            if (movePath.Count > 0)
            {
                BeginNextPathPoint();
            }
            return;
        }

        if (isPlaying)
        {
            CompleteCutscene(false);
        }
    }


    // X축 도착 뒤 Y축 이동 또는 현재 이동 단계 완료
    private void FinishMoveAxis()
    {
        // 두 좌표의 동시 변경 없이 다음 축으로 전환
        if (hasRemainingNpcAxis)
        {
            hasRemainingNpcAxis = false;
            movingAxis = MoveAxis.Y;
            moveDestination = npcMoveDestination;
            return;
        }

        // 경유점의 두 축 이동 완료 후 다음 경유점으로 진행
        if (nextPathPoint < movePath.Count)
        {
            BeginNextPathPoint();
            return;
        }

        // 마지막 축 도착 이후에만 대사 또는 전투 단계 진행
        SetActorAnimation(currentActor, Vector2.zero);
        moveNpc = null;
        isNpcMove = false;
        isMoving = false;
        stepIndex++;
    }


    // 미세한 좌표 차이를 정렬하고 실제 이동이 있는 축부터 시작
    private void BeginNextPathPoint()
    {
        // 현재 물리 좌표를 기준으로 빈 구간을 같은 호출에서 건너뜀
        Vector3 position = currentActor.actorTransform.position;
        if (currentActor.body != null)
        {
            position.x = currentActor.body.position.x;
            position.y = currentActor.body.position.y;
        }

        while (nextPathPoint < movePath.Count)
        {
            Vector2 destination = AlignPathPoint(position, movePath[nextPathPoint++]);
            npcMoveDestination = new Vector3(destination.x, destination.y, position.z);
            bool hasHorizontalMove = destination.x != position.x;
            bool hasVerticalMove = destination.y != position.y;
            if (!hasHorizontalMove && !hasVerticalMove)
            {
                continue;
            }

            movingAxis = hasHorizontalMove ? MoveAxis.X : MoveAxis.Y;
            hasRemainingNpcAxis = hasHorizontalMove && hasVerticalMove;
            moveDestination = hasHorizontalMove
                ? new Vector3(destination.x, position.y, position.z)
                : npcMoveDestination;
            return;
        }

        // 중복점만 남았으면 중간 방향 변경 없이 이동 단계 완료
        FinishMoveAxis();
    }


    // 같은 가로선 또는 세로선으로 의도한 경유점의 미세한 차이 정렬
    public static Vector2 AlignPathPoint(Vector2 start, Vector2 destination)
    {
        // 편집 화면과 실제 이동에서 동일한 정렬 기준 사용
        if (Mathf.Abs(destination.x - start.x) <= PathAlignmentTolerance)
        {
            destination.x = start.x;
        }

        if (Mathf.Abs(destination.y - start.y) <= PathAlignmentTolerance)
        {
            destination.y = start.y;
        }

        return destination;
    }


    // 현재 단일 축의 이동 방향을 걷기 애니메이션으로 전달
    private void UpdateMoveAnimation(float remainingDistance)
    {
        // 길이가 없는 축과 중간 경유점에서는 이전 이동 방향 유지
        if (remainingDistance == 0f)
        {
            return;
        }

        Vector2 direction = movingAxis == MoveAxis.X
            ? new Vector2(Mathf.Sign(remainingDistance), 0f)
            : new Vector2(0f, Mathf.Sign(remainingDistance));
        SetActorAnimation(currentActor, direction);
    }


    // 플레이어의 기존 방향 상태 또는 NPC 애니메이터 파라미터 갱신
    private void SetActorAnimation(ActorState actor, Vector2 direction)
    {
        if (actor.player != null)
        {
            actor.player.SetCutsceneAnimation(direction, moveSpeed);
            return;
        }

        // 애니메이터가 없는 NPC의 기존 이동 동작 유지
        var animator = actor.animator;
        if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null)
        {
            return;
        }

        if (direction != Vector2.zero)
        {
            if (actor.hasMoveX)
            {
                animator.SetFloat("MoveX", direction.x);
            }

            if (actor.hasMoveY)
            {
                animator.SetFloat("MoveY", direction.y);
            }
        }

        if (actor.hasWalkSpeed)
        {
            animator.SetFloat("WalkSpeed", 1f);
        }

        if (actor.hasIsMoving)
        {
            animator.SetBool("IsMoving", direction != Vector2.zero);
        }
    }


    // 정상 종료 또는 전투 전환 수락 시 컷신 완료 상태 저장
    private void CompleteCutscene(bool keepMovementLocked)
    {
        // 일회성 재생 기록과 플레이어 이외 대상의 최종 위치 저장
        hasPlayed = true;
        if (playOnce)
        {
            WorldSaveProgress.Complete(persistentId);
        }

        foreach (var actor in actors)
        {
            if (actor.player == null && actor.target != null &&
                actor.target.TryGetComponent<PersistentWorldActor>(out var persistentActor))
            {
                persistentActor.RecordPosition();
            }
        }

        StopCutscene(keepMovementLocked);
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
