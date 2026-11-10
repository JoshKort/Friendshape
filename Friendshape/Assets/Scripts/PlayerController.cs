using UnityEngine;
using UnityEngine.InputSystem;
 
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement (set per shape by PlayerShapeShifter)")]
    public float moveSpeed = 6f;
    public float jumpForce = 14f;
 
    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.15f;
    public LayerMask groundLayer;
 
    private Rigidbody2D rb;
    private Vector2 moveInput;
    private bool isGrounded;
 
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }
 
    void FixedUpdate()
    {
        isGrounded = groundCheck != null &&
                     Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
 
        rb.linearVelocity = new Vector2(moveInput.x * moveSpeed, rb.linearVelocity.y);
    }
 
    // Input System callbacks (PlayerInput > Behavior: Send Messages)
    public void OnMove(InputValue value) => moveInput = value.Get<Vector2>();
 
    public void OnJump(InputValue value)
    {
        if (value.isPressed && isGrounded)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
    }
 
    void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}
