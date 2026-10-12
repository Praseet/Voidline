using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.IO;

public static class VoidlinePrepareSpaceSky
{
    public static string Run()
    {
        const string source = "Assets/Third Party/AllSkyFree/Space_AnotherPlanet/AllSky_Space_AnotherPlanet.mat";
        const string folder = "Assets/Resources";
        const string target = folder + "/M_VOIDLINE_SpaceSkybox.mat";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets", "Resources");
        if (AssetDatabase.LoadAssetAtPath<Material>(target) != null)
            AssetDatabase.DeleteAsset(target);
        bool copied = AssetDatabase.CopyAsset(source, target);
        AssetDatabase.Refresh();
        var material = AssetDatabase.LoadAssetAtPath<Material>(target);
        if (!copied || material == null)
            throw new System.Exception("Could not copy AllSky Free space material. Source exists=" + (AssetDatabase.LoadAssetAtPath<Material>(source) != null));
        RenderSettings.skybox = material;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        return "SPACE_SKY_READY source=" + source + " target=" + target + " shader=" + material.shader.name;
    }
}