using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
public class CircleController : NetworkBehaviour
{
    const string PlayerMap = "Player";
    const string MoveActionName = "Move";
    const string JumpActionName = "Jump";

    [Header("Input")]
    [SerializeField] private InputActionAsset inputActions;

    [Header("Movement")]
    [SerializeField] private float maxSpeed = 20f;
    [SerializeField] private float acceleration = 80f;
    [SerializeField] private float deceleration = 12f;
    [SerializeField] private float turnAcceleration = 180f;
    [SerializeField] private float airAcceleration = 30f;
    [SerializeField] float stopSpeed = 0.3f; //speed check for velocity haulting
    [Range(0.9f, 1f), SerializeField] private float flatGroundNormalY = 0.98f; //flat/slope normals '1' = perfect flat

    [Header("Jump")]
    [SerializeField] private float minJumpSpeed = 6f; //quick tap
    [SerializeField] private float maxJumpSpeed = 13f; //hold
    [SerializeField] private float coyoteTime = 0.1f; //jump after falling
    [SerializeField] private float jumpBuffer = 0.1f; //jump before landing

    [Header("Weight")]
    [SerializeField] private float mass = 5f;
    [SerializeField] private float gravityScale = 2.5f;
    [SerializeField] private float coastAngularDamping = 0.05f; //angular resistance preventing infinite rolling
    [SerializeField] float coastLinearDamping = 0.02f; //linear resistance preventing infinite rolling

    [Header("Collider Material")]
    [SerializeField] private float friction = 1f;
    [Range(0f, 1f), SerializeField] private float bounciness = 0.3f;

    //actions per player for multiplayer
    private InputActionAsset runtimeActions;
    private InputAction moveAction;
    private InputAction jumpAction;

    private Rigidbody2D rb;
    private CircleCollider2D col;
    private float worldRadius;

    private float moveInput;
    private bool jumpHeld;
    private bool jumpRising;
    private float jumpPressedTime = -10f;
    private float lastGroundedTime = -10f;
    private Vector2 groundNormal = Vector2.up;


    //Mirror override for local testing
    bool HasControl => !NetworkClient.active || isLocalPlayer;

    //Check for other later systems
    public Vector2 Velocity => rb != null ? rb.linearVelocity : Vector2.zero;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<CircleCollider2D>();

        maxJumpSpeed = Mathf.Max(maxJumpSpeed, minJumpSpeed);
        worldRadius = col.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y);

        rb.mass = mass;
        rb.gravityScale = gravityScale;
        rb.linearDamping = coastLinearDamping;
        rb.angularDamping = coastAngularDamping;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous; // fast ball, thin walls
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        col.sharedMaterial = new PhysicsMaterial2D("CircleMaterial")
        {
            friction = friction,
            bounciness = bounciness
        };
    }

    private void OnEnable()
    {
        if (inputActions == null)
        {
            Debug.LogError("CircleController: no Input Actions asset assigned.", this);
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

        Vector2 tangent = onGround ? new Vector2(groundNormal.y, -groundNormal.x) : Vector2.right; //ground check for slope interaction and air force

        // Main Movement
        float currentSpeed = Vector2.Dot(rb.linearVelocity, tangent);

        if (Mathf.Abs(moveInput) > 0.01f)
        {
            float targetSpeed = moveInput * maxSpeed;

            float movementAcceleration = onGround ? acceleration : airAcceleration;

            bool changingDirection = Mathf.Abs(currentSpeed) > stopSpeed && Mathf.Sign(currentSpeed) != Mathf.Sign(targetSpeed);

            if (onGround && changingDirection) movementAcceleration = turnAcceleration;

            ApplySpeedChange(tangent, currentSpeed, targetSpeed, movementAcceleration);
        }
        else if (onGround)
        {
            ApplyRollingResistance(tangent);
        }


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

    private void ApplyRollingResistance(Vector2 tangent)
    {
        float currentSpeed = Vector2.Dot(rb.linearVelocity, tangent);

        bool flatGround = groundNormal.y >= flatGroundNormalY;

        if (flatGround && Mathf.Abs(currentSpeed) < stopSpeed)
        {
            Vector2 velocity = rb.linearVelocity;

            velocity -= tangent * currentSpeed; //force complete stop
            rb.linearVelocity = velocity;

            rb.angularVelocity = 0f;
            return;
        }

        ApplySpeedChange(tangent, currentSpeed, 0f, deceleration);
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