using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class VoidlineAudioFeedback : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] private float masterVolume = 0.52f;
    private AudioSource source;
    private AudioSource boostLoopSource;
    private AudioClip damageClip, passClip, pickupClip, gameOverClip, uiClip, rollClip, boostStartClip, boostLoopClip;
    private AudioClip passageClearClip;
    private AudioClip nearMissClip;

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        if (source == null) source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.volume = masterVolume;

        boostLoopSource = gameObject.AddComponent<AudioSource>();
        boostLoopSource.playOnAwake = false;
        boostLoopSource.loop = true;
        boostLoopSource.spatialBlend = 0f;
        boostLoopSource.priority = 32;
        boostLoopSource.volume = 0f;

        damageClip = MakeSweep("Damage", 0.24f, 210f, 72f, 0.7f, 0.18f, 11);
        passClip = MakeSweep("Pass", 0.13f, 520f, 940f, 0.42f, 0.02f, 23);
        pickupClip = MakeSweep("Pickup", 0.22f, 620f, 1320f, 0.48f, 0.01f, 29);
        gameOverClip = MakeSweep("RunEnd", 0.62f, 260f, 46f, 0.65f, 0.08f, 37);
        uiClip = MakeSweep("UI", 0.07f, 640f, 820f, 0.22f, 0f, 53);
        rollClip = MakeSweep("BarrelRoll", 0.2f, 260f, 1180f, 0.36f, 0.045f, 61);
        boostStartClip = MakeBoostIgnition();
        boostLoopClip = MakeBoostLoop();
        passageClearClip = MakeSweep("PassageClear", 0.38f, 360f, 1080f, 0.46f, 0.02f, 89);
        nearMissClip = MakeSweep("NearMiss", 0.15f, 760f, 220f, 0.26f, 0.22f, 97);
        boostLoopSource.clip = boostLoopClip;
    }

    public void PlayDamage() => Play(damageClip, 0.9f);
    public void PlayPass() => Play(passClip, 0.52f);
    public void PlayPassageClear() => Play(passageClearClip, 0.92f);
    public void PlayNearMiss() => Play(nearMissClip, 0.72f);
    public void PlayPickup() => Play(pickupClip, 0.82f);
    public void PlayGameOver() => Play(gameOverClip, 1f);
    public void PlayUiTick() => Play(uiClip, 0.55f);
    public void PlayRoll() => Play(rollClip, 0.65f);
    public void PlayBoostStart() => Play(boostStartClip, 0.98f);

    public void SetBoostLevel(float intensity)
    {
        float level = Mathf.Clamp01(intensity);
        if (boostLoopSource == null) return;
        if (level > 0.025f && masterVolume > 0f)
        {
            if (!boostLoopSource.isPlaying && boostLoopSource.clip != null) boostLoopSource.Play();
            boostLoopSource.pitch = Mathf.Lerp(0.92f, 1.48f, level);
            boostLoopSource.volume = masterVolume * 0.48f * level;
        }
        else
        {
            boostLoopSource.volume = 0f;
            if (boostLoopSource.isPlaying) boostLoopSource.Stop();
        }
    }

    private void Play(AudioClip clip, float gain)
    {
        if (source == null || clip == null || masterVolume <= 0f) return;
        source.PlayOneShot(clip, gain);
    }

    private void OnDestroy()
    {
        if (boostLoopSource != null) boostLoopSource.Stop();
        if (!Application.isPlaying) return;
        DestroyClip(damageClip); DestroyClip(passClip); DestroyClip(pickupClip);
        DestroyClip(gameOverClip); DestroyClip(uiClip); DestroyClip(rollClip);
        DestroyClip(boostStartClip); DestroyClip(boostLoopClip); DestroyClip(passageClearClip); DestroyClip(nearMissClip);
    }

    private static void DestroyClip(AudioClip clip)
    {
        if (clip != null) Destroy(clip);
    }

    /// <summary>
    /// A compact arcade boost transient: sub thump, clipped attack, filtered ion hiss and
    /// a rising harmonic sweep. The fast attack is synchronised with the camera hit-stop.
    /// </summary>
    private static AudioClip MakeBoostIgnition()
    {
        const int sampleRate = 44100;
        const float duration = 0.62f;
        int count = Mathf.RoundToInt(sampleRate * duration);
        float[] samples = new float[count];
        System.Random random = new System.Random(71);
        double thumpPhase = 0.0;
        double sweepPhase = 0.0;
        float filteredNoise = 0f;

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)sampleRate;
            float normalised = t / duration;
            thumpPhase += 2.0 * Math.PI * Mathf.Lerp(78f, 43f, Mathf.Clamp01(t / 0.16f)) / sampleRate;
            float sweepHz = Mathf.Lerp(115f, 1550f, Mathf.Pow(Mathf.Clamp01(normalised), 1.32f));
            sweepPhase += 2.0 * Math.PI * sweepHz / sampleRate;

            float rawNoise = (float)(random.NextDouble() * 2.0 - 1.0);
            filteredNoise += (rawNoise - filteredNoise) * 0.24f;
            float attack = Mathf.SmoothStep(0f, 1f, t / 0.012f);
            float impact = (float)Math.Sin(thumpPhase) * Mathf.Exp(-t / 0.032f) * 0.82f;
            float sub = (float)Math.Sin(thumpPhase * 0.58) * Mathf.Exp(-t / 0.11f) * 0.34f;
            float rushEnvelope = Mathf.Exp(-t / 0.34f) * Mathf.SmoothStep(0f, 1f, t / 0.018f);
            float whoosh = filteredNoise * rushEnvelope * 0.58f;
            float risingTone = (float)Math.Sin(sweepPhase) * rushEnvelope * 0.34f;
            float highShimmer = (float)Math.Sin(sweepPhase * 2.03) * rushEnvelope * 0.075f;
            samples[i] = Mathf.Clamp((impact + sub + (whoosh + risingTone + highShimmer) * attack), -0.98f, 0.98f);
        }

        AudioClip clip = AudioClip.Create("VOIDLINE_Boost_Ignition_ImpactRush", count, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static AudioClip MakeBoostLoop()
    {
        const int sampleRate = 22050;
        const float duration = 0.75f;
        int count = Mathf.RoundToInt(sampleRate * duration);
        float[] samples = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)count;
            double phaseA = 2.0 * Math.PI * 62.0 * t;
            double phaseB = 2.0 * Math.PI * 91.0 * t;
            double phaseC = 2.0 * Math.PI * 137.0 * t;
            double phaseD = 2.0 * Math.PI * (260.0 * t + 11.0 * t * t);
            float pulse = 0.86f + 0.14f * (float)Math.Sin(2.0 * Math.PI * 3.0 * t);
            float grind = (float)Math.Sin(phaseA) * 0.22f
                + (float)Math.Sin(phaseB) * 0.11f
                + (float)Math.Sin(phaseC) * 0.055f;
            float risingWhine = (float)Math.Sin(phaseD) * 0.055f;
            samples[i] = pulse * (grind + risingWhine);
        }
        AudioClip clip = AudioClip.Create("VOIDLINE_BoostLoop", count, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static AudioClip MakeSweep(string clipName, float duration, float startHz,
        float endHz, float amplitude, float noiseMix, int seed)
    {
        const int sampleRate = 22050;
        int count = Mathf.Max(1, Mathf.RoundToInt(sampleRate * duration));
        float[] samples = new float[count];
        System.Random random = new System.Random(seed);
        double phase = 0.0;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)count;
            float frequency = Mathf.Lerp(startHz, endHz, t * t);
            phase += 2.0 * Math.PI * frequency / sampleRate;
            float envelope = Mathf.Pow(Mathf.Sin(Mathf.PI * t), 0.7f);
            float tone = (float)Math.Sin(phase);
            float noise = (float)(random.NextDouble() * 2.0 - 1.0);
            samples[i] = Mathf.Clamp((tone * (1f - noiseMix) + noise * noiseMix) * envelope * amplitude, -1f, 1f);
        }
        AudioClip clip = AudioClip.Create("VOIDLINE_" + clipName, count, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
