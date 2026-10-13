using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Arcade-speed treatment for space flight: the ship and central route remain readable while
/// peripheral velocity streaks, a brief ignition pulse, camera impulse and restrained URP blur
/// sell acceleration. The lens geometry is camera-relative, so it works in captures and builds.
/// </summary>
[DisallowMultipleComponent]
public sealed class VoidlineSpeedEffects : MonoBehaviour
{
    [Header("Velocity Lens")]
    [SerializeField, Range(0f, 0.9f)] private float cruiseStreakOpacity = 0f;
    [SerializeField, Range(0f, 0.9f)] private float boostStreakOpacity = 0.74f;
    [SerializeField, Range(0f, 1f)] private float boostBlurIntensity = 0.38f;
    [SerializeField, Min(0.1f)] private float effectResponse = 7f;
    [SerializeField, Min(0.1f)] private float burstDuration = 0.72f;
    [SerializeField, Min(1f)] private float lensPlaneDistance = 2.6f;

    private VoidlineGameManager manager;
    private Camera targetCamera;
    private Transform streakRoot;
    private Mesh streakMesh;
    private Texture2D streakFalloffTexture;
    private Material streakMaterial;
    private Volume motionVolume;
    private VolumeProfile runtimeProfile;
    private MotionBlur motionBlur;
    private Vignette speedVignette;
    private ColorAdjustments speedGrade;
    private Bloom speedBloom;
    private ChromaticAberration boostChromatic;
    private FilmGrain speedGrain;
    private float boostBlend;
    private float burstRemaining;
    private float burstStrength;
    private float presentedStreakOpacity;
    private float referenceFov = 58f;

    public void Configure(VoidlineGameManager owner)
    {
        manager = owner;
        EnsureEffects();
    }

    private void Awake() => EnsureEffects();

    private bool TryRecoverExistingEffects()
    {
        if (targetCamera == null) return false;

        Transform existingRoot = transform.Find("VOIDLINE / Peripheral Velocity");
        if (existingRoot != null)
        {
            streakRoot = existingRoot;
            MeshFilter meshFilter = existingRoot.GetComponent<MeshFilter>();
            MeshRenderer meshRenderer = existingRoot.GetComponent<MeshRenderer>();
            streakMesh = meshFilter != null ? meshFilter.sharedMesh : null;
            streakMaterial = meshRenderer != null ? meshRenderer.sharedMaterial : null;
            if (streakMaterial != null && streakMaterial.HasProperty("_BaseMap"))
                streakFalloffTexture = streakMaterial.GetTexture("_BaseMap") as Texture2D;
        }

        Volume[] volumes = targetCamera.GetComponents<Volume>();
        for (int i = 0; i < volumes.Length; i++)
        {
            Volume candidate = volumes[i];
            if (candidate == null || candidate.sharedProfile == null) continue;
            if (candidate.sharedProfile.name != "VOIDLINE Runtime Speed Profile") continue;
            motionVolume = candidate;
            runtimeProfile = candidate.sharedProfile;
            break;
        }

        if (runtimeProfile != null)
        {
            runtimeProfile.TryGet(out motionBlur);
            runtimeProfile.TryGet(out speedVignette);
            runtimeProfile.TryGet(out speedGrade);
            runtimeProfile.TryGet(out speedBloom);
            runtimeProfile.TryGet(out boostChromatic);
            runtimeProfile.TryGet(out speedGrain);
        }

        bool complete = streakRoot != null && streakMesh != null && streakMaterial != null
            && motionVolume != null && runtimeProfile != null
            && motionBlur != null && speedVignette != null && speedGrade != null
            && speedBloom != null && boostChromatic != null && speedGrain != null;

        if (complete)
        {
            UniversalAdditionalCameraData cameraData =
                targetCamera.GetComponent<UniversalAdditionalCameraData>();
            if (cameraData == null)
                cameraData = targetCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = true;
            SetLensScale(targetCamera.fieldOfView);
            SetMaterialAlpha(streakMaterial, presentedStreakOpacity);
            return true;
        }

        // A partial stale runtime rig is worse than rebuilding: remove only the uniquely
        // named generated objects/components before falling through to the normal constructor.
        if (existingRoot != null)
        {
            // Rename before deferred Destroy so an immediate second Configure() cannot
            // rediscover the stale object while Unity waits until end-of-frame to delete it.
            existingRoot.gameObject.name = "VOIDLINE / Peripheral Velocity [stale]";
            if (Application.isPlaying) Destroy(existingRoot.gameObject);
            else DestroyImmediate(existingRoot.gameObject);
        }
        if (motionVolume != null)
        {
            Volume staleVolume = motionVolume;
            VolumeProfile staleProfile = runtimeProfile;
            if (staleProfile != null)
                staleProfile.name = "VOIDLINE Runtime Speed Profile [stale]";
            if (Application.isPlaying) Destroy(staleVolume);
            else DestroyImmediate(staleVolume);
            if (staleProfile != null)
            {
                if (Application.isPlaying) Destroy(staleProfile);
                else DestroyImmediate(staleProfile);
            }
        }

        streakRoot = null;
        streakMesh = null;
        streakFalloffTexture = null;
        streakMaterial = null;
        motionVolume = null;
        runtimeProfile = null;
        motionBlur = null;
        speedVignette = null;
        speedGrade = null;
        speedBloom = null;
        boostChromatic = null;
        speedGrain = null;
        return false;
    }

