using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D), typeof(PolygonCollider2D))]
public class TriangleController : NetworkBehaviour
{
    const string PlayerMap = "Player";
    const string MoveActionName = "Move";
    const string JumpActionName = "Jump";

    [Header("Input")]
    [SerializeField] private InputActionAsset inputActions;

    [Header("Movement")]
    [SerializeField] private float tumbleSpeed = 400f; //target spin on the ground (deg/sec). Every 120 degrees is one step, so speed is about side length * tumbleSpeed / 120
    [SerializeField] private float tumbleAcceleration = 2000f; //how fast it spins up or down (deg/sec^2). Lower = more labored steps, higher = smoother roll
    [SerializeField] private float airSpeed = 8f;
    [SerializeField] private float airAcceleration = 30f;

    [Header("Jump")]
    [SerializeField] private float minJumpSpeed = 6f; //quick tap
    [SerializeField] private float maxJumpSpeed = 13f; //hold
    [SerializeField] private float coyoteTime = 0.1f; //jump after falling
    [SerializeField] private float jumpBuffer = 0.1f; //jump before landing

    [Header("Weight")]
    [SerializeField] private float mass = 5f;
    [SerializeField] private float gravityScale = 2.5f;
    [SerializeField] private float angularDamping = 1f; //how quickly the tumble dies once input is released
    [SerializeField] private float linearDamping = 0f;

    [Header("Collider Material")]
    [SerializeField] private float friction = 2f; //high: the corners must grip the ground for the tumble to turn into travel
    [Range(0f, 1f), SerializeField] private float bounciness = 0f;

    //actions per player for multiplayer
    private InputActionAsset runtimeActions;
    private InputAction moveAction;
    private InputAction jumpAction;

    private Rigidbody2D rb;
    private PolygonCollider2D col;

    private float moveInput;
    private bool jumpHeld;
    private bool jumpRising;
    private float jumpPressedTime = -10f;
    private float lastGroundedTime = -10f;

    //Mirror override for local testing
    bool HasControl => !NetworkClient.active || isLocalPlayer;

    public Vector2 Velocity => rb != null ? rb.linearVelocity : Vector2.zero;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<PolygonCollider2D>();

        maxJumpSpeed = Mathf.Max(maxJumpSpeed, minJumpSpeed);

        rb.mass = mass;
        rb.gravityScale = gravityScale;
        rb.linearDamping = linearDamping;
        rb.angularDamping = angularDamping;
        rb.freezeRotation = false; //the body really tumbles, so the hitbox is always exactly the visible shape
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        col.sharedMaterial = new PhysicsMaterial2D("TriangleMaterial")
        {
            friction = friction,
            bounciness = bounciness
        };
    }

    private void OnEnable()
    {
        if (inputActions == null)
        {
            Debug.LogError("TriangleController: no Input Actions asset assigned.", this);
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

        if (Mathf.Abs(moveInput) > 0.01f)
        {
            if (onGround) Tumble();
            else AirControl();
        }
        //no input: nothing is driven. Angular damping and gravity let it flop over onto a flat side and stop

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

    //drives the spin toward the target; the corners gripping the ground turn that spin into forward travel
    private void Tumble()
    {
        float targetSpin = -moveInput * tumbleSpeed; //clockwise (negative) = tumbling right
        rb.angularVelocity = Mathf.MoveTowards(rb.angularVelocity, targetSpin, tumbleAcceleration * Time.fixedDeltaTime);
    }

    //steering while airborne; leaves speed alone if already above airSpeed in the input direction
    private void AirControl()
    {
        float currentSpeed = rb.linearVelocity.x;
        float targetSpeed = moveInput * airSpeed;

        bool overspeed = Mathf.Abs(currentSpeed) > airSpeed && Mathf.Sign(currentSpeed) == Mathf.Sign(targetSpeed);
        if (overspeed) return;

        float newSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, airAcceleration * Time.fixedDeltaTime);
        float accelerationNeeded = (newSpeed - currentSpeed) / Time.fixedDeltaTime;

        rb.AddForce(Vector2.right * (accelerationNeeded * rb.mass), ForceMode2D.Force);
    }

    private void OnCollisionEnter2D(Collision2D collision) => CheckGround(collision);
    private void OnCollisionStay2D(Collision2D collision) => CheckGround(collision);

    private void CheckGround(Collision2D collision)
    {
        for (int i = 0; i < collision.contactCount; i++)
        {
            if (collision.GetContact(i).normal.y > 0.6f)
            {
                lastGroundedTime = Time.time;
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