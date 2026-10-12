#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
internal static class VOIDLINEMoonRockAssetBuilder
{
    private const string FbxPath = "Assets/VOIDLINE/Art/Models/VOIDLINE_MoonRock_LOD.fbx";
    private const string BasePath = "Assets/VOIDLINE/Art/Textures/MoonRock/T_MoonRock_BaseColor_2K.jpg";
    private const string NormalPath = "Assets/VOIDLINE/Art/Textures/MoonRock/T_MoonRock_NormalGL_1K.png";
    private const string MaskPath = "Assets/VOIDLINE/Art/Textures/MoonRock/T_MoonRock_MetallicSmoothness_1K.png";
    private const string MaterialPath = "Assets/VOIDLINE/Materials/M_MoonRock_PBR.mat";
    private const string PrefabPath = "Assets/VOIDLINE/Prefabs/Obstacles/PF_Asteroid_MoonRock.prefab";

    static VOIDLINEMoonRockAssetBuilder() { EditorApplication.delayCall += TryBuildIfMissing; }

    [MenuItem("VOIDLINE/Assets/Rebuild Moon Rock Asteroid Prefab")]
    private static void RebuildFromMenu() => Build();

    private static void TryBuildIfMissing()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var manager = GameObject.Find("GameManager")?.GetComponent<VoidlineGameManager>();
        if (prefab != null && manager != null && ManagerContains(manager, prefab) && EditorPrefs.GetInt("VOIDLINE_MoonRockBuilderVersion", 0) >= 2) return;
        Build();
    }

    private static bool ManagerContains(VoidlineGameManager manager, GameObject prefab)
    {
        var serialized = new SerializedObject(manager);
        var array = serialized.FindProperty("asteroidPrefabs");
        if (array == null || !array.isArray) return false;
        for (int i = 0; i < array.arraySize; i++)
            if (array.GetArrayElementAtIndex(i).objectReferenceValue == prefab) return true;
        return false;
    }

    private static void ConfigureTexture(string path, Action<TextureImporter> configure)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Missing texture importer: " + path);
        configure(importer);
        importer.SaveAndReimport();
    }

    private static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[VOIDLINE] Moon Rock builder skipped during Play Mode.");
            return;
        }
        GameObject temporaryRoot = null;
        try
        {
            AssetDatabase.ImportAsset(FbxPath, ImportAssetOptions.ForceSynchronousImport);
            ConfigureTexture(BasePath, importer =>
            {
                importer.sRGBTexture = true; importer.maxTextureSize = 2048;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.compressionQuality = 85; importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Repeat; importer.filterMode = FilterMode.Trilinear; importer.anisoLevel = 4;
            });
            ConfigureTexture(NormalPath, importer =>
            {
                importer.textureType = TextureImporterType.NormalMap; importer.sRGBTexture = false;
                importer.maxTextureSize = 1024; importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.compressionQuality = 90; importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Repeat; importer.filterMode = FilterMode.Trilinear; importer.anisoLevel = 4;
                importer.flipGreenChannel = false; // Source map explicitly uses OpenGL convention.
            });
            ConfigureTexture(MaskPath, importer =>
            {
                importer.sRGBTexture = false; importer.maxTextureSize = 1024;
                importer.textureCompression = TextureImporterCompression.Compressed; importer.compressionQuality = 85;
                importer.mipmapEnabled = true; importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = false; importer.wrapMode = TextureWrapMode.Repeat; importer.filterMode = FilterMode.Trilinear;
            });

            var modelImporter = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
            if (modelImporter != null)
            {
                modelImporter.meshCompression = ModelImporterMeshCompression.Medium;
                modelImporter.isReadable = false; modelImporter.importAnimation = false;
                modelImporter.importCameras = false; modelImporter.importLights = false; modelImporter.addCollider = false;
                modelImporter.optimizeMeshPolygons = true; modelImporter.optimizeMeshVertices = true;
                modelImporter.SaveAndReimport();
            }

            var baseTex = AssetDatabase.LoadAssetAtPath<Texture2D>(BasePath);
            var normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath);
            var maskTex = AssetDatabase.LoadAssetAtPath<Texture2D>(MaskPath);
            if (baseTex == null || normalTex == null || maskTex == null)
                throw new InvalidOperationException("One or more optimized Moon Rock textures failed to import.");

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP/Lit shader not found.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "M_MoonRock_PBR" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else material.shader = shader;
            material.SetTexture("_BaseMap", baseTex); material.SetTexture("_BumpMap", normalTex);
            material.SetTexture("_MetallicGlossMap", maskTex); material.SetFloat("_Metallic", 0.08f);
            material.SetFloat("_Smoothness", 1f); material.SetFloat("_BumpScale", 0.82f);
            material.EnableKeyword("_NORMALMAP"); material.EnableKeyword("_METALLICGLOSSMAP");
            EditorUtility.SetDirty(material); AssetDatabase.SaveAssets();

            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (modelAsset == null) throw new InvalidOperationException("Moon Rock FBX failed to import.");
            temporaryRoot = new GameObject("PF_Asteroid_MoonRock");
            var body = temporaryRoot.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
            var collider = temporaryRoot.AddComponent<SphereCollider>(); collider.isTrigger = true; collider.radius = 0.5f;
            temporaryRoot.AddComponent<VoidlineAsteroid>();

            var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            model.name = "Moon Rock LOD Meshes"; model.transform.SetParent(temporaryRoot.transform, false);
            model.transform.localPosition = Vector3.zero; model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;

            var importedLodGroup = model.GetComponent<LODGroup>();
            if (importedLodGroup != null) UnityEngine.Object.DestroyImmediate(importedLodGroup);

            var lodRenderers = new Renderer[4];
            for (int i = 0; i < 4; i++)
            {
                var renderer = model.GetComponentsInChildren<MeshRenderer>(true)
                    .FirstOrDefault(r => r.gameObject.name == "MoonRock_LOD" + i);
                if (renderer == null) throw new InvalidOperationException("Missing MoonRock_LOD" + i + " renderer.");
                renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = true; renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off; lodRenderers[i] = renderer;
            }
            var group = temporaryRoot.AddComponent<LODGroup>();
            group.fadeMode = LODFadeMode.CrossFade; group.animateCrossFading = true; group.localReferencePoint = Vector3.zero;
            group.SetLODs(new[]
            {
                new LOD(0.22f, new[] { lodRenderers[0] }),
                new LOD(0.11f, new[] { lodRenderers[1] }),
                new LOD(0.045f, new[] { lodRenderers[2] }),
                new LOD(0.012f, new[] { lodRenderers[3] })
            });
            group.RecalculateBounds();
            PrefabUtility.SaveAsPrefabAsset(temporaryRoot, PrefabPath);
            UnityEngine.Object.DestroyImmediate(temporaryRoot); temporaryRoot = null;

            var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var manager = GameObject.Find("GameManager")?.GetComponent<VoidlineGameManager>();
            if (manager == null) throw new InvalidOperationException("GameManager not found in active scene.");
            var serialized = new SerializedObject(manager);
            var array = serialized.FindProperty("asteroidPrefabs");
            if (array == null || !array.isArray) throw new InvalidOperationException("asteroidPrefabs property not found.");
            bool already = false;
            for (int i = 0; i < array.arraySize; i++)
                if (array.GetArrayElementAtIndex(i).objectReferenceValue == prefabAsset) already = true;
            if (!already)
            {
                array.InsertArrayElementAtIndex(array.arraySize);
                array.GetArrayElementAtIndex(array.arraySize - 1).objectReferenceValue = prefabAsset;
            }
            serialized.ApplyModifiedProperties(); EditorUtility.SetDirty(manager);
            var scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            EditorPrefs.SetInt("VOIDLINE_MoonRockBuilderVersion", 2);
            Debug.Log("[VOIDLINE] Moon Rock asteroid ready: 4 LODs (5598/2798/1398/698 tris), shared URP material, compressed/mipmapped maps, pooled trigger prefab. Added to asteroid pool variants. Scene: " + scene.path);
        }
        catch (Exception ex)
        {
            if (temporaryRoot != null) UnityEngine.Object.DestroyImmediate(temporaryRoot);
            Debug.LogError("[VOIDLINE] Moon Rock asset build failed: " + ex);
        }
    }
}
#endif