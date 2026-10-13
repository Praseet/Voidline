using UnityEngine;

/// <summary>
/// Chase camera tuned for arcade acceleration: a compact focus-squeeze, forward punch,
/// progressive FOV release and speed-linked dolly create ignition weight; the sustained
/// camera response then follows velocity without destabilising the player's aim.
/// </summary>
[DisallowMultipleComponent]
public sealed class VoidlineCameraEffects : MonoBehaviour
{
    [Header("Impact Response")]
    [SerializeField, Min(0.1f)] private float decayPerSecond = 2.6f;
    [SerializeField, Min(0f)] private float maximumOffset = 0.15f;
    [SerializeField, Min(0f)] private float frequency = 23f;

    [Header("Flight Response")]
    [SerializeField, Range(0f, 32f)] private float boostFovIncrease = 25f;
    [SerializeField, Range(0f, 1.5f)] private float boostDollyIn = 0.92f;
    [SerializeField, Range(0f, 0.1f)] private float boostRumble = 0.044f;
    [SerializeField, Min(0f)] private float fovResponse = 7.5f;
    [SerializeField, Range(0f, 5f)] private float cameraBankDegrees = 3.4f;
    [SerializeField, Range(0f, 0.4f)] private float steeringOffset = 0.18f;
    [SerializeField, Min(0f)] private float steeringResponse = 7.2f;
    [SerializeField, Range(0f, 1f)] private float shakeIntensity = 0.9f;
    [SerializeField, Range(0f, 0.6f)] private float routeYawResponse = 0.42f;
    [SerializeField, Range(0f, 0.6f)] private float routePitchResponse = 0.31f;

    private Camera targetCamera;
    private Vector3 restingLocalPosition;
    private Quaternion restingLocalRotation;
    private float restingFieldOfView = 54f;
    private float trauma;
    private float clock;
    private float boostBlend;
    private float impactRecoil;
    private float impactPitchKick;
    private float impactRollKick;
    private float boostImpactElapsed = -1f;
    private float fieldDeflectionElapsed = -1f;
    private float fieldDeflectionDirection = 1f;
    private Vector2 steering;
    private Vector3 routeOffset;
    private Vector3 routeTangent = Vector3.forward;

    private void Awake()
    {
        // Lower the chase eye slightly and lift its sightline a touch: keep the engine
        // silhouette in view while showing more of the space ahead instead of looking down at it.
        restingLocalPosition = transform.localPosition + new Vector3(0f, -0.12f, 0f);
        restingLocalRotation = Quaternion.Euler(-1.25f, 0f, 0f) * transform.localRotation;
        targetCamera = GetComponent<Camera>();
        if (targetCamera == null) targetCamera = Camera.main;
        if (targetCamera != null)
        {
            // The ship should occupy enough of the chase view to make acceleration feel physical.
            restingFieldOfView = Mathf.Min(targetCamera.fieldOfView, 52f);
            targetCamera.fieldOfView = restingFieldOfView;
        }
    }

    public void AddTrauma(float amount) =>
        trauma = Mathf.Clamp01(trauma + Mathf.Max(0f, amount));

    /// <summary>
    /// An advanced shield/field deflection: brief directional parallax and a tiny lens swell,
    /// not a heavy physical-crash recoil. The boost ignition response remains separate.
    /// </summary>
    public void TriggerFieldDeflection(float direction)
    {
        fieldDeflectionElapsed = 0f;
        fieldDeflectionDirection = direction < 0f ? -1f : 1f;
        AddTrauma(0.055f);
    }

    /// <summary>
    /// NFS-style focus squeeze followed by a rapid lens release. This pulse is independent
    /// of the sustained boost FOV ramp: the player feels one distinct ignition hit.
    /// </summary>
    public void TriggerBoostImpact()
    {
        boostImpactElapsed = 0f;
        impactRecoil = Mathf.Max(impactRecoil, 0.92f);
        impactPitchKick = Mathf.Max(impactPitchKick, 7.5f);
        impactRollKick = Mathf.Max(impactRollKick, 2.0f);
        AddTrauma(0.72f);
    }

    /// <summary>
    /// Fatal collision has a heavier, asymmetric kick than boost ignition: one short impact
    /// pulse, a directional roll, and then a damped settle while the ship performs its final
    /// attitude-loss animation. It does not keep shaking during the result screen.
    /// </summary>
    public void TriggerFinalImpact(float direction)
    {
        float side = direction < 0f ? -1f : 1f;
        impactRecoil = Mathf.Max(impactRecoil, 1.08f);
        impactPitchKick = Mathf.Max(impactPitchKick, 11.5f);
        impactRollKick = side * Mathf.Max(Mathf.Abs(impactRollKick), 6.5f);
        boostImpactElapsed = 0f;
        AddTrauma(0.82f);
    }

    public void SetFlightResponse(float boost, Vector2 steeringInput)
    {
        boostBlend = Mathf.Clamp01(boost);
        steering = Vector2.ClampMagnitude(steeringInput, 1f);
    }

    public void SetRouteResponse(Vector3 centerOffset, Vector3 forwardTangent)
    {
        routeOffset = new Vector3(centerOffset.x, centerOffset.y, 0f);
        routeTangent = forwardTangent.sqrMagnitude > 0.001f
            ? forwardTangent.normalized
            : Vector3.forward;
    }

    public void SetShakeIntensity(float intensity) => shakeIntensity = Mathf.Clamp01(intensity);

