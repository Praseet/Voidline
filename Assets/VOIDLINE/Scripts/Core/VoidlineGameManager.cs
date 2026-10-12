using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Runtime owner for VOIDLINE's first playable loop: ship, hazards, score, hull,
/// HUD and restart flow. Scene setup is intentionally generated at runtime so
/// the prototype does not depend on brittle Inspector references.
/// </summary>
[DisallowMultipleComponent]
public sealed class VoidlineGameManager : MonoBehaviour
{
    [Header("Run Tuning")]
    [SerializeField, Min(1f)] private float worldSpeed = 12f;
    [SerializeField, Min(1f)] private float spawnAheadDistance = 78f;
    [SerializeField, Min(0.35f)] private float initialSpawnInterval = 1.45f;
    [SerializeField, Min(1)] private int startingHull = 3;

    [Header("Passage Rewards")]
    [SerializeField, Min(0)] private int gateCrossingBonus = 250;
    [SerializeField, Min(0)] private int archCrossingBonus = 100;

    [Header("Boost and Momentum")]
    [SerializeField, Range(1f, 2.2f)] private float boostMultiplier = 2.1f;
    [SerializeField, Min(0.1f)] private float boostResponse = 4.6f;
    [SerializeField, Range(0.05f, 1f)] private float boostDrainPerSecond = 0.34f;
    [SerializeField, Range(0.05f, 1f)] private float boostRechargePerSecond = 0.28f;
    [SerializeField, Min(0f)] private float boostRechargeDelay = 0.65f;

    private float baseWorldSpeed = 12f;
    private float boostEnergy = 1f;
    private float boostBlend;
    private float boostRechargeReadyAt;
    private float boostImpactElapsed;
    private bool boostImpactActive;
    private bool boostHeld;
    private bool boostDepleted;
    private CorridorScroller corridorScroller;
    private VoidlineSpaceSequence spaceSequence;
    private VoidlineRouteDirector routeDirector;
    private System.Random gameplayRandom;
    private VoidlineEncounterDirector encounterDirector;
    private string lastEncounterPattern = "INITIAL";
    private Vector2 lastSafeLane;
    private VoidlineFlightEffects flightEffects;
    private VoidlineSpeedEffects speedEffects;

    private Transform player;
    private VoidlinePlayerController playerController;
    private Canvas hudCanvas;
    private Text statusText;
    private Text centerText;
    private Text overlayButtonLabel;
    private Text controlsText;
    private Text milestoneText;
    private float milestoneVisibleUntil;
    private Image damageFlashImage;
    private float damageFlashAlpha;
    private int bestScore;
    private VoidlineAudioFeedback audioFeedback;
    private VoidlineAdaptiveMusic adaptiveMusic;
    private VoidlineCameraEffects cameraEffects;
    private GameObject gameOverPanel;
    private Button restartButton;
    private Material asteroidMaterial;
    private Material courierHullMaterial;
    private Material courierArmorMaterial;
    private Material courierTitaniumMaterial;
    private Material courierDarkMaterial;
    private Material courierCanopyMaterial;
    private Material courierCyanMaterial;
    private Material courierAmberMaterial;
    private Material courierExhaustOuterMaterial;
    private Material courierExhaustCoreMaterial;
    [SerializeField] private GameObject[] asteroidPrefabs;
    [SerializeField] private GameObject repairPickupPrefab;
    private Material pickupMaterial;
    [SerializeField] private VoidlineRunSettings runSettings;
    [SerializeField] private InputActionAsset inputActions;
    private InputAction pauseAction;
    private InputAction restartAction;
    private float minimumSpawnInterval = 0.7f;
    private float difficultyRampPerSecond = 0.004f;
    private float spawnXLimit = 3.2f;
    private float spawnYLimit = 2.2f;
    private float spawnZBackJitter = 8f;
    private float spawnZForwardJitter = 18f;
    private float pickupInterval = 16f;
    private int repairScoreBonus = 100;
    private readonly System.Collections.Generic.Stack<VoidlineAsteroid> asteroidPool = new System.Collections.Generic.Stack<VoidlineAsteroid>();

    private int hull;
    private float elapsed;
    private float nextSpawnAt;
    private float nextPickupAt;
    private int bonusPoints;
    private int repairCellsCollected;
    private readonly System.Collections.Generic.Stack<VoidlinePickup> pickupPool = new System.Collections.Generic.Stack<VoidlinePickup>();
    private float nextDamageAt;
    private bool gameOver;
    private bool paused;
    private bool restartRequested;
    private float scoreRefreshAt;
    private int passedAsteroids;
    private int dodgedAsteroids;
    private int passagesCleared;
    private int gatesCleared;
    private int archesCleared;
    private int encounterIndex;
    private int nearMisses;
    private int flowChain;
    private float flowChainExpiresAt;

    public bool IsGameOver => gameOver;
    public bool IsPaused => paused;
    public float WorldSpeed => worldSpeed;
    public float BoostBlend => boostBlend;
    public float BoostEnergy => boostEnergy;

    private void ApplyRunSettings()
    {
        if (runSettings == null) return;
        worldSpeed = runSettings.WorldSpeed;
        spawnAheadDistance = runSettings.SpawnAheadDistance;
        startingHull = runSettings.StartingHull;
        initialSpawnInterval = runSettings.InitialSpawnInterval;
        minimumSpawnInterval = runSettings.MinimumSpawnInterval;
        difficultyRampPerSecond = runSettings.DifficultyRampPerSecond;
        spawnXLimit = runSettings.SpawnXLimit;
        spawnYLimit = runSettings.SpawnYLimit;
        spawnZBackJitter = runSettings.SpawnZBackJitter;
        spawnZForwardJitter = runSettings.SpawnZForwardJitter;
        pickupInterval = runSettings.PickupInterval;
        repairScoreBonus = runSettings.RepairScoreBonus;
    }
    private void BindRunActions()
    {
        if (inputActions == null) return;

        pauseAction = inputActions.FindAction("Player/Pause", false);
        restartAction = inputActions.FindAction("Player/Restart", false);

        if (pauseAction != null)
        {
            pauseAction.performed -= HandlePauseAction;
            pauseAction.performed += HandlePauseAction;
            pauseAction.Enable();
        }

        if (restartAction != null)
        {
            restartAction.performed -= HandleRestartAction;
            restartAction.performed += HandleRestartAction;
            restartAction.Enable();
        }
    }
    // Time.timeScale and AudioListener.pause are process-wide state. With Unity's
    // Enter Play Mode optimizations enabled they can survive a previous paused run,
    // so reset them even when the domain is not reloaded.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetGlobalPauseState()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }

