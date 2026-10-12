using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Creates two small world-space exhaust trails at the actual ship's rear. Particle count,
/// lifetime and speed scale up while boosting; effects never own or alter gameplay movement.
/// </summary>
[DisallowMultipleComponent]
public sealed class VoidlineFlightEffects : MonoBehaviour
{
    [Header("Exhaust Tuning")]
    [SerializeField, Min(0f)] private float cruiseEmission = 22f;
    [SerializeField, Min(0f)] private float boostEmission = 170f;
    [SerializeField, Min(0f)] private float cruiseParticleSpeed = 3.0f;
    [SerializeField, Min(0f)] private float boostParticleSpeed = 12.5f;
    [SerializeField, Min(0.1f)] private float cruiseLifetime = 0.30f;
    [SerializeField, Min(0.1f)] private float boostLifetime = 0.46f;

    private VoidlineGameManager gameManager;
    private Transform visualRoot;
    private ParticleSystem leftTrail;
    private ParticleSystem rightTrail;
    private Material trailMaterial;
    private Texture2D trailTexture;
    private float boostBlend;
    private readonly string[] plumeNames = { "ExhaustOuter_L", "ExhaustCore_L", "ExhaustOuter_R", "ExhaustCore_R" };
    private readonly Transform[] authoredPlumes = new Transform[4];
    private readonly Vector3[] authoredPlumeRestScales = new Vector3[4];
    private readonly Renderer[] authoredPlumeRenderers = new Renderer[4];
    private MaterialPropertyBlock plumePropertyBlock;
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    public void Configure(VoidlinePlayerController controller, VoidlineGameManager manager)
    {
        gameManager = manager;
        if (visualRoot == null) visualRoot = transform.Find("Model");
        EnsureTrails();
    }

    private void Awake()
    {
        plumePropertyBlock = new MaterialPropertyBlock();
        visualRoot = transform.Find("Model");
        EnsureTrails();
    }

    private void EnsureTrails()
    {
        if (visualRoot == null) visualRoot = transform.Find("Model");
        if (visualRoot == null) return;

        CacheAuthoredPlumes();
        if (leftTrail != null && rightTrail != null) return;

        CreateTrailMaterial();
        MeshFilter meshFilter = visualRoot.GetComponentInChildren<MeshFilter>();
        Vector3 center = Vector3.zero;
        Vector3 extents = new Vector3(0.7f, 0.35f, 0.9f);
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            Bounds bounds = meshFilter.sharedMesh.bounds;
            Vector3 scale = meshFilter.transform.localScale;
            center = meshFilter.transform.localPosition + Vector3.Scale(bounds.center, scale);
            extents = Vector3.Scale(bounds.extents,
                new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
        }

        float engineX = Mathf.Clamp(extents.x * 0.47f, 0.32f, 0.95f);
        float engineY = center.y - extents.y * 0.45f;
        float rearZ = center.z - extents.z * 0.74f;

        Transform leftAnchor = GetAnchor("ExhaustSocket_L", -engineX, engineY, rearZ);
        Transform rightAnchor = GetAnchor("ExhaustSocket_R", engineX, engineY, rearZ);
        if (leftTrail == null) leftTrail = CreateTrail(leftAnchor, 101);
        if (rightTrail == null) rightTrail = CreateTrail(rightAnchor, 202);
    }

    private Transform GetAnchor(string anchorName, float x, float y, float z)
    {
        Transform anchor = FindDescendant(visualRoot, anchorName);
        if (anchor == null)
        {
            GameObject anchorObject = new GameObject(anchorName);
            anchor = anchorObject.transform;
            anchor.SetParent(visualRoot, false);
            anchor.localPosition = new Vector3(x, y, z);
            anchor.localScale = Vector3.one;
        }

        // Preserve the authored nozzle position but make the effect's local forward axis
        // explicit. The particle system child is rotated backward from the ship's +Z nose.
        anchor.localRotation = Quaternion.identity;
        return anchor;
    }

