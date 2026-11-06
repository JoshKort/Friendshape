using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

public class MultiplayerMove : NetworkBehaviour
{
    [SerializeField] private float speed = 5f;

    private InputAction moveAction;
    private Rigidbody2D rb;
    private Vector2 input;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        moveAction = InputSystem.actions.FindAction("Player/Move");
    }

    void Update()
    {
        if (!isLocalPlayer) return;

        input = moveAction.ReadValue<Vector2>();
    }

    void FixedUpdate()
    {
        if (!isLocalPlayer) return;

        rb.linearVelocity = input * speed;
    }
}
