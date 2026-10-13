using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Places a small, bounded set of authored celestial bodies behind the hazard plane.
/// The imported skybox owns the stars/nebula/galaxy layer; these bodies add parallax and scale.
/// All bodies are non-interactive and remain out of the steering/enemy envelope.
/// </summary>
[DisallowMultipleComponent]
public sealed class VoidlineCelestialBackdrop : MonoBehaviour
{
    private enum BodyKind { Planet, GasGiant, Moon, Star }

    private sealed class BodyState
    {
        public GameObject root;
        public BodyKind kind;
        public float spin;
        public bool generatedSphere;
        public int seed;
    }

    private readonly List<BodyState> bodies = new List<BodyState>(4);
    private readonly List<Material> ownedMaterials = new List<Material>(8);
    private readonly List<Texture2D> ownedTextures = new List<Texture2D>(8);
    private readonly List<Mesh> ownedMeshes = new List<Mesh>(4);
    private VoidlineRouteDirector routeDirector;
    private VoidlineCelestialAssetSet assetSet;
    private System.Random random;
    private bool initialized;

    public void Initialize(VoidlineRouteDirector route, VoidlineCelestialAssetSet assets, int runSeed)
    {
        routeDirector = route;
        assetSet = assets != null ? assets : Resources.Load<VoidlineCelestialAssetSet>("VOIDLINE_CelestialAssetSet");
        ResetForRun(runSeed);
    }

    public void ResetForRun(int runSeed)
    {
        ClearBodies();
        random = new System.Random(runSeed ^ 0x71CE1A1);
        initialized = true;

        // The composition is asymmetric by design: planets sit near the edge of the frame,
        // never in the central player/enemy silhouette. Depths stay inside the 1,800-unit far clip.
        float side = (runSeed & 1) == 0 ? -1f : 1f;
        bodies.Add(Spawn(BodyKind.GasGiant, side * 460f, 112f, 1480f, 230f, runSeed + 11, true, side * 0.62f));
        bodies.Add(Spawn(BodyKind.Planet, -side * 505f, -105f, 1630f, 148f, runSeed + 23, false, -side * 0.28f));
        bodies.Add(Spawn(BodyKind.Moon, side * 315f, 172f, 965f, 58f, runSeed + 37, false, side * 1.75f));
        bodies.Add(Spawn(BodyKind.Star, -side * 260f, 258f, 1690f, 50f, runSeed + 41, false, -side * 0.07f));
    }

    public void Tick(float distanceStep, float deltaTime)
    {
        if (!initialized || deltaTime <= 0f) return;
        for (int i = 0; i < bodies.Count; i++)
        {
            BodyState body = bodies[i];
            if (body.root == null) continue;
            body.root.transform.position += Vector3.back * Mathf.Max(0f, distanceStep);
            if (Mathf.Abs(body.spin) > 0.001f)
                body.root.transform.Rotate(Vector3.up, body.spin * deltaTime, Space.Self);

            // Slow recycling keeps the world populated without a large scene hierarchy or
            // spawning assets inside encounter depth. The same authored mesh/material is reused.
            if (body.root.transform.position.z < -420f)
            {
                float z = Range(1450f, 1680f);
                float x = Range(390f, 560f) * (random.Next(0, 2) == 0 ? -1f : 1f);
                float y = Range(95f, 230f) * (random.Next(0, 2) == 0 ? -1f : 1f);
                Vector3 center = routeDirector != null
                    ? routeDirector.GetCenterOffsetAtDistance(routeDirector.DistanceTravelled + z)
                    : Vector3.zero;
                body.root.transform.position = new Vector3(center.x + x, center.y + y, z);
            }
        }
    }

    private BodyState Spawn(BodyKind kind, float x, float y, float z,
        float diameter, int seed, bool addRings, float spin)
    {
        Vector3 center = routeDirector != null
            ? routeDirector.GetCenterOffsetAtDistance(routeDirector.DistanceTravelled + z)
            : Vector3.zero;
        Vector3 position = new Vector3(center.x + x, center.y + y, z);
        GameObject prefab = PickPrefab(kind, seed);
        GameObject root;

        if (prefab != null)
        {
            root = Instantiate(prefab, position, Quaternion.identity, transform);
            root.name = "VOIDLINE / Authored " + kind;
            NormalizeDiameter(root, diameter);
        }
        else
        {
            root = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            root.name = "VOIDLINE / Celestial " + kind;
            root.transform.SetParent(transform, false);
            root.transform.SetPositionAndRotation(position, Quaternion.identity);
            root.transform.localScale = Vector3.one * diameter;
            Renderer renderer = root.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = PickMaterial(kind, seed) ?? CreateFallbackMaterial(kind, seed);
        }

        PrepareNonInteractive(root);
        if (addRings) AttachRing(root, seed);
        return new BodyState { root = root, kind = kind, spin = spin, generatedSphere = prefab == null, seed = seed };
    }

