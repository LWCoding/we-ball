using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WeBall.Gameplay
{
    /// <summary>
    /// A player's ball. The owning player reads input and simulates it locally
    /// (owner-authoritative NetworkTransform), and everyone else sees the synced result.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class BallController : NetworkBehaviour
    {
        [SerializeField] float moveSpeed = 7f;
        [SerializeField] float acceleration = 40f;
        [SerializeField] float hopVelocity = 7f;
        [SerializeField] Color[] playerColors =
        {
            new(0.95f, 0.35f, 0.3f), new(0.3f, 0.6f, 0.95f), new(0.35f, 0.8f, 0.4f), new(0.95f, 0.8f, 0.25f),
        };

        Rigidbody2D body;
        ContactFilter2D groundFilter;
        InputAction moveAction;
        InputAction hopAction;
        float moveInput;
        bool hopQueued;

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();

            // "Grounded" means touching something below us (ground, platforms, or another ball).
            groundFilter = new ContactFilter2D();
            groundFilter.SetNormalAngle(45f, 135f);
        }

        public override void OnNetworkSpawn()
        {
            name = $"Player {OwnerClientId}";
            GetComponent<SpriteRenderer>().color = playerColors[(int)(OwnerClientId % (ulong)playerColors.Length)];

            if (!IsOwner) return;

            moveAction = new InputAction("Move", InputActionType.Value);
            moveAction.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/a").With("Positive", "<Keyboard>/d");
            moveAction.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/leftArrow").With("Positive", "<Keyboard>/rightArrow");
            moveAction.AddBinding("<Gamepad>/leftStick/x");

            hopAction = new InputAction("Hop", InputActionType.Button);
            hopAction.AddBinding("<Keyboard>/space");
            hopAction.AddBinding("<Keyboard>/w");
            hopAction.AddBinding("<Keyboard>/upArrow");
            hopAction.AddBinding("<Gamepad>/buttonSouth");

            moveAction.Enable();
            hopAction.Enable();

            if (Camera.main != null && Camera.main.TryGetComponent(out CameraFollow follow))
                follow.SetTarget(transform);
        }

        public override void OnNetworkDespawn()
        {
            moveAction?.Dispose();
            hopAction?.Dispose();
        }

        void Update()
        {
            if (!IsSpawned || !IsOwner) return;

            moveInput = moveAction.ReadValue<float>();
            if (hopAction.WasPressedThisFrame()) hopQueued = true;
        }

        void FixedUpdate()
        {
            if (!IsSpawned || !IsOwner) return;

            Vector2 velocity = body.linearVelocity;
            velocity.x = Mathf.MoveTowards(velocity.x, moveInput * moveSpeed, acceleration * Time.fixedDeltaTime);
            if (hopQueued && body.IsTouching(groundFilter))
                velocity.y = hopVelocity;
            hopQueued = false;

            body.linearVelocity = velocity;
        }
    }
}
