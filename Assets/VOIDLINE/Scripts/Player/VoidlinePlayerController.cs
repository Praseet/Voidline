using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Responsive arcade flight control: momentum-based lateral steering, visible banking,
/// hold-to-boost, and a readable 360-degree roll with a short dodge window.
/// Collision remains on the upright Player root; only the visual model rotates.
/// </summary>
[DisallowMultipleComponent]
public sealed class VoidlinePlayerController : MonoBehaviour
{
    [Header("Lateral Flight")]
    [SerializeField, Min(1f)] private float movementSpeed = 9.4f;
    [SerializeField, Min(1f)] private float acceleration = 25f;
    [SerializeField, Min(1f)] private float deceleration = 18f;
    [SerializeField] private Vector2 horizontalLimits = new Vector2(-3.45f, 3.45f);
    [SerializeField] private Vector2 verticalLimits = new Vector2(-2.35f, 2.35f);

    [Header("Visual Flight")]
    [SerializeField, Range(0f, 40f)] private float bankAngle = 34f;
    [SerializeField, Range(0f, 25f)] private float pitchAngle = 16f;
    [SerializeField, Range(0f, 15f)] private float yawAngle = 7f;
    [SerializeField, Min(0f)] private float bankResponse = 8f;

    [Header("Barrel Roll / Evasive Ability")]
    [SerializeField, Min(0.2f)] private float rollDuration = 0.72f;
    [SerializeField, Min(0.1f)] private float rollCooldown = 1.05f;
    [SerializeField, Min(0.1f)] private float dodgeWindow = 0.46f;

    [Header("Input System")]
    [SerializeField] private InputActionAsset inputActions;

    private InputAction moveAction;
    private InputAction sprintAction;
    private InputAction rollLeftAction;
    private InputAction rollRightAction;
    private VoidlineGameManager gameManager;
    private VoidlineRouteDirector routeDirector;
    private bool controlEnabled = true;
    private bool boostRequested;
    private Vector2 lanePosition;
    private Vector2 velocity;
    private Transform visualRoot;
    private Quaternion initialVisualRotation;
    private bool visualRotationCaptured;
    private float rollElapsed = -1f;
    private float rollDirection = 1f;
    private float nextRollReadyAt;

    public Vector2 LanePosition => lanePosition;
    public bool IsRolling => rollElapsed >= 0f;
    public bool IsDodgeActive => controlEnabled && IsRolling && rollElapsed <= dodgeWindow;
    public Vector2 NormalizedVelocity => new Vector2(
        Mathf.Clamp(velocity.x / Mathf.Max(0.01f, movementSpeed), -1f, 1f),
        Mathf.Clamp(velocity.y / Mathf.Max(0.01f, movementSpeed), -1f, 1f));

    public void Configure(VoidlineGameManager manager, InputActionAsset actions, VoidlineRouteDirector route = null)
    {
        gameManager = manager;
        if (actions != null) inputActions = actions;
        routeDirector = route;
        Vector3 center = routeDirector != null
            ? routeDirector.GetCenterOffsetAtDistance(routeDirector.DistanceTravelled)
            : Vector3.zero;
        lanePosition = new Vector2(transform.position.x - center.x, transform.position.y - center.y);
        CacheVisualRoot();
        BindActions();
    }

    public void SetControlEnabled(bool enabled)
    {
        controlEnabled = enabled;
        if (!enabled)
        {
            velocity = Vector2.zero;
            SetBoost(false);
            rollElapsed = -1f;
        }
    }

    private void Awake()
    {
        CacheVisualRoot();
        BindActions();
    }

    private void OnEnable() => BindActions();

    private void CacheVisualRoot()
    {
        if (visualRoot == null) visualRoot = transform.Find("Model");
        if (visualRoot != null && !visualRotationCaptured)
        {
            initialVisualRotation = visualRoot.localRotation;
            visualRotationCaptured = true;
        }
    }

    private void BindActions()
    {
        if (inputActions == null) return;

        moveAction = inputActions.FindAction("Player/Move", false);
        sprintAction = inputActions.FindAction("Player/Sprint", false);
        rollLeftAction = inputActions.FindAction("Player/RollLeft", false);
        rollRightAction = inputActions.FindAction("Player/RollRight", false);

        if (rollLeftAction != null)
        {
            rollLeftAction.performed -= HandleRollLeft;
            rollLeftAction.performed += HandleRollLeft;
            rollLeftAction.Enable();
        }
        if (rollRightAction != null)
        {
            rollRightAction.performed -= HandleRollRight;
            rollRightAction.performed += HandleRollRight;
            rollRightAction.Enable();
        }
        if (moveAction != null) moveAction.Enable();
        if (sprintAction != null) sprintAction.Enable();
    }