    private void EnsureEffects()
    {
        targetCamera = GetComponent<Camera>();
        if (targetCamera == null) targetCamera = Camera.main;
        if (targetCamera == null) return;

        // Private runtime references can be cleared by script/domain reload while generated
        // children and the Volume remain on the camera. Rebind them first instead of stacking
        // another set of meshes and full-screen URP effects.
        if (TryRecoverExistingEffects()) return;
        referenceFov = 58f;

        // The streak mesh lives a few units in front of the lens, outside a large clear centre.
        // Unlike a UI overlay this remains part of the camera render path and survives resolution changes.
        GameObject streakObject = new GameObject("VOIDLINE / Peripheral Velocity");
        streakRoot = streakObject.transform;
        streakRoot.SetParent(transform, false);
        streakRoot.localPosition = Vector3.zero;
        streakRoot.localRotation = Quaternion.identity;
        streakRoot.localScale = Vector3.one;

        streakMesh = BuildRadialStreakMesh(44, lensPlaneDistance, targetCamera.aspect, referenceFov, 0x51A7u);
        streakObject.AddComponent<MeshFilter>().sharedMesh = streakMesh;
        MeshRenderer streakRenderer = streakObject.AddComponent<MeshRenderer>();
        streakMaterial = CreateTransparentUnlitMaterial("VOIDLINE / Velocity Streaks", new Color(0.60f, 0.88f, 1f, 0f));
        streakFalloffTexture = BuildStreakFalloffTexture();
        if (streakMaterial.HasProperty("_BaseMap")) streakMaterial.SetTexture("_BaseMap", streakFalloffTexture);
        streakRenderer.sharedMaterial = streakMaterial;
        streakRenderer.shadowCastingMode = ShadowCastingMode.Off;
        streakRenderer.receiveShadows = false;
        streakRenderer.lightProbeUsage = LightProbeUsage.Off;
        streakRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

        UniversalAdditionalCameraData cameraData = targetCamera.GetComponent<UniversalAdditionalCameraData>();
        if (cameraData == null) cameraData = targetCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();
        cameraData.renderPostProcessing = true;

        // State-driven blur affects moving world geometry during boost, not merely camera rotation.
        // The edge streaks add perceived outward motion while the central ship remains readable.
        motionVolume = targetCamera.gameObject.AddComponent<Volume>();
        motionVolume.isGlobal = true;
        motionVolume.priority = 1000f;
        runtimeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        runtimeProfile.name = "VOIDLINE Runtime Speed Profile";
        motionVolume.sharedProfile = runtimeProfile;
        motionBlur = runtimeProfile.Add<MotionBlur>(true);
        // Object motion vectors smear the moving asteroid field and corridor while the
        // edge streaks keep the courier visually anchored in the centre of the frame.
        motionBlur.mode.value = MotionBlurMode.CameraAndObjects;
        motionBlur.mode.overrideState = true;
        motionBlur.quality.value = MotionBlurQuality.Low;
        motionBlur.quality.overrideState = true;
        motionBlur.intensity.value = 0f;
        motionBlur.intensity.overrideState = true;
        motionBlur.clamp.value = 0.075f;
        motionBlur.clamp.overrideState = true;

        // Most Wanted's image treatment changed with speed. In space, use the same principle
        // without copying its warm tint: tighten the edges and add a restrained contrast lift.
        speedVignette = runtimeProfile.Add<Vignette>(true);
        speedVignette.intensity.overrideState = true;
        speedVignette.smoothness.overrideState = true;
        speedVignette.color.overrideState = true;
        speedVignette.rounded.overrideState = true;
        speedVignette.intensity.value = 0.05f;
        speedVignette.smoothness.value = 0.72f;
        speedVignette.color.value = Color.black;
        speedVignette.rounded.value = true;

        speedGrade = runtimeProfile.Add<ColorAdjustments>(true);
        speedGrade.saturation.overrideState = true;
        speedGrade.contrast.overrideState = true;
        speedGrade.postExposure.overrideState = true;
        speedGrade.colorFilter.overrideState = true;
        // A modest neon-retro grade lifts the nebula colour without flattening the dark void.
        // Boost then trades saturation for contrast and bloom, preserving a sharp ship silhouette.
        speedGrade.saturation.value = 9f;
        speedGrade.contrast.value = 5f;
        speedGrade.postExposure.value = 0.16f;
        speedGrade.colorFilter.value = Color.white;

        // Bloom belongs on the engines and a few star highlights—not as a cyan wash.
        speedBloom = runtimeProfile.Add<Bloom>(true);
        speedBloom.intensity.overrideState = true;
        speedBloom.threshold.overrideState = true;
        speedBloom.scatter.overrideState = true;
        speedBloom.intensity.value = 0.22f;
        speedBloom.threshold.value = 1.1f;
        speedBloom.scatter.value = 0.64f;

        speedGrain = runtimeProfile.Add<FilmGrain>(true);
        speedGrain.intensity.overrideState = true;
        speedGrain.intensity.value = 0.025f;

        boostChromatic = runtimeProfile.Add<ChromaticAberration>(true);
        boostChromatic.intensity.overrideState = true;
        boostChromatic.intensity.value = 0f;

        SetLensScale(targetCamera.fieldOfView);
        SetMaterialAlpha(streakMaterial, 0f);
    }

