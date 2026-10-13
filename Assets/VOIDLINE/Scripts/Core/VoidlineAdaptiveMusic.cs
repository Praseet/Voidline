using UnityEngine;

/// <summary>
/// Adaptive soundtrack for VOIDLINE. The imported Deep In Space arrangement opens the run as a
/// non-looping lead theme; when it completes, a smooth crossfade hands off to the authored looped
/// Space Threat beds:
///
///   ambient  -> Slow Ambient Version, Looped        (open-space cruising)
///   tension  -> Electronic Dramatic Version, Looped (critical-hull pressure)
///   boost    -> Energetic Powerful Version, Looped  (boost and skill-flow momentum)
///
/// Space Threat supplies complete alternate arrangements, not stems. Mood changes crossfade
/// between those versions; Deep In Space is the non-looping opening lead. The music component
/// is created at runtime, so every runtime clip is loaded by Resources name.
/// </summary>
[DisallowMultipleComponent]
public sealed class VoidlineAdaptiveMusic : MonoBehaviour
{
    private const string DeepSpaceThemeResource = "Deep_In_Space";
    private const string AmbientResource = "Space Threat  (Slow Ambient Version, Looped)";
    private const string TensionResource = "Space Threat (Electronic Dramatic Version, Looped)";
    private const string BoostResource = "Space Threat (Energetic Powerful Version, Looped)";

    [Header("Soundtrack Mix")]
    [SerializeField, Range(0f, 1f)] private float ambientLevel = 0.55f;
    [SerializeField, Range(0f, 1f)] private float tensionLevel = 0.50f;
    [SerializeField, Range(0f, 1f)] private float boostLevel = 0.62f;
    [SerializeField, Range(0f, 1f)] private float criticalLevel = 0.72f;
    [SerializeField, Min(0.1f)] private float mixResponse = 0.85f;

    [Header("Music Intensity Memory")]
    [Tooltip("Minimum energetic hold after boost/flow activity. The four-bar phrase floor takes precedence.")]
    [SerializeField, Range(2f, 18f)] private float boostMusicTailSeconds = 12f;
    [Tooltip("Musical intensity release duration. Six seconds is two bars at the measured ~80 BPM tempo.")]
    [SerializeField, Range(3f, 18f)] private float intensityReleaseSeconds = 6f;
    [Tooltip("Speed at which musical intensity responds to gameplay boost.")]
    [SerializeField, Range(0.5f, 10f)] private float intensityAttack = 3.8f;

    [Header("Musical Timing")]
    [Tooltip("Estimated tempo of the supplied Space Threat arrangements; confirm against the soundtrack if it is replaced.")]
    [SerializeField, Range(60f, 180f)] private float musicTempoBpm = 80f;
    [Tooltip("Minimum number of bars the energetic arrangement gets to play after its last boost/flow trigger.")]
    [SerializeField, Range(1, 8)] private int boostPhraseBars = 4;

    [Header("Transitions")]
    [SerializeField, Range(0f, 1f)] private float duckToPause = 0.35f;
    [SerializeField, Min(0.05f)] private float gameOverFadeSeconds = 1.6f;

    private AudioSource ambient;
    private AudioSource tension;
    private AudioSource boost;
    private AudioSource deepSpaceTheme;

    private float targetAmbient;
    private float targetDeepSpace;
    private float targetTension;
    private float targetBoost;
    private float carriedBoostIntensity;
    private float boostMusicHoldRemaining;
    private double musicClockStartDsp;
    private double boostScheduledStartDsp;
    private double tensionScheduledStartDsp;
    private bool boostTrackScheduled;
    private bool tensionTrackScheduled;
    private bool gameOverFading;
    private float gameOverFade = 1f;

    private void Awake()
    {
        musicClockStartDsp = AudioSettings.dspTime;
        AudioClip deepSpaceThemeClip = Resources.Load<AudioClip>(DeepSpaceThemeResource);
        AudioClip ambientClip = Resources.Load<AudioClip>(AmbientResource);
        AudioClip tensionClip = Resources.Load<AudioClip>(TensionResource);
        AudioClip boostClip = Resources.Load<AudioClip>(BoostResource);

        if (deepSpaceThemeClip == null)
            Debug.LogWarning("[VOIDLINE] Deep In Space opening theme was not found at Resources/" +
                DeepSpaceThemeResource + ". The adaptive looped ambient soundtrack will start directly.");

        if (ambientClip == null || tensionClip == null || boostClip == null)
        {
            Debug.LogWarning("[VOIDLINE] One or more Space Threat looped versions were not found in Resources. " +
                "The opening theme can still play; missing adaptive moods will remain silent.");
        }

        ambient = CreateSource(ambientClip);
        // Dramatic and energetic files are alternative full arrangements, not underlays.
        // Start a variant only when gameplay actually requests that mood.
        tension = CreateSource(tensionClip, true, false);
        boost = CreateSource(boostClip, true, false);
        deepSpaceTheme = CreateSource(deepSpaceThemeClip, false);

        // The authored Deep In Space track leads the opening run. It is not hard-looped:
        // once it reaches its end, the familiar looped ambient arrangement smoothly resumes.
        bool hasOpeningTheme = deepSpaceTheme != null;
        targetAmbient = hasOpeningTheme ? 0f : ambientLevel;
        targetDeepSpace = hasOpeningTheme ? ambientLevel : 0f;
        if (ambient != null) ambient.volume = targetAmbient;
    }

