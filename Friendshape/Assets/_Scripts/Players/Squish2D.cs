using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class Squish2D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform squishPivot; //rotation child
    [SerializeField] private Transform rollArt;
    [SerializeField] private CircleCollider2D circleCollider;

    [Header("Spring")]
    [SerializeField] private float stiffness = 250f; //how quick it snaps, higher more snappy
    [Range(0.05f, 1.5f), SerializeField] private float dampingRatio = 0.3f; //jiggly
    [SerializeField] private float maxCompression = 0.3f;
    [SerializeField] private float maxStretch = 0.15f;

    [Header("Impact")]
    [SerializeField] private float impactThreshold = 4f; //minimum impact for squish
    [SerializeField] private float kickStrength = 0.025f; //knockback
    [SerializeField] private float retriggerCooldown = 0.05f;
    [SerializeField] private float volumeFactor = 0.75f; //sideways buldge 0.5-1 keeps volume

    [Header("Collider")]
    [Range(0f, 1f), SerializeField] private float colliderShrink = 0.5f;

    [Header("Roll Visuals")]
    [SerializeField] private bool applyRoll = true;
    [SerializeField] private float rollRadius = 0.5f;

    private Rigidbody2D rb;
    private float compression; // 0 = unchanged, + = squashed, - = stretched
    private float compressionVelocity;
    private Vector2 squishNormal = Vector2.up;
    private Vector2 preStepVelocity;
    private float baseRadius;
    private float nextImpactTime;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (circleCollider != null) baseRadius = circleCollider.radius;
    }

    private void FixedUpdate()
    {
        preStepVelocity = rb.linearVelocity; //get velocity before collision

        if (circleCollider != null && colliderShrink > 0f)
        {
            circleCollider.radius = baseRadius * (1f - colliderShrink * Mathf.Max(0f, compression));
        }
    }

    private void Update()
    {
        StepSpring();
        ApplyVisual();
        if (applyRoll) ApplyRoll();
    }

    private void StepSpring()
    {
        if (Mathf.Abs(compression) < 0.0001f && Mathf.Abs(compressionVelocity) < 0.0001f)
        {
            compression = 0f;
            compressionVelocity = 0f;
            return;
        }

        float dt = Mathf.Min(Time.deltaTime, 1f / 30f);
        float damping = 2f * dampingRatio * Mathf.Sqrt(stiffness);

        float accel = -stiffness * compression - damping * compressionVelocity;
        compressionVelocity += accel * dt;
        compression += compressionVelocity * dt;

        if (compression > maxCompression)
        {
            compression = maxCompression;
            if (compressionVelocity > 0f) compressionVelocity = 0f;
        }
        else if (compression < -maxStretch)
        {
            compression = -maxStretch;
            if (compressionVelocity < 0f) compressionVelocity = 0f;
        }
    }

    private void ApplyVisual()
    {
        if (squishPivot == null) return;

        float angle = Mathf.Atan2(squishNormal.y, squishNormal.x) * Mathf.Rad2Deg;
        squishPivot.rotation = Quaternion.Euler(0f, 0f, angle);

        squishPivot.localScale = new Vector3(1f - compression, 1f + compression * volumeFactor, 1f);
    }

    private void ApplyRoll()
    {
        if (rollArt == null) return;

        float degrees = rb.linearVelocity.x * Time.deltaTime / rollRadius * Mathf.Rad2Deg; // Dist travelled / radius = angle rolled (only horizontal)
        rollArt.Rotate(0f, 0f, -degrees);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (Time.time < nextImpactTime) return;

        //Covers multicorner contact
        float bestSpeed = 0f;
        Vector2 bestNormal = squishNormal;
        for (int i = 0; i < collision.contactCount; i++)
        {
            Vector2 colNormal = collision.GetContact(i).normal;
            float speedIntoSurface = Mathf.Abs(Vector2.Dot(preStepVelocity, colNormal));
            if (speedIntoSurface > bestSpeed)
            {
                bestSpeed = speedIntoSurface;
                bestNormal = colNormal;
            }
        }

        if (bestSpeed < impactThreshold) return;

        AddImpact(bestNormal, bestSpeed);
        nextImpactTime = Time.time + retriggerCooldown;
    }


    //Public: Calls an impact
    public void AddImpact(Vector2 normal, float speed)
    {
        float excess = Mathf.Max(0f, speed - impactThreshold);
        squishNormal = normal.normalized;

        compressionVelocity += excess * kickStrength * Mathf.Sqrt(stiffness);
    }
}