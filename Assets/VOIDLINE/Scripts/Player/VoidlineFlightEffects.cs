using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// VOIDLINE's custom twin-engine propulsion. The sculpted plume is authored in Blender and
/// imported as a reusable mesh asset; only tiny boost motes use particles. There are no broad
/// smoke cones or long soft billboard streaks. Mesh brightness, length, and filament motion scale
/// smoothly with the actual gameplay boost value.
/// </summary>
[DisallowMultipleComponent]
public sealed class VoidlineFlightEffects : MonoBehaviour
{
    private const string PlumeResource = "VOIDLINE_Propulsion_Module";
    private const float PlumeBaseScale = 0.86f;

    private VoidlineGameManager gameManager;
    private Transform visualRoot;
    private Transform leftAnchor;
    private Transform rightAnchor;
    private Transform leftPlume;
    private Transform rightPlume;
    private ParticleSystem leftSparks;
    private ParticleSystem rightSparks;
    private Light leftNozzleGlow;
    private Light rightNozzleGlow;
    private readonly List<Transform> leftAnimatedParts = new List<Transform>(16);
    private readonly List<Transform> rightAnimatedParts = new List<Transform>(16);
    private readonly List<float> leftSpinRates = new List<float>(16);
    private readonly List<float> rightSpinRates = new List<float>(16);
    private readonly List<DeformablePlumePart> leftDeformableSheets = new List<DeformablePlumePart>(4);
    private readonly List<DeformablePlumePart> rightDeformableSheets = new List<DeformablePlumePart>(4);
    private readonly List<Renderer> leftPlumeRenderers = new List<Renderer>(16);
    private readonly List<Renderer> rightPlumeRenderers = new List<Renderer>(16);
    private Dictionary<Renderer, Color> basePlumeColors = new Dictionary<Renderer, Color>();
    private MaterialPropertyBlock plumePropertyBlock;
    private Dictionary<string, Material> plumeMaterials = new Dictionary<string, Material>();
    private Material basePlasmaMaterial;

    private sealed class DeformablePlumePart
    {
        public Mesh mesh;
        public Vector3[] baseVertices;
        public Vector3[] animatedVertices;
        public int axis;
        public int sideA;
        public int sideB;
        public float axialMin;
        public float axialLength;
        public float sideACenter;
        public float sideBCenter;
        public float amplitude;
        public float phase;
        public Transform sheetTransform;
        public Vector3 importedScale;
    }
    private Material sparkMaterial;
    private Texture2D sparkTexture;
    private Vector3 leftRestScale;
    private Vector3 rightRestScale;
    private int longitudinalAxis = 2;
    private float boostBlend;
    private float phase;
    private float finalDischargeElapsed = -1f;

    public void Configure(VoidlinePlayerController controller, VoidlineGameManager manager)
    {
        gameManager = manager;
        EnsurePropulsion();
    }

    private void Awake()
    {
        // Native Unity objects must not be constructed in a MonoBehaviour field initializer.
        // This project disables domain reload on Play, so also recover any null runtime caches.
        if (plumePropertyBlock == null) plumePropertyBlock = new MaterialPropertyBlock();
        if (basePlumeColors == null) basePlumeColors = new Dictionary<Renderer, Color>();
        if (plumeMaterials == null) plumeMaterials = new Dictionary<string, Material>();
        visualRoot = transform.Find("Model");
        EnsurePropulsion();
    }

    private void LateUpdate()
    {
        if (gameManager != null)
            SetBoostBlend(gameManager.BoostBlend);

        phase += Time.deltaTime * Mathf.Lerp(0.85f, 2.9f, boostBlend);
        AnimatePlume(leftPlume, leftRestScale, leftAnimatedParts, leftSpinRates, phase);
        AnimatePlume(rightPlume, rightRestScale, rightAnimatedParts, rightSpinRates, -phase * 0.93f);
        AnimatePlasmaSheets(leftDeformableSheets, phase, boostBlend);
        AnimatePlasmaSheets(rightDeformableSheets, -phase * 0.93f, boostBlend);
        AnimatePlumeLuminance(leftPlumeRenderers, boostBlend, phase);
        AnimatePlumeLuminance(rightPlumeRenderers, boostBlend, -phase * 0.93f);

        float pulse = 0.94f + 0.06f * Mathf.Sin(Time.unscaledTime * Mathf.Lerp(7.5f, 19f, boostBlend));
        float glow = Mathf.Lerp(0.12f, 2.2f, boostBlend) * pulse;
        if (finalDischargeElapsed >= 0f)
        {
            finalDischargeElapsed += Time.unscaledDeltaTime;
            float discharge = Mathf.Exp(-finalDischargeElapsed * 5.2f);
            glow = Mathf.Max(glow, 3.4f * discharge);
            if (finalDischargeElapsed >= 0.72f) finalDischargeElapsed = -1f;
        }
        if (leftNozzleGlow != null) leftNozzleGlow.intensity = glow;
        if (rightNozzleGlow != null) rightNozzleGlow.intensity = glow;
    }