    private void Awake()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        paused = false;
        gameOver = false;
        restartRequested = false;
        ApplyRunSettings();
        baseWorldSpeed = Mathf.Max(1f, worldSpeed);
        worldSpeed = baseWorldSpeed;
        boostEnergy = 1f;
        BindRunActions();
        bestScore = PlayerPrefs.GetInt("VOIDLINE_BEST_SCORE", 0);
        audioFeedback = GetComponent<VoidlineAudioFeedback>();
        if (audioFeedback == null) audioFeedback = gameObject.AddComponent<VoidlineAudioFeedback>();
        adaptiveMusic = GetComponent<VoidlineAdaptiveMusic>();
        if (adaptiveMusic == null) adaptiveMusic = gameObject.AddComponent<VoidlineAdaptiveMusic>();
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            cameraEffects = mainCamera.GetComponent<VoidlineCameraEffects>();
            if (cameraEffects == null) cameraEffects = mainCamera.gameObject.AddComponent<VoidlineCameraEffects>();
            speedEffects = mainCamera.GetComponent<VoidlineSpeedEffects>();
            if (speedEffects == null) speedEffects = mainCamera.gameObject.AddComponent<VoidlineSpeedEffects>();
            speedEffects.Configure(this);
        }
        corridorScroller = FindAnyObjectByType<CorridorScroller>();
        routeDirector = corridorScroller != null
            ? corridorScroller.GetComponent<VoidlineRouteDirector>()
            : FindAnyObjectByType<VoidlineRouteDirector>();
        if (routeDirector != null)
        {
            routeDirector.BeginRun();
            gameplayRandom = new System.Random(routeDirector.RunSeed);
        }
        encounterDirector = new VoidlineEncounterDirector();
        encounterDirector.BeginRun(routeDirector != null ? routeDirector.RunSeed : 73129);
        spaceSequence = corridorScroller != null
            ? corridorScroller.GetComponent<VoidlineSpaceSequence>()
            : FindAnyObjectByType<VoidlineSpaceSequence>();
        if (spaceSequence != null) spaceSequence.ResetForRun();
        if (corridorScroller != null) corridorScroller.SetSpeed(worldSpeed);
        hull = startingHull;
        if (asteroidPrefabs == null || asteroidPrefabs.Length == 0) asteroidMaterial = CreateMaterial(new Color(0.24f, 0.27f, 0.32f), 0.08f);
        if (repairPickupPrefab == null) pickupMaterial = CreateMaterial(new Color(0.025f, 0.65f, 0.92f), 0.35f);
        EnsurePlayer();
        flightEffects = player.GetComponent<VoidlineFlightEffects>();
        if (flightEffects == null) flightEffects = player.gameObject.AddComponent<VoidlineFlightEffects>();
        flightEffects.Configure(playerController, this);
        EnsureHud();
        nextSpawnAt = Time.time + 1.0f;
        nextPickupAt = Time.time + 4.5f;
        RefreshHud();
    }

    private void HandlePauseAction(InputAction.CallbackContext context)
    {
        // The Input System asset contains keyboard and gamepad bindings; handle both here.
        // Keyboard polling below remains only as a fallback when no Pause action is configured.
        if (!gameOver) TogglePause();
    }

    private void HandleRestartAction(InputAction.CallbackContext context)
    {
        if (gameOver) restartRequested = true;
    }
    public void SetBoostHeld(bool requested)
    {
        if (!requested)
        {
            if (boostHeld) boostRechargeReadyAt = Time.time + boostRechargeDelay;
            boostHeld = false;
            boostDepleted = false;
            return;
        }

        if (paused || gameOver || boostDepleted || boostEnergy <= 0.005f) return;
        if (boostHeld) return;

        boostHeld = true;
        if (audioFeedback != null) audioFeedback.PlayBoostStart();
        TriggerBoostImpact();
    }

    /// <summary>
    /// Adds weight to boost ignition with a short hit-stop followed by a slower release.
    /// The envelope is driven from unscaled Update time (not a coroutine), so it always
    /// restores the global time scale even if the Editor stalls during a runtime command.
    /// </summary>
    private void TriggerBoostImpact()
    {
        if (cameraEffects != null) cameraEffects.TriggerBoostImpact();
        if (speedEffects != null) speedEffects.TriggerBoostBurst();
        if (paused || gameOver) return;

        boostImpactElapsed = 0f;
        boostImpactActive = true;
        Time.timeScale = 0.035f;
    }

    private void UpdateBoostImpactFrame(float unscaledDeltaTime)
    {
        if (!boostImpactActive) return;
        if (paused || gameOver)
        {
            boostImpactActive = false;
            Time.timeScale = paused ? 0f : 1f;
            return;
        }

        boostImpactElapsed += unscaledDeltaTime;
        if (boostImpactElapsed < 0.085f)
        {
            Time.timeScale = 0.035f;
        }
        else if (boostImpactElapsed < 0.30f)
        {
            Time.timeScale = 0.28f;
        }
        else if (boostImpactElapsed < 0.45f)
        {
            Time.timeScale = 0.66f;
        }
        else
        {
            boostImpactActive = false;
            Time.timeScale = 1f;
        }
    }

    private void CancelBoostImpactFrame()
    {
        boostImpactActive = false;
        boostImpactElapsed = 0f;
        Time.timeScale = paused ? 0f : 1f;
    }

    private void UpdateBoostState(float dt)
    {
        if (boostHeld)
        {
            boostEnergy = Mathf.Max(0f, boostEnergy - boostDrainPerSecond * dt);
            if (boostEnergy <= 0f)
            {
                boostHeld = false;
                boostDepleted = true;
                boostRechargeReadyAt = Time.time + boostRechargeDelay;
            }
        }
        else if (boostEnergy < 1f && Time.time >= boostRechargeReadyAt)
        {
            boostEnergy = Mathf.Min(1f, boostEnergy + boostRechargePerSecond * dt);
        }

        float target = boostHeld ? 1f : 0f;
        // The ignition hit-stop slows world simulation, but it must not delay the visual
        // response itself. Unscaled time lets boost reach the camera, plume and world-speed
        // ramps immediately, so the slow frame is followed by a real acceleration release.
        boostBlend = Mathf.MoveTowards(boostBlend, target, boostResponse * Time.unscaledDeltaTime);
        float easedBoost = boostBlend * boostBlend * (3f - 2f * boostBlend);
        worldSpeed = baseWorldSpeed * Mathf.Lerp(1f, boostMultiplier, easedBoost);

        if (corridorScroller != null) corridorScroller.SetSpeed(worldSpeed);
        if (cameraEffects != null)
        {
            cameraEffects.SetFlightResponse(boostBlend,
                playerController != null ? playerController.NormalizedVelocity : Vector2.zero);
            if (routeDirector != null)
            {
                cameraEffects.SetRouteResponse(
                    routeDirector.GetCenterOffsetAtDistance(routeDirector.DistanceTravelled),
                    routeDirector.GetTrackTangentAtDistance(routeDirector.DistanceTravelled));
            }
        }
        if (flightEffects != null) flightEffects.SetBoostBlend(boostBlend);
        if (speedEffects != null) speedEffects.SetBoostBlend(boostBlend);
        if (audioFeedback != null) audioFeedback.SetBoostLevel(boostBlend);
    }

    private void ClearBoostPresentation()
    {
        CancelBoostImpactFrame();
        boostHeld = false;
        boostDepleted = false;
        boostRechargeReadyAt = Time.time + boostRechargeDelay;
        boostBlend = 0f;
        worldSpeed = baseWorldSpeed;
        if (corridorScroller != null) corridorScroller.SetSpeed(worldSpeed);
        if (cameraEffects != null) cameraEffects.SetFlightResponse(0f, Vector2.zero);
        if (flightEffects != null) flightEffects.SetBoostBlend(0f);
        if (speedEffects != null) speedEffects.SetBoostBlend(0f);
        if (audioFeedback != null) audioFeedback.SetBoostLevel(0f);
    }

    public void NotifyRollStarted(float direction)
    {
        if (gameOver || paused) return;
        if (audioFeedback != null) audioFeedback.PlayRoll();
        if (cameraEffects != null) cameraEffects.AddTrauma(0.065f);
    }

    public void RegisterAsteroidDodged()
    {
        if (gameOver) return;
        dodgedAsteroids++;
        int chain = AdvanceFlowChain();
        int reward = 50 + Mathf.Min(chain - 1, 5) * 10;
        bonusPoints += reward;
        if (audioFeedback != null) audioFeedback.PlayPass();
        if (cameraEffects != null) cameraEffects.AddTrauma(0.035f);
        ShowMilestoneMessage("ROLL DODGE  //  FLOW x" + chain + "  //  +" + reward, 1.2f);
        RefreshHud();
    }

    private void Update()
    {
        UpdateBoostImpactFrame(Time.unscaledDeltaTime);
        UpdateAdaptiveMusic();
        UpdateMilestonePresentation();
        if (flowChain > 0 && Time.time >= flowChainExpiresAt) flowChain = 0;

        if (damageFlashImage != null && damageFlashAlpha > 0f)
        {
            damageFlashAlpha = Mathf.MoveTowards(damageFlashAlpha, 0f, Time.unscaledDeltaTime * 1.8f);
            Color flashColor = damageFlashImage.color;
            flashColor.a = damageFlashAlpha;
            damageFlashImage.color = flashColor;
        }

        if (restartRequested)
        {
            restartRequested = false;
            RestartRun();
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (pauseAction == null && !gameOver && keyboard != null &&
            (keyboard.escapeKey.wasPressedThisFrame || keyboard.pKey.wasPressedThisFrame))
            TogglePause();

        if (paused) return;

        if (gameOver)
        {
            if (restartAction == null && keyboard != null &&
                (keyboard.rKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame))
                RestartRun();
            return;
        }

        UpdateBoostState(Time.deltaTime);
        elapsed += Time.deltaTime;
        if (Time.time >= nextSpawnAt)
        {
            float pacingMultiplier = SpawnAsteroid();
            float difficulty = Mathf.Clamp(1f - elapsed * difficultyRampPerSecond,
                minimumSpawnInterval / Mathf.Max(initialSpawnInterval, 0.01f), 1f);
            nextSpawnAt = Time.time + initialSpawnInterval * difficulty * pacingMultiplier;
        }

        if (Time.time >= nextPickupAt)
        {
            SpawnPickup();
            nextPickupAt = Time.time + Mathf.Max(4f, pickupInterval);
        }

        if (Time.time >= scoreRefreshAt)
        {
            RefreshHud();
            scoreRefreshAt = Time.time + 0.12f;
        }
    }
    private void UpdateAdaptiveMusic()
    {
        if (adaptiveMusic == null) return;
        // A single continuous route: music reacts to boost, hull, pause and game over.
        adaptiveMusic.SetSituation(boostBlend, hull, startingHull, paused, gameOver);
    }

    private void TogglePause()
    {
        if (gameOver || gameOverPanel == null) return;

        paused = !paused;
        if (paused) ClearBoostPresentation();
        if (audioFeedback != null) audioFeedback.PlayUiTick();
        Time.timeScale = paused ? 0f : 1f;

        if (paused)
        {
            centerText.text = "PAUSED" + System.Environment.NewLine + System.Environment.NewLine + "Press Escape to resume";
            if (overlayButtonLabel != null) overlayButtonLabel.text = "RESUME RUN";
            gameOverPanel.SetActive(true);
        }
        else
        {
            gameOverPanel.SetActive(false);
            if (overlayButtonLabel != null) overlayButtonLabel.text = "RESTART RUN";
        }
    }
    private void HandleOverlayButton()
    {
        if (paused) TogglePause();
        else RestartRun();
    }
    private void EnsurePlayer()
    {
        GameObject playerObject = GameObject.Find("Player");
        if (playerObject == null)
            playerObject = new GameObject("Player");

        player = playerObject.transform;
        player.position = new Vector3(0f, 0f, 0f);

        Rigidbody body = playerObject.GetComponent<Rigidbody>();
        if (body == null)
            body = playerObject.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;

        BoxCollider collider = playerObject.GetComponent<BoxCollider>();
        if (collider == null)
            collider = playerObject.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.center = Vector3.zero;
        collider.size = new Vector3(2.05f, 0.84f, 2.3f);

        playerController = playerObject.GetComponent<VoidlinePlayerController>();
        if (playerController == null)
            playerController = playerObject.AddComponent<VoidlinePlayerController>();
        playerController.Configure(this, inputActions, routeDirector);

        Transform visualRoot = player.Find("Model");
        if (visualRoot == null)
        {
            GameObject model = new GameObject("Model");
            visualRoot = model.transform;
            visualRoot.SetParent(player, false);
        }

        RemoveLegacyShipVisuals(player);
        RemoveLegacyShipVisuals(visualRoot);

        // The authored courier is the only intended player ship. The legacy Kenney placeholder
        // used to satisfy the check below and short-circuit this path, so the bespoke craft was
        // imported and textured but never actually displayed at runtime.
        Transform authoredCraft = visualRoot.Find("VOIDLINE_Courier");
        if (authoredCraft == null)
        {
            BuildShipVisual(visualRoot);
        }
    }

    /// <summary>
    /// Removes the superseded placeholder ship, its stray empty engine anchors and any primitive
    /// fallback parts. The authored FBX carries its own exhaust sockets, so leaving scene-level
    /// sockets behind would create duplicate anchors for the exhaust trails. Call on both the player
    /// root and the Model child, because the shipped scenes place EngineLeft/EngineRight on Player.
    /// </summary>
    private static void RemoveLegacyShipVisuals(Transform searchRoot)
    {
        string[] legacyNames =
        {
            "craft_speederD",
            "EngineLeft",
            "EngineRight",
            "ExhaustSocket_L",
            "ExhaustSocket_R",
            "Hull", "Nose", "LeftWing", "RightWing", "LeftEngine", "RightEngine"
        };

        for (int i = 0; i < legacyNames.Length; i++)
        {
            Transform legacy = searchRoot.Find(legacyNames[i]);
            if (legacy != null) Destroy(legacy.gameObject);
        }
    }

    /// <summary>
    /// The authored craft is modelled at roughly 3.6 x 4.2 units, while hazards are 0.68-0.96
    /// units and the player trigger collider is 1.55 x 1.8. This brings the bespoke hull down to
    /// the established gameplay scale so it reads as a fragile courier, not a battlestation.
    /// </summary>
    private const float CourierScale = 0.82f;

    /// <summary>
    /// Recentres the scaled craft so its visual volume sits on the player collision root instead
    /// of the FBX origin, which is offset towards the nozzles.
    /// </summary>
    private static void CentreCraftOnPlayer(GameObject craft)
    {
        Renderer[] renderers = craft.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            craft.transform.localPosition = Vector3.zero;
            return;
        }

        Bounds combined = new Bounds();
        bool hasBounds = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i].bounds.size == Vector3.zero) continue;
            if (!hasBounds)
            {
                combined = new Bounds(renderers[i].bounds.center, renderers[i].bounds.size);
                hasBounds = true;
            }
            else
            {
                combined.Encapsulate(renderers[i].bounds);
            }
        }

        if (!hasBounds)
        {
            craft.transform.localPosition = Vector3.zero;
            return;
        }

        // Shift by the visual centre relative to the parented player root.
        Transform parent = craft.transform.parent;
        Vector3 localCentre = parent == null
            ? combined.center
            : parent.InverseTransformPoint(combined.center);
        craft.transform.localPosition -= localCentre;
    }

    private void BuildShipVisual(Transform root)
    {
        // Prefer the authored Blender craft; primitives remain only as an explicit fallback
        // for an import failure, never as the intended player-ship art direction.
        GameObject courierAsset = Resources.Load<GameObject>("VOIDLINE_Courier");
        if (courierAsset != null)
        {
            GameObject craft = Instantiate(courierAsset, root, false);
            craft.name = "VOIDLINE_Courier";
            craft.transform.localRotation = Quaternion.identity;
            craft.transform.localScale = Vector3.one * CourierScale;
            CentreCraftOnPlayer(craft);
            ApplyCourierMaterials(craft);
            Debug.Log("[VOIDLINE] Loaded authored courier craft and layered exhaust meshes.");
            return;
        }

        Debug.LogWarning("[VOIDLINE] Resources/VOIDLINE_Courier.fbx is missing; using emergency primitive ship fallback.");
        Material hullMaterial = CreateMaterial(new Color(0.12f, 0.78f, 0.92f), 0.35f);
        Material wingMaterial = CreateMaterial(new Color(0.12f, 0.22f, 0.34f), 0.25f);
        Material cockpitMaterial = CreateMaterial(new Color(0.55f, 0.93f, 1f), 0.6f);
        Material engineMaterial = CreateMaterial(new Color(0.1f, 0.45f, 1f), 0.5f);

        CreateShipPart(root, "Hull", PrimitiveType.Cube, new Vector3(0f, 0f, 0.05f),
            new Vector3(0.58f, 0.24f, 1.55f), hullMaterial);
        CreateShipPart(root, "Nose", PrimitiveType.Cube, new Vector3(0f, 0.015f, 0.82f),
            new Vector3(0.28f, 0.18f, 0.55f), cockpitMaterial);
        CreateShipPart(root, "LeftWing", PrimitiveType.Cube, new Vector3(-0.58f, -0.015f, -0.05f),
            new Vector3(0.78f, 0.12f, 0.68f), wingMaterial);
        CreateShipPart(root, "RightWing", PrimitiveType.Cube, new Vector3(0.58f, -0.015f, -0.05f),
            new Vector3(0.78f, 0.12f, 0.68f), wingMaterial);
        CreateShipPart(root, "LeftEngine", PrimitiveType.Sphere, new Vector3(-0.24f, -0.02f, -0.76f),
            new Vector3(0.19f, 0.19f, 0.38f), engineMaterial);
        CreateShipPart(root, "RightEngine", PrimitiveType.Sphere, new Vector3(0.24f, -0.02f, -0.76f),
            new Vector3(0.19f, 0.19f, 0.38f), engineMaterial);
    }

    private void ApplyCourierMaterials(GameObject craft)
    {
        Texture2D baseMap = Resources.Load<Texture2D>("T_VOIDLINE_Courier_BaseColor");
        Texture2D normalMap = Resources.Load<Texture2D>("T_VOIDLINE_Courier_Normal");
        // Mask carries metal/smooth variation; ORM carries the packed occlusion/roughness/metal
        // detail. Both were authored, so use each for the channel it was actually baked for
        // instead of feeding the same mask into metallic and occlusion.
        Texture2D maskMap = Resources.Load<Texture2D>("T_VOIDLINE_Courier_Mask");
        Texture2D ormMap = Resources.Load<Texture2D>("T_VOIDLINE_Courier_ORM");

        courierHullMaterial = CreateCourierMaterial("VOIDLINE / Ceramic Titanium", new Color(0.80f, 0.88f, 0.96f),
            0.78f, 0.38f, baseMap, normalMap, maskMap, Color.black, 0f, false, 1f, ormMap);
        courierArmorMaterial = CreateCourierMaterial("VOIDLINE / Graphite Armor", new Color(0.38f, 0.52f, 0.67f),
            0.84f, 0.34f, baseMap, normalMap, maskMap, Color.black, 0f, false, 1f, ormMap);
        courierTitaniumMaterial = CreateCourierMaterial("VOIDLINE / Brushed Titanium", new Color(0.88f, 0.94f, 1f),
            0.90f, 0.30f, baseMap, normalMap, maskMap, Color.black, 0f, false, 1f, ormMap);
        courierDarkMaterial = CreateCourierMaterial("VOIDLINE / Ceramic Nozzles", new Color(0.16f, 0.24f, 0.32f),
            0.78f, 0.30f, baseMap, normalMap, maskMap, Color.black, 0f, false);
        courierCanopyMaterial = CreateCourierMaterial("VOIDLINE / Ion Glass", new Color(0.025f, 0.13f, 0.22f),
            0.52f, 0.88f, null, normalMap, null, new Color(0.01f, 0.24f, 0.46f), 0.55f, false);
        courierCyanMaterial = CreateCourierMaterial("VOIDLINE / Cyan Emitters", new Color(0.05f, 0.38f, 0.58f),
            0.42f, 0.48f, null, null, null, new Color(0.015f, 0.54f, 1f), 2.2f, false);
        courierAmberMaterial = CreateCourierMaterial("VOIDLINE / Amber Warnings", new Color(0.62f, 0.22f, 0.045f),
            0.40f, 0.44f, null, null, null, new Color(1f, 0.16f, 0.018f), 1.8f, false);
        courierExhaustOuterMaterial = CreateCourierMaterial("VOIDLINE / Plasma Sheath", new Color(0.018f, 0.25f, 0.62f),
            0.04f, 0.28f, null, null, null, new Color(0.008f, 0.26f, 1f), 3.6f, true, 0.42f);
        courierExhaustCoreMaterial = CreateCourierMaterial("VOIDLINE / Plasma Core", new Color(0.42f, 0.82f, 1f),
            0f, 0.22f, null, null, null, new Color(0.16f, 0.68f, 1f), 5.2f, true, 0.88f);

        Renderer[] renderers = craft.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            string part = renderer.gameObject.name.ToLowerInvariant();

            // The authored FBX ships several multi-material renderers (Engine_Nozzles and
            // Titanium_Details each carry three slots). Assign per material slot so the layered
            // ceramic/titanium split survives instead of collapsing to one flat material.
            Material[] slots = renderer.sharedMaterials;
            if (slots != null && slots.Length > 1)
            {
                for (int s = 0; s < slots.Length; s++)
                {
                    string slotName = slots[s] == null ? string.Empty : slots[s].name.ToLowerInvariant();
                    slots[s] = ResolveCourierMaterial(slotName, part);
                }

                // Writing the array back is required; mutating the copy changes nothing.
                renderer.sharedMaterials = slots;
            }
            else
            {
                Material single = slots != null && slots.Length == 1 ? slots[0] : null;
                string slotName = single == null ? string.Empty : single.name.ToLowerInvariant();
                renderer.sharedMaterial = ResolveCourierMaterial(slotName, part);
            }

            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Simple;
        }

        if (baseMap == null || normalMap == null)
            Debug.LogWarning("[VOIDLINE] One or more courier PBR texture maps are missing. The craft still renders, but material detail is reduced.");
    }

    /// <summary>
    /// Maps one authored material slot onto its runtime courier material. The FBX slot name is
    /// authoritative because several meshes deliberately mix materials; the mesh name is consulted
    /// only when the slot carries no recognisable signal.
    /// </summary>
    private Material ResolveCourierMaterial(string slotName, string meshName)
    {
        Material fromSlot = MatchCourierToken(slotName);
        if (fromSlot != null) return fromSlot;

        Material fromMesh = MatchCourierToken(meshName);
        return fromMesh != null ? fromMesh : courierHullMaterial;
    }

    /// <summary>
    /// Classifies a single name into a courier material, or null when nothing matches. Checking one
    /// name at a time keeps a mesh called "Armor_Panels" from overriding a nozzle slot it contains.
    /// </summary>
    private Material MatchCourierToken(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        string key = name.ToLowerInvariant();

        if (key.Contains("exhaustcore") || key.Contains("plasma_core")) return courierExhaustCoreMaterial;
        if (key.Contains("exhaustouter") || key.Contains("plasma_outer")) return courierExhaustOuterMaterial;
        if (key.Contains("cockpit") || key.Contains("canopy") || key.Contains("cyanglass")) return courierCanopyMaterial;
        if (key.Contains("amber") || key.Contains("warning")) return courierAmberMaterial;
        if (key.Contains("cyan") || key.Contains("emitter")) return courierCyanMaterial;
        if (key.Contains("nozzle")) return courierDarkMaterial;
        if (key.Contains("titanium")) return courierTitaniumMaterial;
        if (key.Contains("armor") || key.Contains("slate")) return courierArmorMaterial;
        if (key.Contains("engine")) return courierDarkMaterial;
        if (key.Contains("hull") || key.Contains("navy") || key.Contains("ceramic")) return courierHullMaterial;
        return null;
    }

    private static Material CreateCourierMaterial(string materialName, Color tint, float metallic, float smoothness,
        Texture2D baseMap, Texture2D normalMap, Texture2D maskMap, Color emission, float emissionStrength,
        bool transparent, float alpha = 1f, Texture2D occlusionMap = null)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        Material material = new Material(shader) { name = materialName, color = new Color(tint.r, tint.g, tint.b, alpha) };

        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", new Color(tint.r, tint.g, tint.b, alpha));
        if (material.HasProperty("_Color")) material.SetColor("_Color", new Color(tint.r, tint.g, tint.b, alpha));
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        if (baseMap != null && material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", baseMap);
        if (normalMap != null && material.HasProperty("_BumpMap"))
        {
            material.SetTexture("_BumpMap", normalMap);
            material.SetFloat("_BumpScale", 0.78f);
            material.EnableKeyword("_NORMALMAP");
        }
        if (maskMap != null && material.HasProperty("_MetallicGlossMap"))
        {
            material.SetTexture("_MetallicGlossMap", maskMap);
            material.EnableKeyword("_METALLICGLOSSMAP");
        }
        // URP samples occlusion from the green channel, which is what the authored ORM map packs
        // there. Feeding the metallic mask here instead darkened the hull with noise.
        Texture2D occlusion = occlusionMap != null ? occlusionMap : maskMap;
        if (occlusion != null && material.HasProperty("_OcclusionMap"))
        {
            material.SetTexture("_OcclusionMap", occlusion);
            material.SetFloat("_OcclusionStrength", 0.35f);
            material.EnableKeyword("_OCCLUSIONMAP");
        }
        if (emissionStrength > 0f)
        {
            material.EnableKeyword("_EMISSION");
            if (material.HasProperty("_EmissionColor"))
                material.SetColor("_EmissionColor", emission * emissionStrength);
        }
        if (transparent)
        {
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = 3000;
        }
        return material;
    }

    private static void CreateShipPart(Transform parent, string partName, PrimitiveType primitive,
        Vector3 localPosition, Vector3 localScale, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(primitive);
        part.name = partName;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = localScale;
        Renderer renderer = part.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        Collider collider = part.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);
    }

    private void EnsureHud()
    {
        if (EventSystem.current == null)
        {
            GameObject eventSystem = new GameObject("VOIDLINE EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            DontDestroyOnLoad(eventSystem);
        }
        GameObject canvasObject = new GameObject("VOIDLINE HUD", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        hudCanvas = canvasObject.GetComponent<Canvas>();
        hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        hudCanvas.sortingOrder = 50;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        statusText = CreateText(hudCanvas.transform, "Run Status", font, 25,
            TextAnchor.UpperLeft, new Vector2(24f, -20f), new Vector2(470f, 100f));
        RectTransform statusRect = statusText.rectTransform;
        statusRect.sizeDelta = new Vector2(720f, 230f);
        statusRect.anchorMin = new Vector2(0f, 1f);
        statusRect.anchorMax = new Vector2(0f, 1f);
        statusRect.pivot = new Vector2(0f, 1f);
        statusText.fontSize = 23;
        statusText.color = new Color(0.78f, 0.95f, 1f);

        milestoneText = CreateText(hudCanvas.transform, "Passage Milestone", font, 27,
            TextAnchor.MiddleCenter, new Vector2(0f, -122f), new Vector2(1020f, 52f));
        RectTransform milestoneRect = milestoneText.rectTransform;
        milestoneRect.anchorMin = new Vector2(0.5f, 1f);
        milestoneRect.anchorMax = new Vector2(0.5f, 1f);
        milestoneRect.pivot = new Vector2(0.5f, 1f);
        milestoneText.fontStyle = FontStyle.Bold;
        milestoneText.color = new Color(0.42f, 0.92f, 1f, 0f);

        GameObject flashObject = new GameObject("Damage Flash", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        flashObject.transform.SetParent(hudCanvas.transform, false);
        RectTransform flashRect = flashObject.GetComponent<RectTransform>();
        flashRect.anchorMin = Vector2.zero;
        flashRect.anchorMax = Vector2.one;
        flashRect.offsetMin = Vector2.zero;
        flashRect.offsetMax = Vector2.zero;
        damageFlashImage = flashObject.GetComponent<Image>();
        damageFlashImage.color = new Color(0.95f, 0.035f, 0.09f, 0f);
        damageFlashImage.raycastTarget = false;

        controlsText = CreateText(hudCanvas.transform, "Controls Hint", font, 18,
            TextAnchor.LowerLeft, new Vector2(24f, 22f), new Vector2(780f, 34f));
        RectTransform controlsRect = controlsText.rectTransform;
        controlsRect.anchorMin = Vector2.zero;
        controlsRect.anchorMax = Vector2.zero;
        controlsRect.pivot = Vector2.zero;
        controlsRect.sizeDelta = new Vector2(1180f, 42f);
        controlsText.text = "FLIGHT WASD / ARROWS / STICK   BOOST SHIFT / L3   ROLL Q / E / LB / RB   PAUSE ESC";
        controlsText.color = new Color(0.72f, 0.88f, 0.95f, 0.92f);

        gameOverPanel = new GameObject("Game Over Overlay", typeof(RectTransform), typeof(Image));
        gameOverPanel.transform.SetParent(hudCanvas.transform, false);
        RectTransform panelRect = gameOverPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        gameOverPanel.GetComponent<Image>().color = new Color(0.015f, 0.025f, 0.06f, 0.88f);
        gameOverPanel.SetActive(false);

        centerText = CreateText(gameOverPanel.transform, "Game Over Text", font, 42,
            TextAnchor.MiddleCenter, Vector2.zero, new Vector2(900f, 260f));
        RectTransform centerRect = centerText.rectTransform;
        centerRect.anchorMin = new Vector2(0.5f, 0.5f);
        centerRect.anchorMax = new Vector2(0.5f, 0.5f);
        centerRect.pivot = new Vector2(0.5f, 0.5f);
        centerText.color = Color.white;
        centerText.text = "RUN ENDED";

        GameObject buttonObject = new GameObject("Restart Button", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(gameOverPanel.transform, false);
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = new Vector2(0f, -125f);
        buttonRect.sizeDelta = new Vector2(260f, 62f);
        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = new Color(0.05f, 0.55f, 0.72f, 1f);
        restartButton = buttonObject.GetComponent<Button>();
        restartButton.onClick.AddListener(HandleOverlayButton);

        overlayButtonLabel = CreateText(buttonObject.transform, "Label", font, 23,
            TextAnchor.MiddleCenter, Vector2.zero, new Vector2(250f, 55f));
        RectTransform labelRect = overlayButtonLabel.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        overlayButtonLabel.text = "RESTART RUN";
        overlayButtonLabel.color = Color.white;
    }

    private static Text CreateText(Transform parent, string objectName, Font font, int fontSize,
        TextAnchor alignment, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObject.transform.SetParent(parent, false);
        Text text = textObject.GetComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        RectTransform rect = text.rectTransform;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        return text;
    }

    private static Material CreateMaterial(Color color, float metallic)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        Material material = new Material(shader);
        material.color = color;
        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", metallic);
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", 0.42f);
        return material;
    }

    private float NextRandomFloat(float minimum, float maximum)
    {
        if (gameplayRandom == null)
            gameplayRandom = new System.Random(routeDirector != null ? routeDirector.RunSeed : 73129);
        return minimum + (float)gameplayRandom.NextDouble() * (maximum - minimum);
    }

    private int NextRandomInt(int minimumInclusive, int maximumExclusive)
    {
        if (gameplayRandom == null)
            gameplayRandom = new System.Random(routeDirector != null ? routeDirector.RunSeed : 73129);
        return gameplayRandom.Next(minimumInclusive, maximumExclusive);
    }

    private Vector3 ToRouteWorldPosition(Vector3 lanePosition)
    {
        if (routeDirector == null) return lanePosition;
        Vector3 center = routeDirector.GetCenterOffsetAtDistance(
            routeDirector.DistanceTravelled + lanePosition.z);
        return new Vector3(lanePosition.x + center.x, lanePosition.y + center.y, lanePosition.z);
    }

    private float SpawnAsteroid()
    {
        if (encounterDirector == null)
        {
            encounterDirector = new VoidlineEncounterDirector();
            encounterDirector.BeginRun(routeDirector != null ? routeDirector.RunSeed : 73129);
        }

        VoidlineEncounterDirector.Plan plan = encounterDirector.Next(
            elapsed, passagesCleared, hull, spawnAheadDistance,
            spawnXLimit, spawnYLimit, spawnZBackJitter, spawnZForwardJitter);

        lastEncounterPattern = plan.PatternName;
        lastSafeLane = plan.SafeLane;
        SpawnAsteroidAt(plan.First.Position, plan.First.Radius);
        if (plan.Count > 1)
            SpawnAsteroidAt(plan.Second.Position, plan.Second.Radius);

        return plan.DelayMultiplier;
    }

    private void SpawnAsteroidAt(Vector3 spawnPosition, float radius)
    {
        VoidlineAsteroid behaviour;
        if (asteroidPool.Count > 0)
        {
            behaviour = asteroidPool.Pop();
        }
        else
        {
            bool usePrefab = asteroidPrefabs != null && asteroidPrefabs.Length > 0;
            GameObject selectedPrefab = usePrefab
                ? asteroidPrefabs[NextRandomInt(0, asteroidPrefabs.Length)]
                : null;
            GameObject asteroid = usePrefab
                ? Instantiate(selectedPrefab)
                : GameObject.CreatePrimitive(PrimitiveType.Sphere);
            asteroid.name = "Asteroid";
            asteroid.SetActive(false);

            if (!usePrefab)
            {
                Renderer renderer = asteroid.GetComponent<Renderer>();
                if (renderer != null) renderer.sharedMaterial = asteroidMaterial;
            }

            SphereCollider collider = asteroid.GetComponent<SphereCollider>();
            if (collider == null) collider = asteroid.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            Rigidbody body = asteroid.GetComponent<Rigidbody>();
            if (body == null) body = asteroid.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            behaviour = asteroid.GetComponent<VoidlineAsteroid>();
            if (behaviour == null) behaviour = asteroid.AddComponent<VoidlineAsteroid>();
        }

        behaviour.transform.position = ToRouteWorldPosition(spawnPosition);
        behaviour.Configure(this, worldSpeed, radius);
        behaviour.gameObject.SetActive(true);
    }

    private VoidlinePickup GetOrCreatePickup()
    {
        if (pickupPool.Count > 0)
            return pickupPool.Pop();

        bool usePrefab = repairPickupPrefab != null;
        GameObject pickupObject = usePrefab
            ? Instantiate(repairPickupPrefab)
            : GameObject.CreatePrimitive(PrimitiveType.Sphere);
        pickupObject.name = "Repair Cell";
        pickupObject.SetActive(false);

        SphereCollider collider = pickupObject.GetComponent<SphereCollider>();
        if (collider == null) collider = pickupObject.AddComponent<SphereCollider>();
        collider.isTrigger = true;
        Rigidbody body = pickupObject.GetComponent<Rigidbody>();
        if (body == null) body = pickupObject.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;

        if (!usePrefab)
        {
            Renderer renderer = pickupObject.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = pickupMaterial;
        }

        VoidlinePickup pickup = pickupObject.GetComponent<VoidlinePickup>();
        if (pickup == null) pickup = pickupObject.AddComponent<VoidlinePickup>();
        return pickup;
    }

    private void SpawnPickup()
    {
        Vector3 localSpawnPosition = new Vector3(NextRandomFloat(-spawnXLimit, spawnXLimit),
            NextRandomFloat(-spawnYLimit, spawnYLimit),
            Mathf.Max(18f, spawnAheadDistance * 0.72f) + NextRandomFloat(0f, 6f));

        VoidlinePickup pickup = GetOrCreatePickup();
        Vector3 spawnPosition = ToRouteWorldPosition(localSpawnPosition);
        for (int attempt = 0; attempt < 5; attempt++)
        {
            bool blocked = false;
            foreach (VoidlineAsteroid asteroid in FindObjectsByType<VoidlineAsteroid>())
            {
                Vector3 delta = asteroid.transform.position - spawnPosition;
                if (Mathf.Abs(delta.z) < 5f && new Vector2(delta.x, delta.y).sqrMagnitude < 2.25f)
                {
                    blocked = true;
                    break;
                }
            }
            if (!blocked) break;
            localSpawnPosition.x = NextRandomFloat(-spawnXLimit, spawnXLimit);
            localSpawnPosition.y = NextRandomFloat(-spawnYLimit, spawnYLimit);
            spawnPosition = ToRouteWorldPosition(localSpawnPosition);
        }

        pickup.transform.position = spawnPosition;
        pickup.Configure(this, worldSpeed);
        pickup.gameObject.SetActive(true);
    }

    public void CollectRepairPickup(VoidlinePickup pickup)
    {
        if (pickup == null) return;
        if (gameOver)
        {
            ReturnPickup(pickup);
            return;
        }

        repairCellsCollected++;
        if (hull < startingHull) hull++;
        bonusPoints += repairScoreBonus;
        if (audioFeedback != null) audioFeedback.PlayPickup();
        if (cameraEffects != null) cameraEffects.AddTrauma(0.12f);
        ReturnPickup(pickup);
        RefreshHud();
    }

    public void ReturnPickup(VoidlinePickup pickup)
    {
        if (pickup == null) return;
        pickup.gameObject.SetActive(false);
        if (!pickupPool.Contains(pickup)) pickupPool.Push(pickup);
    }
    public void ReturnAsteroid(VoidlineAsteroid asteroid)
    {
        if (asteroid == null) return;
        asteroid.SetMotionEnabled(false);
        asteroid.gameObject.SetActive(false);
        if (!asteroidPool.Contains(asteroid)) asteroidPool.Push(asteroid);
    }

    public void RegisterPassageCrossed(bool isTransitGate)
    {
        if (gameOver) return;

        passagesCleared++;
        int reward;
        if (isTransitGate)
        {
            gatesCleared++;
            reward = gateCrossingBonus;
        }
        else
        {
            archesCleared++;
            reward = archCrossingBonus;
        }

        bonusPoints += reward;
        if (audioFeedback != null) audioFeedback.PlayPassageClear();
        if (cameraEffects != null) cameraEffects.AddTrauma(isTransitGate ? 0.16f : 0.09f);
        string passageLabel = isTransitGate
            ? "TRANSIT GATE CLEARED"
            : "HOLLOW ARCH CLEARED";
        ShowMilestoneMessage(passageLabel + "  //  SECTOR " +
            (passagesCleared + 1).ToString("D2") + "  //  +" + reward, 2.4f);
        RefreshHud();
    }

    public void RegisterNearMiss(Vector3 hazardPosition)
    {
        if (gameOver || player == null) return;

        float dx = Mathf.Abs(hazardPosition.x - player.position.x);
        float dy = Mathf.Abs(hazardPosition.y - player.position.y);
        // Approximate a conservative combined ship+asteroid collision footprint, then
        // reward only close clearances outside that footprint (not distant misses).
        float clearance = Mathf.Sqrt((dx / 1.45f) * (dx / 1.45f) +
            (dy / 0.88f) * (dy / 0.88f));
        if (clearance <= 1f || clearance > 1.55f) return;

        nearMisses++;
        int chain = AdvanceFlowChain();
        int reward = 30 + Mathf.Min(chain - 1, 5) * 10;
        bonusPoints += reward;
        if (audioFeedback != null) audioFeedback.PlayNearMiss();
        if (cameraEffects != null) cameraEffects.AddTrauma(0.025f);
        ShowMilestoneMessage("NEAR MISS  //  FLOW x" + chain + "  //  +" + reward, 1.25f);
        RefreshHud();
    }

    public void RegisterAsteroidPassed()
    {
        if (gameOver) return;
        passedAsteroids++;
        if (audioFeedback != null) audioFeedback.PlayPass();
        RefreshHud();
    }
    public void TakeDamage()
    {
        if (gameOver || Time.time < nextDamageAt) return;

        nextDamageAt = Time.time + 1.1f;
        hull = Mathf.Max(0, hull - 1);
        flowChain = 0;
        flowChainExpiresAt = 0f;
        damageFlashAlpha = 0.26f;
        if (audioFeedback != null) audioFeedback.PlayDamage();
        if (cameraEffects != null) cameraEffects.AddTrauma(0.28f);
        RefreshHud();

        if (hull <= 0) EndRun();
    }
    private void EndRun()
    {
        if (gameOver) return;

        gameOver = true;
        ClearBoostPresentation();
        // Freeze the corridor and distant set pieces too; the end screen should not drift.
        if (corridorScroller != null) corridorScroller.SetSpeed(0f);
        int finalScore = GetScore();
        if (finalScore > bestScore) bestScore = finalScore;
        PlayerPrefs.SetInt("VOIDLINE_BEST_SCORE", bestScore);
        PlayerPrefs.Save();
        if (audioFeedback != null) audioFeedback.PlayGameOver();
        if (cameraEffects != null) cameraEffects.AddTrauma(0.55f);

        paused = false;
        Time.timeScale = 1f;
        if (overlayButtonLabel != null) overlayButtonLabel.text = "RESTART RUN";
        if (playerController != null) playerController.SetControlEnabled(false);
        foreach (VoidlineAsteroid asteroid in FindObjectsByType<VoidlineAsteroid>())
            asteroid.SetMotionEnabled(false);
        foreach (VoidlinePickup pickup in FindObjectsByType<VoidlinePickup>())
            pickup.SetMotionEnabled(false);

        centerText.text = "RUN ENDED" + System.Environment.NewLine +
            "SCORE  " + GetScore().ToString("D5") + System.Environment.NewLine +
            "BEST   " + bestScore.ToString("D5") + System.Environment.NewLine +
            "SECTOR " + (passagesCleared + 1).ToString("D2") + "   ROUTE #" +
            (routeDirector != null ? routeDirector.RunSeed.ToString("D8") : "00000000");
        gameOverPanel.SetActive(true);
    }
    private int AdvanceFlowChain()
    {
        if (Time.time > flowChainExpiresAt) flowChain = 0;
        flowChain = Mathf.Min(flowChain + 1, 10);
        flowChainExpiresAt = Time.time + 4.5f;
        return flowChain;
    }

    private void ShowMilestoneMessage(string message, float duration)
    {
        if (milestoneText == null) return;
        milestoneText.text = message;
        milestoneVisibleUntil = Time.unscaledTime + duration;
        Color color = milestoneText.color;
        color.a = 1f;
        milestoneText.color = color;
    }

    private void UpdateMilestonePresentation()
    {
        if (milestoneText == null) return;
        float remaining = milestoneVisibleUntil - Time.unscaledTime;
        float alpha = remaining > 0f ? Mathf.Clamp01(remaining / 0.55f) : 0f;
        Color color = milestoneText.color;
        if (!Mathf.Approximately(color.a, alpha))
        {
            color.a = alpha;
            milestoneText.color = color;
        }
    }

    private int GetScore()
    {
        return Mathf.FloorToInt(elapsed * 10f) + passedAsteroids * 25 + bonusPoints;
    }
    private void RefreshHud()
    {
        if (statusText == null) return;

        int score = GetScore();
        if (score > bestScore)
        {
            bestScore = score;
            PlayerPrefs.SetInt("VOIDLINE_BEST_SCORE", bestScore);
        }

        statusText.text = "VOIDLINE   //   SURVIVAL RUN" + System.Environment.NewLine +
            "SECTOR  " + (passagesCleared + 1).ToString("D2") + "   GATES  " + gatesCleared.ToString("D2") +
            "   ARCHES  " + archesCleared.ToString("D2") +
            System.Environment.NewLine + "HULL  " + new string('|', hull) + new string('.', Mathf.Max(0, startingHull - hull)) +
            System.Environment.NewLine + "SCORE  " + score.ToString("D5") + "   BEST  " + bestScore.ToString("D5") +
            System.Environment.NewLine + "CELLS  " + repairCellsCollected.ToString("D2") + "   CLOSE " + nearMisses.ToString("D2") +
            System.Environment.NewLine + "SPEED  " + worldSpeed.ToString("0") + "   FLOW x" + flowChain.ToString("D2") +
            System.Environment.NewLine + "BOOST  [" + new string('|', Mathf.RoundToInt(boostEnergy * 10f)) +
            new string('.', 10 - Mathf.RoundToInt(boostEnergy * 10f)) + "]";
    }
    private void OnDestroy()
    {
        CancelBoostImpactFrame();
        if (pauseAction != null)
        {
            pauseAction.performed -= HandlePauseAction;
            pauseAction.Disable();
        }
        if (restartAction != null)
        {
            restartAction.performed -= HandleRestartAction;
            restartAction.Disable();
        }
        Time.timeScale = 1f;
        if (audioFeedback != null) audioFeedback.SetBoostLevel(0f);
        if (Application.isPlaying)
        {
            if (asteroidMaterial != null) Destroy(asteroidMaterial);
            if (pickupMaterial != null) Destroy(pickupMaterial);
        }
    }

    private void RestartRun()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(activeScene.name);
    }
}












