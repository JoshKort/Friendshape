using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerShapeShift : MonoBehaviour
{
    public enum Shape { Square, Circle, Triangle }

    [System.Serializable]
    public class ShapeData
    {
        public Shape shape;
        public GameObject visual;
        public float moveSpeed = 6f; 
        public float jumpForce = 3f;
    }

    public ShapeData[] shapes;
    public Shape startingShape = Shape.Square;

    public Shape CurrentShape { get; private set; }
    public event System.Action<Shape> ShapeChanged;

    private PlayerController controller;

    void Awake()
    {
        controller = GetComponent<PlayerController>();
    }

    void Start ()
    {
        SetShape(startingShape);
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.digit1Key.wasPressedThisFrame) SetShape(Shape.Square);
        else if (kb.digit2Key.wasPressedThisFrame) SetShape(Shape.Circle);
        else if (kb.digit3Key.wasPressedThisFrame) SetShape(Shape.Triangle);

    }

     public void SetShape(Shape newShape)
    {
        foreach (var data in shapes)
        {
            bool active = data.shape == newShape;
 
            if (data.visual != null)
                data.visual.SetActive(active);
 
            if (active)
            {
                controller.moveSpeed = data.moveSpeed;
                controller.jumpForce = data.jumpForce;
            }
        }
 
        CurrentShape = newShape;
        ShapeChanged?.Invoke(newShape);
    }

}