    public void SetBoostBlend(float value)
    {
        boostBlend = Mathf.Clamp01(value);
        UpdateSparks(leftSparks);
        UpdateSparks(rightSparks);
    }

    /// <summary>
    /// Short terminal energy dump: a bright but brief nozzle flare and a controlled burst of
    /// fine ion fragments. It is event-driven and never turns the normal plume into smoke.
    /// </summary>
    public void TriggerFinalDischarge()
    {
        finalDischargeElapsed = 0f;
        if (leftSparks != null) leftSparks.Emit(24);
        if (rightSparks != null) rightSparks.Emit(24);
        if (leftNozzleGlow != null) leftNozzleGlow.intensity = 3.4f;
        if (rightNozzleGlow != null) rightNozzleGlow.intensity = 3.4f;
    }

    private void EnsurePropulsion()
    {
        // Configure() can run during another component's Awake. Keep the lazy path equally safe.
        if (plumePropertyBlock == null) plumePropertyBlock = new MaterialPropertyBlock();
        if (basePlumeColors == null) basePlumeColors = new Dictionary<Renderer, Color>();
        if (plumeMaterials == null) plumeMaterials = new Dictionary<string, Material>();
        if (visualRoot == null) visualRoot = transform.Find("Model");
        if (visualRoot == null) return;

        // Rebuild non-serialized caches after Unity hot reloads while reusing existing objects.
        if (leftPlume != null && rightPlume != null)
        {
            if (leftAnchor == null) leftAnchor = FindDescendant(visualRoot, "ExhaustSocket_L");
            if (rightAnchor == null) rightAnchor = FindDescendant(visualRoot, "ExhaustSocket_R");
            Transform leftModel = GetPlumeModelRoot(leftPlume);
            Transform rightModel = GetPlumeModelRoot(rightPlume);
            if (leftModel != null)
            {
                leftModel.localRotation = Quaternion.identity;
                leftModel.localScale = Vector3.one * PlumeBaseScale;
                AssignPlumeMaterials(leftModel);
                CacheDeformableSheets(leftModel, leftDeformableSheets);
            }
            if (rightModel != null)
            {
                rightModel.localRotation = Quaternion.identity;
                rightModel.localScale = Vector3.one * PlumeBaseScale;
                AssignPlumeMaterials(rightModel);
                CacheDeformableSheets(rightModel, rightDeformableSheets);
            }
            CacheAnimatedParts(leftPlume, leftAnimatedParts, leftSpinRates);
            CacheAnimatedParts(rightPlume, rightAnimatedParts, rightSpinRates);
            CachePlumeRenderers(leftPlume, leftPlumeRenderers);
            CachePlumeRenderers(rightPlume, rightPlumeRenderers);
            leftRestScale = leftPlume.localScale;
            rightRestScale = rightPlume.localScale;
            if (leftAnchor != null)
            {
                leftSparks = CreateBoostSparks(leftAnchor, 821);
                leftNozzleGlow = CreateNozzleGlow(leftAnchor);
            }
            if (rightAnchor != null)
            {
                rightSparks = CreateBoostSparks(rightAnchor, 971);
                rightNozzleGlow = CreateNozzleGlow(rightAnchor);
            }
            SetBoostBlend(boostBlend);
            return;
        }

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
        leftAnchor = GetAnchor("ExhaustSocket_L", -engineX, engineY, rearZ);
        rightAnchor = GetAnchor("ExhaustSocket_R", engineX, engineY, rearZ);

        GameObject prefab = Resources.Load<GameObject>(PlumeResource);
        if (prefab == null)
        {
            Debug.LogError("[VOIDLINE] Sculpted propulsion FBX is missing from Resources/" + PlumeResource +
                ". The authored mesh effect cannot be attached.");
            return;
        }

        leftPlume = CreatePlumeInstance(prefab, leftAnchor, "VOIDLINE Sculpted Exhaust L",
            leftAnimatedParts, leftSpinRates, leftDeformableSheets);
        rightPlume = CreatePlumeInstance(prefab, rightAnchor, "VOIDLINE Sculpted Exhaust R",
            rightAnimatedParts, rightSpinRates, rightDeformableSheets);
        CachePlumeRenderers(leftPlume, leftPlumeRenderers);
        CachePlumeRenderers(rightPlume, rightPlumeRenderers);
        leftRestScale = leftPlume != null ? leftPlume.localScale : Vector3.one;
        rightRestScale = rightPlume != null ? rightPlume.localScale : Vector3.one;
        leftSparks = CreateBoostSparks(leftAnchor, 821);
        rightSparks = CreateBoostSparks(rightAnchor, 971);
        leftNozzleGlow = CreateNozzleGlow(leftAnchor);
        rightNozzleGlow = CreateNozzleGlow(rightAnchor);
        SetBoostBlend(boostBlend);
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
        anchor.localRotation = Quaternion.identity;
        return anchor;
    }

