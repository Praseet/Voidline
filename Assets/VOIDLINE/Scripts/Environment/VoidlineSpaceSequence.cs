using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Stages occasional non-colliding landmarks in an open-space flight. The intro corridor
/// fades out permanently before the first landmark; scoring, steering, hazards, and the run
/// lifecycle remain owned by their existing systems.
/// </summary>
[DisallowMultipleComponent]
public sealed class VoidlineSpaceSequence : MonoBehaviour
{
    [Header("Authored Space Backdrop")]
    [SerializeField] private bool installSpaceSkybox = true;
    [SerializeField, Range(0.2f, 1.2f)] private float ambientLightScale = 0.54f;

    [Header("Landmark Rhythm")]
    [SerializeField, Min(1f)] private float firstLandmarkDistance = 12f;
    [SerializeField, Min(80f)] private float landmarkSpacing = 320f;
    [SerializeField, Min(70f)] private float spawnAheadZ = 185f;

    [Tooltip("The opening transit corridor is a brief intro and then retires permanently.")]
    [SerializeField, Min(1f)] private float introTunnelRetireDistance = 26f;
    [Tooltip("Distance over which the opening corridor fades away; it never fades back in.")]
    [SerializeField, Min(5f)] private float transitionDistance = 32f;
    [Tooltip("Retain a landmark far enough behind the craft to finish its crossing before cleanup.")]
    [SerializeField, Min(1f)] private float cleanupBehindZ = 112f;

    [Header("Optional Explicit Prefabs")]
    [SerializeField] private GameObject transitGatePrefab;
    [SerializeField] private GameObject hollowAsteroidPrefab;

    [Header("Distant Asteroid Field")]
    [Tooltip("Rock prefabs used only as distant scenery. Their colliders and gameplay behaviours are disabled.")]
    [SerializeField] private GameObject[] backdropRockPrefabs;
    [SerializeField, Min(1f)] private float firstBackdropRockDistance = 1f;
    [SerializeField, Min(40f)] private float backdropRockSpacing = 64f;
    [SerializeField, Min(30f)] private float backdropRockCleanupBehindZ = 80f;
    [SerializeField, Min(300f)] private float backdropSpawnDistance = 520f;
    [SerializeField, Min(20f)] private float backdropScaleInDistance = 105f;
    [SerializeField] private Vector2 backdropXRange = new Vector2(85f, 155f);
    [SerializeField] private Vector2 backdropYRange = new Vector2(32f, 82f);
    [SerializeField] private Vector2 backdropScaleRange = new Vector2(6f, 12f);

    [Header("Sector Transitions")]
    [SerializeField, Min(1f)] private float sectorTransitionDuration = 8f;
    [SerializeField, Min(300f)] private float sectorMonumentSpawnDistance = 440f;
    [SerializeField] private Vector2 sectorMonumentXRange = new Vector2(100f, 155f);
    [SerializeField] private Vector2 sectorMonumentYRange = new Vector2(36f, 82f);
    [Tooltip("Scales the configured far-field offsets inward so large sector landmarks remain inside the gameplay camera view.")]
    [SerializeField, Range(0.2f, 0.6f)] private float sectorMonumentOffsetScale = 0.42f;
    [SerializeField] private Vector2 sectorMonumentScaleRange = new Vector2(20f, 28f);

    private sealed class RendererState
    {
        public Renderer renderer;
        public bool originalEnabled;
        public bool hasBaseColor;
        public bool hasEmissionColor;
        public Color baseColor;
        public Color emissionColor;
        public MaterialPropertyBlock block;
    }

    private readonly List<RendererState> tunnelRenderers = new List<RendererState>(64);
    private readonly List<Collider> tunnelColliders = new List<Collider>(32);
    private readonly List<bool> originalColliderStates = new List<bool>(32);

    private CorridorScroller corridorScroller;
    private VoidlineRouteDirector routeDirector;
    private VoidlineGameManager gameManager;
    private VoidlineCelestialAssetSet celestialAssetSet;
    private VoidlineCelestialBackdrop celestialBackdrop;
    private GameObject activeLandmark;
    private readonly List<GameObject> backdropRocks = new List<GameObject>(3);
    private System.Random backdropRandom;
    private float nextBackdropRockAt;
    private bool activeLandmarkIsGate;
    private bool activeLandmarkResolved;
    private Material previousSkybox;
    private Material activeSkyboxMaterial;
    private Material ownedSectorSkybox;
    private Material runtimeFallbackSkybox;
    private Texture2D runtimeFallbackSkyTexture;
    private Texture2D currentSpacePanorama;
    private Texture2D targetSpacePanorama;
    private int initialSpacePanoramaIndex;
    private int currentSpacePanoramaIndex;
    private int targetSpacePanoramaIndex;
    private AmbientMode previousAmbientMode;
    private float previousAmbientIntensity;
    private bool previousFog;
    private Color baseSkyTint = Color.white;
    private float baseSkyExposure = 1f;
    private float baseSectorAmbientIntensity = 0.46f;
    private Color sectorSkyStartTint = Color.white;
    private Color sectorSkyTargetTint = Color.white;
    private float sectorSkyStartExposure = 1f;
    private float sectorSkyTargetExposure = 1f;
    private float sectorAmbientStart;
    private float sectorAmbientTarget;
    private float sectorTransitionElapsed;
    private bool sectorTransitionActive;
    private int crossedLandmarks;
    private readonly Dictionary<GameObject, Vector3> backdropTargetScales =
        new Dictionary<GameObject, Vector3>(16);
    private float distanceTravelled;
    private float nextLandmarkAt;
    private int landmarkIndex;
    private float tunnelVisibility = 1f;
    private bool tunnelStateCached;
    private bool tunnelRetired;
    private bool sequenceInitialized;
    private bool resetRequestedBeforeAwake;

