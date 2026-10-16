using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>플레이어 이동과 Scene 간 이동 가능 상태를 관리한다</summary>
[RequireComponent(typeof(Rigidbody2D))]
[DisallowMultipleComponent]
[DefaultExecutionOrder(-1000)]
public class PlayerController : MonoBehaviour
{
    public static PlayerController Instance { get; private set; }

    [Header("Movement Settings")]
    public float moveSpeed = 2f;

    private Rigidbody2D rb;
    private Vector2 moveInput;
    private bool isFacingRight = true;
    private bool isFasted;
    private bool movementEnabled = true;
    private Animator animator;
    private bool hasDirectionalAnimation;
    private bool isWalking;
    private Vector2 animationDirection = Vector2.down;

    private static readonly int MoveXId = Animator.StringToHash("MoveX");
    private static readonly int MoveYId = Animator.StringToHash("MoveY");
    private static readonly int IsMovingId = Animator.StringToHash("IsMoving");
    private static readonly int WalkSpeedId = Animator.StringToHash("WalkSpeed");
    private static readonly int IdleStateId = Animator.StringToHash("Base Layer.Idle");
    private static readonly int WalkStateId = Animator.StringToHash("Base Layer.Walk");

    public bool IsMovementEnabled => movementEnabled;

    // 저장 및 복원에서 사용하는 현재 좌우 방향.
    public bool IsFacingRight => isFacingRight;

    // 싱글톤과 물리 참조를 준비하고 Scene 이동 후에도 유지한다
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        if (Instance != null && Instance != this)
        {
            // 지연 파괴되는 동안 자식 카메라와 입력이 중복 실행되지 않게 한다.
            gameObject.SetActive(false);
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        hasDirectionalAnimation = false;
        if (!TryGetComponent(out animator) || animator.runtimeAnimatorController == null)
        {
            return;
        }

        var hasMoveX = false;
        var hasMoveY = false;
        var hasIsMoving = false;
        var hasWalkSpeed = false;
        foreach (var parameter in animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Float)
            {
                hasMoveX |= parameter.nameHash == MoveXId;
                hasMoveY |= parameter.nameHash == MoveYId;
                hasWalkSpeed |= parameter.nameHash == WalkSpeedId;
            }
            else if (parameter.type == AnimatorControllerParameterType.Bool)
            {
                hasIsMoving |= parameter.nameHash == IsMovingId;
            }
        }

        hasDirectionalAnimation = hasMoveX && hasMoveY && hasIsMoving && hasWalkSpeed &&
            animator.HasState(0, IdleStateId) && animator.HasState(0, WalkStateId);

        if (!hasDirectionalAnimation)
        {
            Debug.LogWarning("PlayerController: Animator에 MoveX/MoveY/WalkSpeed 실수, IsMoving bool과 Idle/Walk Blend Tree 상태가 필요합니다.", this);
            return;
        }

