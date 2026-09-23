using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 4.0f;

    Rigidbody2D rb;
    Vector2 moveInput;
    bool isFacingRight = true;
    bool isFasted = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();



        //Y축 정렬 모드 강제 적용 (Y값이 클수록 뒤로 가고, 작을수록 앞으로 나옴)
        if (Camera.main != null)
        {
            Camera.main.transparencySortMode = TransparencySortMode.CustomAxis;
            Camera.main.transparencySortAxis = new Vector3(0, 1, 0);
        }
    }

    
    void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }
    
    void OnSprint(InputValue value)
    {
        isFasted = value.isPressed;
    }
    private void FixedUpdate()
    {
        Vector2 normalizedInput = moveInput.normalized;

        float speedMultiplier = isFasted ? 1.5f : 1.0f;
        float currentSpeed = moveSpeed * speedMultiplier;

        rb.linearVelocity = normalizedInput * currentSpeed;

        HandleSpriteDirection();
    }
    void HandleSpriteDirection()
    {
        if (moveInput.x > 0 && !isFacingRight)
        {
            Filp();
        }
        else if (moveInput.x < 0 && isFacingRight)
        {
            Filp();
        }
    }
    void Filp()
    {
        isFacingRight = !isFacingRight;
        Vector3 scale = transform.localScale;
        scale.x *= -1;
        transform.localScale = scale;
    }
}