    private GameObject PickPrefab(BodyKind kind, int seed)
    {
        if (assetSet == null) return null;
        switch (kind)
        {
            case BodyKind.GasGiant: return assetSet.PickGasGiantPrefab(seed);
            case BodyKind.Planet: return assetSet.PickPlanetPrefab(seed);
            case BodyKind.Moon: return assetSet.PickMoonPrefab(seed);
            case BodyKind.Star: return assetSet.PickStarPrefab(seed);
            default: return null;
        }
    }

    private Material PickMaterial(BodyKind kind, int seed)
    {
        if (assetSet == null) return null;
        switch (kind)
        {
            case BodyKind.GasGiant: return assetSet.PickGasGiantMaterial(seed);
            case BodyKind.Planet: return assetSet.PickPlanetMaterial(seed);
            case BodyKind.Moon: return assetSet.PickMoonMaterial(seed);
            case BodyKind.Star: return assetSet.PickStarMaterial(seed);
            default: return null;
        }
    }

    private void NormalizeDiameter(GameObject root, float diameter)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            root.transform.localScale = Vector3.one * diameter;
            return;
        }
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        float extent = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        if (extent > 0.001f) root.transform.localScale *= diameter / extent;
    }

    private Material CreateFallbackMaterial(BodyKind kind, int seed)
    {
        Color[] gas =
        {
            new Color(0.95f, 0.39f, 0.18f), new Color(0.92f, 0.65f, 0.28f),
            new Color(0.24f, 0.73f, 0.86f), new Color(0.68f, 0.39f, 0.91f)
        };
        Color[] planet =
        {
            new Color(0.12f, 0.52f, 0.88f), new Color(0.18f, 0.72f, 0.51f),
            new Color(0.78f, 0.38f, 0.23f), new Color(0.45f, 0.38f, 0.9f)
        };
        Color baseColor = kind == BodyKind.GasGiant ? gas[Mathf.Abs(seed % gas.Length)]
            : kind == BodyKind.Planet ? planet[Mathf.Abs(seed % planet.Length)]
            : kind == BodyKind.Moon ? new Color(0.58f, 0.76f, 0.9f)
            : new Color(1f, 0.74f, 0.40f);
        Shader shader = kind == BodyKind.Star
            ? Shader.Find("Universal Render Pipeline/Unlit")
            : Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) return null;

        Material material = new Material(shader)
        {
            name = "VOIDLINE / Fallback " + kind + " Surface",
            hideFlags = HideFlags.DontSave
        };
        Texture2D texture = PickSurfaceTexture(kind, seed);
        if (texture == null)
        {
            texture = BuildSurfaceTexture(kind, baseColor, seed);
            if (texture != null) ownedTextures.Add(texture);
        }
        if (texture != null)
        {
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
        }
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", baseColor);
        if (material.HasProperty("_Color")) material.SetColor("_Color", baseColor);
        if (kind == BodyKind.Star && material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", baseColor * 2.5f);
        }
        ownedMaterials.Add(material);
        return material;
    }

    private Texture2D PickSurfaceTexture(BodyKind kind, int seed)
    {
        if (assetSet == null) return null;
        switch (kind)
        {
            case BodyKind.GasGiant: return assetSet.PickGasGiantTexture(seed);
            case BodyKind.Planet: return assetSet.PickPlanetTexture(seed);
            case BodyKind.Moon: return assetSet.PickMoonTexture(seed);
            default: return null;
        }
    }

    private static Texture2D BuildSurfaceTexture(BodyKind kind, Color baseColor, int seed)
    {
        const int width = 512;
        const int height = 256;
        float phase = (seed & 0x7fffffff) * 0.00073f;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
        {
            name = "VOIDLINE / " + kind + " Fallback Surface",
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontSave
        };
        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            float v = y / (float)height;
            for (int x = 0; x < width; x++)
            {
                float u = x / (float)width;
                float noise = Mathf.PerlinNoise(u * 14f + phase, v * 8f + phase * 0.6f);
                Color color = baseColor;
                if (kind == BodyKind.GasGiant)
                {
                    float band = Mathf.Sin(v * 50f + Mathf.Sin(u * 12f + phase) * 1.7f + noise * 2.6f);
                    float stripe = Mathf.SmoothStep(-0.62f, 0.62f, band);
                    Color lightBand = Color.Lerp(new Color(1f, 0.83f, 0.62f), new Color(0.98f, 0.48f, 0.28f), noise);
                    Color shadowBand = Color.Lerp(baseColor * 0.64f, new Color(0.18f, 0.4f, 0.68f), noise * 0.42f);
                    color = Color.Lerp(shadowBand, lightBand, stripe);
                }
                else if (kind == BodyKind.Planet)
                {
                    float land = Mathf.PerlinNoise(u * 8f + phase, v * 5f + phase * 1.6f);
                    color = Color.Lerp(baseColor, Color.Lerp(baseColor, new Color(0.98f, 0.79f, 0.43f), 0.62f),
                        Mathf.SmoothStep(0.48f, 0.63f, land));
                }
                else if (kind == BodyKind.Moon)
                {
                    float craters = Mathf.PerlinNoise(u * 37f + phase, v * 31f + phase * 0.7f);
                    color = Color.Lerp(baseColor * 0.48f, baseColor * 1.12f, craters);
                }
                pixels[y * width + x] = color;
            }
        }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private void AttachRing(GameObject planet, int seed)
    {
        GameObject ringObject = new GameObject("VOIDLINE / Planetary Ring System");
        ringObject.transform.SetParent(planet.transform, false);
        ringObject.transform.localRotation = Quaternion.Euler(22f + (seed % 9), seed % 360, 7f);
        Mesh ringMesh = BuildRingMesh(64);
        ownedMeshes.Add(ringMesh);
        ringObject.AddComponent<MeshFilter>().sharedMesh = ringMesh;
        MeshRenderer renderer = ringObject.AddComponent<MeshRenderer>();
        Material ringMaterial = assetSet != null ? assetSet.PickRingMaterial(seed) : null;
        renderer.sharedMaterial = ringMaterial != null ? ringMaterial : CreateFallbackRingMaterial(seed);
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
    }

    private Material CreateFallbackRingMaterial(int seed)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) return null;
        Color tint = (seed & 1) == 0 ? new Color(0.98f, 0.66f, 0.37f, 0.76f)
            : new Color(0.42f, 0.77f, 1f, 0.69f);
        Material material = new Material(shader)
        {
            name = "VOIDLINE / Fallback Ring Sheen",
            renderQueue = 3000,
            hideFlags = HideFlags.DontSave
        };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", tint);
        if (material.HasProperty("_Color")) material.SetColor("_Color", tint);
        material.SetOverrideTag("RenderType", "Transparent");
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        ownedMaterials.Add(material);
        return material;
    }

    private Mesh BuildRingMesh(int segments)
    {
        Mesh mesh = new Mesh { name = "VOIDLINE / Planet Ring Geometry" };
        Vector3[] vertices = new Vector3[(segments + 1) * 2];
        Vector2[] uv = new Vector2[vertices.Length];
        int[] triangles = new int[segments * 6];
        const float innerRadius = 0.56f;
        const float outerRadius = 0.97f;
        for (int i = 0; i <= segments; i++)
        {
            float angle = i / (float)segments * Mathf.PI * 2f;
            float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
            vertices[i * 2] = new Vector3(c * innerRadius, 0f, s * innerRadius);
            vertices[i * 2 + 1] = new Vector3(c * outerRadius, 0f, s * outerRadius);
            uv[i * 2] = new Vector2(0f, 0f);
            uv[i * 2 + 1] = new Vector2(1f, 1f);
            if (i == segments) continue;
            int v = i * 2, t = i * 6;
            triangles[t] = v; triangles[t + 1] = v + 2; triangles[t + 2] = v + 1;
            triangles[t + 3] = v + 1; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
        }
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private void PrepareNonInteractive(GameObject root)
    {
        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            if (colliders[i] != null) colliders[i].enabled = false;
        Rigidbody[] bodiesInScene = root.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < bodiesInScene.Length; i++)
        {
            if (bodiesInScene[i] == null) continue;
            bodiesInScene[i].isKinematic = true;
            bodiesInScene[i].useGravity = false;
            bodiesInScene[i].detectCollisions = false;
        }
        VoidlineAsteroid[] asteroids = root.GetComponentsInChildren<VoidlineAsteroid>(true);
        for (int i = 0; i < asteroids.Length; i++) if (asteroids[i] != null) asteroids[i].enabled = false;
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

    private float Range(float min, float max)
    {
        if (random == null) random = new System.Random(260412);
        return min + (float)random.NextDouble() * (max - min);
    }

    private void ClearBodies()
    {
        for (int i = bodies.Count - 1; i >= 0; i--)
            if (bodies[i].root != null) Destroy(bodies[i].root);
        bodies.Clear();
        ReleaseGeneratedAssets();
    }

    private void ReleaseGeneratedAssets()
    {
        for (int i = 0; i < ownedMaterials.Count; i++)
            if (ownedMaterials[i] != null) Destroy(ownedMaterials[i]);
        ownedMaterials.Clear();
        for (int i = 0; i < ownedTextures.Count; i++)
            if (ownedTextures[i] != null) Destroy(ownedTextures[i]);
        ownedTextures.Clear();
        for (int i = 0; i < ownedMeshes.Count; i++)
            if (ownedMeshes[i] != null) Destroy(ownedMeshes[i]);
        ownedMeshes.Clear();
    }

    private void OnDestroy()
    {
        ClearBodies();
        initialized = false;
    }
}