        // 방향별 그림을 사용하므로 캐릭터와 자식 카메라의 스케일을 반전하지 않는다.
        var scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x);
        transform.localScale = scale;
        animator.speed = 1f;
        ShowStandingPose();
    }

    private void OnDisable()
    {
        moveInput = Vector2.zero;
        isFasted = false;
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
        // 종료 중인 Animator에는 Play/Update를 호출하지 않는다.
        isWalking = false;
        if (hasDirectionalAnimation && animator != null && animator.isActiveAndEnabled && gameObject.activeInHierarchy)
        {
            animator.SetBool(IsMovingId, false);
        }
    }

    private void Update()
    {
        if (!hasDirectionalAnimation || animator == null || !animator.isActiveAndEnabled)
        {
            return;
        }

        if (!movementEnabled || GameSaveSystem.IsBusy || Time.timeScale == 0f || moveInput.sqrMagnitude == 0f)
        {
            if (GameSaveSystem.IsBusy || Time.timeScale == 0f)
            {
                moveInput = Vector2.zero;
                isFasted = false;
            }
            if (isWalking)
            {
                ShowStandingPose();
            }
            return;
        }

        animationDirection = GetCardinalInput();
        animator.SetFloat(MoveXId, animationDirection.x);
        animator.SetFloat(MoveYId, animationDirection.y);
        animator.SetFloat(WalkSpeedId, isFasted ? 1.5f : 1f);
        animator.SetBool(IsMovingId, true);
        if (animationDirection.x != 0f)
        {
            isFacingRight = animationDirection.x > 0f;
        }
        isWalking = true;
    }

    // 멈추면 마지막 방향의 첫 그림을 유지한다.
    private void ShowStandingPose()
    {
        isWalking = false;
        if (!hasDirectionalAnimation || animator == null || !animator.isActiveAndEnabled)
        {
            return;
        }
        animator.SetFloat(MoveXId, animationDirection.x);
        animator.SetFloat(MoveYId, animationDirection.y);
        animator.SetBool(IsMovingId, false);
    }

    // 크기가 같은 동시 입력은 수평 축을 우선하여 대각선 이동을 막는다.
    private Vector2 GetCardinalInput()
    {
        if (moveInput == Vector2.zero)
        {
            return Vector2.zero;
        }
        if (Mathf.Abs(moveInput.x) >= Mathf.Abs(moveInput.y))
        {
            return moveInput.x < 0f ? Vector2.left : Vector2.right;
        }
        return moveInput.y < 0f ? Vector2.down : Vector2.up;
    }

    // 현재 Scene 카메라의 Y축 정렬 방식을 설정한다
    private void Start()
    {
        if (Camera.main == null)
            return;

        Camera.main.transparencySortMode = TransparencySortMode.CustomAxis;
        Camera.main.transparencySortAxis = new Vector3(0, 1, 0);
    }

    // 이동 가능 상태에 따라 방향 입력을 저장한다
    private void OnMove(InputValue value)
    {
        if (!isActiveAndEnabled || !movementEnabled || GameSaveSystem.IsBusy || Time.timeScale == 0f)
        {
            moveInput = Vector2.zero;
            return;
        }

        moveInput = value.Get<Vector2>();
    }

    // 이동 가능 상태에서만 달리기 입력을 저장한다
    private void OnSprint(InputValue value)
    {
        isFasted = isActiveAndEnabled && movementEnabled && !GameSaveSystem.IsBusy && Time.timeScale != 0f && value.isPressed;
    }

    // 물리 프레임마다 플레이어 이동 속도를 적용한다
    private void FixedUpdate()
    {
        if (!movementEnabled || GameSaveSystem.IsBusy)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 normalizedInput = GetCardinalInput();
        float speedMultiplier = isFasted ? 1.5f : 1f;

        rb.linearVelocity = normalizedInput * moveSpeed * speedMultiplier;

        if (!hasDirectionalAnimation)
        {
            HandleSpriteDirection();
        }
    }

    // Battle과 Map 사이에서 플레이어 이동 가능 상태를 변경한다
    public void SetMovementEnabled(bool enabled)
    {
        movementEnabled = enabled;

        if (enabled)
            return;

        moveInput = Vector2.zero;
        isFasted = false;
        ShowStandingPose();

        if (rb != null)
            rb.linearVelocity = Vector2.zero;
    }

    // 불러오기 시 좌표와 방향을 복원하고 이전 이동 입력을 지운다.
    public void RestoreSaveTransform(Vector3 position, bool facingRight)
    {
        SetMovementEnabled(false);
        transform.position = position;
        rb.position = position;
        if (isFacingRight != facingRight)
        {
            Flip();
        }
        SetMovementEnabled(true);
        // 메뉴 복귀 시 꺼졌을 수 있는 플레이어 카메라의 리스너를 복구한다.
        foreach (var listener in GetComponentsInChildren<AudioListener>(true))
        {
            listener.enabled = true;
        }
    }

    // 좌우 입력 방향에 맞게 캐릭터 이미지를 뒤집는다
    private void HandleSpriteDirection()
    {
        if (moveInput.x > 0f && !isFacingRight)
            Flip();
        else if (moveInput.x < 0f && isFacingRight)
            Flip();
    }

    // 플레이어 Sprite의 좌우 방향을 반전한다
    private void Flip()
    {
        isFacingRight = !isFacingRight;

        if (hasDirectionalAnimation)
        {
            animationDirection = isFacingRight ? Vector2.right : Vector2.left;
            ShowStandingPose();
            return;
        }

        Vector3 scale = transform.localScale;
        scale.x *= -1f;
        transform.localScale = scale;
    }

    // 현재 Instance가 파괴될 때 싱글톤 참조를 정리한다
    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
