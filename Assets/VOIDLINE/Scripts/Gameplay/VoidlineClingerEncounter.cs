using UnityEngine;

/// <summary>
/// A single, readable Clinger encounter. It attaches only after a landmark telegraph,
/// grants a grace window, then applies a bounded steering/boost-efficiency penalty.
/// Two clean near-misses during the roll dodge window shear it off; missing a scrape never
/// deals bonus damage and the encounter can be tried again on the next clean pass.
/// </summary>
[DisallowMultipleComponent]
public sealed class VoidlineClingerEncounter : MonoBehaviour
{
    [Header("Unlock and warning")]
    [SerializeField, Min(1)] private int attachAfterPassages = 2;
    [SerializeField, Min(0.1f)] private float warningSeconds = 1.8f;
    [SerializeField, Min(1f)] private float graceSeconds = 8f;
    [SerializeField, Range(0.15f, 0.8f)] private float warningCalloutDelay = 0.48f;

    [Header("Penalty")]
    [SerializeField, Range(0.45f, 0.95f)] private float handlingMultiplier = 0.78f;
    [SerializeField, Range(1f, 2f)] private float boostDrainMultiplier = 1.35f;

    [Header("Scrape-Off")]
    [SerializeField, Range(1, 3)] private int requiredScrapes = 2;
    [SerializeField, Min(0.2f)] private float failedAttemptQuietSeconds = 2.5f;
    [SerializeField, Min(0.05f)] private float recoveryBoostReward = 0.18f;

    private VoidlineGameManager manager;
    private VoidlinePlayerController controller;
    private Transform playerRoot;
    private Transform shipRoot;
    private GameObject clingerPrefab;
    private GameObject liveClinger;
    private float warningRemaining;
    private float warningCalloutRemaining;
    private float attachedElapsed;
    private bool warningShown;
    private float quietRemaining;
    private float animationClock;
    private int scrapeCount;
    private bool warningPending;
    private bool hasAttached;
    private bool cleared;

    public bool IsAttached => liveClinger != null && !cleared;
    public int ScrapeProgress => scrapeCount;

    public void Configure(VoidlineGameManager owner, Transform player, VoidlinePlayerController playerControl)
    {
        manager = owner;
        playerRoot = player;
        controller = playerControl;
        Transform model = playerRoot != null ? playerRoot.Find("Model") : null;
        shipRoot = model != null ? model.Find("VOIDLINE_Courier") : model;
        clingerPrefab = Resources.Load<GameObject>("PF_VOIDLINE_Clinger_Industrial");
        ResetEncounterState();
        if (clingerPrefab == null)
            Debug.LogWarning("[VOIDLINE CLINGER] PF_VOIDLINE_Clinger_Industrial prefab is missing; encounter stays disabled.");
    }

    private void ResetEncounterState()
    {
        if (liveClinger != null)
        {
            if (Application.isPlaying) Destroy(liveClinger);
            else DestroyImmediate(liveClinger);
        }
        liveClinger = null;
        warningRemaining = 0f;
        warningCalloutRemaining = 0f;
        warningShown = false;
        attachedElapsed = 0f;
        quietRemaining = 0f;
        animationClock = 0f;
        scrapeCount = 0;
        warningPending = false;
        hasAttached = false;
        cleared = false;
        if (controller != null) controller.SetClingerHandlingMultiplier(1f);
        if (manager != null) manager.SetClingerBoostDrainMultiplier(1f);
    }

    public void NotifyPassageCrossed(int passagesCleared)
    {
        if (manager == null || manager.IsGameOver || manager.IsPaused ||
            hasAttached || warningPending || clingerPrefab == null || passagesCleared < attachAfterPassages)
            return;

        warningPending = true;
        warningRemaining = warningSeconds + warningCalloutDelay;
        warningCalloutRemaining = warningCalloutDelay;
        warningShown = false;
    }

    private void Update()
    {
        if (manager == null || manager.IsGameOver || manager.IsPaused || Time.deltaTime <= 0f)
            return;

        float dt = Time.deltaTime;
        if (quietRemaining > 0f) quietRemaining = Mathf.Max(0f, quietRemaining - dt);

        if (warningPending)
        {
            warningRemaining -= dt;
            if (!warningShown)
            {
                warningCalloutRemaining -= dt;
                if (warningCalloutRemaining <= 0f)
                {
                    warningShown = true;
                    manager.NotifyClingerWarning();
                }
            }
            if (warningRemaining <= 0f) AttachClinger();
            return;
        }

        if (!IsAttached) return;

        attachedElapsed += dt;
        animationClock += dt;

        if (liveClinger != null)
        {
            Transform visual = liveClinger.transform;
            visual.localRotation = Quaternion.Euler(
                3.0f * Mathf.Sin(animationClock * 2.1f),
                7.0f * Mathf.Sin(animationClock * 1.25f),
                45f + 4f * Mathf.Sin(animationClock * 2.7f));
            float pulse = 1f + 0.025f * Mathf.Sin(animationClock * 4.2f);
            visual.localScale = Vector3.one * 1.55f * pulse;
        }

        if (attachedElapsed >= graceSeconds)
        {
            if (controller != null) controller.SetClingerHandlingMultiplier(handlingMultiplier);
            manager.SetClingerBoostDrainMultiplier(boostDrainMultiplier);
        }

    }