    private float EvaluateImpactFovKick(float dt)
    {
        if (boostImpactElapsed < 0f) return 0f;
        boostImpactElapsed += dt;

        // Squeeze the image very briefly; release wider than the squeeze before settling.
        if (boostImpactElapsed < 0.075f)
            return Mathf.Lerp(0f, -8.5f, boostImpactElapsed / 0.075f);
        if (boostImpactElapsed < 0.205f)
            return Mathf.Lerp(-8.5f, 15f, (boostImpactElapsed - 0.075f) / 0.130f);
        if (boostImpactElapsed < 0.62f)
            return 15f * Mathf.Pow(1f - Mathf.Clamp01((boostImpactElapsed - 0.205f) / 0.415f), 1.45f);

        boostImpactElapsed = -1f;
        return 0f;
    }

    private void LateUpdate()
    {
        float dt = Time.unscaledDeltaTime;
        if (dt <= 0f) return;

        Vector3 shake = Vector3.zero;
        if (trauma > 0f)
        {
            clock += dt * frequency;
            trauma = Mathf.Max(0f, trauma - decayPerSecond * dt);
            float strength = trauma * trauma * shakeIntensity;
            shake = new Vector3(
                Mathf.Sin(clock * 1.71f),
                Mathf.Sin(clock * 2.37f),
                Mathf.Sin(clock * 1.19f) * 0.28f) * (maximumOffset * strength);
        }

        Vector3 steeringNudge = new Vector3(-steering.x * steeringOffset,
            -steering.y * steeringOffset * 0.48f, 0f);

        impactRecoil = Mathf.MoveTowards(impactRecoil, 0f, dt * 2.8f);
        impactPitchKick = Mathf.MoveTowards(impactPitchKick, 0f, dt * 21f);
        impactRollKick = Mathf.MoveTowards(impactRollKick, 0f, dt * 10f);

        // The initial impulse moves toward the ship, not away from it. The boosted craft
        // grows subtly in frame as the camera pulls forward into the acceleration.
        Vector3 recoil = new Vector3(0f, -impactRecoil * 0.055f, impactRecoil);
        float speedWeight = boostBlend * boostBlend;
        float rumbleClock = Time.unscaledTime;
        Vector3 speedRumble = new Vector3(
            Mathf.Sin(rumbleClock * 31f) * boostRumble,
            Mathf.Sin(rumbleClock * 43f) * boostRumble * 0.45f,
            Mathf.Sin(rumbleClock * 23f) * boostRumble * 0.18f) * speedWeight;
        Vector3 boostDolly = Vector3.forward * (boostDollyIn * boostBlend);
        float fieldDeflection = 0f;
        if (fieldDeflectionElapsed >= 0f)
        {
            fieldDeflectionElapsed += dt;
            float progress = Mathf.Clamp01(fieldDeflectionElapsed / 0.28f);
            fieldDeflection = Mathf.Sin(progress * Mathf.PI) * (1f - 0.18f * progress);
            if (progress >= 1f) fieldDeflectionElapsed = -1f;
        }
        Vector3 deflectionNudge = new Vector3(
            fieldDeflectionDirection * 0.028f * fieldDeflection,
            0.012f * fieldDeflection,
            0f);
        transform.localPosition = restingLocalPosition + routeOffset + steeringNudge
            + shake + recoil + boostDolly + speedRumble + deflectionNudge;

        float routeYaw = Mathf.Atan2(routeTangent.x, Mathf.Max(0.001f, routeTangent.z)) * Mathf.Rad2Deg;
        float routePitch = Mathf.Atan2(routeTangent.y,
            Mathf.Sqrt(routeTangent.x * routeTangent.x + routeTangent.z * routeTangent.z)) * Mathf.Rad2Deg;
        Quaternion routeAim = Quaternion.Euler(
            -Mathf.Clamp(routePitch, -12f, 12f) * routePitchResponse,
            Mathf.Clamp(routeYaw, -18f, 18f) * routeYawResponse,
            -Mathf.Clamp(routeYaw, -18f, 18f) * 0.10f);
        Quaternion lean = Quaternion.Euler(-steering.y * 0.65f, steering.x * 0.45f,
            -steering.x * cameraBankDegrees);
        Quaternion impactRotation = Quaternion.Euler(impactPitchKick, 0f, impactRollKick);
        Quaternion fieldRotation = Quaternion.Euler(0f,
            -fieldDeflectionDirection * 0.7f * fieldDeflection,
            fieldDeflectionDirection * 0.45f * fieldDeflection);
        Quaternion targetRotation = restingLocalRotation * routeAim * lean * impactRotation * fieldRotation;
        float rotationBlend = 1f - Mathf.Exp(-steeringResponse * dt);
        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRotation, rotationBlend);

        if (targetCamera != null)
        {
            float impactKick = EvaluateImpactFovKick(dt);
            float targetFov = restingFieldOfView + boostFovIncrease * boostBlend + impactKick
                + 1.8f * fieldDeflection;
            targetFov = Mathf.Clamp(targetFov, 42f, 92f);
            float response = boostImpactElapsed >= 0f && boostImpactElapsed < 0.24f
                ? 28f
                : fovResponse;
            float fovBlend = 1f - Mathf.Exp(-response * dt);
            targetCamera.fieldOfView = Mathf.Lerp(targetCamera.fieldOfView, targetFov, fovBlend);
        }
    }

    private void OnDisable()
    {
        transform.localPosition = restingLocalPosition;
        transform.localRotation = restingLocalRotation;
        boostImpactElapsed = -1f;
        if (targetCamera != null) targetCamera.fieldOfView = restingFieldOfView;
    }
}
