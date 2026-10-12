using UnityEngine;

/// <summary>
/// Adaptive soundtrack for VOIDLINE, built from the imported "Space Threat (Free Action Music)"
/// stems. Three pre-looped stems crossfade with flight state, replacing the earlier procedural
/// fallback that existed only because the project contained no AudioClip:
///
///   ambient  -> Slow Ambient Version, Looped        (open-space cruising)
///   tension  -> Electronic Dramatic Version, Looped (risk and low-hull pressure)
///   boost    -> Energetic Powerful Version, Looped  (thruster boost)
///
/// The "Looped" variants are used deliberately: they carry authored loop points, so wrapping does
/// not click. The non-looped masters are intentionally not loaded.
/// </summary>
[DisallowMultipleComponent]
public sealed class VoidlineAdaptiveMusic : MonoBehaviour
{
    private const string AmbientResource = "Space Threat  (Slow Ambient Version, Looped)";
    private const string TensionResource = "Space Threat (Electronic Dramatic Version, Looped)";
    private const string BoostResource = "Space Threat (Energetic Powerful Version, Looped)";

    [Header("Soundtrack Mix")]
    [SerializeField, Range(0f, 1f)] private float ambientLevel = 0.55f;
    [SerializeField, Range(0f, 1f)] private float tensionLevel = 0.50f;
    [SerializeField, Range(0f, 1f)] private float boostLevel = 0.62f;
    [SerializeField, Range(0f, 1f)] private float criticalLevel = 0.72f;
    [SerializeField, Min(0.1f)] private float mixResponse = 0.85f;

    [Header("Transitions")]
    [SerializeField, Range(0f, 1f)] private float duckToPause = 0.35f;
    [SerializeField, Min(0.05f)] private float gameOverFadeSeconds = 1.6f;

    private AudioSource ambient;
    private AudioSource tension;
    private AudioSource boost;
    private AudioSource critical;

    private float targetAmbient;
    private float targetTension;
    private float targetBoost;
    private float targetCritical;
    private bool gameOverFading;
    private float gameOverFade = 1f;

    private void Awake()
    {
        AudioClip ambientClip = Resources.Load<AudioClip>(AmbientResource);
        AudioClip tensionClip = Resources.Load<AudioClip>(TensionResource);
        AudioClip boostClip = Resources.Load<AudioClip>(BoostResource);

        if (ambientClip == null || tensionClip == null || boostClip == null)
        {
            Debug.LogWarning("[VOIDLINE] Space Threat looped stems were not found in a Resources " +
                "folder. Move the three looped WAVs (with their .meta files) into Assets/Resources " +
                "so the soundtrack can load them; music stays silent until then.");
        }

        ambient = CreateSource(ambientClip);
        tension = CreateSource(tensionClip);
        boost = CreateSource(boostClip);
        // The dramatic stem doubles as the critical-hull layer, so the score stays coherent
        // instead of introducing an unrelated fourth track for one gameplay state.
        critical = CreateSource(tensionClip);

        targetAmbient = ambientLevel;
        if (ambient != null) ambient.volume = ambientLevel;
    }

    private AudioSource CreateSource(AudioClip clip)
    {
        if (clip == null) return null;

        var source = gameObject.AddComponent<AudioSource>();
        // Deliberately do NOT rename gameObject: this component lives on the shared GameManager,
        // so naming it after a music layer would rename the manager itself. The AudioSource is
        // tracked in a field, so no name-based lookup is needed.
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;      // 2D music bed, not a positional emitter
        source.dopplerLevel = 0f;
        source.priority = 32;          // music sits above transient SFX
        source.volume = 0f;
        source.clip = clip;
        source.Play();
        return source;
    }


    /// <summary>Called once per frame by the game loop; transitions smooth in unscaled time.</summary>
    public void SetSituation(float boostBlend, int hull, int maxHull,
        bool paused, bool gameOver)
    {
        if (gameOver)
        {
            // Fade the whole bed out rather than cutting it, so Game Over reads as deliberate.
            gameOverFading = true;
            targetAmbient = 0f;
            targetTension = 0f;
            targetBoost = 0f;
            targetCritical = 0f;
            return;
        }

        gameOverFading = false;
        gameOverFade = 1f;

        // Critical hull is a genuine "one hit from death" state, not an arbitrary threshold.
        float lowHull = hull == 1 && maxHull > 1 ? 1f : 0f;
        float boost = Mathf.Clamp01(boostBlend);
        float duck = paused ? duckToPause : 1f;

        targetAmbient = ambientLevel * duck;
        targetTension = tensionLevel * duck * (0.30f + 0.70f * lowHull);
        targetBoost = boostLevel * duck * (paused ? 0.4f : boost);
        targetCritical = criticalLevel * duck * lowHull;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        if (dt <= 0f) return;

        if (gameOverFading)
        {
            gameOverFade = Mathf.MoveTowards(
                gameOverFade, 0f, dt / Mathf.Max(0.05f, gameOverFadeSeconds));
        }

        float fade = gameOverFading ? gameOverFade : 1f;

        MoveSource(ambient, targetAmbient * fade, dt);
        MoveSource(tension, targetTension * fade, dt);
        MoveSource(boost, targetBoost * fade, dt);
        MoveSource(critical, targetCritical * fade, dt);
    }

    private void MoveSource(AudioSource source, float target, float dt)
    {
        if (source == null) return;
        // Exponential approach gives a musical, frame-rate independent crossfade.
        source.volume = Mathf.Lerp(source.volume, target, 1f - Mathf.Exp(-mixResponse * dt));
    }

    private void OnDestroy()
    {
        if (ambient != null) ambient.Stop();
        if (tension != null) tension.Stop();
        if (boost != null) boost.Stop();
        if (critical != null) critical.Stop();
    }
}