    public void SetBoostBlend(float value) => boostBlend = Mathf.Clamp01(value);

    public void TriggerBoostBurst()
    {
        burstRemaining = burstDuration;
        burstStrength = 1f;
    }

    private void LateUpdate()
    {
        if (targetCamera == null || streakMaterial == null) return;
        float dt = Time.unscaledDeltaTime;
        if (dt <= 0f) return;
        if (manager != null) boostBlend = manager.BoostBlend;
        SetLensScale(targetCamera.fieldOfView);

        float smoothBoost = boostBlend * boostBlend * (3f - 2f * boostBlend);
        float targetOpacity = Mathf.Lerp(cruiseStreakOpacity, boostStreakOpacity, smoothBoost);
        if (burstRemaining > 0f)
        {
            burstRemaining = Mathf.Max(0f, burstRemaining - dt);
            burstStrength = Mathf.MoveTowards(burstStrength, 0f, dt * 1.45f);
            targetOpacity = Mathf.Max(targetOpacity, boostStreakOpacity * burstStrength);
        }
        presentedStreakOpacity = Mathf.Lerp(presentedStreakOpacity, targetOpacity,
            1f - Mathf.Exp(-effectResponse * dt));
        float rushPhase = Mathf.Repeat(Time.unscaledTime * Mathf.Lerp(0.08f, 1.9f, smoothBoost), 1f);
        // Let streaks travel outward across the lens during each rush pulse. This gives
        // actual apparent flow instead of leaving a static ring of lines at the frame edge.
        float rushScale = 1f + 0.20f * rushPhase * Mathf.Clamp01(presentedStreakOpacity * 2.2f);
        float fovScale = Mathf.Tan(targetCamera.fieldOfView * Mathf.Deg2Rad * 0.5f) /
            Mathf.Tan(referenceFov * Mathf.Deg2Rad * 0.5f);
        if (streakRoot != null) streakRoot.localScale = new Vector3(fovScale * rushScale, fovScale * rushScale, 1f);
        SetMaterialAlpha(streakMaterial, presentedStreakOpacity);

        float speedPresentation = Mathf.Max(smoothBoost,
            burstRemaining > 0f ? 0.65f * burstStrength : 0f);
        if (speedVignette != null)
            speedVignette.intensity.value = Mathf.Lerp(0.05f, 0.28f, speedPresentation);
        if (speedGrade != null)
        {
            speedGrade.saturation.value = Mathf.Lerp(9f, -8f, speedPresentation);
            speedGrade.contrast.value = Mathf.Lerp(5f, 14f, speedPresentation);
            speedGrade.postExposure.value = Mathf.Lerp(0.16f, 0.22f, speedPresentation);
        }
        if (speedBloom != null)
            speedBloom.intensity.value = Mathf.Lerp(0.22f, 0.60f, speedPresentation);
        if (speedGrain != null)
            speedGrain.intensity.value = Mathf.Lerp(0.025f, 0.055f, speedPresentation);
        if (boostChromatic != null)
        {
            float burstWeight = burstRemaining > 0f ? burstStrength : 0f;
            boostChromatic.intensity.value = Mathf.Lerp(0f, 0.028f, smoothBoost) + 0.06f * burstWeight;
        }

        if (motionBlur != null)
        {
            float targetBlur = boostBlurIntensity * smoothBoost;
            if (burstRemaining > 0f)
                targetBlur = Mathf.Max(targetBlur, boostBlurIntensity * 0.72f * burstStrength);
            motionBlur.intensity.value = Mathf.Lerp(motionBlur.intensity.value, targetBlur,
                1f - Mathf.Exp(-8f * dt));
        }
    }

