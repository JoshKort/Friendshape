using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D), typeof(CapsuleCollider2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 6f;
    public float crouchSpeed = 2.5f;
    public float jumpForce = 14f;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.15f;
    public LayerMask groundLayer;

    [Header("Crouch")]
    [Range(0.3f, 0.9f)] public float crouchHeightMultiplier = 0.5f;
    public Transform headCheck;              // optional: stops standing up under a low ceiling
    public float headCheckRadius = 0.2f;

    private Rigidbody2D rb;
    private CapsuleCollider2D col;
    private Vector2 standSize, standOffset;

    private Vector2 moveInput;
    private bool isGrounded;
    private bool isCrouching;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<CapsuleCollider2D>();
        standSize = col.size;
        standOffset = col.offset;
    }

    void Update()
    {
        UpdateCrouch();
        UpdateFacing();
    }

    void FixedUpdate()
    {
        isGrounded = groundCheck != null &&
                     Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        float speed = isCrouching ? crouchSpeed : moveSpeed;
        rb.linearVelocity = new Vector2(moveInput.x * speed, rb.linearVelocity.y);
    }

    void UpdateCrouch()
    {
        bool wantsCrouch = moveInput.y < -0.5f && isGrounded;
        bool blockedAbove = headCheck != null &&
                            Physics2D.OverlapCircle(headCheck.position, headCheckRadius, groundLayer);

        if (wantsCrouch && !isCrouching)
        {
            isCrouching = true;
            float newHeight = standSize.y * crouchHeightMultiplier;
            col.size = new Vector2(standSize.x, newHeight);
            // keep the feet in the same place
            col.offset = new Vector2(standOffset.x, standOffset.y - (standSize.y - newHeight) / 2f);
        }
        else if (!wantsCrouch && isCrouching && !blockedAbove)
        {
            isCrouching = false;
            col.size = standSize;
            col.offset = standOffset;
        }
    }

    void UpdateFacing()
    {
        Vector3 scale = transform.localScale;
        if (moveInput.x > 0.1f) scale.x = Mathf.Abs(scale.x);
        else if (moveInput.x < -0.1f) scale.x = -Mathf.Abs(scale.x);
        transform.localScale = scale;

    }

    // Input System callbacks (PlayerInput > Behavior: Send Messages)
    public void OnMove(InputValue value) => moveInput = value.Get<Vector2>();

    public void OnJump(InputValue value)
    {
        if (value.isPressed && isGrounded && !isCrouching)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
    }

    void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
        if (headCheck != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(headCheck.position, headCheckRadius);
        }
    }
}
