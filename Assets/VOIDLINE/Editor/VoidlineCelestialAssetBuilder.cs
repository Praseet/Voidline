#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Imports official free space panoramas from Space Spheremaps and scans any locally imported
/// celestial pack. Asset Store source files are referenced, never copied or redistributed.
/// </summary>
public static class VoidlineCelestialAssetBuilder
{
    private const string ResourceFolder = "Assets/VOIDLINE/Resources";
    private const string ConfigPath = ResourceFolder + "/VOIDLINE_CelestialAssetSet.asset";

    private static readonly string[] PanoramaUrls =
    {
        "https://www.spacespheremaps.com/wp-content/uploads/multi_nebulae_1.png",
        "https://www.spacespheremaps.com/wp-content/uploads/galactic_plane_1.png",
        "https://www.spacespheremaps.com/wp-content/uploads/blue_nebulae_1.png"
    };
    private static readonly string[] PanoramaFileNames =
    {
        "VOIDLINE_MultiNebula_01.png",
        "VOIDLINE_GalacticPlane_01.png",
        "VOIDLINE_BlueNebula_01.png"
    };
    private const string PanoramaFolder = "Assets/VOIDLINE/Art/External/SpaceSpheremaps";
    private static readonly List<Texture2D> downloadedPanoramas = new List<Texture2D>(3);
    private static int panoramaDownloadIndex;
    private static int panoramaDownloadErrors;
    private static bool isDownloadingPanoramas;

    private const string BlendMaterialPath = ResourceFolder + "/M_VOIDLINE_CelestialBlend.mat";

    private static void EnsureBlendSkyboxMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(BlendMaterialPath);
        if (material == null)
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(
                "Assets/VOIDLINE/Shaders/VOIDLINE_CelestialBlendSkybox.shader");
            if (shader == null) shader = Shader.Find("Skybox/VOIDLINE Celestial Blend");
            if (shader == null)
            {
                Debug.LogWarning("[VOIDLINE] Celestial blend shader has not imported yet.");
                return;
            }
            material = new Material(shader) { name = "M_VOIDLINE_CelestialBlend" };
            AssetDatabase.CreateAsset(material, BlendMaterialPath);
        }
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
    }

    [MenuItem("VOIDLINE/Environment/Download Multicolour Space Panoramas (33 MB)")]
    public static void DownloadSpacePanoramas()
    {
        if (isDownloadingPanoramas)
        {
            Debug.LogWarning("[VOIDLINE] A panorama download is already in progress.");
            return;
        }

        isDownloadingPanoramas = true;
        downloadedPanoramas.Clear();
        panoramaDownloadIndex = 0;
        panoramaDownloadErrors = 0;
        Directory.CreateDirectory(PanoramaFolder);
        EnsureBlendSkyboxMaterial();
        EditorUtility.DisplayProgressBar("VOIDLINE Celestial Environment",
            "Preparing official multicolour nebula maps…", 0f);
        DownloadNextPanorama();
    }

    private static void DownloadNextPanorama()
    {
        if (panoramaDownloadIndex >= PanoramaUrls.Length)
        {
            EditorUtility.ClearProgressBar();
            SaveDownloadedPanoramas();
            return;
        }

        int index = panoramaDownloadIndex;
        string path = PanoramaFolder + "/" + PanoramaFileNames[index];
        EditorUtility.DisplayProgressBar("VOIDLINE Celestial Environment",
            "Importing nebula panorama " + (index + 1) + " / " + PanoramaUrls.Length,
            index / (float)PanoramaUrls.Length);

        if (File.Exists(path) && new FileInfo(path).Length > 1024 * 1024)
        {
            ImportPanorama(path);
            panoramaDownloadIndex++;
            DownloadNextPanorama();
            return;
        }

        UnityWebRequest request = UnityWebRequest.Get(PanoramaUrls[index]);
        request.timeout = 90;
        UnityWebRequestAsyncOperation operation = request.SendWebRequest();
        operation.completed += _ =>
        {
            try
            {
                if (request.result != UnityWebRequest.Result.Success ||
                    request.downloadHandler == null || request.downloadHandler.data == null ||
                    request.downloadHandler.data.Length < 1024 * 1024)
                {
                    panoramaDownloadErrors++;
                    Debug.LogWarning("[VOIDLINE] Could not download " + PanoramaFileNames[index] +
                        ": " + request.error + ". The old sky is not restored; use the configured fallback or retry.");
                }
                else
                {
                    File.WriteAllBytes(path, request.downloadHandler.data);
                    ImportPanorama(path);
                }
            }
            catch (Exception exception)
            {
                panoramaDownloadErrors++;
                Debug.LogException(exception);
            }
            finally
            {
                request.Dispose();
                panoramaDownloadIndex++;
                DownloadNextPanorama();
            }
        };
    }

    private static void ImportPanorama(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 4;
            importer.maxTextureSize = 8192;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.streamingMipmaps = true;
            importer.SaveAndReimport();
        }

        Texture2D panorama = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (panorama != null && !downloadedPanoramas.Contains(panorama))
            downloadedPanoramas.Add(panorama);
    }

    private static void SaveDownloadedPanoramas()
    {
        try
        {
            if (!Directory.Exists(ResourceFolder)) Directory.CreateDirectory(ResourceFolder);
            VoidlineCelestialAssetSet set =
                AssetDatabase.LoadAssetAtPath<VoidlineCelestialAssetSet>(ConfigPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<VoidlineCelestialAssetSet>();
                AssetDatabase.CreateAsset(set, ConfigPath);
            }

            SerializedObject serializedSet = new SerializedObject(set);
            SerializedProperty property = serializedSet.FindProperty("spacePanoramas");
            if (property != null)
            {
                property.arraySize = downloadedPanoramas.Count;
                for (int i = 0; i < downloadedPanoramas.Count; i++)
                    property.GetArrayElementAtIndex(i).objectReferenceValue = downloadedPanoramas[i];
                serializedSet.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(set);
                AssetDatabase.SaveAssets();
            }

            string message = "Imported " + downloadedPanoramas.Count + " multicolour Space Spheremaps panoramas.";
            if (panoramaDownloadErrors > 0)
                message += "\\n" + panoramaDownloadErrors + " download(s) failed; inspect the Console and retry.";
            message += "\\n\\nThe asset set now references the actual panorama textures. Restart Play Mode to see the new environment.";
            Debug.Log("[VOIDLINE] " + message.Replace("\\n", " "));
            EditorUtility.DisplayDialog("VOIDLINE Celestial Environment", message, "OK");
        }
        finally
        {
            downloadedPanoramas.Clear();
            isDownloadingPanoramas = false;
            EditorUtility.ClearProgressBar();
        }
    }

    [MenuItem("VOIDLINE/Environment/Build Celestial Asset Set")]
    public static void BuildCelestialAssetSet()
    {
        if (!Directory.Exists(ResourceFolder))
        {
            Directory.CreateDirectory(ResourceFolder);
            AssetDatabase.Refresh();
        }

        VoidlineCelestialAssetSet set = AssetDatabase.LoadAssetAtPath<VoidlineCelestialAssetSet>(ConfigPath);
        if (set == null)
        {
            set = ScriptableObject.CreateInstance<VoidlineCelestialAssetSet>();
            AssetDatabase.CreateAsset(set, ConfigPath);
        }

        List<Material> skyboxes = new List<Material>();
        List<Material> planets = new List<Material>();
        List<Material> gasGiants = new List<Material>();
        List<Material> moons = new List<Material>();
        List<Material> stars = new List<Material>();
        List<Material> rings = new List<Material>();
        List<Texture2D> planetTextures = new List<Texture2D>();
        List<Texture2D> gasGiantTextures = new List<Texture2D>();
        List<Texture2D> moonTextures = new List<Texture2D>();

        string[] materialGuids = AssetDatabase.FindAssets("t:Material");
        Array.Sort(materialGuids, StringComparer.Ordinal);
        foreach (string guid in materialGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) continue;

            string name = material.name.ToLowerInvariant();
            string source = path.ToLowerInvariant();
            string shader = material.shader != null ? material.shader.name.ToLowerInvariant() : string.Empty;
            string key = name + " " + source;
            bool hasSkyWords = ContainsAny(key, "space", "cosmic", "galaxy", "nebula", "starfield", "skybox");
            // Explicitly exclude the old local VOIDLINE panorama: it is deliberately no longer
            // the primary environment and must not be rediscovered by this new asset scan.
            bool legacyDarkVoidlineSky = name.Contains("m_voidline_darkspace") ||
                source.Contains("spaceskiespurple2k") || source.Contains("m_voidline_darkspace");
            bool isSky = !legacyDarkVoidlineSky && hasSkyWords &&
                (shader.Contains("skybox") || name.Contains("skybox") ||
                source.Contains("skybox") || shader.Contains("panoramic"));
            if (isSky) skyboxes.Add(material);

            bool likelyPlanetPack = ContainsAny(source, "procedural planet", "planet generation", "parallel cascades");
            if (ContainsAny(key, "gas giant", "gasgiant", "jupiter", "saturn"))
                gasGiants.Add(material);
            else if (ContainsAny(key, "moon", "lunar", "cratered"))
                moons.Add(material);
            else if (ContainsAny(key, "asteroid ring", "planet ring", "ring bands", "ring material", "saturn ring"))
                rings.Add(material);
            else if (ContainsAny(name, "star", "stellar", "sun corona") && !isSky)
                stars.Add(material);
            else if (ContainsAny(name, "planet", "terrestrial", "oceanic", "ice world") ||
                (likelyPlanetPack && (shader.Contains("planet") || name.Contains("earth") || name.Contains("world"))))
                planets.Add(material);
        }

        string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D");
        Array.Sort(textureGuids, StringComparer.Ordinal);
        foreach (string guid in textureGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null) continue;
            string name = texture.name.ToLowerInvariant();
            string key = name + " " + path.ToLowerInvariant();

            // Assign only colour/albedo maps to the sphere surface. A normal, ORM, roughness,
            // metal, height or emission map must never be used as the base colour by accident.
            if (ContainsAny(key, "normal", "_n.", "_n_", "orm", "roughness", "metallic",
                "height", "specular", "emission", "mask", "ambient occlusion", "_ao", "bump"))
                continue;
            bool celestialTexture = ContainsAny(key, "planet", "gas giant", "gasgiant", "jupiter",
                "saturn", "moon", "lunar", "cratered", "terrestrial", "oceanic", "earth", "celestial");
            if (!celestialTexture) continue;

            if (ContainsAny(key, "gas giant", "gasgiant", "jupiter", "saturn", "banded giant"))
                gasGiantTextures.Add(texture);
            else if (ContainsAny(key, "moon", "lunar", "cratered"))
                moonTextures.Add(texture);
            else
                planetTextures.Add(texture);
        }

        List<GameObject> planetPrefabs = new List<GameObject>();
        List<GameObject> gasGiantPrefabs = new List<GameObject>();
        List<GameObject> moonPrefabs = new List<GameObject>();
        List<GameObject> starPrefabs = new List<GameObject>();
        List<GameObject> ringPrefabs = new List<GameObject>();
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        Array.Sort(prefabGuids, StringComparer.Ordinal);
        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string key = Path.GetFileNameWithoutExtension(path).ToLowerInvariant() + " " + path.ToLowerInvariant();
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;
            if (ContainsAny(key, "gas giant", "gasgiant", "jupiter", "saturn")) gasGiantPrefabs.Add(prefab);
            else if (ContainsAny(key, "moon", "lunar")) moonPrefabs.Add(prefab);
            else if (ContainsAny(key, "ring", "saturn ring")) ringPrefabs.Add(prefab);
            else if (ContainsAny(key, "star", "stellar", "sun corona")) starPrefabs.Add(prefab);
            else if (ContainsAny(key, "planet", "terrestrial", "oceanic")) planetPrefabs.Add(prefab);
        }

        // Keep custom inspector choices already made by the user. Auto-fill only empty categories.
        SerializedObject serializedSet = new SerializedObject(set);
        AssignIfEmpty(serializedSet, "spaceSkyboxes", skyboxes);
        AssignIfEmpty(serializedSet, "planetMaterials", planets);
        AssignIfEmpty(serializedSet, "gasGiantMaterials", gasGiants);
        AssignIfEmpty(serializedSet, "moonMaterials", moons);
        AssignIfEmpty(serializedSet, "starMaterials", stars);
        AssignIfEmpty(serializedSet, "ringMaterials", rings);
        AssignIfEmpty(serializedSet, "planetTextures", planetTextures);
        AssignIfEmpty(serializedSet, "gasGiantTextures", gasGiantTextures);
        AssignIfEmpty(serializedSet, "moonTextures", moonTextures);
        AssignIfEmpty(serializedSet, "planetPrefabs", planetPrefabs);
        AssignIfEmpty(serializedSet, "gasGiantPrefabs", gasGiantPrefabs);
        AssignIfEmpty(serializedSet, "moonPrefabs", moonPrefabs);
        AssignIfEmpty(serializedSet, "starPrefabs", starPrefabs);
        AssignIfEmpty(serializedSet, "ringPrefabs", ringPrefabs);
        serializedSet.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(set);
        AssetDatabase.SaveAssets();

        string report = "Celestial asset set saved at:\n" + ConfigPath +
            "\n\nFound (and assigned to empty categories):" +
            "\nSpace skyboxes: " + skyboxes.Count +
            "\nPlanet materials: " + planets.Count +
            "\nGas giant materials: " + gasGiants.Count +
            "\nMoon materials: " + moons.Count +
            "\nStar materials: " + stars.Count +
            "\nRing materials: " + rings.Count +
            "\nPlanet colour textures: " + planetTextures.Count +
            "\nGas giant colour textures: " + gasGiantTextures.Count +
            "\nMoon colour textures: " + moonTextures.Count +
            "\nCelestial prefabs: " + (planetPrefabs.Count + gasGiantPrefabs.Count +
                moonPrefabs.Count + starPrefabs.Count + ringPrefabs.Count) +
            "\n\nOpen the generated asset to review or change the selected source materials.";
        Debug.Log("[VOIDLINE] " + report.Replace("\n", " "));
        EditorUtility.DisplayDialog("VOIDLINE Celestial Asset Set", report, "OK");
    }

    private static void AssignIfEmpty(SerializedObject asset, string propertyName, List<Material> candidates)
    {
        SerializedProperty property = asset.FindProperty(propertyName);
        if (property == null || property.arraySize != 0 || candidates.Count == 0) return;
        property.arraySize = candidates.Count;
        for (int i = 0; i < candidates.Count; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = candidates[i];
    }

    private static void AssignIfEmpty(SerializedObject asset, string propertyName, List<GameObject> candidates)
    {
        SerializedProperty property = asset.FindProperty(propertyName);
        if (property == null || property.arraySize != 0 || candidates.Count == 0) return;
        property.arraySize = candidates.Count;
        for (int i = 0; i < candidates.Count; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = candidates[i];
    }

    private static void AssignIfEmpty(SerializedObject asset, string propertyName, List<Texture2D> candidates)
    {
        SerializedProperty property = asset.FindProperty(propertyName);
        if (property == null || property.arraySize != 0 || candidates.Count == 0) return;
        property.arraySize = candidates.Count;
        for (int i = 0; i < candidates.Count; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = candidates[i];
    }

    private static bool ContainsAny(string value, params string[] words)
    {
        for (int i = 0; i < words.Length; i++)
            if (value.Contains(words[i])) return true;
        return false;
    }
}
#endif
