using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
public class SquareController : NetworkBehaviour
{
    const string PlayerMap = "Player";
    const string MoveActionName = "Move";
    const string JumpActionName = "Jump";

    [Header("Input")]
    [SerializeField] private InputActionAsset inputActions;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 10f;
    [SerializeField] private float acceleration = 120f; //ground: high = starts and stops almost instantly, lower for a softer feel
    [SerializeField] private float airAcceleration = 60f; //air: same idea, applies when releasing input too

    [Header("Jump")]
    [SerializeField] private float minJumpSpeed = 6f; //quick tap
    [SerializeField] private float maxJumpSpeed = 13f; //hold
    [SerializeField] private float coyoteTime = 0.1f; //jump after falling
    [SerializeField] private float jumpBuffer = 0.1f; //jump before landing

    [Header("Weight")]
    [SerializeField] private float mass = 5f;
    [SerializeField] private float gravityScale = 2.5f;

    [Header("Collider Material")]
    [SerializeField] private float friction = 0f; //0 so it never clings to walls; movement is speed-controlled so it doesn't need grip
    [Range(0f, 1f), SerializeField] private float bounciness = 0f;

    //actions per player for multiplayer
    private InputActionAsset runtimeActions;
    private InputAction moveAction;
    private InputAction jumpAction;

    private Rigidbody2D rb;
    private BoxCollider2D col;

    private float moveInput;
    private bool jumpHeld;
    private bool jumpRising;
    private float jumpPressedTime = -10f;
    private float lastGroundedTime = -10f;
    private Vector2 groundNormal = Vector2.up;

    //Mirror override for local testing
    bool HasControl => !NetworkClient.active || isLocalPlayer;

    public Vector2 Velocity => rb != null ? rb.linearVelocity : Vector2.zero;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<BoxCollider2D>();

        maxJumpSpeed = Mathf.Max(maxJumpSpeed, minJumpSpeed);

        rb.mass = mass;
        rb.gravityScale = gravityScale;
        rb.linearDamping = 0f;
        rb.freezeRotation = true; //the square always stays upright
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        col.sharedMaterial = new PhysicsMaterial2D("SquareMaterial")
        {
            friction = friction,
            bounciness = bounciness
        };
    }

    private void OnEnable()
    {
        if (inputActions == null)
        {
            Debug.LogError("SquareController: no Input Actions asset assigned.", this);
            return;
        }

        runtimeActions = Instantiate(inputActions);
        InputActionMap map = runtimeActions.FindActionMap(PlayerMap, true);
        moveAction = map.FindAction(MoveActionName, true);
        jumpAction = map.FindAction(JumpActionName, true);
        map.Enable();
    }

    private void OnDisable()
    {
        if (runtimeActions == null) return;

        runtimeActions.Disable();
        Destroy(runtimeActions);
        runtimeActions = null;
        moveAction = null;
        jumpAction = null;
    }

    private void Update()
    {
        if (!HasControl || moveAction == null)
        {
            moveInput = 0f;
            jumpHeld = false;
            return;
        }

        ReadInput();
    }

    private void FixedUpdate()
    {
        if (!HasControl) return;

        bool onGround = Time.time - lastGroundedTime <= 0.06f;

        Vector2 tangent = onGround ? new Vector2(groundNormal.y, -groundNormal.x) : Vector2.right; //along the surface on the ground so slopes work

        //no momentum: speed always heads straight for input * moveSpeed, and for 0 when there's no input
        float currentSpeed = Vector2.Dot(rb.linearVelocity, tangent);
        float targetSpeed = moveInput * moveSpeed;
        ApplySpeedChange(tangent, currentSpeed, targetSpeed, onGround ? acceleration : airAcceleration);

        bool buffered = Time.time - jumpPressedTime <= jumpBuffer;
        bool canJump = Time.time - lastGroundedTime <= coyoteTime;

        if (buffered && canJump)
        {
            Vector2 velocity = rb.linearVelocity;
            velocity.y = jumpHeld ? maxJumpSpeed : minJumpSpeed;
            rb.linearVelocity = velocity;

            jumpRising = jumpHeld;
            jumpPressedTime = -10f;
            lastGroundedTime = -10f;
        }

        if (jumpRising) //release mid jump
        {
            Vector2 velocity = rb.linearVelocity;
            if (!jumpHeld && velocity.y > minJumpSpeed)
            {
                velocity.y = minJumpSpeed;
                rb.linearVelocity = velocity;
            }

            if (!jumpHeld || velocity.y <= 0f) jumpRising = false;
        }
    }

    private void ApplySpeedChange(Vector2 tangent, float currentSpeed, float targetSpeed, float accelerationRate)
    {
        float allowedSpeedChange = accelerationRate * Time.fixedDeltaTime;

        float newSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, allowedSpeedChange);

        float speedChange = newSpeed - currentSpeed;

        if (Mathf.Abs(speedChange) < 0.001f) return;

        float accelerationNeeded = speedChange / Time.fixedDeltaTime;

        rb.AddForce(tangent * accelerationNeeded * rb.mass, ForceMode2D.Force);
    }

    private void OnCollisionEnter2D(Collision2D collision) => CheckGround(collision);
    private void OnCollisionStay2D(Collision2D collision) => CheckGround(collision);

    private void CheckGround(Collision2D collision)
    {
        for (int i = 0; i < collision.contactCount; i++)
        {
            Vector2 colNormal = collision.GetContact(i).normal;
            if (colNormal.y > 0.6f)
            {
                lastGroundedTime = Time.time;
                groundNormal = colNormal;
                return;
            }
        }
    }

    private void ReadInput()
    {
        float x = Mathf.Clamp(moveAction.ReadValue<Vector2>().x, -1f, 1f);
        if (Mathf.Abs(x) < 0.1f) x = 0f;

        moveInput = x;
        jumpHeld = jumpAction.IsPressed();
        if (jumpAction.WasPressedThisFrame()) jumpPressedTime = Time.time;
    }
}