    private void Awake()
    {
        corridorScroller = GetComponent<CorridorScroller>();
        routeDirector = GetComponent<VoidlineRouteDirector>();
        gameManager = FindAnyObjectByType<VoidlineGameManager>();
        CacheTunnelState();

        previousSkybox = RenderSettings.skybox;
        previousAmbientMode = RenderSettings.ambientMode;
        previousAmbientIntensity = RenderSettings.ambientIntensity;
        previousFog = RenderSettings.fog;

        celestialAssetSet = Resources.Load<VoidlineCelestialAssetSet>("VOIDLINE_CelestialAssetSet")
            ?? celestialAssetSet;

        if (installSpaceSkybox)
        {
            // First choice is real, authored 10k multicolour nebula panoramas. The former
            // near-black SpaceSkies material is intentionally not selected as the environment.
            Texture2D panorama = null;
            if (celestialAssetSet != null && celestialAssetSet.SpacePanoramaCount > 0)
            {
                int seed = routeDirector != null ? routeDirector.RunSeed : 260412;
                initialSpacePanoramaIndex = Mathf.Abs(seed == int.MinValue ? 0 : seed) %
                    Mathf.Max(1, celestialAssetSet.SpacePanoramaCount);
                currentSpacePanoramaIndex = initialSpacePanoramaIndex;
                panorama = celestialAssetSet.PickSpacePanorama(currentSpacePanoramaIndex);
            }

            Material importedSky = celestialAssetSet != null
                ? celestialAssetSet.PickSpaceSkybox(routeDirector != null ? routeDirector.RunSeed : 260412)
                : null;
            Shader celestialShader = Shader.Find("Skybox/VOIDLINE Celestial Blend");
            bool installedCelestialSky = false;
            if (panorama != null && celestialShader != null)
            {
                ownedSectorSkybox = new Material(celestialShader)
                {
                    name = "VOIDLINE / Layered Celestial Sky",
                    hideFlags = HideFlags.DontSave
                };
                ownedSectorSkybox.SetTexture("_TexA", panorama);
                ownedSectorSkybox.SetTexture("_TexB", panorama);
                ownedSectorSkybox.SetFloat("_Blend", 0f);
                currentSpacePanorama = panorama;
                RenderSettings.skybox = ownedSectorSkybox;
                installedCelestialSky = true;
            }
            else if (panorama != null)
            {
                // Still use the actual downloaded panorama if the crossfade shader is not available.
                Shader panoramicShader = Shader.Find("Skybox/Panoramic");
                if (panoramicShader != null)
                {
                    ownedSectorSkybox = new Material(panoramicShader)
                    {
                        name = "VOIDLINE / Authored Space Panorama",
                        hideFlags = HideFlags.DontSave
                    };
                    ownedSectorSkybox.SetTexture("_MainTex", panorama);
                    if (ownedSectorSkybox.HasProperty("_ImageType")) ownedSectorSkybox.SetFloat("_ImageType", 0f);
                    if (ownedSectorSkybox.HasProperty("_Mapping")) ownedSectorSkybox.SetFloat("_Mapping", 1f);
                    if (ownedSectorSkybox.HasProperty("_Exposure")) ownedSectorSkybox.SetFloat("_Exposure", 1.1f);
                    if (ownedSectorSkybox.HasProperty("_Rotation")) ownedSectorSkybox.SetFloat("_Rotation", 0f);
                    currentSpacePanorama = panorama;
                    RenderSettings.skybox = ownedSectorSkybox;
                    installedCelestialSky = true;
                }
            }

            if (!installedCelestialSky && importedSky != null && HasCompleteSixSidedSkybox(importedSky))
            {
                ownedSectorSkybox = new Material(importedSky)
                {
                    name = "VOIDLINE / Runtime Authored Celestial Sky",
                    hideFlags = HideFlags.DontSave
                };
                RenderSettings.skybox = ownedSectorSkybox;
                installedCelestialSky = true;
            }

            if (!installedCelestialSky)
            {
                // A vivid procedural fallback exists only until the authentic free panorama set is imported.
                runtimeFallbackSkybox = CreateProceduralFallbackSkybox(out runtimeFallbackSkyTexture);
                if (runtimeFallbackSkybox != null)
                {
                    RenderSettings.skybox = runtimeFallbackSkybox;
                    installedCelestialSky = true;
                }
                else
                    Debug.LogWarning("VOIDLINE could not find a valid celestial panorama or the built-in Panoramic shader.");
            }

            activeSkyboxMaterial = RenderSettings.skybox;
            if (activeSkyboxMaterial != null)
            {
                baseSkyTint = activeSkyboxMaterial.HasProperty("_Tint")
                    ? activeSkyboxMaterial.GetColor("_Tint")
                    : Color.white;
                baseSkyExposure = activeSkyboxMaterial.HasProperty("_Exposure")
                    ? activeSkyboxMaterial.GetFloat("_Exposure")
                    : 1f;
                if (activeSkyboxMaterial.HasProperty("_Exposure"))
                {
                    baseSkyExposure = Mathf.Clamp(baseSkyExposure * 1.06f, 0.05f, 2.5f);
                    activeSkyboxMaterial.SetFloat("_Exposure", baseSkyExposure);
                }
                sectorSkyStartTint = baseSkyTint;
                sectorSkyTargetTint = baseSkyTint;
                sectorSkyStartExposure = baseSkyExposure;
                sectorSkyTargetExposure = baseSkyExposure;
                RenderSettings.ambientMode = AmbientMode.Skybox;
                RenderSettings.ambientIntensity = Mathf.Clamp(ambientLightScale, 0.42f, 0.68f);
                baseSectorAmbientIntensity = RenderSettings.ambientIntensity;
                sectorAmbientStart = baseSectorAmbientIntensity;
                sectorAmbientTarget = baseSectorAmbientIntensity;
                RenderSettings.fog = false;
                DynamicGI.UpdateEnvironment();
            }
        }

        nextLandmarkAt = firstLandmarkDistance;
        nextBackdropRockAt = firstBackdropRockDistance;
        backdropRandom = new System.Random(routeDirector != null ? routeDirector.RunSeed ^ 0x5EED : 260412);
        celestialBackdrop = GetComponent<VoidlineCelestialBackdrop>();
        if (celestialBackdrop == null) celestialBackdrop = gameObject.AddComponent<VoidlineCelestialBackdrop>();
        celestialBackdrop.Initialize(routeDirector, celestialAssetSet, routeDirector != null ? routeDirector.RunSeed : 260412);
        // Begin with the full celestial world visible. The removed corridor must not flash at run start.
        tunnelRetired = true;
        tunnelVisibility = 0f;
        ApplyTunnelVisibility(0f);
        sequenceInitialized = true;
        if (resetRequestedBeforeAwake)
        {
            resetRequestedBeforeAwake = false;
            ResetForRunCore();
        }
    }

