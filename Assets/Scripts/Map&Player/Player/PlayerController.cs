using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PlayerInput))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 4.0f;

    Rigidbody rb;
    Vector2 moveInput;
    bool isFacingRight = true;
    bool isFasted = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();

        rb.constraints = RigidbodyConstraints.FreezeRotationZ | RigidbodyConstraints.FreezeRotation;
    }
    
    void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }
    
    void OnFast(InputValue value)
    {
        isFasted = value.isPressed;
    }
    private void FixedUpdate()
    {
        Vector2 normalizedInput = moveInput.normalized;

        float speedMultiplier = isFasted ? 1.5f : 1.0f;
        float currentSpeed = moveSpeed * speedMultiplier;

        Vector3 targetVelocity = new Vector3(normalizedInput.x * currentSpeed, normalizedInput.y * currentSpeed, 0f);
        rb.linearVelocity = targetVelocity;

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
