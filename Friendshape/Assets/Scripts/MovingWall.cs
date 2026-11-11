using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class MovingWall : MonoBehaviour
{
    [Header("Movement")]
    public Vector2 openOffset = new Vector2(0f, 4f);
    public float speed = 3f;

    private Rigidbody2D rb;
    private Vector2 closedPosition;
    private Vector2 openPos;
    private bool isOpen;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
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
        Vector2 target = isOpen ? openPos : closedPosition;
        rb.MovePosition(Vector2.MoveTowards(rb.position, target, speed * Time.fixedDeltaTime));
    }

    void OnDrawGizmosSelected()
    {
        Vector3 start = Application.isPlaying ? (Vector3)closedPosition : transform.position;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(start, start + (Vector3)openOffset);
        Gizmos.DrawWireCube(start + (Vector3)openOffset, transform.lossyScale);
    }
}