    private void AttachClinger()
    {
        warningPending = false;
        warningShown = false;
        if (shipRoot == null)
        {
            Transform model = playerRoot != null ? playerRoot.Find("Model") : null;
            shipRoot = model != null ? model.Find("VOIDLINE_Courier") : model;
        }
        if (shipRoot == null || clingerPrefab == null)
        {
            Debug.LogWarning("[VOIDLINE CLINGER] Attach aborted: ship anchor or prefab unavailable.");
            return;
        }

        Transform engineAnchor = shipRoot.Find("ExhaustSocket_R");
        Transform parent = engineAnchor != null ? engineAnchor : shipRoot;
        liveClinger = Instantiate(clingerPrefab, parent, false);
        liveClinger.name = "VOIDLINE / Attached Clinger";
        liveClinger.transform.localPosition = engineAnchor != null
            ? new Vector3(0.17f, 0.11f, 0.08f)
            : new Vector3(0.55f, 0.12f, -0.38f);
        liveClinger.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        liveClinger.transform.localScale = Vector3.one * 1.55f;

        foreach (var collider in liveClinger.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;
        foreach (var body in liveClinger.GetComponentsInChildren<Rigidbody>(true))
        {
            body.isKinematic = true;
            body.useGravity = false;
            body.detectCollisions = false;
        }

        hasAttached = true;
        cleared = false;
        attachedElapsed = 0f;
        scrapeCount = 0;
        quietRemaining = 0f;
        if (controller != null) controller.SetClingerHandlingMultiplier(1f);
        manager.SetClingerBoostDrainMultiplier(1f);
        manager.NotifyClingerAttached();
        Debug.Log("[VOIDLINE CLINGER] Attached; grace window started.");
    }

    /// <summary>Called only after the game's safe-clearance test approves a genuine near miss.</summary>
    public void NotifyClosePass(bool rollDodgeWindow)
    {
        if (!IsAttached || !rollDodgeWindow || manager == null || manager.IsPaused || manager.IsGameOver)
            return;
        if (attachedElapsed < 0.6f || quietRemaining > 0f) return;

        scrapeCount++;
        quietRemaining = failedAttemptQuietSeconds;

        if (scrapeCount >= requiredScrapes)
        {
            RemoveClinger();
            return;
        }

        manager.NotifyClingerScrapeProgress(scrapeCount, requiredScrapes);
        // The model snaps briefly from its grip, then settles back for the next scrape attempt.
        if (liveClinger != null)
            liveClinger.transform.localRotation *= Quaternion.Euler(0f, 0f, 23f);
    }

    private void RemoveClinger()
    {
        cleared = true;
        if (controller != null) controller.SetClingerHandlingMultiplier(1f);
        if (manager != null)
        {
            manager.SetClingerBoostDrainMultiplier(1f);
            manager.RegisterClingerFreed(recoveryBoostReward);
        }

        if (liveClinger != null)
        {
            if (Application.isPlaying) Destroy(liveClinger);
            else DestroyImmediate(liveClinger);
        }
        liveClinger = null;
        Debug.Log("[VOIDLINE CLINGER] Sheared off after " + scrapeCount + " clean roll passes.");
    }

    public void StopEncounter()
    {
        warningPending = false;
        warningShown = false;
        warningRemaining = 0f;
        warningCalloutRemaining = 0f;
        if (controller != null) controller.SetClingerHandlingMultiplier(1f);
        if (manager != null) manager.SetClingerBoostDrainMultiplier(1f);
        if (liveClinger != null)
        {
            liveClinger.SetActive(false);
            if (Application.isPlaying) Destroy(liveClinger);
            else DestroyImmediate(liveClinger);
        }
        liveClinger = null;
    }

    private void OnDisable()
    {
        // Editor reloads and scene shutdowns must not leave a detached model or altered
        // boost/handling state behind on the courier.
        StopEncounter();
    }
}
