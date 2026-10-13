#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Scans already-imported asset packs and references matching assets. It never downloads,
/// duplicates, modifies, or redistributes their original source files.
/// </summary>
public static class VoidlineCelestialAssetBuilder
{
    private const string ResourceFolder = "Assets/VOIDLINE/Resources";
    private const string ConfigPath = ResourceFolder + "/VOIDLINE_CelestialAssetSet.asset";

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
