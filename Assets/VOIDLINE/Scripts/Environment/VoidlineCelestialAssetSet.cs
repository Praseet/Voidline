using UnityEngine;

/// <summary>
/// References the project's imported celestial art without copying or redistributing third-party
/// source files. Generate/populate this asset with VOIDLINE/Environment/Build Celestial Asset Set.
/// </summary>
[CreateAssetMenu(fileName = "VOIDLINE_CelestialAssetSet", menuName = "VOIDLINE/Environment/Celestial Asset Set")]
public sealed class VoidlineCelestialAssetSet : ScriptableObject
{
    [Header("Distant Space")]
    [Tooltip("Space panorama/cubemap materials from the installed skybox asset pack.")]
    [SerializeField] private Material[] spaceSkyboxes;
    [Header("Planet Surface Materials")]
    [SerializeField] private Material[] planetMaterials;
    [SerializeField] private Material[] gasGiantMaterials;
    [SerializeField] private Material[] moonMaterials;
    [SerializeField] private Material[] starMaterials;
    [SerializeField] private Material[] ringMaterials;
    [Header("Baked Planet Surface Textures (optional)")]
    [SerializeField] private Texture2D[] planetTextures;
    [SerializeField] private Texture2D[] gasGiantTextures;
    [SerializeField] private Texture2D[] moonTextures;
    [Header("Authored Celestial Prefabs (optional)")
    [SerializeField] private GameObject[] planetPrefabs;
    [SerializeField] private GameObject[] gasGiantPrefabs;
    [SerializeField] private GameObject[] moonPrefabs;
    [SerializeField] private GameObject[] starPrefabs;
    [SerializeField] private GameObject[] ringPrefabs;

    public Material PickSpaceSkybox(int seed) => Pick(spaceSkyboxes, seed);
    public Material PickPlanetMaterial(int seed) => Pick(planetMaterials, seed);
    public Material PickGasGiantMaterial(int seed) => Pick(gasGiantMaterials, seed);
    public Material PickMoonMaterial(int seed) => Pick(moonMaterials, seed);
    public Material PickStarMaterial(int seed) => Pick(starMaterials, seed);
    public Material PickRingMaterial(int seed) => Pick(ringMaterials, seed);
    public Texture2D PickPlanetTexture(int seed) => Pick(planetTextures, seed);
    public Texture2D PickGasGiantTexture(int seed) => Pick(gasGiantTextures, seed);
    public Texture2D PickMoonTexture(int seed) => Pick(moonTextures, seed);

    public GameObject PickPlanetPrefab(int seed) => Pick(planetPrefabs, seed);
    public GameObject PickGasGiantPrefab(int seed) => Pick(gasGiantPrefabs, seed);
    public GameObject PickMoonPrefab(int seed) => Pick(moonPrefabs, seed);
    public GameObject PickStarPrefab(int seed) => Pick(starPrefabs, seed);
    public GameObject PickRingPrefab(int seed) => Pick(ringPrefabs, seed);

    public int SpaceSkyboxCount => Count(spaceSkyboxes);
    public int PlanetMaterialCount => Count(planetMaterials);
    public int GasGiantMaterialCount => Count(gasGiantMaterials);
    public int MoonMaterialCount => Count(moonMaterials);
    public int StarMaterialCount => Count(starMaterials);
    public int RingMaterialCount => Count(ringMaterials);
    public int PlanetTextureCount => Count(planetTextures);
    public int GasGiantTextureCount => Count(gasGiantTextures);
    public int MoonTextureCount => Count(moonTextures);
    public int PrefabCount => Count(planetPrefabs) + Count(gasGiantPrefabs) +
        Count(moonPrefabs) + Count(starPrefabs) + Count(ringPrefabs);

    private static int Count<T>(T[] items) where T : Object
    {
        if (items == null) return 0;
        int count = 0;
        for (int i = 0; i < items.Length; i++) if (items[i] != null) count++;
        return count;
    }

    private static T Pick<T>(T[] items, int seed) where T : Object
    {
        if (items == null || items.Length == 0) return null;
        int start = Mathf.Abs(seed == int.MinValue ? 0 : seed) % items.Length;
        for (int i = 0; i < items.Length; i++)
        {
            T candidate = items[(start + i) % items.Length];
            if (candidate != null) return candidate;
        }
        return null;
    }
}