    private void CacheAuthoredPlumes()
    {
        for (int i = 0; i < plumeNames.Length; i++)
        {
            if (authoredPlumes[i] == null)
                authoredPlumes[i] = FindDescendant(visualRoot, plumeNames[i]);
            if (authoredPlumes[i] == null) continue;

            if (authoredPlumeRestScales[i] == Vector3.zero)
                authoredPlumeRestScales[i] = authoredPlumes[i].localScale;
            if (authoredPlumeRenderers[i] == null)
                authoredPlumeRenderers[i] = authoredPlumes[i].GetComponentInChildren<Renderer>(true);
            // These FBX nodes can wrap their mesh renderer in a child. Disable the complete
            // authored plume subtree: it is still represented by this transform for sizing,
            // but only the soft particle exhaust is rendered at gameplay distance.
            Renderer[] plumeRenderers = authoredPlumes[i].GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < plumeRenderers.Length; r++)
                if (plumeRenderers[r] != null) plumeRenderers[r].enabled = false;
        }
    }

    private static Transform FindDescendant(Transform root, string targetName)
    {
        if (root == null) return null;
        if (root.name == targetName) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDescendant(root.GetChild(i), targetName);
            if (found != null) return found;
        }
        return null;
    }

    private ParticleSystem CreateTrail(Transform anchor, uint randomSeed)
    {
        GameObject trailObject = new GameObject("Particle Exhaust Wisps");
        trailObject.transform.SetParent(anchor, false);
        trailObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        ParticleSystem particles = trailObject.AddComponent<ParticleSystem>();
        // New particle systems can auto-start immediately when added at runtime.
        // Stop and clear before setting deterministic seeds to avoid Unity runtime warnings.
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        particles.useAutoRandomSeed = false;
        particles.randomSeed = randomSeed;

        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.startDelay = 0f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(cruiseLifetime * 0.78f, cruiseLifetime * 1.18f);
        main.startSpeed = cruiseParticleSpeed;
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);
        main.startColor = Color.white;
        main.gravityModifier = 0f;
        main.maxParticles = 256;
        main.cullingMode = ParticleSystemCullingMode.Automatic;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = cruiseEmission;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 8f;
        shape.radius = 0.05f;
        shape.radiusThickness = 1f;
        shape.alignToDirection = true;

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.34f, 0.96f, 1f), 0f),
                new GradientColorKey(new Color(0.12f, 0.54f, 1f), 0.52f),
                new GradientColorKey(new Color(0.08f, 0.12f, 0.5f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.84f, 0f),
                new GradientAlphaKey(0.62f, 0.22f),
                new GradientAlphaKey(0f, 1f)
            });
        ParticleSystem.ColorOverLifetimeModule colour = particles.colorOverLifetime;
        colour.enabled = true;
        colour.color = new ParticleSystem.MinMaxGradient(gradient);

        AnimationCurve taper = new AnimationCurve(
            new Keyframe(0f, 0.25f), new Keyframe(0.18f, 1f), new Keyframe(1f, 0.04f));
        ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, taper);

        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.alignment = ParticleSystemRenderSpace.View;
        // A long, low-alpha soft sprite stretches along each particle's velocity.
        renderer.velocityScale = 0.19f;
        renderer.lengthScale = 0.92f;
        renderer.sharedMaterial = trailMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        particles.Play(true);
        return particles;
    }

    private void CreateTrailMaterial()
    {
        if (trailMaterial != null) return;

        // URP/Unlit gives us predictable RGBA alpha for the authored soft sprite;
        // the Particles/Unlit variant can fall back to its default opaque blend state when
        // constructed at runtime, which turns the plume texture into visible square cards.
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null) return;

        trailMaterial = new Material(shader)
        {
            name = "VOIDLINE_Runtime_Exhaust",
            renderQueue = 3000
        };
        Color tint = new Color(0.52f, 0.95f, 1f, 0.86f);
        if (trailMaterial.HasProperty("_BaseColor")) trailMaterial.SetColor("_BaseColor", tint);
        if (trailMaterial.HasProperty("_Color")) trailMaterial.SetColor("_Color", tint);
        trailMaterial.SetOverrideTag("RenderType", "Transparent");
        if (trailMaterial.HasProperty("_Surface")) trailMaterial.SetFloat("_Surface", 1f);
        if (trailMaterial.HasProperty("_Blend")) trailMaterial.SetFloat("_Blend", 0f);
        if (trailMaterial.HasProperty("_ZWrite")) trailMaterial.SetFloat("_ZWrite", 0f);
        if (trailMaterial.HasProperty("_SrcBlend")) trailMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (trailMaterial.HasProperty("_DstBlend")) trailMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (trailMaterial.HasProperty("_Cull")) trailMaterial.SetFloat("_Cull", (float)CullMode.Off);
        trailMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        trailMaterial.EnableKeyword("_ALPHABLEND_ON");
        trailMaterial.DisableKeyword("_ALPHATEST_ON");
        if (trailMaterial.HasProperty("_BaseMap") || trailMaterial.HasProperty("_MainTex"))
        {
            trailTexture = CreateSoftTrailTexture();
            if (trailMaterial.HasProperty("_BaseMap")) trailMaterial.SetTexture("_BaseMap", trailTexture);
            if (trailMaterial.HasProperty("_MainTex")) trailMaterial.SetTexture("_MainTex", trailTexture);
        }
    }

    private static Texture2D CreateSoftTrailTexture()
    {
        const int resolution = 32;
        Texture2D texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false, true)
        {
            name = "VOIDLINE_Runtime_ExhaustFalloff",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        Color[] pixels = new Color[resolution * resolution];
        for (int y = 0; y < resolution; y++)
        for (int x = 0; x < resolution; x++)
        {
            float dx = (x + 0.5f) / resolution * 2f - 1f;
            float dy = (y + 0.5f) / resolution * 2f - 1f;
            float radius = Mathf.Sqrt(dx * dx + dy * dy);
            float alpha = Mathf.Pow(Mathf.Clamp01(1f - radius), 1.8f);
            pixels[y * resolution + x] = new Color(1f, 1f, 1f, alpha);
        }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private void LateUpdate()
    {
        if (gameManager != null) SetBoostBlend(gameManager.BoostBlend);
    }

    public void SetBoostBlend(float value)
    {
        boostBlend = Mathf.Clamp01(value);
        UpdateTrail(leftTrail);
        UpdateTrail(rightTrail);
        UpdateAuthoredPlumes();
        if (trailMaterial != null)
        {
            Color tint = Color.Lerp(new Color(0.32f, 0.68f, 1f, 0.78f),
                new Color(0.66f, 0.98f, 1f, 0.96f), boostBlend);
            if (trailMaterial.HasProperty("_BaseColor")) trailMaterial.SetColor("_BaseColor", tint);
            if (trailMaterial.HasProperty("_Color")) trailMaterial.SetColor("_Color", tint);
        }
    }

    private void UpdateAuthoredPlumes()
    {
        CacheAuthoredPlumes();
        float pulse = 0.94f + 0.06f * Mathf.Sin(Time.unscaledTime * Mathf.Lerp(9f, 19f, boostBlend));
        float boost = boostBlend * boostBlend * (3f - 2f * boostBlend);

        for (int i = 0; i < authoredPlumes.Length; i++)
        {
            Transform plume = authoredPlumes[i];
            if (plume == null) continue;

            bool core = i == 1 || i == 3;
            float length = core ? Mathf.Lerp(0.20f, 0.88f, boost) : Mathf.Lerp(0.18f, 0.82f, boost);
            // Broaden the plasma sheath and let the particle volume provide the soft edge;
            // narrow elongated meshes alone read as laser lines at gameplay distance.
            float width = core ? Mathf.Lerp(1.05f, 1.42f, boost) : Mathf.Lerp(1.45f, 2.0f, boost);
            Vector3 rest = authoredPlumeRestScales[i];
            plume.localScale = new Vector3(rest.x * width * pulse, rest.y * width * pulse, rest.z * length);

            Renderer plumeRenderer = authoredPlumeRenderers[i];
            if (plumeRenderer != null)
            {
                // Awake normally creates this, but a component added after the Awake phase (or a
                // domain reload with scene reload disabled) can reach here with it still null,
                // which throws ArgumentNullException from GetPropertyBlock every frame.
                if (plumePropertyBlock == null) plumePropertyBlock = new MaterialPropertyBlock();

                Color glow = core
                    ? new Color(0.18f, 0.72f, 1f, Mathf.Lerp(0.30f, 0.92f, boost)) * Mathf.Lerp(1.1f, 4.8f, boost)
                    : new Color(0.015f, 0.32f, 1f, Mathf.Lerp(0.12f, 0.52f, boost)) * Mathf.Lerp(0.7f, 3.3f, boost);
                plumeRenderer.GetPropertyBlock(plumePropertyBlock);
                plumePropertyBlock.SetColor(EmissionColorId, glow);
                plumePropertyBlock.SetColor(BaseColorId, new Color(glow.r, glow.g, glow.b, core ? 0.92f : 0.42f));
                plumeRenderer.SetPropertyBlock(plumePropertyBlock);
            }
        }
    }

    private void UpdateTrail(ParticleSystem particles)
    {
        if (particles == null) return;
        ParticleSystem.MainModule main = particles.main;
        main.startSpeed = Mathf.Lerp(cruiseParticleSpeed, boostParticleSpeed, boostBlend);
        main.startLifetime = Mathf.Lerp(cruiseLifetime, boostLifetime, boostBlend);
        main.startSize = new ParticleSystem.MinMaxCurve(
            Mathf.Lerp(0.10f, 0.115f, boostBlend),
            Mathf.Lerp(0.18f, 0.22f, boostBlend));
        ParticleSystem.ShapeModule shape = particles.shape;
        // Hold the boost jet tight to each nozzle. Keep intensity from more emission/speed,
        // not from widening the cone into a diffuse fan that disconnects from the engine.
        shape.angle = Mathf.Lerp(5f, 5.5f, boostBlend);
        shape.radius = Mathf.Lerp(0.032f, 0.042f, boostBlend);
        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = Mathf.Lerp(cruiseEmission, boostEmission, boostBlend);
    }

    private void OnDestroy()
    {
        if (trailMaterial != null) Destroy(trailMaterial);
        if (trailTexture != null) Destroy(trailTexture);
    }
}