    private Transform CreatePlumeInstance(GameObject prefab, Transform anchor, string rootName,
        List<Transform> animatedParts, List<float> spinRates, List<DeformablePlumePart> deformableSheets)
    {
        Transform alreadyThere = anchor.Find(rootName);
        if (alreadyThere != null)
        {
            Transform existingModel = GetPlumeModelRoot(alreadyThere);
            if (existingModel != null)
            {
                // FBX axis conversion is already applied to each imported mesh child (local X rotation 270°).
                // Keep the prefab root neutral; rotating here would undo that conversion and aim the plume sideways.
                existingModel.localRotation = Quaternion.identity;
                existingModel.localScale = Vector3.one * PlumeBaseScale;
                AssignPlumeMaterials(existingModel);
                CacheDeformableSheets(existingModel, deformableSheets);
            }
            CacheAnimatedParts(alreadyThere, animatedParts, spinRates);
            return alreadyThere;
        }

        GameObject wrapper = new GameObject(rootName);
        Transform root = wrapper.transform;
        root.SetParent(anchor, false);
        root.localPosition = Vector3.zero;
        root.localRotation = Quaternion.identity;

        GameObject model = Instantiate(prefab, root, false);
        model.name = "Blender Plume Mesh";
        model.transform.localPosition = Vector3.zero;
        // FBX importer has already converted the authored Blender Y axis to the ship's aft -Z axis
        // on each imported child mesh. Keep this parent neutral to avoid a second axis rotation.
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one * PlumeBaseScale;

        AssignPlumeMaterials(model.transform);
        CacheAnimatedParts(root, animatedParts, spinRates);
        CacheDeformableSheets(model.transform, deformableSheets);
        return root;
    }

    private static Transform GetPlumeModelRoot(Transform plume)
    {
        if (plume == null) return null;

        Transform named = FindDescendant(plume, "Blender Plume Mesh");
        if (named != null && named != plume) return named;

        // Older live instances may retain the FBX root's original imported name.
        for (int i = 0; i < plume.childCount; i++)
        {
            Transform child = plume.GetChild(i);
            if (child.GetComponentInChildren<MeshFilter>(true) != null)
                return child;
        }
        return plume.GetComponentInChildren<MeshFilter>(true)?.transform;
    }