    private AudioSource CreateSource(AudioClip clip, bool loop = true, bool startImmediately = true)
    {
        if (clip == null) return null;

        var source = gameObject.AddComponent<AudioSource>();
        // Deliberately do NOT rename gameObject: this component lives on the shared GameManager,
        // so naming it after a music layer would rename the manager itself. The AudioSource is
        // tracked in a field, so no name-based lookup is needed.
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f;      // 2D music bed, not a positional emitter
        source.dopplerLevel = 0f;
        source.priority = 32;          // music sits above transient SFX
        source.volume = 0f;
        source.clip = clip;
        if (startImmediately) source.Play();
        return source;
    }


    /// <summary>
    /// Raises the soundtrack's energy independently of boost fuel. Short boosts can therefore
    /// launch a longer musical phrase; flow events can sustain it without adding boost duration.
    /// </summary>
    public void PulseIntensity(float intensity, float holdSeconds)
    {
        if (gameOverFading || intensity <= 0f) return;
        carriedBoostIntensity = Mathf.Max(carriedBoostIntensity, Mathf.Clamp01(intensity));
        float phraseFloor = GetBarDurationSeconds() * Mathf.Clamp(boostPhraseBars, 1, 8);
        float requestedHold = Mathf.Max(Mathf.Max(0f, holdSeconds),
            Mathf.Max(boostMusicTailSeconds, phraseFloor));
        boostMusicHoldRemaining = Mathf.Max(boostMusicHoldRemaining, requestedHold);
        EnsureBoostTrackPlaying();
    }

    private void EnsureBoostTrackPlaying()
    {
        EnsureTrackPlayingFromCue(boost);
    }

    private void EnsureTensionTrackPlaying()
    {
        EnsureTrackPlayingFromCue(tension);
    }

    private void EnsureTrackPlayingFromCue(AudioSource source)
    {
        if (source == null || source.clip == null || source.isPlaying) return;
        if (source == boost && boostTrackScheduled) return;
        if (source == tension && tensionTrackScheduled) return;

        // The three supplied Space Threat loop versions share an approximately 80 BPM pulse.
        // Start the incoming full arrangement on the next beat, using the ambient loop as the
        // DSP clock. This avoids an arbitrary mid-beat entrance without delaying boost by a bar.
        double beatSeconds = GetBeatDurationSeconds();
        double clockSeconds = GetMusicClockSeconds();
        double beatPhase = clockSeconds % beatSeconds;
        if (beatPhase < 0.0) beatPhase += beatSeconds;
        double wait = beatSeconds - beatPhase;
        // PlayScheduled needs some lead time. If the next beat is too close, target the one after it.
        if (wait < 0.06) wait += beatSeconds;

        double scheduledDsp = AudioSettings.dspTime + wait;
        double referenceClipSeconds = clockSeconds + wait;
        if (ambient != null && ambient.clip != null && ambient.clip.length > 0f)
            referenceClipSeconds %= ambient.clip.length;
        double alignedClipSeconds = referenceClipSeconds % source.clip.length;
        int startSample = Mathf.Clamp(
            (int)System.Math.Round(alignedClipSeconds * source.clip.frequency),
            0, Mathf.Max(0, source.clip.samples - 1));

        source.Stop();
        source.timeSamples = startSample;
        source.volume = 0f;
        source.PlayScheduled(scheduledDsp);

        if (source == boost)
        {
            boostTrackScheduled = true;
            boostScheduledStartDsp = scheduledDsp;
            // The phrase hold starts after the cue actually enters, not while it waits for a beat.
            float phraseFloor = GetBarDurationSeconds() * Mathf.Clamp(boostPhraseBars, 1, 8);
            boostMusicHoldRemaining = Mathf.Max(boostMusicHoldRemaining, (float)wait + phraseFloor);
        }
        else if (source == tension)
        {
            tensionTrackScheduled = true;
            tensionScheduledStartDsp = scheduledDsp;
        }
    }

    private float GetBeatDurationSeconds()
    {
        return 60f / Mathf.Clamp(musicTempoBpm, 60f, 180f);
    }

    private float GetBarDurationSeconds()
    {
        return GetBeatDurationSeconds() * 4f;
    }

    private double GetMusicClockSeconds()
    {
        // The silent ambient source keeps a stable position while the opening theme plays,
        // so the alternate loop can enter at the equivalent musical position.
        if (ambient != null && ambient.clip != null && ambient.isPlaying)
            return ambient.timeSamples / (double)Mathf.Max(1, ambient.clip.frequency);

        return System.Math.Max(0d, AudioSettings.dspTime - musicClockStartDsp);
    }

    public void NotifyBoostStarted()
    {
        PulseIntensity(1f, boostMusicTailSeconds);
    }

