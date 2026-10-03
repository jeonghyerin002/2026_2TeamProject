using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>플레이어 이동과 Scene 간 이동 가능 상태를 관리한다</summary>
[RequireComponent(typeof(Rigidbody2D))]
[DisallowMultipleComponent]
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

    // 싱글톤과 물리 참조를 준비하고 Scene 이동 후에도 유지한다
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
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
        if (!movementEnabled)
        {
            moveInput = Vector2.zero;
            return;
        }

        moveInput = value.Get<Vector2>();
    }

    // 이동 가능 상태에서만 달리기 입력을 저장한다
    private void OnSprint(InputValue value)
    {
        isFasted = movementEnabled && value.isPressed;
    }

    // 물리 프레임마다 플레이어 이동 속도를 적용한다
    private void FixedUpdate()
    {
        if (!movementEnabled)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 normalizedInput = moveInput.normalized;
        float speedMultiplier = isFasted ? 1.5f : 1f;

        rb.linearVelocity = normalizedInput * moveSpeed * speedMultiplier;

        HandleSpriteDirection();
    }

    // Battle과 Map 사이에서 플레이어 이동 가능 상태를 변경한다
    public void SetMovementEnabled(bool enabled)
    {
        movementEnabled = enabled;

        if (enabled)
            return;

        moveInput = Vector2.zero;
        isFasted = false;

        if (rb != null)
            rb.linearVelocity = Vector2.zero;
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