using UnityEngine;

namespace Friendshape.Characters
{
    /// <summary>
    /// Shared "squishy" feel for every character (Circle, Square, Triangle).
    ///
    /// When the body hits something, the sprite flattens along the contact normal, bulges
    /// sideways to keep its volume, then wobbles back to rest on a spring. This is purely
    /// visual: the collider and physics are never touched, so gameplay stays predictable.
    ///
    /// Setup: put this on the root (next to the Rigidbody2D/collider) and assign the child
    /// object that holds the SpriteRenderer to <see cref="visual"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class SquishyPhysics : MonoBehaviour
    {
        [Header("Visual")]
        [Tooltip("Child object holding the sprite. This is the thing that gets squashed.")]
        [SerializeField] Transform visual;

        [Tooltip("Keep the sprite upright while the body rotates (e.g. so a face doesn't spin while rolling).")]
        [SerializeField] bool keepUpright = true;

        [Header("Squish")]
        [Tooltip("Impacts slower than this (units/sec) are ignored, so resting contact doesn't jitter.")]
        [SerializeField] float minImpactSpeed = 2f;

        [Tooltip("How much squash you get per unit of impact speed.")]
        [SerializeField] float squashPerSpeed = 0.035f;

        [Tooltip("Largest squash. 0.4 means flattened to 60% along the hit direction.")]
        [Range(0.1f, 0.6f)]
        [SerializeField] float maxSquash = 0.4f;

        [Header("Spring")]
        [Tooltip("Higher = snaps back faster.")]
        [SerializeField] float stiffness = 220f;

        [Tooltip("Higher = fewer wobbles. Around 12 gives a couple of jelly wobbles; 30 is no wobble.")]
        [SerializeField] float damping = 12f;

        Transform pivot;
        Vector2 axis = Vector2.up;
        float squash;      // + = flattened along axis, - = stretched along axis
        float squashVel;

        void Awake()
        {
            if (visual == null)
            {
                Debug.LogWarning("[SquishyBody] No visual assigned, squish disabled.", this);
                enabled = false;
                return;
            }

            // Insert a pivot between the root and the sprite. The pivot is rotated to line up
            // with the hit direction and scaled; the sprite is counter-rotated so it stays upright.
            pivot = new GameObject("SquishPivot").transform;
            pivot.SetParent(visual.parent, false);
            pivot.localPosition = visual.localPosition;

            visual.SetParent(pivot, false);
            visual.localPosition = Vector3.zero;
        }

        void OnCollisionEnter2D(Collision2D collision)
        {
            if (collision.contactCount == 0) return;

            Vector2 normal = collision.GetContact(0).normal;
            float impactSpeed = Mathf.Abs(Vector2.Dot(collision.relativeVelocity, normal));
            Hit(normal, impactSpeed);
        }

        /// <summary>
        /// Squashes the body along a direction. Called automatically on collisions, but other
        /// scripts can call it too (landing, being shoved by a teammate, bounce pads...).
        /// </summary>
        public void Hit(Vector2 direction, float impactSpeed)
        {
            float amount = (impactSpeed - minImpactSpeed) * squashPerSpeed;
            if (amount <= 0f) return;
            amount = Mathf.Min(amount, maxSquash);

            // A weaker bump doesn't interrupt a bigger squish that is still playing out.
            if (amount > Mathf.Abs(squash))
            {
                axis = direction.normalized;
                squash = amount;
                squashVel = 0f;
            }
        }

        void LateUpdate()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.033f);

            // Damped spring pulling squash back to 0. Underdamped, so it overshoots into a
            // small stretch before settling, which is what reads as "squishy".
            squashVel += (-stiffness * squash - damping * squashVel) * dt;
            squash += squashVel * dt;
            squash = Mathf.Clamp(squash, -maxSquash, maxSquash);

            // Volume-preserving: shrink along the axis, grow across it.
            float along = Mathf.Clamp(1f - squash, 0.4f, 1.6f);
            float across = Mathf.Min(1f / along, 1.6f);

            float angle = Mathf.Atan2(axis.y, axis.x) * Mathf.Rad2Deg;
            pivot.rotation = Quaternion.Euler(0f, 0f, angle);
            pivot.localScale = new Vector3(along, across, 1f);

            // Undo the pivot's rotation on the sprite so only the deformation is axis-aligned.
            visual.rotation = keepUpright ? Quaternion.identity : transform.rotation;
        }
    }
}