    private void AssignPlumeMaterials(Transform root)
    {
        if (root == null) return;
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null) continue;
            Material target = GetPlumeMaterial(renderer.gameObject.name);
            if (target != null) renderer.sharedMaterial = target;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        }
    }

    private void CacheAnimatedParts(Transform root, List<Transform> animatedParts, List<float> spinRates)
    {
        animatedParts.Clear();
        spinRates.Clear();
        longitudinalAxis = 2;
        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
        bool hasBounds = false;
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null) continue;
            Bounds b = renderer.bounds;
            Vector3[] corners =
            {
                new Vector3(b.min.x,b.min.y,b.min.z), new Vector3(b.max.x,b.min.y,b.min.z),
                new Vector3(b.min.x,b.max.y,b.min.z), new Vector3(b.min.x,b.min.y,b.max.z),
                new Vector3(b.max.x,b.max.y,b.min.z), new Vector3(b.max.x,b.min.y,b.max.z),
                new Vector3(b.min.x,b.max.y,b.max.z), new Vector3(b.max.x,b.max.y,b.max.z)
            };
            for (int c = 0; c < corners.Length; c++)
            {
                Vector3 local = root.InverseTransformPoint(corners[c]);
                if (!hasBounds) { bounds = new Bounds(local, Vector3.zero); hasBounds = true; }
                else bounds.Encapsulate(local);
            }

            string n = renderer.gameObject.name;
            // Wide plasma sheets deform in their own mesh space; rotating the complete sheet
            // made it read as a rigid card. Only tiny induction arcs/filaments spin slightly.
            if (n.Contains("Arc") || n.Contains("FieldFilament"))
            {
                animatedParts.Add(renderer.transform);
                float rate = n.Contains("Violet") ? -0.24f : 0.19f;
                if (n.Contains("Arc")) rate *= 0.72f;
                if (n.Contains("FieldFilament")) rate *= 0.50f;
                spinRates.Add(rate);
            }
        }

        if (hasBounds)
        {
            Vector3 size = bounds.size;
            longitudinalAxis = size.x > size.y && size.x > size.z ? 0 : size.y > size.z ? 1 : 2;
        }
    }

    private void CachePlumeRenderers(Transform root, List<Renderer> cache)
    {
        cache.Clear();
        if (root == null) return;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null) continue;
            cache.Add(renderer);

            Material material = renderer.sharedMaterial;
            if (material == null) continue;
            Color baseColor = material.HasProperty("_BaseColor")
                ? material.GetColor("_BaseColor")
                : material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
            basePlumeColors[renderer] = baseColor;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        }
    }

    private static void CacheDeformableSheets(Transform root, List<DeformablePlumePart> cache)
    {
        cache.Clear();
        if (root == null) return;

        MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < filters.Length; i++)
        {
            MeshFilter filter = filters[i];
            if (filter == null || !filter.gameObject.name.Contains("Ribbon")) continue;

            // Read/Write must be enabled on the FBX importer before mesh.vertices can be
            // edited. Fail closed once during setup rather than logging the same error every frame.
            Mesh sharedMesh = filter.sharedMesh;
            if (sharedMesh == null || sharedMesh.vertexCount < 8) continue;
            if (!sharedMesh.isReadable)
            {
                Debug.LogWarning("[VOIDLINE] Propulsion sheet '" + filter.name +
                    "' is not readable. Enable Read/Write on VOIDLINE_Propulsion_Module.fbx; " +
                    "the sheet will render statically until the importer is fixed.");
                continue;
            }

            // The imported ribbon sheets were too narrow at the actual spacecraft scale.
            // Keep their exhaust-axis (local Y) length intact and widen both transverse axes.
            // Assignment is absolute so Configure()/hot-reload recovery never compounds scaling.
            // FBX mesh children use unit local scale. A runtime/hot-reload pass may see the
            // previous animated scale rather than the importer baseline, so explicitly reset
            // the transverse axes instead of multiplying the current value (which compounds).
            Vector3 importedScale = new Vector3(1f, filter.transform.localScale.y, 1f);
            filter.transform.localScale = new Vector3(importedScale.x * 0.90f,
                importedScale.y, importedScale.z * 0.90f);

            // Never promote a previously animated runtime copy into the next rest pose. A
            // domain/hot reload preserves MeshFilter.sharedMesh even when all private caches
            // are lost, so reacquire the authored FBX mesh by child name, then build a fresh
            // dynamic copy from that immutable source on every cache recovery.
            Mesh sourceMesh = sharedMesh;
            Mesh previousRuntimeMesh = sourceMesh.name.Contains(" / Runtime Wave")
                ? sourceMesh : null;
            if (previousRuntimeMesh != null)
            {
                GameObject authoredPrefab = Resources.Load<GameObject>(PlumeResource);
                Transform authoredPart = authoredPrefab != null
                    ? FindDescendant(authoredPrefab.transform, filter.gameObject.name) : null;
                MeshFilter authoredFilter = authoredPart != null
                    ? authoredPart.GetComponent<MeshFilter>() : null;
                if (authoredFilter == null || authoredFilter.sharedMesh == null)
                {
                    Debug.LogWarning("[VOIDLINE] Could not recover authored mesh for propulsion sheet '" +
                        filter.gameObject.name + "'. Skipping dynamic deformation this run.");
                    continue;
                }
                sourceMesh = authoredFilter.sharedMesh;
            }

            Mesh mesh = Instantiate(sourceMesh);
            mesh.name = filter.gameObject.name + " / Runtime Wave";
            filter.sharedMesh = mesh;
            if (previousRuntimeMesh != null && previousRuntimeMesh != mesh)
            {
                if (Application.isPlaying) Destroy(previousRuntimeMesh);
                else DestroyImmediate(previousRuntimeMesh);
            }
            if (!mesh.isReadable || mesh.vertexCount < 8) continue;
            mesh.MarkDynamic();

            Vector3 size = mesh.bounds.size;
            int axis = size.x >= size.y && size.x >= size.z ? 0 : size.y >= size.z ? 1 : 2;
            int sideA = (axis + 1) % 3;
            int sideB = (axis + 2) % 3;
            Vector3[] vertices = mesh.vertices;
            float min = float.PositiveInfinity;
            float max = float.NegativeInfinity;
            float centerA = 0f;
            float centerB = 0f;

            for (int v = 0; v < vertices.Length; v++)
            {
                float axial = vertices[v][axis];
                min = Mathf.Min(min, axial);
                max = Mathf.Max(max, axial);
                centerA += vertices[v][sideA];
                centerB += vertices[v][sideB];
            }

            centerA /= vertices.Length;
            centerB /= vertices.Length;
            int hash = 0;
            string partName = filter.gameObject.name;
            for (int c = 0; c < partName.Length; c++) hash = (hash * 31 + partName[c]) & 0x7fffffff;

            float amplitude = partName.Contains("Cyan") ? 0.078f :
                partName.Contains("Cobalt") ? 0.062f : 0.048f;

            cache.Add(new DeformablePlumePart
            {
                mesh = mesh,
                baseVertices = vertices,
                animatedVertices = new Vector3[vertices.Length],
                axis = axis,
                sideA = sideA,
                sideB = sideB,
                axialMin = min,
                axialLength = Mathf.Max(0.001f, max - min),
                sideACenter = centerA,
                sideBCenter = centerB,
                amplitude = amplitude,
                phase = (hash % 1000) * 0.00628318f,
                sheetTransform = filter.transform,
                importedScale = importedScale
            });
        }
    }

    private static void AnimatePlasmaSheets(List<DeformablePlumePart> sheets, float time, float boostBlendValue)
    {
        float boost = boostBlendValue * boostBlendValue * (3f - 2f * boostBlendValue);
        float energy = Mathf.Lerp(0.28f, 1f, boost);

        for (int s = 0; s < sheets.Count; s++)
        {
            DeformablePlumePart part = sheets[s];
            if (part == null || part.mesh == null || part.baseVertices == null) continue;

            Vector3[] source = part.baseVertices;
            Vector3[] target = part.animatedVertices;
            float amp = part.amplitude * energy;
            float timePhase = time * (2.35f + 1.95f * boost) + part.phase;
            if (part.sheetTransform != null)
            {
                // Cruise ribbons stay tightly braided to the core. Boost unfolds them into three distinct
                // energy membranes. Animate scale rather than switching meshes
                // so they never pop into a different silhouette.
                float width = Mathf.Lerp(0.36f, 1.22f, boost);
                part.sheetTransform.localScale = new Vector3(
                    part.importedScale.x * width,
                    part.importedScale.y,
                    part.importedScale.z * width);
            }

            for (int i = 0; i < source.Length; i++)
            {
                Vector3 vertex = source[i];
                float t = Mathf.Clamp01((vertex[part.axis] - part.axialMin) / part.axialLength);
                float envelope = Mathf.Pow(Mathf.Clamp01(Mathf.Sin(t * Mathf.PI)), 0.62f);

                // Two unequal travelling waves disturb both edges independently. The sheet
                // visibly flexes through its length rather than spinning like a rigid card.
                float waveA = Mathf.Sin(t * 8.2f - timePhase) * amp +
                    Mathf.Sin(t * 16.7f + timePhase * 1.31f) * amp * 0.19f;
                float waveB = Mathf.Sin(t * 6.4f + timePhase * 0.73f) * amp * 0.46f;
                vertex[part.sideA] += waveA * envelope;
                vertex[part.sideB] += waveB * envelope;

                // A slight breathing width is boost-controlled and returns smoothly on release.
                float widthBreath = Mathf.Lerp(0.025f, 0.155f, boost);
                float widthPulse = 1f + Mathf.Sin(t * 7.1f - timePhase * 0.68f) *
                    widthBreath * envelope;
                vertex[part.sideB] = part.sideBCenter +
                    (vertex[part.sideB] - part.sideBCenter) * widthPulse;

                target[i] = vertex;
            }

            part.mesh.vertices = target;
            part.mesh.RecalculateNormals();
            part.mesh.RecalculateBounds();
        }
    }

    private void AnimatePlumeLuminance(List<Renderer> renderers, float boostBlendValue, float time)
    {
        float boost = boostBlendValue * boostBlendValue * (3f - 2f * boostBlendValue);
        float travellingPulse = 0.91f + 0.09f * Mathf.Sin(time * Mathf.Lerp(5.0f, 12.5f, boost));
        float gain = Mathf.Lerp(0.78f, 1.05f, boost) * travellingPulse;
        float alpha = Mathf.Lerp(0.58f, 0.68f, boost);
        // Keep the hot spine luminous without letting stacked additive layers erase the ship silhouette.
        // The boost reads from the expanding shell and faster flow, not a white exposure flash.
        float glow = Mathf.Lerp(0.62f, 0.78f, boost);

        for (int i = 0; i < renderers.Count; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !basePlumeColors.TryGetValue(renderer, out Color baseColor)) continue;

            plumePropertyBlock.Clear();
            Color driven = new Color(baseColor.r * gain, baseColor.g * gain,
                baseColor.b * gain, Mathf.Clamp01(baseColor.a * alpha));
            plumePropertyBlock.SetColor("_BaseColor", driven);
            plumePropertyBlock.SetColor("_Color", driven);
            plumePropertyBlock.SetFloat("_Boost", boost);
            plumePropertyBlock.SetFloat("_Glow", glow * travellingPulse);
            plumePropertyBlock.SetFloat("_ScrollSpeed", Mathf.Lerp(1.25f, 3.8f, boost));
            MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
            Bounds localBounds = meshFilter != null && meshFilter.sharedMesh != null
                ? meshFilter.sharedMesh.bounds : new Bounds(Vector3.zero, Vector3.one);
            plumePropertyBlock.SetFloat("_FlowMin", localBounds.min.y);
            plumePropertyBlock.SetFloat("_FlowInvLength", 1f / Mathf.Max(0.01f, localBounds.size.y));
            plumePropertyBlock.SetFloat("_FlowWidth", Mathf.Max(localBounds.size.x, localBounds.size.z));
            plumePropertyBlock.SetFloat("_IsSheet", renderer.gameObject.name.Contains("Ribbon") ? 1f : 0f);
            renderer.SetPropertyBlock(plumePropertyBlock);
        }
    }

    private void AnimatePlume(Transform plume, Vector3 restScale, List<Transform> parts,
        List<float> spinRates, float time)
    {
        if (plume == null) return;
        float boost = boostBlend * boostBlend * (3f - 2f * boostBlend);
        float breathing = 0.98f + 0.02f * Mathf.Sin(time * 2.7f);
        // A subdued idle plume blooms into a broad, fast exhaust on boost rather than a short teardrop.
        // Cruise exhaust stays tucked close to the nozzle; boost expands it with a distinct ignition bloom.
        float width = breathing * Mathf.Lerp(0.62f, 1.50f, boost);
        float length = Mathf.Lerp(1.02f, 1.92f, boost) * (0.965f + 0.035f * Mathf.Sin(time * 4.2f));
        Vector3 target = restScale;
        if (longitudinalAxis == 0) target = new Vector3(restScale.x * length, restScale.y * width, restScale.z * width);
        else if (longitudinalAxis == 1) target = new Vector3(restScale.x * width, restScale.y * length, restScale.z * width);
        else target = new Vector3(restScale.x * width, restScale.y * width, restScale.z * length);
        plume.localScale = Vector3.Lerp(plume.localScale, target, 1f - Mathf.Exp(-Time.deltaTime * 7f));

        Vector3 axis = longitudinalAxis == 0 ? Vector3.right :
            longitudinalAxis == 1 ? Vector3.up : Vector3.forward;
        for (int i = 0; i < parts.Count; i++)
        {
            Transform part = parts[i];
            if (part != null)
                part.Rotate(axis, spinRates[i] * (0.7f + boost * 1.7f), Space.Self);
        }
    }

    private Material GetPlumeMaterial(string objectName)
    {
        string key;
        Color color;
        if (objectName.Contains("Envelope"))
        {
            // A low-energy additive shell leaves the white core and background visible through it.
            key = "Envelope";
            color = new Color(0.018f, 0.13f, 0.82f, 0.36f);
        }
        else if (objectName.Contains("WhiteHot") || objectName.Contains("Arc_White"))
        {
            key = "White";
            color = new Color(0.70f, 0.96f, 1.25f, 0.96f);
        }
        else if (objectName.Contains("Violet"))
        {
            key = "Violet";
            color = new Color(0.42f, 0.035f, 1.25f, 0.63f);
        }
        else if (objectName.Contains("Azure") || objectName.Contains("Cobalt") || objectName.Contains("Blue"))
        {
            key = "Azure";
            color = new Color(0.018f, 0.12f, 1.25f, 0.56f);
        }
        else if (objectName.Contains("Cyan_Confinement"))
        {
            key = "CoreCyan";
            color = new Color(0.035f, 0.70f, 1.45f, 0.78f);
        }
        else if (objectName.Contains("FieldArc"))
        {
            key = "ArcCyan";
            color = new Color(0.08f, 0.52f, 1.35f, 0.82f);
        }
        else if (objectName.Contains("IonFilament"))
        {
            key = "Filament";
            color = new Color(0.04f, 0.48f, 1.1f, 0.74f);
        }
        else
        {
            key = "Cyan";
            color = new Color(0.02f, 0.58f, 1.35f, 0.62f);
        }

        if (basePlasmaMaterial == null)
            basePlasmaMaterial = Resources.Load<Material>("M_VOIDLINE_PlasmaFlow");
        Shader shader = basePlasmaMaterial != null ? basePlasmaMaterial.shader : Shader.Find("VOIDLINE/PlasmaFlow");

        if (plumeMaterials.TryGetValue(key, out Material cached) && cached != null)
        {
            if (shader == null || cached.shader == shader) return cached;
            // A prior hot reload can leave old URP particle materials cached on the live ship.
            Destroy(cached);
            plumeMaterials.Remove(key);
        }

        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Transparent");
        if (shader == null) return null;

        Material material = basePlasmaMaterial != null
            ? new Material(basePlasmaMaterial) : new Material(shader);
        material.name = "VOIDLINE_Plasma_" + key;
        material.renderQueue = (int)RenderQueue.Transparent;
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 2f); // additive
        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.One);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)CullMode.Off);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_BLENDMODE_ADD");
        material.DisableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.enableInstancing = true;
        plumeMaterials[key] = material;
        return material;
    }

    private ParticleSystem CreateBoostSparks(Transform anchor, uint seed)
    {
        Transform existing = anchor.Find("Ion Flecks / Boost Only");
        if (existing != null && existing.TryGetComponent(out ParticleSystem existingParticles))
        {
            if (!existingParticles.isPlaying) existingParticles.Play(true);
            return existingParticles;
        }

        GameObject go = new GameObject("Ion Flecks / Boost Only");
        go.transform.SetParent(anchor, false);
        go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ps.useAutoRandomSeed = false;
        ps.randomSeed = seed;
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.07f, 0.15f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(8f, 20f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.009f, 0.023f);
        main.startColor = new Color(0.16f, 0.83f, 1f, 1f);
        main.gravityModifier = 0f;
        main.maxParticles = 48;
        main.cullingMode = ParticleSystemCullingMode.Automatic;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 20f;
        shape.radius = 0.07f;
        shape.radiusThickness = 0.3f;
        var color = ps.colorOverLifetime;
        color.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] {
                new GradientColorKey(new Color(0.7f,1f,1f),0f),
                new GradientColorKey(new Color(0.05f,0.65f,1f),0.5f),
                new GradientColorKey(new Color(0.45f,0.12f,1f),1f)
            },
            new[] {
                new GradientAlphaKey(1f,0f),
                new GradientAlphaKey(0.9f,0.55f),
                new GradientAlphaKey(0f,1f)
            });
        color.color = new ParticleSystem.MinMaxGradient(gradient);
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = GetSparkMaterial();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        ps.Play(true);
        return ps;
    }

    private Material GetSparkMaterial()
    {
        if (sparkMaterial != null) return sparkMaterial;
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) return null;
        sparkTexture = new Texture2D(32, 32, TextureFormat.RGBA32, false, true)
        {
            name = "VOIDLINE_Ion_Fleck_Falloff",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        Color[] pixels = new Color[32 * 32];
        for (int y = 0; y < 32; y++)
        for (int x = 0; x < 32; x++)
        {
            float dx = (x + 0.5f) / 16f - 1f;
            float dy = (y + 0.5f) / 16f - 1f;
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            float a = Mathf.Pow(Mathf.Clamp01(1f - r), 2.5f);
            pixels[y * 32 + x] = new Color(1f, 1f, 1f, a);
        }
        sparkTexture.SetPixels(pixels);
        sparkTexture.Apply(false, true);
        sparkMaterial = new Material(shader) { name = "VOIDLINE_IonFleck_Additive", renderQueue = 3000 };
        if (sparkMaterial.HasProperty("_BaseMap")) sparkMaterial.SetTexture("_BaseMap", sparkTexture);
        if (sparkMaterial.HasProperty("_MainTex")) sparkMaterial.SetTexture("_MainTex", sparkTexture);
        if (sparkMaterial.HasProperty("_BaseColor")) sparkMaterial.SetColor("_BaseColor", new Color(0.1f, 0.7f, 1.8f, 1f));
        if (sparkMaterial.HasProperty("_Color")) sparkMaterial.SetColor("_Color", new Color(0.1f, 0.7f, 1.8f, 1f));
        if (sparkMaterial.HasProperty("_Surface")) sparkMaterial.SetFloat("_Surface", 1f);
        if (sparkMaterial.HasProperty("_Blend")) sparkMaterial.SetFloat("_Blend", 2f);
        if (sparkMaterial.HasProperty("_ZWrite")) sparkMaterial.SetFloat("_ZWrite", 0f);
        if (sparkMaterial.HasProperty("_SrcBlend")) sparkMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (sparkMaterial.HasProperty("_DstBlend")) sparkMaterial.SetFloat("_DstBlend", (float)BlendMode.One);
        sparkMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        sparkMaterial.DisableKeyword("_ALPHABLEND_ON");
        return sparkMaterial;
    }

    private Light CreateNozzleGlow(Transform anchor)
    {
        Transform existing = anchor.Find("Nozzle Glow / Local Spill");
        if (existing != null && existing.TryGetComponent(out Light existingLight))
            return existingLight;

        GameObject go = new GameObject("Nozzle Glow / Local Spill");
        go.transform.SetParent(anchor, false);
        go.transform.localPosition = new Vector3(0f, 0f, -0.1f);
        Light light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(0.12f, 0.62f, 1f);
        light.intensity = 0.12f;
        light.range = 2.1f;
        light.shadows = LightShadows.None;
        light.renderMode = LightRenderMode.Auto;
        return light;
    }

    private void UpdateSparks(ParticleSystem ps)
    {
        if (ps == null) return;
        float b = boostBlend * boostBlend * (3f - 2f * boostBlend);
        var emission = ps.emission;
        emission.rateOverTime = Mathf.Lerp(0f, 34f, b);
        var main = ps.main;
        main.startSpeed = new ParticleSystem.MinMaxCurve(Mathf.Lerp(7f, 12f, b), Mathf.Lerp(14f, 25f, b));
        main.startSize = new ParticleSystem.MinMaxCurve(0.008f, Mathf.Lerp(0.018f, 0.027f, b));
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

    private void OnDisable()
    {
        ClearSparks(leftSparks);
        ClearSparks(rightSparks);
        if (leftNozzleGlow != null) leftNozzleGlow.intensity = 0f;
        if (rightNozzleGlow != null) rightNozzleGlow.intensity = 0f;
    }

    private static void ClearSparks(ParticleSystem ps)
    {
        if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void OnEnable()
    {
        if (leftSparks != null) leftSparks.Play(true);
        if (rightSparks != null) rightSparks.Play(true);
    }

    private void OnDestroy()
    {
        ReleaseDeformableSheets(leftDeformableSheets);
        ReleaseDeformableSheets(rightDeformableSheets);
        foreach (KeyValuePair<string, Material> item in plumeMaterials)
            if (item.Value != null) Destroy(item.Value);
        if (sparkMaterial != null) Destroy(sparkMaterial);
        if (sparkTexture != null) Destroy(sparkTexture);
        basePlumeColors.Clear();
    }

    private static void ReleaseDeformableSheets(List<DeformablePlumePart> sheets)
    {
        for (int i = 0; i < sheets.Count; i++)
        {
            Mesh mesh = sheets[i] != null ? sheets[i].mesh : null;
            if (mesh != null) Destroy(mesh);
        }
        sheets.Clear();
    }
}