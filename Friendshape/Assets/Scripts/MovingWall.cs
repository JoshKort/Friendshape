using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class MovingWall : MonoBehaviour
{
    [Header("Movement")]
    public Vector2 openOffset = new Vector2(0f, 4f);
    public float speed = 3f;

    [Header("Crush")]
    public PlayerShapeShift.Shape safeShape = PlayerShapeShift.Shape.Square;

    private Rigidbody2D rb;
    private Collider2D col;
    private Vector2 closedPosition;
    private Vector2 openPos;
    private bool isOpen;
    private bool crushed;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;

        closedPosition = rb.position;
        openPos = closedPosition + openOffset;
    }

    public void SetOpen(bool open)
    {
        isOpen = open;
    }
 
    void FixedUpdate()
    {
        if (crushed) return;
        Vector2 target = isOpen ? openPos : closedPosition;
        Vector2 next = Vector2.MoveTowards(rb.position, target, speed * Time.fixedDeltaTime);

        bool movingDown = next.y < rb.position.y;
        if (movingDown)
        {
            PlayerShapeShift playerUnder = FindPlayerAt(next);
            if (playerUnder != null)
            {
                if (playerUnder != null)
                {
                    if (playerUnder.CurrentShape == safeShape)
                    {
                        return;
                    }
                    crushed = true;
                    SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                    return;
                }
            }
        }
        rb.MovePosition(next);
    }

    PlayerShapeShift FindPlayerAt(Vector2 nextPos)
    {
        Vector2 center = (Vector2)col.bounds.center + (nextPos - rb.position);
        Vector2 size = (Vector2)col.bounds.size - new Vector2(0.1f, 0f);

        Collider2D[] hits =Physics2D.OverlapBoxAll(center, size, 0f);
        foreach (Collider2D hit in hits)
        {
            PlayerShapeShift player = hit.GetComponentInParent<PlayerShapeShift>();
            if (player != null)
                return player;
        }

        return null;
    }

    void OnDrawGizmosSelected()
    {
        Vector3 start = Application.isPlaying ? (Vector3)closedPosition : transform.position;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(start, start + (Vector3)openOffset);
        Gizmos.DrawWireCube(start + (Vector3)openOffset, transform.lossyScale);
    }
}