    private void HandleRollLeft(InputAction.CallbackContext context) => TryStartRoll(-1f);
    private void HandleRollRight(InputAction.CallbackContext context) => TryStartRoll(1f);

    private void TryStartRoll(float direction)
    {
        if (!controlEnabled || (gameManager != null && (gameManager.IsGameOver || gameManager.IsPaused)))
            return;
        if (IsRolling || Time.time < nextRollReadyAt) return;

        rollDirection = direction;
        rollElapsed = 0f;
        nextRollReadyAt = Time.time + rollCooldown;
        if (gameManager != null) gameManager.NotifyRollStarted(direction);
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        if (!controlEnabled || (gameManager != null && gameManager.IsGameOver))
        {
            velocity = Vector2.MoveTowards(velocity, Vector2.zero, deceleration * dt);
            SetBoost(false);
            UpdateVisuals(Vector2.zero, dt);
            return;
        }

        Vector2 input = ReadMoveInput();
        bool wantsBoost = sprintAction != null && sprintAction.IsPressed();
        if (gameManager != null && gameManager.IsPaused) wantsBoost = false;
        SetBoost(wantsBoost);

        if (gameManager == null || !gameManager.IsPaused)
        {
            float response = input.sqrMagnitude > 0.001f ? acceleration : deceleration;
            velocity = Vector2.MoveTowards(velocity, input * movementSpeed, response * dt);

            Vector2 nextLane = lanePosition + velocity * dt;
            nextLane.x = Mathf.Clamp(nextLane.x, horizontalLimits.x, horizontalLimits.y);
            nextLane.y = Mathf.Clamp(nextLane.y, verticalLimits.x, verticalLimits.y);

            if ((nextLane.x <= horizontalLimits.x && velocity.x < 0f) ||
                (nextLane.x >= horizontalLimits.y && velocity.x > 0f))
                velocity.x = 0f;
            if ((nextLane.y <= verticalLimits.x && velocity.y < 0f) ||
                (nextLane.y >= verticalLimits.y && velocity.y > 0f))
                velocity.y = 0f;

            lanePosition = nextLane;
            UpdateRoutePosition();
        }

        if (IsRolling)
        {
            rollElapsed += dt;
            if (rollElapsed >= rollDuration) rollElapsed = -1f;
        }

        UpdateVisuals(NormalizedVelocity, dt);
    }

    private void LateUpdate()
    {
        // The scroller advances its route distance during Update; snap after all Update
        // methods so ship and corridor share the same lane frame even at high speed.
        UpdateRoutePosition();
    }

    private void UpdateRoutePosition()
    {
        if (routeDirector == null)
        {
            transform.position = new Vector3(lanePosition.x, lanePosition.y, 0f);
            return;
        }

        transform.position = routeDirector.GetPlayerWorldPosition(
            lanePosition, routeDirector.DistanceTravelled, 0f);
    }

    private Vector2 ReadMoveInput()
    {
        if (moveAction != null) return Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f);

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return Vector2.zero;
        float x = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f) -
                  (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f);
        float y = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1f : 0f) -
                  (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1f : 0f);
        return Vector2.ClampMagnitude(new Vector2(x, y), 1f);
    }

    private void SetBoost(bool active)
    {
        boostRequested = active;
        // Send held state while the key remains down so boost can resume after recharging.
        if (gameManager != null) gameManager.SetBoostHeld(active);
    }

    private void UpdateVisuals(Vector2 steering, float dt)
    {
        CacheVisualRoot();
        if (visualRoot == null) return;

        float rollAngle = 0f;
        if (IsRolling)
        {
            float progress = Mathf.Clamp01(rollElapsed / Mathf.Max(0.01f, rollDuration));
            float easedProgress = progress * progress * (3f - 2f * progress);
            rollAngle = 360f * easedProgress * rollDirection;
        }

        Quaternion steeringRotation = Quaternion.Euler(
            -steering.y * pitchAngle,
            steering.x * yawAngle,
            -steering.x * bankAngle + rollAngle);
        Quaternion targetRotation = initialVisualRotation * steeringRotation;
        float blend = 1f - Mathf.Exp(-bankResponse * dt);
        visualRoot.localRotation = Quaternion.Slerp(visualRoot.localRotation, targetRotation, blend);
    }

    private void OnDisable()
    {
        SetBoost(false);
        if (rollLeftAction != null)
        {
            rollLeftAction.performed -= HandleRollLeft;
            rollLeftAction.Disable();
        }
        if (rollRightAction != null)
        {
            rollRightAction.performed -= HandleRollRight;
            rollRightAction.Disable();
        }
        if (moveAction != null) moveAction.Disable();
        if (sprintAction != null) sprintAction.Disable();
    }
}
