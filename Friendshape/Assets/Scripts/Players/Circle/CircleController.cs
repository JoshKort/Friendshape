using Mirror;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Friendshape.Characters
{
    /// <summary>
    /// The Circle: a heavy ball that is all about momentum.
    ///
    /// It doesn't walk. Input applies torque, so the ball has to spin up, takes a while to
    /// reach top speed, keeps rolling when you let go, and carries its speed into slopes and
    /// walls. Hold Brake to scrub speed off on purpose.
    ///
    /// Controls: A/D or arrows or left stick = roll, Space or South button = jump (tap = hop, hold = higher),
    /// Left Shift or left trigger = brake, S or Down or stick down = fall faster in the air.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public class CircleController : NetworkBehaviour
    {
        [Header("Rolling")]
        [Tooltip("Torque applied while rolling. Lower = slower to spin up = heavier feel.")]
        [SerializeField] float rollTorque = 25f;

        [Tooltip("Rolling speed cap (units/sec). Hitting it doesn't slow you, you just stop accelerating.")]
        [SerializeField] float maxSpeed = 18f;

        [Tooltip("Weak sideways push while airborne. Low on purpose: in the air the Circle mostly keeps what it had.")]
        [SerializeField] float airForce = 10f;

        [Header("Weight")]
        [SerializeField] float mass = 5f;
        [SerializeField] float gravityScale = 2.5f;

        [Tooltip("Spin resistance while coasting. Near zero = rolls for a long time.")]
        [SerializeField] float coastAngularDamping = 0.05f;

        [Tooltip("Air/ground drag while coasting. Near zero = keeps momentum.")]
        [SerializeField] float coastLinearDamping = 0.02f;

        [Tooltip("Spin resistance while Brake is held.")]
        [SerializeField] float brakeAngularDamping = 5f;

        [Header("Surface")]
        [Tooltip("Grip. The ball needs this to turn its spin into forward motion.")]
        [SerializeField] float friction = 1f;

        [Tooltip("Bounce off walls and floors. The bigger of the two surfaces wins.")]
        [Range(0f, 1f)]
        [SerializeField] float bounciness = 0.3f;

        [Header("Jump")]
        [Tooltip("Jump speed from a quick tap (the smallest hop).")]
        [SerializeField] float minJumpSpeed = 6f;

        [Tooltip("MAX jump speed, reached by holding the button. Height is roughly speed squared / (2 x gravity).")]
        [SerializeField] float maxJumpSpeed = 13f;

        [Tooltip("Can still jump this long after rolling off a ledge.")]
        [SerializeField] float coyoteTime = 0.1f;
        [Tooltip("A jump pressed slightly before landing still counts.")]
        [SerializeField] float jumpBuffer = 0.1f;

        [Header("Fast Fall")]
        [Tooltip("Gravity multiplier while holding Down in the air. 1 = no effect.")]
        [SerializeField] float fastFallMultiplier = 2.5f;

        [Tooltip("Fall speed cap while fast-falling (units/sec).")]
        [SerializeField] float fastFallMaxSpeed = 30f;

        Rigidbody2D rb;
        CircleCollider2D col;

        float moveInput;
        bool brakeHeld;
        bool jumpHeld;
        bool downHeld;
        bool jumpRising;
        float jumpPressedTime = -10f;
        float lastGroundedTime = -10f;

        // When no Mirror client is running (e.g. testing the prefab alone in a scene) we take
        // control directly. Once networked, only the local player's copy reads input.
        bool HasControl => !NetworkClient.active || isLocalPlayer;

        /// <summary>Quick check used by other systems (camera, audio, squish) later.</summary>
        public Vector2 Velocity => rb != null ? rb.linearVelocity : Vector2.zero;

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            col = GetComponent<CircleCollider2D>();

            maxJumpSpeed = Mathf.Max(maxJumpSpeed, minJumpSpeed);

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

        void Update()
        {
            if (!HasControl)
            {
                moveInput = 0f;
                brakeHeld = false;
                jumpHeld = false;
                downHeld = false;
                return;
            }

            ReadInput();
        }

        void FixedUpdate()
        {
            if (!HasControl) return;

            bool onGround = Time.time - lastGroundedTime <= 0.06f;

            // --- Rolling -------------------------------------------------------------
            if (Mathf.Abs(moveInput) > 0.01f && !brakeHeld)
            {
                float vx = rb.linearVelocity.x;
                bool belowCap = Mathf.Abs(vx) < maxSpeed || Mathf.Sign(moveInput) != Mathf.Sign(vx);

                if (belowCap)
                {
                    if (onGround)
                        rb.AddTorque(-moveInput * rollTorque); // negative torque = clockwise = roll right
                    else
                        rb.AddForce(Vector2.right * (moveInput * airForce));
                }
            }

            rb.angularDamping = brakeHeld ? brakeAngularDamping : coastAngularDamping;

            // --- Fast fall -----------------------------------------------------------
            bool fastFalling = downHeld && !onGround && rb.linearVelocity.y < 0f;
            rb.gravityScale = fastFalling ? gravityScale * fastFallMultiplier : gravityScale;
            if (fastFalling && rb.linearVelocity.y < -fastFallMaxSpeed)
            {
                Vector2 fv = rb.linearVelocity;
                fv.y = -fastFallMaxSpeed;
                rb.linearVelocity = fv;
            }

            // --- Jump ----------------------------------------------------------------
            bool buffered = Time.time - jumpPressedTime <= jumpBuffer;
            bool canJump = Time.time - lastGroundedTime <= coyoteTime;
            if (buffered && canJump)
            {
                Vector2 v = rb.linearVelocity;
                v.y = jumpHeld ? maxJumpSpeed : minJumpSpeed;
                rb.linearVelocity = v;

                jumpRising = jumpHeld;
                jumpPressedTime = -10f;
                lastGroundedTime = -10f;
            }

            // Let go early and the rise is cut short, so hold time sets the jump height.
            if (jumpRising)
            {
                Vector2 v = rb.linearVelocity;
                if (!jumpHeld && v.y > minJumpSpeed)
                {
                    v.y = minJumpSpeed;
                    rb.linearVelocity = v;
                }

                if (!jumpHeld || v.y <= 0f) jumpRising = false;
            }
        }

        void OnCollisionEnter2D(Collision2D collision) => CheckGround(collision);
        void OnCollisionStay2D(Collision2D collision) => CheckGround(collision);

        // Standing on anything whose surface faces mostly up counts as ground, slopes included.
        void CheckGround(Collision2D collision)
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

        void ReadInput()
        {
            float x = 0f;
            bool brake = false;
            bool jump = false;
            bool held = false;
            bool down = false;

#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) x -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;
                brake |= kb.leftShiftKey.isPressed;
                jump |= kb.spaceKey.wasPressedThisFrame;
                held |= kb.spaceKey.isPressed;
                down |= kb.sKey.isPressed || kb.downArrowKey.isPressed;
            }

            Gamepad pad = Gamepad.current;
            if (pad != null)
            {
                x += pad.leftStick.x.ReadValue();
                brake |= pad.leftTrigger.ReadValue() > 0.5f;
                jump |= pad.buttonSouth.wasPressedThisFrame;
                held |= pad.buttonSouth.isPressed;
                down |= pad.leftStick.y.ReadValue() < -0.5f || pad.dpad.down.isPressed;
            }
#else
            x = Input.GetAxisRaw("Horizontal");
            brake = Input.GetKey(KeyCode.LeftShift);
            jump = Input.GetButtonDown("Jump");
            held = Input.GetButton("Jump");
            down = Input.GetAxisRaw("Vertical") < -0.5f;
#endif

            x = Mathf.Clamp(x, -1f, 1f);
            if (Mathf.Abs(x) < 0.1f) x = 0f;

            moveInput = x;
            brakeHeld = brake;
            jumpHeld = held;
            downHeld = down;
            if (jump) jumpPressedTime = Time.time;
        }
    }
}