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

    [Header("Impact Response")]
    [Tooltip("Total lane displacement applied away from a damaging contact; movement control stays live.")]
    [SerializeField, Range(0f, 0.85f)] private float impactNudgeDistance = 0.42f;
    [Tooltip("Time over which the impact displacement is applied and recovered.")]
    [SerializeField, Range(0.05f, 0.45f)] private float impactNudgeDuration = 0.22f;

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
    private Quaternion steeringVisualRotation;
    private bool visualRotationCaptured;
    private float rollElapsed = -1f;
    private float rollDirection = 1f;
    private float rollRecoveryElapsed = -1f;
    private float rollRecoveryDirection = 1f;
    private float nextRollReadyAt;
    private float clingerHandlingMultiplier = 1f;
    private bool finalCrashInProgress;
    private bool finalCrashFinished;
    private float finalCrashElapsed;
    private float finalCrashRollDirection = 1f;
    private Vector3 finalCrashStartPosition;
    private Quaternion finalCrashStartRotation;
    private Vector2 impactNudgeRemainingDistance;
    private float impactNudgeRemainingTime;

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
        impactNudgeRemainingDistance = Vector2.zero;
        impactNudgeRemainingTime = 0f;
        clingerHandlingMultiplier = 1f;
        finalCrashInProgress = false;
        finalCrashFinished = false;
        finalCrashElapsed = 0f;
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
            rollRecoveryElapsed = -1f;
            impactNudgeRemainingDistance = Vector2.zero;
            impactNudgeRemainingTime = 0f;
            clingerHandlingMultiplier = 1f;
        }
    }

    public void SetClingerHandlingMultiplier(float multiplier)
    {
        clingerHandlingMultiplier = Mathf.Clamp(multiplier, 0.45f, 1f);
    }

    /// <summary>
    /// Adds a brief, bounded lane displacement away from a damaging contact. The player can
    /// steer immediately; the nudge has no control lock and is always clamped to lane bounds.
    /// </summary>
    public void ApplyImpactDeflection(Vector3 impactSource)
    {
        if (!controlEnabled || impactNudgeDistance <= 0f) return;

        Vector2 away = new Vector2(transform.position.x - impactSource.x,
            transform.position.y - impactSource.y);
        if (away.sqrMagnitude < 0.01f)
            away = new Vector2(transform.position.x >= impactSource.x ? 1f : -1f, 0f);

        away.Normalize();
        impactNudgeRemainingDistance = Vector2.ClampMagnitude(
            impactNudgeRemainingDistance + away * impactNudgeDistance,
            impactNudgeDistance * 1.35f);
        impactNudgeRemainingTime = Mathf.Max(impactNudgeRemainingTime, impactNudgeDuration);
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
            steeringVisualRotation = initialVisualRotation;
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
        rollRecoveryElapsed = -1f;
        nextRollReadyAt = Time.time + rollCooldown;
        if (gameManager != null) gameManager.NotifyRollStarted(direction);
    }

    /// <summary>
    /// Play the fatal-hit follow-through on the visual model only. The collision root remains
    /// upright and deterministic; the craft kicks off-axis, loses attitude, and drifts into a
    /// held final pose while the run-end UI waits for the short cinematic beat.
    /// </summary>
    public void PlayFinalCrash(Vector3 impactSource)
    {
        CacheVisualRoot();
        controlEnabled = false;
        velocity = Vector2.zero;
        SetBoost(false);
        rollElapsed = -1f;
        rollRecoveryElapsed = -1f;
        impactNudgeRemainingDistance = Vector2.zero;
        impactNudgeRemainingTime = 0f;
        finalCrashElapsed = 0f;
        finalCrashFinished = false;
        finalCrashRollDirection = impactSource.x < transform.position.x ? 1f : -1f;
        if (visualRoot == null)
        {
            finalCrashInProgress = false;
            finalCrashFinished = true;
            return;
        }
        finalCrashStartPosition = visualRoot.localPosition;
        finalCrashStartRotation = visualRoot.localRotation;
        finalCrashInProgress = true;
    }

    private void UpdateFinalCrash(float unscaledDeltaTime)
    {
        if (visualRoot == null) { finalCrashInProgress = false; finalCrashFinished = true; return; }
        finalCrashElapsed += Mathf.Max(0f, unscaledDeltaTime);
        const float duration = 1.12f;
        float t = Mathf.Clamp01(finalCrashElapsed / duration);
        float ease = 1f - Mathf.Pow(1f - t, 3f);
        float rattle = Mathf.Sin(finalCrashElapsed * 49f) * 3.2f *
            Mathf.Exp(-finalCrashElapsed * 4.3f);
        float yaw = finalCrashRollDirection * 13f * ease +
            Mathf.Sin(finalCrashElapsed * 17f) * 2.2f * Mathf.Exp(-finalCrashElapsed * 3.4f);
        float pitch = -25f * ease + Mathf.Sin(finalCrashElapsed * 32f) *
            3.0f * Mathf.Exp(-finalCrashElapsed * 4.0f);
        float roll = finalCrashRollDirection * 72f * ease + rattle;
        visualRoot.localRotation = finalCrashStartRotation * Quaternion.Euler(pitch, yaw, roll);
        visualRoot.localPosition = finalCrashStartPosition + new Vector3(
            finalCrashRollDirection * 0.075f * ease,
            -0.19f * ease + Mathf.Sin(finalCrashElapsed * 26f) * 0.012f *
                Mathf.Exp(-finalCrashElapsed * 4f),
            -0.11f * ease);

        if (finalCrashElapsed >= duration)
        {
            finalCrashInProgress = false;
            finalCrashFinished = true;
        }
    }

    private void Update()
    {
        if (finalCrashFinished) return;
        if (finalCrashInProgress)
        {
            UpdateFinalCrash(Time.unscaledDeltaTime);
            return;
        }
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
        // Keep the advertised keyboard control reliable even if an InputAction map is present
        // but its Sprint action is not currently receiving keyboard state (e.g. after hot reload).
        Keyboard keyboard = Keyboard.current;
        bool wantsBoost = (sprintAction != null && sprintAction.IsPressed()) ||
            (keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed));
        if (gameManager != null && gameManager.IsPaused) wantsBoost = false;
        SetBoost(wantsBoost);

        if (gameManager == null || !gameManager.IsPaused)
        {
            float response = input.sqrMagnitude > 0.001f ? acceleration : deceleration;
            velocity = Vector2.MoveTowards(velocity,
                input * movementSpeed * clingerHandlingMultiplier, response * dt);

            Vector2 nextLane = lanePosition + velocity * dt;
            if (impactNudgeRemainingTime > 0f)
            {
                float nudgeStep = Mathf.Clamp01(dt /
                    Mathf.Max(dt, impactNudgeRemainingTime));
                Vector2 displacement = impactNudgeRemainingDistance * nudgeStep;
                nextLane += displacement;
                impactNudgeRemainingDistance -= displacement;
                impactNudgeRemainingTime = Mathf.Max(0f, impactNudgeRemainingTime - dt);
                if (impactNudgeRemainingTime <= 0f)
                    impactNudgeRemainingDistance = Vector2.zero;
            }
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
            if (rollElapsed >= rollDuration)
            {
                rollElapsed = -1f;
                rollRecoveryElapsed = 0f;
                rollRecoveryDirection = rollDirection;
            }
        }
        else if (rollRecoveryElapsed >= 0f)
        {
            rollRecoveryElapsed += dt;
            if (rollRecoveryElapsed >= 0.24f) rollRecoveryElapsed = -1f;
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

        // Smooth ordinary attitude separately from the barrel-roll channel. Filtering a
        // combined Euler target can take the shortest quaternion path and visually swallow
        // most of a 360-degree roll, especially when steering at the same time.
        Quaternion steeringRotation = Quaternion.Euler(
            -steering.y * pitchAngle,
            steering.x * yawAngle,
            -steering.x * bankAngle);
        Quaternion targetSteeringRotation = initialVisualRotation * steeringRotation;
        float blend = 1f - Mathf.Exp(-bankResponse * dt);
        steeringVisualRotation = Quaternion.Slerp(steeringVisualRotation, targetSteeringRotation, blend);

        float rollAngle = 0f;
        if (IsRolling)
        {
            float progress = Mathf.Clamp01(rollElapsed / Mathf.Max(0.01f, rollDuration));
            // Maintain continuous angular travel through 360 degrees with a small speed
            // wave. The visual roll is intentionally not slerp-filtered with the steering pose.
            rollAngle = (360f * progress + 8f * Mathf.Sin(progress * Mathf.PI * 2f)) * rollDirection;
        }
        else if (rollRecoveryElapsed >= 0f)
        {
            float recovery = Mathf.Clamp01(rollRecoveryElapsed / 0.24f);
            // A brief, cancelable inertial settle reads as mass without delaying steering.
            rollAngle = rollRecoveryDirection * 4.5f * Mathf.Sin(recovery * Mathf.PI) * (1f - recovery);
        }

        visualRoot.localRotation = steeringVisualRotation * Quaternion.AngleAxis(rollAngle, Vector3.forward);
    }

    private void OnDisable()
    {
        finalCrashInProgress = false;
        finalCrashFinished = false;
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