    /// <summary>Called once per frame by the game loop; transitions smooth in unscaled time.</summary>
    public void SetSituation(float boostBlend, int hull, int maxHull,
        bool paused, bool gameOver)
    {
        if (gameOver)
        {
            // Do not allow a queued bar-synchronized cue to start after the run has ended.
            if (boostTrackScheduled)
            {
                if (this.boost != null) this.boost.Stop();
                boostTrackScheduled = false;
            }
            if (tensionTrackScheduled)
            {
                if (tension != null) tension.Stop();
                tensionTrackScheduled = false;
            }

            // Fade the whole bed out rather than cutting it, so Game Over reads as deliberate.
            gameOverFading = true;
            targetAmbient = 0f;
            targetDeepSpace = 0f;
            targetTension = 0f;
            targetBoost = 0f;
            return;
        }

        gameOverFading = false;
        gameOverFade = 1f;

        // Critical hull is a genuine "one hit from death" state, not an arbitrary threshold.
        float lowHull = hull == 1 && maxHull > 1 ? 1f : 0f;
        float boost = Mathf.Clamp01(boostBlend);
        float dt = Time.unscaledDeltaTime;
        float duck = paused ? duckToPause : 1f;

        // Retain musical momentum when the player's fuel ends. A boost raises the carried
        // intensity and starts a hold; skill events can refresh it via PulseIntensity().
        // Pause ducks audio but freezes this clock so the player doesn't lose a music phrase
        // while reading the pause screen.
        if (!paused)
        {
            if (boost > 0.055f)
            {
                carriedBoostIntensity = Mathf.Max(carriedBoostIntensity,
                    Mathf.MoveTowards(carriedBoostIntensity, boost, intensityAttack * dt));
                boostMusicHoldRemaining = Mathf.Max(boostMusicHoldRemaining, 0.9f);
            }
            else if (boostMusicHoldRemaining > 0f)
            {
                boostMusicHoldRemaining = Mathf.Max(0f, boostMusicHoldRemaining - dt);
            }
            else
            {
                carriedBoostIntensity = Mathf.MoveTowards(carriedBoostIntensity, 0f,
                    dt / Mathf.Max(0.1f, intensityReleaseSeconds));
            }
        }

        float musicalEnergy = Mathf.Max(boost, carriedBoostIntensity);

        // The pack supplies complete alternate versions, not stems. Treat intensity as a
        // crossfade state: ambient -> energetic -> ambient, with the dramatic version taking
        // priority at critical hull. This avoids two unrelated full arrangements fighting.
        bool themePlaying = deepSpaceTheme != null && deepSpaceTheme.isPlaying;
        float threatEnergy = lowHull;
        if (threatEnergy > 0.01f) EnsureTensionTrackPlaying();
        float activeEnergy = Mathf.Max(musicalEnergy, threatEnergy);
        float ambientWeight = 1f - activeEnergy;
        float openingWeight = 1f - (0.85f * musicalEnergy);
        float tensionWeight = lowHull * (1f - 0.55f * musicalEnergy);

        targetAmbient = ambientLevel * duck * (!themePlaying ? ambientWeight : 0f);
        targetDeepSpace = ambientLevel * duck * (themePlaying ? openingWeight : 0f);
        targetTension = Mathf.Lerp(tensionLevel, criticalLevel, lowHull) * duck * tensionWeight;
        targetBoost = boostLevel * duck * (paused ? musicalEnergy * 0.4f : musicalEnergy);
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        if (dt <= 0f) return;

        if (boostTrackScheduled && AudioSettings.dspTime >= boostScheduledStartDsp)
            boostTrackScheduled = false;
        if (tensionTrackScheduled && AudioSettings.dspTime >= tensionScheduledStartDsp)
            tensionTrackScheduled = false;

        if (gameOverFading)
        {
            gameOverFade = Mathf.MoveTowards(
                gameOverFade, 0f, dt / Mathf.Max(0.05f, gameOverFadeSeconds));
        }

        float fade = gameOverFading ? gameOverFade : 1f;

        MoveSource(ambient, targetAmbient * fade, dt);
        MoveSource(deepSpaceTheme, targetDeepSpace * fade, dt);
        // Keep a queued track silent until its DSP start boundary, then blend it in normally.
        MoveSource(tension, (tensionTrackScheduled ? 0f : targetTension) * fade, dt);
        MoveSource(boost, (boostTrackScheduled ? 0f : targetBoost) * fade, dt);

        // Stop full alternate arrangements after their fade-out; future mood changes
        // restart from an authored cue rather than resuming a random loop phase.
        bool finishedGameOver = gameOverFading && gameOverFade <= 0.001f;
        bool finishedPhrase = boostMusicHoldRemaining <= 0f && carriedBoostIntensity <= 0.015f;
        if (boost != null && boost.isPlaying && boost.volume <= 0.002f
            && (finishedPhrase || finishedGameOver))
            boost.Stop();
        if (tension != null && tension.isPlaying && tension.volume <= 0.002f
            && (targetTension <= 0.002f || finishedGameOver))
            tension.Stop();
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
        if (deepSpaceTheme != null) deepSpaceTheme.Stop();
        if (tension != null) tension.Stop();
        if (boost != null) boost.Stop();
    }
}