    private static bool HasCompleteSixSidedSkybox(Material material)
    {
        if (material == null || material.shader == null) return false;
        if (material.shader.name != "Skybox/6 Sided") return true;
        return material.GetTexture("_FrontTex") != null
            && material.GetTexture("_BackTex") != null
            && material.GetTexture("_LeftTex") != null
            && material.GetTexture("_RightTex") != null
            && material.GetTexture("_UpTex") != null
            && material.GetTexture("_DownTex") != null;
    }

    /// <summary>
    /// Generates a lightweight, original equirectangular sky for clean checkouts where the
    /// locally installed SpaceSkies Free source textures are not present. This is intentionally
    /// a fallback, not the main art direction: a dark dust band, scattered stars and a restrained
    /// teal/violet cloud field keep the scene readable without third-party source files.
    /// </summary>
    private static Material CreateProceduralFallbackSkybox(out Texture2D texture)
    {
        texture = null;
        Shader shader = Shader.Find("Skybox/Panoramic");
        if (shader == null) return null;

        const int width = 1024;
        const int height = 512;
        texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
        {
            name = "VOIDLINE / Procedural Space Fallback",
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear,
            anisoLevel = 1,
            hideFlags = HideFlags.DontSave
        };
        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            float v = (y + 0.5f) / height;
            float latitude = (v - 0.5f) * Mathf.PI;
            float polarFade = Mathf.SmoothStep(0f, 0.28f, Mathf.Clamp01(Mathf.Cos(latitude)));
            for (int x = 0; x < width; x++)
            {
                float u = (x + 0.5f) / width;
                float warp = 0.11f * Mathf.Sin(u * 18.85f) + 0.045f * Mathf.Sin(u * 43.98f + 1.7f);
                float bandDistance = (latitude - warp - 0.12f) / 0.19f;
                float band = Mathf.Exp(-bandDistance * bandDistance);
                float turbulence = 0.5f
                    + 0.22f * Mathf.Sin(u * 49f + latitude * 23f)
                    + 0.16f * Mathf.Sin(u * 91f - latitude * 37f)
                    + 0.08f * Mathf.Sin(u * 167f + latitude * 61f);
                band *= Mathf.Clamp01(turbulence) * polarFade;
                float filament = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(bandDistance + 0.17f * Mathf.Sin(u * 27f)) * 2.2f), 3f);
                float dust = band * (0.015f + Mathf.Clamp01(turbulence) * 0.045f);
                Color c = new Color(0.006f, 0.010f, 0.026f);
                c += new Color(0.065f, 0.025f, 0.16f) * dust * 3.8f;
                c += new Color(0.018f, 0.11f, 0.24f) * band * 0.92f;
                c += new Color(0.22f, 0.075f, 0.38f) * filament * band * 0.32f;
                pixels[y * width + x] = c;
            }
        }

        // Star sizes and brightness are deterministic, so the fallback is stable across runs.
        System.Random random = new System.Random(260412);
        for (int i = 0; i < 1400; i++)
        {
            int x = random.Next(width);
            int y = random.Next(height);
            float brightness = (float)random.NextDouble();
            Color star = brightness > 0.94f
                ? new Color(0.52f, 0.66f, 0.92f)
                : brightness > 0.72f
                    ? new Color(0.20f, 0.32f, 0.57f)
                    : new Color(0.075f, 0.11f, 0.20f);
            float radius = brightness > 0.985f ? 2f : 1f;
            for (int oy = -(int)radius; oy <= (int)radius; oy++)
            for (int ox = -(int)radius; ox <= (int)radius; ox++)
            {
                if (ox * ox + oy * oy > radius * radius) continue;
                int px = (x + ox + width) % width;
                int py = Mathf.Clamp(y + oy, 0, height - 1);
                int index = py * width + px;
                float weight = ox == 0 && oy == 0 ? 1f : 0.22f;
                pixels[index] += star * weight;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);

        Material material = new Material(shader)
        {
            name = "VOIDLINE / Procedural Space Fallback Material",
            hideFlags = HideFlags.DontSave
        };
        material.SetTexture("_MainTex", texture);
        if (material.HasProperty("_Tint")) material.SetColor("_Tint", Color.white);
        if (material.HasProperty("_Exposure")) material.SetFloat("_Exposure", 1.0f);
        if (material.HasProperty("_Rotation")) material.SetFloat("_Rotation", 0f);
        if (material.HasProperty("_Mapping")) material.SetFloat("_Mapping", 1f);
        if (material.HasProperty("_ImageType")) material.SetFloat("_ImageType", 0f);
        return material;
    }

    private void BeginSectorTransition()
    {
        crossedLandmarks++;
        if (activeSkyboxMaterial != null)
        {
            sectorSkyStartTint = activeSkyboxMaterial.HasProperty("_Tint")
                ? activeSkyboxMaterial.GetColor("_Tint")
                : baseSkyTint;
            sectorSkyStartExposure = activeSkyboxMaterial.HasProperty("_Exposure")
                ? activeSkyboxMaterial.GetFloat("_Exposure")
                : baseSkyExposure;
        }
        sectorAmbientStart = RenderSettings.ambientIntensity;

        // Four distinct moods for one continuous route. The palette changes at a real
        // arch/gate crossing, never by snapping on score frames while the player is mid-flight.
        Color tintMultiplier;
        float exposureMultiplier;
        float ambientMultiplier;
        switch ((crossedLandmarks - 1) % 4)
        {
            case 0: // Ion Veil
                tintMultiplier = new Color(0.72f, 0.96f, 1.24f, 1f);
                exposureMultiplier = 1.02f;
                ambientMultiplier = 0.94f;
                break;
            case 1: // Ember Drift
                tintMultiplier = new Color(1.22f, 0.78f, 0.54f, 1f);
                exposureMultiplier = 0.86f;
                ambientMultiplier = 0.82f;
                break;
            case 2: // Violet Hollow
                tintMultiplier = new Color(0.82f, 0.68f, 1.28f, 1f);
                exposureMultiplier = 0.92f;
                ambientMultiplier = 0.88f;
                break;
            default: // Verdigris Wake
                tintMultiplier = new Color(0.62f, 1.12f, 0.96f, 1f);
                exposureMultiplier = 0.98f;
                ambientMultiplier = 0.90f;
                break;
        }

        sectorSkyTargetTint = MultiplySkyTint(baseSkyTint, tintMultiplier);
        sectorSkyTargetExposure = Mathf.Clamp(baseSkyExposure * exposureMultiplier, 0.02f, 2f);
        sectorAmbientTarget = baseSectorAmbientIntensity * ambientMultiplier;

        // Crossfade into a genuinely different authored panorama on each sector crossing.
        // This replaces the original same-texture colour-only "transition".
        targetSpacePanorama = null;
        if (celestialAssetSet != null && celestialAssetSet.SpacePanoramaCount > 1 &&
            activeSkyboxMaterial != null && activeSkyboxMaterial.HasProperty("_TexA") &&
            activeSkyboxMaterial.HasProperty("_TexB") && activeSkyboxMaterial.HasProperty("_Blend") &&
            currentSpacePanorama != null)
        {
            targetSpacePanoramaIndex = (currentSpacePanoramaIndex + 1) % celestialAssetSet.SpacePanoramaCount;
            targetSpacePanorama = celestialAssetSet.PickSpacePanorama(targetSpacePanoramaIndex);
            if (targetSpacePanorama != null)
            {
                activeSkyboxMaterial.SetTexture("_TexA", currentSpacePanorama);
                activeSkyboxMaterial.SetTexture("_TexB", targetSpacePanorama);
                activeSkyboxMaterial.SetFloat("_Blend", 0f);
            }
        }

        sectorTransitionElapsed = 0f;
        sectorTransitionActive = true;
        SpawnSectorMonument((crossedLandmarks - 1) % 4);
    }

    private void UpdateSectorTransition(float deltaTime)
    {
        if (!sectorTransitionActive) return;
        sectorTransitionElapsed = Mathf.Min(sectorTransitionDuration,
            sectorTransitionElapsed + Mathf.Max(0f, deltaTime));
        float t = Mathf.Clamp01(sectorTransitionElapsed / Mathf.Max(0.01f, sectorTransitionDuration));
        float eased = t * t * (3f - 2f * t);

        if (activeSkyboxMaterial != null)
        {
            if (activeSkyboxMaterial.HasProperty("_Tint"))
                activeSkyboxMaterial.SetColor("_Tint",
                    Color.Lerp(sectorSkyStartTint, sectorSkyTargetTint, eased));
            if (activeSkyboxMaterial.HasProperty("_Exposure"))
                activeSkyboxMaterial.SetFloat("_Exposure",
                    Mathf.Lerp(sectorSkyStartExposure, sectorSkyTargetExposure, eased));
            if (targetSpacePanorama != null && activeSkyboxMaterial.HasProperty("_Blend"))
                activeSkyboxMaterial.SetFloat("_Blend", eased);
        }
        RenderSettings.ambientIntensity = Mathf.Lerp(sectorAmbientStart, sectorAmbientTarget, eased);

        if (t >= 1f)
        {
            sectorTransitionActive = false;
            if (targetSpacePanorama != null && activeSkyboxMaterial != null &&
                activeSkyboxMaterial.HasProperty("_TexA") && activeSkyboxMaterial.HasProperty("_TexB"))
            {
                currentSpacePanorama = targetSpacePanorama;
                currentSpacePanoramaIndex = targetSpacePanoramaIndex;
                activeSkyboxMaterial.SetTexture("_TexA", currentSpacePanorama);
                activeSkyboxMaterial.SetTexture("_TexB", currentSpacePanorama);
                if (activeSkyboxMaterial.HasProperty("_Blend")) activeSkyboxMaterial.SetFloat("_Blend", 0f);
                targetSpacePanorama = null;
            }
            if (activeSkyboxMaterial != null && RenderSettings.skybox == activeSkyboxMaterial)
                DynamicGI.UpdateEnvironment();
        }
    }

    private static Color MultiplySkyTint(Color baseTint, Color multiplier)
    {
        return new Color(
            Mathf.Clamp(baseTint.r * multiplier.r, 0f, 1.25f),
            Mathf.Clamp(baseTint.g * multiplier.g, 0f, 1.25f),
            Mathf.Clamp(baseTint.b * multiplier.b, 0f, 1.25f),
            baseTint.a);
    }

    private void ResetSectorPresentation()
    {
        sectorTransitionActive = false;
        sectorTransitionElapsed = 0f;
        sectorSkyStartTint = baseSkyTint;
        sectorSkyTargetTint = baseSkyTint;
        sectorSkyStartExposure = baseSkyExposure;
        sectorSkyTargetExposure = baseSkyExposure;
        sectorAmbientStart = baseSectorAmbientIntensity;
        sectorAmbientTarget = baseSectorAmbientIntensity;
        targetSpacePanorama = null;
        currentSpacePanoramaIndex = initialSpacePanoramaIndex;
        if (celestialAssetSet != null && celestialAssetSet.SpacePanoramaCount > 0)
            currentSpacePanorama = celestialAssetSet.PickSpacePanorama(initialSpacePanoramaIndex);

        if (activeSkyboxMaterial != null)
        {
            if (activeSkyboxMaterial.HasProperty("_Tint"))
                activeSkyboxMaterial.SetColor("_Tint", baseSkyTint);
            if (activeSkyboxMaterial.HasProperty("_Exposure"))
                activeSkyboxMaterial.SetFloat("_Exposure", baseSkyExposure);
            if (currentSpacePanorama != null && activeSkyboxMaterial.HasProperty("_TexA") &&
                activeSkyboxMaterial.HasProperty("_TexB"))
            {
                activeSkyboxMaterial.SetTexture("_TexA", currentSpacePanorama);
                activeSkyboxMaterial.SetTexture("_TexB", currentSpacePanorama);
                if (activeSkyboxMaterial.HasProperty("_Blend")) activeSkyboxMaterial.SetFloat("_Blend", 0f);
            }
        }

        RenderSettings.ambientIntensity = baseSectorAmbientIntensity;
        if (activeSkyboxMaterial != null && RenderSettings.skybox == activeSkyboxMaterial)
            DynamicGI.UpdateEnvironment();
    }

    private void SpawnSectorMonument(int paletteIndex)
    {
        if (backdropRandom == null)
            backdropRandom = new System.Random(routeDirector != null ? routeDirector.RunSeed ^ 0x5EED : 260412);

        // Sector identity changes the silhouette, not just the color grade. Use the designed
        // hollow-arch asset as the first arrival cue; reserve a giant natural rock for the next
        // mood and keep the engineered gate rare. Each one remains distant, non-interactive art.
        GameObject prefab = null;
        bool createNaturalArchCue = paletteIndex % 4 == 0 || paletteIndex % 4 == 2;
        Vector2 cueScaleRange = sectorMonumentScaleRange;
        switch (paletteIndex % 4)
        {
            case 0:
                // Use the known asteroid meshes to build an irregular natural arch. The
                // imported arch prefab root does not expose a reliable readable silhouette.
                cueScaleRange = new Vector2(2.4f, 3.0f);
                break;
            case 1:
                // The natural rock mesh is small, so it can be magnified to read as a scale cue.
                cueScaleRange = new Vector2(20f, 28f);
                break;
            case 2:
                cueScaleRange = new Vector2(2.6f, 3.2f);
                break;
            default:
                if (transitGatePrefab == null)
                    transitGatePrefab = Resources.Load<GameObject>("PF_AncientTransitGate");
                prefab = transitGatePrefab;
                // A gate is a rare landmark, but its imported bounds already define a large relic.
                cueScaleRange = new Vector2(1.2f, 2.0f);
                break;
        }

        if (prefab == null && !createNaturalArchCue && backdropRockPrefabs != null && backdropRockPrefabs.Length > 0)
        {
            int start = paletteIndex % backdropRockPrefabs.Length;
            for (int attempt = 0; attempt < backdropRockPrefabs.Length && prefab == null; attempt++)
                prefab = backdropRockPrefabs[(start + attempt) % backdropRockPrefabs.Length];
        }
        if (prefab == null && !createNaturalArchCue) return;

        float z = Mathf.Max(300f,
            sectorMonumentSpawnDistance + NextBackdropFloat(-35f, 65f));
        float absoluteDistance = routeDirector != null
            ? routeDirector.DistanceTravelled + z
            : distanceTravelled + z;
        Vector3 routeOffset = routeDirector != null
            ? routeDirector.GetCenterOffsetAtDistance(absoluteDistance)
            : Vector3.zero;
        float side = crossedLandmarks % 2 == 0 ? -1f : 1f;
        float cueOffsetScale = Mathf.Clamp(sectorMonumentOffsetScale > 0f ? sectorMonumentOffsetScale : 0.42f, 0.2f, 0.6f);
        float x = side * NextBackdropFloat(sectorMonumentXRange.x, sectorMonumentXRange.y) * cueOffsetScale;
        float y = NextBackdropFloat(sectorMonumentYRange.x, sectorMonumentYRange.y)
            * (paletteIndex % 2 == 0 ? 1f : -1f) * cueOffsetScale;
        Quaternion rotation = Quaternion.Euler(
            NextBackdropFloat(-18f, 18f),
            NextBackdropFloat(0f, 360f),
            NextBackdropFloat(-22f, 22f));

        Vector3 cuePosition = new Vector3(routeOffset.x + x, routeOffset.y + y, z);
        GameObject monument = createNaturalArchCue
            ? CreateNaturalAsteroidArch(cuePosition, rotation)
            : Instantiate(prefab, cuePosition, rotation, transform);
        if (createNaturalArchCue)
            monument.transform.SetParent(transform, true);
        monument.name = createNaturalArchCue
            ? "VOIDLINE / Sector Natural Asteroid Arch"
            : paletteIndex % 4 == 3
                ? "VOIDLINE / Distant Transit Relic"
                : "VOIDLINE / Sector Asteroid Scale Cue";
        PrepareAsNonInteractiveScenery(monument);

        Vector3 targetScale = monument.transform.localScale
            * NextBackdropFloat(cueScaleRange.x, cueScaleRange.y);
        monument.transform.localScale = targetScale * 0.012f;
        backdropTargetScales[monument] = targetScale;
        backdropRocks.Add(monument);
    }

    /// <summary>Reset non-serialized scene state for a fresh Play Mode entry.</summary>
    public void ResetForRun()
    {
        if (!sequenceInitialized)
        {
            resetRequestedBeforeAwake = true;
            distanceTravelled = 0f;
            landmarkIndex = 0;
            crossedLandmarks = 0;
            return;
        }
        ResetForRunCore();
    }

    private void ResetForRunCore()
    {
        if (activeLandmark != null)
        {
            if (Application.isPlaying) Destroy(activeLandmark);
            else DestroyImmediate(activeLandmark);
        }
        activeLandmark = null;
        activeLandmarkIsGate = false;
        activeLandmarkResolved = false;
        for (int i = backdropRocks.Count - 1; i >= 0; i--)
        {
            if (backdropRocks[i] == null) continue;
            if (Application.isPlaying) Destroy(backdropRocks[i]);
            else DestroyImmediate(backdropRocks[i]);
        }
        backdropRocks.Clear();
        backdropTargetScales.Clear();
        distanceTravelled = 0f;
        landmarkIndex = 0;
        crossedLandmarks = 0;
        ResetSectorPresentation();
        nextLandmarkAt = firstLandmarkDistance;
        nextBackdropRockAt = firstBackdropRockDistance;
        backdropRandom = new System.Random(routeDirector != null ? routeDirector.RunSeed ^ 0x5EED : 260412);
        if (celestialBackdrop != null) celestialBackdrop.ResetForRun(routeDirector != null ? routeDirector.RunSeed : 260412);
        tunnelRetired = true;
        tunnelVisibility = 0f;
        ApplyTunnelVisibility(0f);
    }

    private void CacheTunnelState()
    {
        tunnelRenderers.Clear();
        tunnelColliders.Clear();
        originalColliderStates.Clear();

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform segment = transform.GetChild(i);
            if (!segment.name.StartsWith("Segment_")) continue;

            Renderer[] renderers = segment.GetComponentsInChildren<Renderer>(true);
            for (int j = 0; j < renderers.Length; j++)
            {
                Renderer renderer = renderers[j];
                Material material = renderer.sharedMaterial;
                var state = new RendererState
                {
                    renderer = renderer,
                    originalEnabled = renderer.enabled,
                    block = new MaterialPropertyBlock()
                };

                if (material != null)
                {
                    if (material.HasProperty("_BaseColor"))
                    {
                        state.hasBaseColor = true;
                        state.baseColor = material.GetColor("_BaseColor");
                    }
                    else if (material.HasProperty("_Color"))
                    {
                        state.hasBaseColor = true;
                        state.baseColor = material.GetColor("_Color");
                    }

                    if (material.HasProperty("_EmissionColor"))
                    {
                        state.hasEmissionColor = true;
                        state.emissionColor = material.GetColor("_EmissionColor");
                    }
                }

                tunnelRenderers.Add(state);
            }

            Collider[] colliders = segment.GetComponentsInChildren<Collider>(true);
            for (int j = 0; j < colliders.Length; j++)
            {
                tunnelColliders.Add(colliders[j]);
                originalColliderStates.Add(colliders[j].enabled);
            }
        }

        tunnelStateCached = true;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        UpdateSectorTransition(dt);
        // A very slow equirectangular drift gives the sky tangible scale without turning
        // distant clouds into distracting motion behind an incoming enemy tell.
        if (activeSkyboxMaterial != null && activeSkyboxMaterial.HasProperty("_Rotation"))
        {
            float rotation = (activeSkyboxMaterial.GetFloat("_Rotation") + dt * 0.16f) % 360f;
            activeSkyboxMaterial.SetFloat("_Rotation", rotation);
        }
        float speed = corridorScroller != null ? corridorScroller.Speed : 12f;
        float distanceStep = Mathf.Max(0f, speed * dt);
        distanceTravelled += distanceStep;
        if (celestialBackdrop != null) celestialBackdrop.Tick(distanceStep, dt);

        if (activeLandmark != null)
        {
            float previousZ = activeLandmark.transform.position.z;
            activeLandmark.transform.position += Vector3.back * distanceStep;
            float currentZ = activeLandmark.transform.position.z;

            // Score the physical crossing, not the spawn or fade trigger; this can only fire once per landmark.
            if (!activeLandmarkResolved && previousZ > 0f && currentZ <= 0f)
            {
                activeLandmarkResolved = true;
                if (gameManager == null) gameManager = FindAnyObjectByType<VoidlineGameManager>();
                if (gameManager != null)
                    gameManager.RegisterPassageCrossed(activeLandmarkIsGate);

                // The structure is the threshold: the next sector's sky, ambient character,
                // and distant scale cue begin changing only after the player physically crosses it.
                BeginSectorTransition();
            }

            if (currentZ < -cleanupBehindZ)
            {
                Destroy(activeLandmark);
                activeLandmark = null;
                activeLandmarkResolved = false;
            }
        }

        UpdateBackdropRocks(distanceStep);
        if (backdropRockPrefabs != null && backdropRockPrefabs.Length > 0 &&
            distanceTravelled >= nextBackdropRockAt)
        {
            SpawnBackdropRock();
            nextBackdropRockAt += Mathf.Max(40f, backdropRockSpacing);
        }

        if (activeLandmark == null && distanceTravelled >= nextLandmarkAt)
        {
            SpawnLandmark();
            nextLandmarkAt += landmarkSpacing;
        }

        if (!tunnelRetired)
        {
            float fade = Mathf.Clamp01((distanceTravelled - introTunnelRetireDistance) / Mathf.Max(1f, transitionDistance));
            float desiredVisibility = 1f - fade;
            if (distanceTravelled >= introTunnelRetireDistance + transitionDistance)
            {
                desiredVisibility = 0f;
                tunnelRetired = true;
            }

            if (!Mathf.Approximately(desiredVisibility, tunnelVisibility))
            {
                tunnelVisibility = desiredVisibility;
                ApplyTunnelVisibility(tunnelVisibility);
            }
        }
    }

    private void ApplyTunnelVisibility(float visibility)
    {
        if (!tunnelStateCached) return;

        float amount = Mathf.Clamp01(visibility);
        bool renderTunnel = amount > 0.005f;
        bool keepTunnelCollider = amount > 0.05f;

        for (int i = 0; i < tunnelRenderers.Count; i++)
        {
            RendererState state = tunnelRenderers[i];
            if (state.renderer == null) continue;

            state.renderer.enabled = renderTunnel && state.originalEnabled;
            if (!renderTunnel)
            {
                state.renderer.SetPropertyBlock(null);
                continue;
            }

            state.block.Clear();
            if (state.hasBaseColor)
            {
                Color fadedBase = state.baseColor * amount;
                state.block.SetColor("_BaseColor", fadedBase);
                state.block.SetColor("_Color", fadedBase);
            }
            if (state.hasEmissionColor)
                state.block.SetColor("_EmissionColor", state.emissionColor * amount);

            state.renderer.SetPropertyBlock(state.block);
        }

        for (int i = 0; i < tunnelColliders.Count; i++)
        {
            Collider collider = tunnelColliders[i];
            if (collider != null)
                collider.enabled = keepTunnelCollider && originalColliderStates[i];
        }
    }

    private float NextBackdropFloat(float min, float max)
    {
        if (backdropRandom == null)
            backdropRandom = new System.Random(routeDirector != null ? routeDirector.RunSeed ^ 0x5EED : 260412);
        return min + (float)backdropRandom.NextDouble() * (max - min);
    }

    private void UpdateBackdropRocks(float distanceStep)
    {
        for (int i = backdropRocks.Count - 1; i >= 0; i--)
        {
            GameObject rock = backdropRocks[i];
            if (rock == null)
            {
                backdropRocks.RemoveAt(i);
                backdropTargetScales.Remove(rock);
                continue;
            }

            rock.transform.position += Vector3.back * distanceStep;
            if (backdropTargetScales.TryGetValue(rock, out Vector3 targetScale))
            {
                float scaleStep = Mathf.Max(0f, distanceStep) *
                    targetScale.magnitude / Mathf.Max(1f, backdropScaleInDistance);
                rock.transform.localScale = Vector3.MoveTowards(
                    rock.transform.localScale, targetScale, scaleStep);
                if ((rock.transform.localScale - targetScale).sqrMagnitude < 0.0001f)
                {
                    rock.transform.localScale = targetScale;
                    backdropTargetScales.Remove(rock);
                }
            }

            if (rock.transform.position.z < -backdropRockCleanupBehindZ)
            {
                Destroy(rock);
                backdropRocks.RemoveAt(i);
                backdropTargetScales.Remove(rock);
            }
        }
    }

    private void SpawnBackdropRock()
    {
        if (backdropRockPrefabs == null || backdropRockPrefabs.Length == 0) return;
        if (backdropRandom == null)
            backdropRandom = new System.Random(routeDirector != null ? routeDirector.RunSeed ^ 0x5EED : 260412);

        int attempts = backdropRockPrefabs.Length;
        GameObject prefab = null;
        while (attempts-- > 0 && prefab == null)
            prefab = backdropRockPrefabs[backdropRandom.Next(0, backdropRockPrefabs.Length)];
        if (prefab == null) return;

        // Spawn beyond the near visual field. New scenery scales in over travelled distance,
        // so a full-size mesh never suddenly appears in the player's peripheral vision.
        float z = Mathf.Max(backdropSpawnDistance, spawnAheadZ + NextBackdropFloat(335f, 445f));
        Vector3 routeOffset = routeDirector != null
            ? routeDirector.GetCenterOffsetAtDistance(routeDirector.DistanceTravelled + z)
            : Vector3.zero;
        float x = NextBackdropFloat(backdropXRange.x, backdropXRange.y) *
            (backdropRandom.Next(0, 2) == 0 ? -1f : 1f);
        float y = NextBackdropFloat(backdropYRange.x, backdropYRange.y) *
            (backdropRandom.Next(0, 2) == 0 ? -1f : 1f);
        Quaternion rotation = Quaternion.Euler(
            NextBackdropFloat(0f, 360f),
            NextBackdropFloat(0f, 360f),
            NextBackdropFloat(0f, 360f));

        GameObject rock = Instantiate(prefab,
            new Vector3(routeOffset.x + x, routeOffset.y + y, z), rotation, transform);
        rock.name = "VOIDLINE / Distant Asteroid";
        Vector3 targetScale = rock.transform.localScale
            * NextBackdropFloat(backdropScaleRange.x, backdropScaleRange.y);
        rock.transform.localScale = targetScale * 0.012f;
        backdropTargetScales[rock] = targetScale;

        Collider[] colliders = rock.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            if (colliders[i] != null) colliders[i].enabled = false;

        Rigidbody[] bodies = rock.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < bodies.Length; i++)
        {
            if (bodies[i] == null) continue;
            bodies[i].isKinematic = true;
            bodies[i].useGravity = false;
            bodies[i].detectCollisions = false;
        }

        VoidlineAsteroid[] behaviours = rock.GetComponentsInChildren<VoidlineAsteroid>(true);
        for (int i = 0; i < behaviours.Length; i++)
            if (behaviours[i] != null) behaviours[i].enabled = false;

        Renderer[] renderers = rock.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            renderers[i].shadowCastingMode = ShadowCastingMode.Off;
            renderers[i].receiveShadows = false;
            renderers[i].lightProbeUsage = LightProbeUsage.Off;
            renderers[i].reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        backdropRocks.Add(rock);
    }

    private void SpawnLandmark()
    {
        // Natural passages use an irregular boulder arch rather than a uniform metal ring.
        // A manufactured transit gate is intentionally rare and only appears every third sector.
        bool requestedGate = landmarkIndex % 3 == 2;
        GameObject prefab = requestedGate ? GetNextLandmarkPrefab() : null;
        bool useGate = requestedGate && prefab != null;
        if (requestedGate && prefab == null)
            Debug.LogWarning("VOIDLINE transit-gate prefab is missing; using a natural asteroid arch.");

        float absoluteDistance = routeDirector != null
            ? routeDirector.DistanceTravelled + spawnAheadZ
            : distanceTravelled + spawnAheadZ;
        Vector3 routeOffset = routeDirector != null
            ? routeDirector.GetCenterOffsetAtDistance(absoluteDistance)
            : Vector3.zero;
        Quaternion routeRotation = routeDirector != null
            ? routeDirector.GetTrackRotationAtDistance(absoluteDistance)
            : Quaternion.identity;
        Vector3 spawnPosition = new Vector3(routeOffset.x, routeOffset.y, spawnAheadZ);

        activeLandmark = useGate
            ? Instantiate(prefab, spawnPosition, routeRotation)
            : CreateNaturalAsteroidArch(spawnPosition, routeRotation);
        PrepareAsNonInteractiveScenery(activeLandmark);

        activeLandmarkIsGate = useGate;
        activeLandmarkResolved = false;
        activeLandmark.name = useGate
            ? "VOIDLINE / Ancient Transit Gate"
            : "VOIDLINE / Hollow Asteroid Arch";
        activeLandmark.transform.localScale = Vector3.one;
        landmarkIndex++;
        Debug.Log("VOIDLINE space passage started: " + activeLandmark.name + ".");
    }

    private GameObject CreateNaturalAsteroidArch(Vector3 position, Quaternion rotation)
    {
        GameObject arch = new GameObject("VOIDLINE / Hollow Asteroid Arch");
        arch.transform.SetPositionAndRotation(position, rotation);

        GameObject[] sources = backdropRockPrefabs;
        if (sources == null || sources.Length == 0)
        {
            sources = new[]
            {
                Resources.Load<GameObject>("PF_Asteroid_MoonRock"),
                Resources.Load<GameObject>("PF_Asteroid_Meteor")
            };
        }

        // Irregular horseshoe of rock chunks, leaving a clean lower gap and a generous
        // flight opening. No frame, rail, beam, or hidden solid collider defines the route.
        float[] angles = { 18f, 47f, 78f, 109f, 141f, 171f, 201f, 230f, 247f, 301f, 331f };
        for (int i = 0; i < angles.Length; i++)
        {
            GameObject source = sources[i % sources.Length];
            if (source == null) continue;

            float angle = (angles[i] + NextBackdropFloat(-4.5f, 4.5f)) * Mathf.Deg2Rad;
            float radiusX = 7.0f + NextBackdropFloat(-0.45f, 0.55f);
            float radiusY = 5.0f + NextBackdropFloat(-0.4f, 0.45f);
            GameObject chunk = Instantiate(source, arch.transform, false);
            chunk.name = "Rock Segment " + (i + 1).ToString("00");
            chunk.transform.localPosition = new Vector3(
                Mathf.Cos(angle) * radiusX,
                Mathf.Sin(angle) * radiusY,
                NextBackdropFloat(-0.8f, 0.8f));
            chunk.transform.localRotation = Quaternion.Euler(
                NextBackdropFloat(-30f, 30f),
                NextBackdropFloat(0f, 360f),
                NextBackdropFloat(-30f, 30f));
            chunk.transform.localScale = Vector3.one * NextBackdropFloat(1.35f, 1.9f);
            PrepareAsNonInteractiveScenery(chunk);
        }

        return arch;
    }

    private static void PrepareAsNonInteractiveScenery(GameObject root)
    {
        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            if (colliders[i] != null) colliders[i].enabled = false;

        Rigidbody[] bodies = root.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < bodies.Length; i++)
        {
            if (bodies[i] == null) continue;
            bodies[i].isKinematic = true;
            bodies[i].useGravity = false;
            bodies[i].detectCollisions = false;
        }

        VoidlineAsteroid[] behaviours = root.GetComponentsInChildren<VoidlineAsteroid>(true);
        for (int i = 0; i < behaviours.Length; i++)
            if (behaviours[i] != null) behaviours[i].enabled = false;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            renderers[i].shadowCastingMode = ShadowCastingMode.Off;
            renderers[i].receiveShadows = false;
            renderers[i].lightProbeUsage = LightProbeUsage.Off;
            renderers[i].reflectionProbeUsage = ReflectionProbeUsage.Off;
        }
    }

    private GameObject GetNextLandmarkPrefab()
    {
        bool useGate = landmarkIndex % 3 == 2;
        if (useGate)
        {
            if (transitGatePrefab == null)
                transitGatePrefab = Resources.Load<GameObject>("PF_AncientTransitGate");
            return transitGatePrefab;
        }

        if (hollowAsteroidPrefab == null)
            hollowAsteroidPrefab = Resources.Load<GameObject>("PF_HollowAsteroidArch");
        return hollowAsteroidPrefab;
    }

    private void RestoreTunnelState()
    {
        // Restore original authored state when leaving the scene/editor; during play,
        // the corridor is intentionally hidden, not deleted or destructively edited.
        if (!tunnelStateCached) return;
        tunnelVisibility = 1f;
        ApplyTunnelVisibility(1f);
    }

    private void RestoreRenderSettings()
    {
        RenderSettings.skybox = previousSkybox;
        RenderSettings.ambientMode = previousAmbientMode;
        RenderSettings.ambientIntensity = previousAmbientIntensity;
        RenderSettings.fog = previousFog;
    }

    private void OnDisable()
    {
        RestoreTunnelState();
        // Play Mode exits can disable scene behaviours without destroying the scene object.
        // Restore global render state here, not only in OnDestroy.
        RestoreRenderSettings();
    }

    private void OnDestroy()
    {
        RestoreTunnelState();
        RestoreRenderSettings();

        if (ownedSectorSkybox != null) Destroy(ownedSectorSkybox);
        if (runtimeFallbackSkybox != null) Destroy(runtimeFallbackSkybox);
        if (runtimeFallbackSkyTexture != null) Destroy(runtimeFallbackSkyTexture);
    }
}