    private void SetLensScale(float fov)
    {
        if (streakRoot == null) return;
        float baseTan = Mathf.Tan(referenceFov * Mathf.Deg2Rad * 0.5f);
        float currentTan = Mathf.Tan(Mathf.Clamp(fov, 25f, 100f) * Mathf.Deg2Rad * 0.5f);
        float scale = currentTan / Mathf.Max(0.001f, baseTan);
        streakRoot.localScale = new Vector3(scale, scale, 1f);
    }

    private static void SetMaterialAlpha(Material material, float alpha)
    {
        if (material == null || !material.HasProperty("_BaseColor")) return;
        Color color = material.GetColor("_BaseColor");
        color.a = Mathf.Clamp01(alpha);
        material.SetColor("_BaseColor", color);
    }

    private static Material CreateTransparentUnlitMaterial(string materialName, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        Material material = new Material(shader)
        {
            name = materialName,
            renderQueue = (int)RenderQueue.Transparent
        };
        material.SetOverrideTag("RenderType", "Transparent");
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)CullMode.Off);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHATEST_ON");
        return material;
    }

    private static Mesh BuildRadialStreakMesh(int count, float z, float aspect, float fov, uint seed)
    {
        Mesh mesh = new Mesh { name = "VOIDLINE / Peripheral Streak Geometry" };
        Vector3[] vertices = new Vector3[count * 6];
        Color32[] colors = new Color32[count * 6];
        Vector2[] uvs = new Vector2[count * 6];
        int[] indices = new int[count * 12];
        System.Random random = new System.Random(unchecked((int)seed));
        float halfHeight = z * Mathf.Tan(fov * Mathf.Deg2Rad * 0.5f);
        float halfWidth = halfHeight * Mathf.Max(0.1f, aspect);

        for (int i = 0; i < count; i++)
        {
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);
            float startRadius = Mathf.Lerp(0.78f, 0.92f, (float)random.NextDouble());
            float endRadius = Mathf.Lerp(1.10f, 1.28f, (float)random.NextDouble());
            Vector2 direction = new Vector2(cos * halfWidth, sin * halfHeight).normalized;
            Vector2 perpendicular = new Vector2(-direction.y, direction.x);
            Vector3 start = new Vector3(cos * startRadius * halfWidth, sin * startRadius * halfHeight, z);
            Vector3 middle = new Vector3(cos * Mathf.Lerp(startRadius, endRadius, 0.56f) * halfWidth,
                sin * Mathf.Lerp(startRadius, endRadius, 0.56f) * halfHeight, z);
            Vector3 end = new Vector3(cos * endRadius * halfWidth, sin * endRadius * halfHeight, z);
            float width = Mathf.Lerp(0.006f, 0.013f, (float)random.NextDouble());
            float startWidth = width * 0.15f;
            float endWidth = width * 0.035f;
            int v = i * 6;

            vertices[v] = start - new Vector3(perpendicular.x, perpendicular.y, 0f) * startWidth;
            vertices[v + 1] = start + new Vector3(perpendicular.x, perpendicular.y, 0f) * startWidth;
            vertices[v + 2] = middle - new Vector3(perpendicular.x, perpendicular.y, 0f) * width;
            vertices[v + 3] = middle + new Vector3(perpendicular.x, perpendicular.y, 0f) * width;
            vertices[v + 4] = end - new Vector3(perpendicular.x, perpendicular.y, 0f) * endWidth;
            vertices[v + 5] = end + new Vector3(perpendicular.x, perpendicular.y, 0f) * endWidth;

            byte peak = (byte)random.Next(60, 125);
            colors[v] = new Color32(200, 239, 255, 0);
            colors[v + 1] = new Color32(200, 239, 255, 0);
            colors[v + 2] = new Color32(200, 239, 255, peak);
            colors[v + 3] = new Color32(200, 239, 255, peak);
            colors[v + 4] = new Color32(200, 239, 255, 0);
            colors[v + 5] = new Color32(200, 239, 255, 0);
            uvs[v] = new Vector2(0f, 0f);
            uvs[v + 1] = new Vector2(0f, 1f);
            uvs[v + 2] = new Vector2(0.56f, 0f);
            uvs[v + 3] = new Vector2(0.56f, 1f);
            uvs[v + 4] = new Vector2(1f, 0f);
            uvs[v + 5] = new Vector2(1f, 1f);

            int t = i * 12;
            indices[t] = v; indices[t + 1] = v + 1; indices[t + 2] = v + 2;
            indices[t + 3] = v + 2; indices[t + 4] = v + 1; indices[t + 5] = v + 3;
            indices[t + 6] = v + 2; indices[t + 7] = v + 3; indices[t + 8] = v + 4;
            indices[t + 9] = v + 4; indices[t + 10] = v + 3; indices[t + 11] = v + 5;
        }

        mesh.vertices = vertices;
        mesh.colors32 = colors;
        mesh.uv = uvs;
        mesh.triangles = indices;
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Texture2D BuildStreakFalloffTexture()
    {
        const int width = 32;
        const int height = 16;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
        {
            name = "VOIDLINE / Soft Velocity Falloff",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        Color32[] pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            float u = (x + 0.5f) / width;
            float v = (y + 0.5f) / height;
            float along = Mathf.Pow(Mathf.Clamp01(Mathf.Sin(u * Mathf.PI)), 0.75f);
            float across = Mathf.Exp(-Mathf.Pow((v - 0.5f) / 0.33f, 2f) * 1.6f);
            byte alpha = (byte)(255f * Mathf.Clamp01(along * across));
            pixels[y * width + x] = new Color32(255, 255, 255, alpha);
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private static Mesh BuildFlashMesh(float z, float aspect, float fov)
    {
        Mesh mesh = new Mesh { name = "VOIDLINE / Ignition Flash Quad" };
        float halfHeight = z * Mathf.Tan(fov * Mathf.Deg2Rad * 0.5f);
        float halfWidth = halfHeight * Mathf.Max(0.1f, aspect);
        mesh.vertices = new[]
        {
            new Vector3(-halfWidth, -halfHeight, z),
            new Vector3(-halfWidth, halfHeight, z),
            new Vector3(halfWidth, halfHeight, z),
            new Vector3(halfWidth, -halfHeight, z)
        };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
        mesh.RecalculateBounds();
        return mesh;
    }

    private void OnDestroy()
    {
        if (streakMesh != null) Destroy(streakMesh);
        if (streakFalloffTexture != null) Destroy(streakFalloffTexture);
        if (streakMaterial != null) Destroy(streakMaterial);
        if (runtimeProfile != null) Destroy(runtimeProfile);
        if (motionVolume != null) Destroy(motionVolume);
    }
}
