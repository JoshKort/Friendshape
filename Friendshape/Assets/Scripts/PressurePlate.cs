using UnityEngine;

public class PressurePlate : MonoBehaviour
{
    [Header("Plate")]
    public PlayerShapeShift.Shape requiredShape = PlayerShapeShift.Shape.Square;
    public bool stayPressed = false;

    [Header("Target")]
    public MovingWall wall;

    [Header("Feedback")]
    public SpriteRenderer spriteRenderer;
    public Color idleColor = Color.white;
    public Color pressedColor = Color.green;

    public bool IsPressed { get; private set; }

    private BoxCollider2D box;

    void Awake()
    {
        box = GetComponent<BoxCollider2D>();
        box.isTrigger = true;

        if (spriteRenderer == null) 
            spriteRenderer = GetComponent<SpriteRenderer>();

        UpdateVisual();
    } 

    void FixedUpdate()
    {
        if (IsPressed && stayPressed) return;

        bool pressedNow = CheckForRequiredShape();
        if (pressedNow == IsPressed) return;

        IsPressed = pressedNow;
        if (wall != null) wall.SetOpen(IsPressed);
        UpdateVisual();
    }

    bool CheckForRequiredShape()
    {
        Collider2D[] colliders = Physics2D.OverlapBoxAll(box.bounds.center, box.bounds.size, 0f);

        foreach (Collider2D hit in colliders) 
        {
            PlayerShapeShift player = hit.GetComponentInParent<PlayerShapeShift>();
            if (player != null && player.CurrentShape == requiredShape)
                return true;
        }
        return false;
    }

    void UpdateVisual()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = IsPressed ? pressedColor : idleColor;
        }
        
    }
}
