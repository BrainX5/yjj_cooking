using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

[RequireComponent(typeof(AudioSource))]
public class CookingAudioController : MonoBehaviour
{
    private enum ProceduralSound
    {
        Pickup,
        MushroomDrop,
        Stir,
        FireTap,
        FishPress,
        FishEscape,
        FishCaught,
        FishFlip,
        Seasoning,
        DishComplete,
        UiClose
    }

    private static CookingAudioController instance;

    [Header("Optional Real Clips")]
    [SerializeField] private AudioClip pickupClip;
    [SerializeField] private AudioClip mushroomDropClip;
    [SerializeField] private AudioClip stirClip;
    [SerializeField] private AudioClip fireTapClip;
    [SerializeField] private AudioClip fishPressClip;
    [SerializeField] private AudioClip fishEscapeClip;
    [SerializeField] private AudioClip fishCaughtClip;
    [SerializeField] private AudioClip fishFlipClip;
    [SerializeField] private AudioClip seasoningClip;
    [SerializeField] private AudioClip dishCompleteClip;
    [SerializeField] private AudioClip uiCloseClip;
    [SerializeField] private AudioClip fireLoopOverrideClip;
    [SerializeField] private AudioClip forestAmbienceClip;
    [SerializeField] private AudioClip riverLoopClip;
    [SerializeField] private AudioClip walkingBgmClipOverride;
    [SerializeField] private AudioClip focusBgmClipOverride;

    [Header("Mix")]
    [SerializeField] private float masterVolume = 0.45f;
    [SerializeField] private float forestAmbienceVolume = 0f;
    [SerializeField] private float riverLoopVolume = 0.14f;
    [SerializeField] private float walkingBgmVolume = 0.72f;
    [SerializeField] private float focusMinVolume = 0.5f;
    [SerializeField] private float focusMaxVolume = 0.8f;

    [Header("Focus Music Feedback")]
    [SerializeField] private float musicFadeDuration = 2f;
    [SerializeField] private float focusMinCutoffFrequency = 800f;
    [SerializeField] private float focusMaxCutoffFrequency = 8000f;
    [SerializeField] private float lowAttentionThreshold = 60f;
    [SerializeField] private float highAttentionThreshold = 80f;
    [SerializeField] private float lowBandSmoothingSeconds = 5f;
    [SerializeField] private float midBandSmoothingSeconds = 3f;
    [SerializeField] private float highBandSmoothingSeconds = 2f;
    [SerializeField] private float lowAttentionPitch = 0.82f;
    [SerializeField] private float midAttentionPitch = 1f;
    [SerializeField] private float highAttentionPitch = 1.08f;

    private readonly Dictionary<ProceduralSound, AudioClip> generatedClips = new Dictionary<ProceduralSound, AudioClip>();
    private AudioSource audioSource;
    private AudioSource fireLoopSource;
    private AudioSource forestLoopSource;
    private AudioSource riverLoopSource;
    private AudioSource walkingMusicSource;
    private AudioSource focusMusicSource;
    private AudioLowPassFilter focusLowPassFilter;
    private AudioClip fireLoopClip;
    private bool cookingMode;
    private bool hasAttentionFeedback;
    private bool focusMusicLoadRequested;
    private float currentAttentionValue;
    private float targetWalkingVolume;
    private float targetFocusMusicVolume;
    private float targetFocusFeedbackVolume;
    private float targetFocusCutoffFrequency;
    private float currentFocusFeedbackVolume;
    private float currentFocusCutoffFrequency;
    private float currentFocusPitch = 1f;
    private float walkingVolumeVelocity;
    private float focusMusicVolumeVelocity;
    private float focusFeedbackVolumeVelocity;
    private float focusCutoffVelocity;
    private float focusPitchVelocity;

    public static CookingAudioController Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<CookingAudioController>();
            }

            return instance;
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        TryLoadClipsFromResources();
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;

        fireLoopSource = gameObject.AddComponent<AudioSource>();
        fireLoopSource.playOnAwake = false;
        fireLoopSource.loop = true;
        fireLoopSource.spatialBlend = 0f;
        fireLoopSource.volume = 0f;
        fireLoopClip = fireLoopOverrideClip != null ? fireLoopOverrideClip : CreateFireLoopClip("Sfx_FireLoop", 1.2f, 0.08f);
        fireLoopSource.clip = fireLoopClip;

        forestLoopSource = gameObject.AddComponent<AudioSource>();
        forestLoopSource.playOnAwake = false;
        forestLoopSource.loop = true;
        forestLoopSource.spatialBlend = 0f;
        forestLoopSource.volume = 0f;
        forestLoopSource.clip = forestAmbienceClip;
        if (forestLoopSource.clip != null && forestAmbienceVolume > 0.001f)
        {
            forestLoopSource.Play();
        }

        riverLoopSource = gameObject.AddComponent<AudioSource>();
        riverLoopSource.playOnAwake = false;
        riverLoopSource.loop = true;
        riverLoopSource.spatialBlend = 0f;
        riverLoopSource.volume = 0f;
        riverLoopSource.clip = riverLoopClip;

        walkingMusicSource = CreateChildLoopSource("行走背景音乐");
        focusMusicSource = CreateChildLoopSource("专注模式音乐");
        focusLowPassFilter = focusMusicSource.gameObject.AddComponent<AudioLowPassFilter>();
        currentFocusCutoffFrequency = focusMaxCutoffFrequency;
        targetFocusCutoffFrequency = currentFocusCutoffFrequency;
        focusLowPassFilter.cutoffFrequency = currentFocusCutoffFrequency;

        currentFocusFeedbackVolume = Mathf.Lerp(focusMinVolume, focusMaxVolume, 0.6f);
        targetFocusFeedbackVolume = currentFocusFeedbackVolume;
        targetWalkingVolume = walkingBgmVolume * masterVolume;
        currentFocusPitch = midAttentionPitch;

        StartCoroutine(LoadFocusMusicClips());
    }

    private void Update()
    {
        UpdateFocusFeedbackTargets();
        UpdateMusicMixing();
    }

    public void PlayPickup()
    {
        PlayClipOrGenerated(pickupClip, ProceduralSound.Pickup, 0.6f);
    }

    public void PlayMushroomDrop()
    {
        PlayClipOrGenerated(mushroomDropClip, ProceduralSound.MushroomDrop, 0.52f);
    }

    public void PlayStir()
    {
        PlayClipOrGenerated(stirClip, ProceduralSound.Stir, 0.48f);
    }

    public void PlayFireTap()
    {
        PlayClipOrGenerated(fireTapClip, ProceduralSound.FireTap, 1f);
    }

    public void UpdateFireLoop(float fireStrength)
    {
        if (fireLoopSource == null)
        {
            return;
        }

        var targetVolume = Mathf.Clamp01(fireStrength) * masterVolume * 0.9f;
        fireLoopSource.volume = Mathf.MoveTowards(fireLoopSource.volume, targetVolume, Time.deltaTime * 1.8f);

        if (targetVolume > 0.01f)
        {
            if (!fireLoopSource.isPlaying && fireLoopClip != null)
            {
                fireLoopSource.Play();
            }

            fireLoopSource.pitch = 0.9f + Mathf.Clamp01(fireStrength) * 0.45f;
        }
        else if (fireLoopSource.isPlaying && fireLoopSource.volume <= 0.01f)
        {
            fireLoopSource.Stop();
        }
    }

    public void UpdateRiverLoop(float riverStrength)
    {
        if (riverLoopSource == null || riverLoopSource.clip == null)
        {
            return;
        }

        var targetVolume = Mathf.Clamp01(riverStrength) * riverLoopVolume * masterVolume;
        riverLoopSource.volume = Mathf.MoveTowards(riverLoopSource.volume, targetVolume, Time.deltaTime * 1.5f);

        if (targetVolume > 0.01f)
        {
            if (!riverLoopSource.isPlaying)
            {
                riverLoopSource.Play();
            }
        }
        else if (riverLoopSource.isPlaying && riverLoopSource.volume <= 0.01f)
        {
            riverLoopSource.Stop();
        }
    }

    public void PlayFishPress()
    {
        PlayClipOrGenerated(fishPressClip, ProceduralSound.FishPress, 0.35f);
    }

    public void PlayFishEscape()
    {
        PlayClipOrGenerated(fishEscapeClip, ProceduralSound.FishEscape, 0.58f);
    }

    public void PlayFishCaught()
    {
        PlayClipOrGenerated(fishCaughtClip, ProceduralSound.FishCaught, 0.62f);
    }

    public void PlayFishFlip()
    {
        PlayClipOrGenerated(fishFlipClip, ProceduralSound.FishFlip, 0.56f);
    }

    public void PlaySeasoning()
    {
        PlayClipOrGenerated(seasoningClip, ProceduralSound.Seasoning, 0.44f);
    }

    public void PlayDishComplete()
    {
        PlayClipOrGenerated(dishCompleteClip, ProceduralSound.DishComplete, 0.66f);
    }

    public void PlayUiClose()
    {
        PlayClipOrGenerated(uiCloseClip, ProceduralSound.UiClose, 0.42f);
    }

    public void SetCookingMode(bool enabled)
    {
        if (cookingMode == enabled)
        {
            return;
        }

        cookingMode = enabled;
        RefreshMusicTargets();
        EnsureMusicPlaybackState();
    }

    public void UpdateFocusFeedback(float attentionValue, bool hasLiveAttention)
    {
        currentAttentionValue = Mathf.Clamp(attentionValue, 0f, 100f);
        hasAttentionFeedback = hasLiveAttention;
        RefreshMusicTargets();
    }

    private void PlayClipOrGenerated(AudioClip clip, ProceduralSound sound, float volume)
    {
        if (audioSource == null)
        {
            return;
        }

        var selectedClip = clip != null ? clip : GetGeneratedClip(sound);
        if (selectedClip == null)
        {
            return;
        }

        audioSource.PlayOneShot(selectedClip, volume * masterVolume);
    }

    private AudioClip GetGeneratedClip(ProceduralSound sound)
    {
        if (generatedClips.TryGetValue(sound, out var cachedClip))
        {
            return cachedClip;
        }

        var clip = CreateGeneratedClip(sound);
        generatedClips[sound] = clip;
        return clip;
    }

    private AudioClip CreateGeneratedClip(ProceduralSound sound)
    {
        switch (sound)
        {
            case ProceduralSound.Pickup:
                return CreateLayeredClip(
                    "Sfx_Pickup",
                    CreateToneSweep("PickupTone", 680f, 940f, 0.13f, 0.08f),
                    CreateNoiseBurst("PickupLeaf", 0.08f, 0.035f));
            case ProceduralSound.MushroomDrop:
                return CreateLayeredClip(
                    "Sfx_MushroomDrop",
                    CreateToneSweep("DropTone", 240f, 140f, 0.18f, 0.08f),
                    CreateNoiseBurst("DropRustle", 0.12f, 0.06f));
            case ProceduralSound.Stir:
                return CreateFilteredNoise("Sfx_Stir", 0.22f, 0.08f, 0.78f);
            case ProceduralSound.FireTap:
                return CreateLayeredClip(
                    "Sfx_FireTap",
                    CreateFilteredNoise("FireCrackle", 0.14f, 0.14f, 0.88f),
                    CreateToneSweep("FireHeat", 140f, 230f, 0.11f, 0.06f));
            case ProceduralSound.FishPress:
                return CreateLayeredClip(
                    "Sfx_FishPress",
                    CreateToneSweep("FishGrip", 260f, 320f, 0.08f, 0.05f),
                    CreateFilteredNoise("FishSplashTick", 0.06f, 0.04f, 0.85f));
            case ProceduralSound.FishEscape:
                return CreateLayeredClip(
                    "Sfx_FishEscape",
                    CreateToneSweep("FishEscapeTone", 420f, 180f, 0.24f, 0.08f),
                    CreateFilteredNoise("FishSplash", 0.22f, 0.08f, 0.9f));
            case ProceduralSound.FishCaught:
                return CreateLayeredClip(
                    "Sfx_FishCaught",
                    CreateDualTone("FishCaughtTone", 460f, 720f, 0.26f, 0.11f),
                    CreateFilteredNoise("FishCaughtSplash", 0.12f, 0.04f, 0.82f));
            case ProceduralSound.FishFlip:
                return CreateLayeredClip(
                    "Sfx_FishFlip",
                    CreateToneSweep("FishFlipTone", 180f, 120f, 0.16f, 0.06f),
                    CreateFilteredNoise("FishFlipSizzle", 0.16f, 0.05f, 0.86f));
            case ProceduralSound.Seasoning:
                return CreateFilteredNoise("Sfx_Seasoning", 0.16f, 0.06f, 0.68f);
            case ProceduralSound.DishComplete:
                return CreateLayeredClip(
                    "Sfx_DishComplete",
                    CreateDualTone("DishCompleteTone", 520f, 820f, 0.36f, 0.1f),
                    CreateToneSweep("DishCompleteSparkle", 900f, 1200f, 0.18f, 0.04f));
            case ProceduralSound.UiClose:
                return CreateToneSweep("Sfx_UiClose", 420f, 620f, 0.1f, 0.08f);
            default:
                return null;
        }
    }

    private AudioClip CreateToneSweep(string clipName, float startFrequency, float endFrequency, float duration, float amplitude)
    {
        const int sampleRate = 44100;
        var sampleCount = Mathf.Max(1, Mathf.CeilToInt(sampleRate * duration));
        var samples = new float[sampleCount];
        var phase = 0f;

        for (var i = 0; i < sampleCount; i++)
        {
            var t = i / (float)(sampleCount - 1);
            var frequency = Mathf.Lerp(startFrequency, endFrequency, t);
            phase += 2f * Mathf.PI * frequency / sampleRate;
            var envelope = Mathf.Sin(t * Mathf.PI);
            samples[i] = Mathf.Sin(phase) * envelope * amplitude;
        }

        var clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip CreateDualTone(string clipName, float firstFrequency, float secondFrequency, float duration, float amplitude)
    {
        const int sampleRate = 44100;
        var sampleCount = Mathf.Max(1, Mathf.CeilToInt(sampleRate * duration));
        var samples = new float[sampleCount];
        var phaseA = 0f;
        var phaseB = 0f;

        for (var i = 0; i < sampleCount; i++)
        {
            var t = i / (float)(sampleCount - 1);
            phaseA += 2f * Mathf.PI * firstFrequency / sampleRate;
            phaseB += 2f * Mathf.PI * secondFrequency / sampleRate;
            var envelope = Mathf.Sin(t * Mathf.PI);
            samples[i] = (Mathf.Sin(phaseA) * 0.65f + Mathf.Sin(phaseB) * 0.35f) * envelope * amplitude;
        }

        var clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip CreateNoiseBurst(string clipName, float duration, float amplitude)
    {
        const int sampleRate = 44100;
        var sampleCount = Mathf.Max(1, Mathf.CeilToInt(sampleRate * duration));
        var samples = new float[sampleCount];

        for (var i = 0; i < sampleCount; i++)
        {
            var t = i / (float)(sampleCount - 1);
            var envelope = Mathf.Sin(t * Mathf.PI);
            samples[i] = Random.Range(-1f, 1f) * envelope * amplitude;
        }

        var clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip CreateFilteredNoise(string clipName, float duration, float amplitude, float smoothing)
    {
        const int sampleRate = 44100;
        var sampleCount = Mathf.Max(1, Mathf.CeilToInt(sampleRate * duration));
        var samples = new float[sampleCount];
        var filtered = 0f;

        for (var i = 0; i < sampleCount; i++)
        {
            var t = i / (float)(sampleCount - 1);
            var envelope = Mathf.Sin(t * Mathf.PI);
            filtered = Mathf.Lerp(filtered, Random.Range(-1f, 1f), 1f - smoothing);
            samples[i] = filtered * envelope * amplitude;
        }

        var clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip CreateLayeredClip(string clipName, params AudioClip[] layers)
    {
        if (layers == null || layers.Length == 0)
        {
            return null;
        }

        const int sampleRate = 44100;
        var maxSamples = 0;
        for (var i = 0; i < layers.Length; i++)
        {
            if (layers[i] != null && layers[i].samples > maxSamples)
            {
                maxSamples = layers[i].samples;
            }
        }

        if (maxSamples <= 0)
        {
            return null;
        }

        var mixed = new float[maxSamples];
        for (var i = 0; i < layers.Length; i++)
        {
            var layer = layers[i];
            if (layer == null)
            {
                continue;
            }

            var data = new float[layer.samples];
            layer.GetData(data, 0);
            for (var j = 0; j < data.Length; j++)
            {
                mixed[j] += data[j];
            }
        }

        for (var i = 0; i < mixed.Length; i++)
        {
            mixed[i] = Mathf.Clamp(mixed[i], -1f, 1f);
        }

        var clip = AudioClip.Create(clipName, maxSamples, 1, sampleRate, false);
        clip.SetData(mixed, 0);
        return clip;
    }

    private AudioClip CreateFireLoopClip(string clipName, float duration, float amplitude)
    {
        const int sampleRate = 44100;
        var sampleCount = Mathf.Max(1, Mathf.CeilToInt(sampleRate * duration));
        var samples = new float[sampleCount];
        var filteredA = 0f;
        var filteredB = 0f;
        var phase = 0f;

        for (var i = 0; i < sampleCount; i++)
        {
            filteredA = Mathf.Lerp(filteredA, Random.Range(-1f, 1f), 0.08f);
            filteredB = Mathf.Lerp(filteredB, Random.Range(-1f, 1f), 0.02f);
            phase += 2f * Mathf.PI * 70f / sampleRate;
            var lowTone = Mathf.Sin(phase) * 0.18f;
            samples[i] = Mathf.Clamp((filteredA * 0.65f + filteredB * 0.35f + lowTone) * amplitude, -1f, 1f);
        }

        var clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private void TryLoadClipsFromResources()
    {
        pickupClip = pickupClip != null ? pickupClip : LoadClip("pickup_mushroom");
        mushroomDropClip = mushroomDropClip != null ? mushroomDropClip : LoadClip("drop_mushroom_into_pot");
        stirClip = stirClip != null ? stirClip : LoadClip("stir_soup");
        fireTapClip = fireTapClip != null ? fireTapClip : LoadClip("fire_boost_tap");
        fishPressClip = fishPressClip != null ? fishPressClip : LoadClip("fish_catch_press");
        fishEscapeClip = fishEscapeClip != null ? fishEscapeClip : LoadClip("fish_escape");
        fishCaughtClip = fishCaughtClip != null ? fishCaughtClip : LoadClip("fish_catch_success");
        fishFlipClip = fishFlipClip != null ? fishFlipClip : LoadClip("fish_flip");
        seasoningClip = seasoningClip != null ? seasoningClip : LoadClip("seasoning_sprinkle");
        dishCompleteClip = dishCompleteClip != null ? dishCompleteClip : LoadClip("dish_complete");
        uiCloseClip = uiCloseClip != null ? uiCloseClip : LoadClip("ui_confirm_close");
        fireLoopOverrideClip = fireLoopOverrideClip != null ? fireLoopOverrideClip : LoadClip("fire_loop");
        forestAmbienceClip = forestAmbienceClip != null ? forestAmbienceClip : LoadClip("forest_ambience_loop");
        riverLoopClip = riverLoopClip != null ? riverLoopClip : LoadClip("river_loop");
        walkingBgmClipOverride = walkingBgmClipOverride != null ? walkingBgmClipOverride : LoadClip("bgm");
        focusBgmClipOverride = focusBgmClipOverride != null ? focusBgmClipOverride : LoadClip("Gentle Focus");
    }

    private AudioClip LoadClip(string clipName)
    {
        return Resources.Load<AudioClip>($"Audio/Cooking/{clipName}");
    }

    private IEnumerator LoadFocusMusicClips()
    {
        if (focusMusicLoadRequested)
        {
            yield break;
        }

        focusMusicLoadRequested = true;

        if (walkingBgmClipOverride == null)
        {
            yield return LoadExternalClip("bgm.mp3", clip => walkingBgmClipOverride = clip);
        }

        if (focusBgmClipOverride == null)
        {
            yield return LoadExternalClip("Gentle Focus.mp3", clip => focusBgmClipOverride = clip);
        }

        walkingMusicSource.clip = walkingBgmClipOverride;
        focusMusicSource.clip = focusBgmClipOverride;

        RefreshMusicTargets();
        EnsureMusicPlaybackState();
    }

    private IEnumerator LoadExternalClip(string fileName, System.Action<AudioClip> assignClip)
    {
        var path = ResolveExternalAudioPath(fileName);
        if (string.IsNullOrWhiteSpace(path))
        {
            Debug.LogWarning($"CookingAudioController could not find external music file: {fileName}", this);
            yield break;
        }

        using (var request = UnityWebRequestMultimedia.GetAudioClip(new System.Uri(path).AbsoluteUri, AudioType.MPEG))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"CookingAudioController failed to load {fileName}: {request.error}", this);
                yield break;
            }

            var clip = DownloadHandlerAudioClip.GetContent(request);
            if (clip == null)
            {
                Debug.LogWarning($"CookingAudioController loaded an empty clip for {fileName}.", this);
                yield break;
            }

            clip.name = Path.GetFileNameWithoutExtension(fileName);
            assignClip?.Invoke(clip);
        }
    }

    private string ResolveExternalAudioPath(string fileName)
    {
        var candidates = new List<string>();
        var currentDirectory = Directory.GetCurrentDirectory();
        if (!string.IsNullOrWhiteSpace(currentDirectory))
        {
            candidates.Add(Path.Combine(currentDirectory, fileName));
        }

        var dataDirectory = Application.dataPath;
        if (!string.IsNullOrWhiteSpace(dataDirectory))
        {
            candidates.Add(Path.Combine(dataDirectory, fileName));

            var parentDirectory = Directory.GetParent(dataDirectory);
            if (parentDirectory != null)
            {
                candidates.Add(Path.Combine(parentDirectory.FullName, fileName));
            }
        }

        for (var i = 0; i < candidates.Count; i++)
        {
            if (File.Exists(candidates[i]))
            {
                return candidates[i];
            }
        }

        return null;
    }

    private void RefreshMusicTargets()
    {
        targetWalkingVolume = cookingMode ? 0f : walkingBgmVolume * masterVolume;
        targetFocusMusicVolume = cookingMode ? currentFocusFeedbackVolume * masterVolume : 0f;
    }

    private AudioSource CreateChildLoopSource(string childName)
    {
        var child = new GameObject(childName);
        child.transform.SetParent(transform, false);

        var source = child.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.volume = 0f;
        return source;
    }

    private void EnsureMusicPlaybackState()
    {
        if (walkingMusicSource != null &&
            walkingMusicSource.clip != null &&
            !walkingMusicSource.isPlaying)
        {
            walkingMusicSource.Play();
        }

        if (focusMusicSource != null &&
            focusMusicSource.clip != null &&
            !focusMusicSource.isPlaying)
        {
            focusMusicSource.Play();
        }
    }

    private void UpdateFocusFeedbackTargets()
    {
        var normalizedAttention = hasAttentionFeedback
            ? Mathf.Clamp01(currentAttentionValue / 100f)
            : Mathf.InverseLerp(0f, 100f, lowAttentionThreshold);
        var targetPitch = ResolveFocusPitch(currentAttentionValue, normalizedAttention);

        targetFocusCutoffFrequency = Mathf.Lerp(
            focusMinCutoffFrequency,
            focusMaxCutoffFrequency,
            normalizedAttention);
        targetFocusFeedbackVolume = Mathf.Lerp(
            focusMinVolume,
            focusMaxVolume,
            normalizedAttention);

        var smoothingSeconds = ResolveFeedbackSmoothingSeconds(currentAttentionValue);
        currentFocusCutoffFrequency = Mathf.SmoothDamp(
            currentFocusCutoffFrequency,
            targetFocusCutoffFrequency,
            ref focusCutoffVelocity,
            smoothingSeconds,
            Mathf.Infinity,
            Time.unscaledDeltaTime);
        currentFocusFeedbackVolume = Mathf.SmoothDamp(
            currentFocusFeedbackVolume,
            targetFocusFeedbackVolume,
            ref focusFeedbackVolumeVelocity,
            smoothingSeconds,
            Mathf.Infinity,
            Time.unscaledDeltaTime);
        currentFocusPitch = Mathf.SmoothDamp(
            currentFocusPitch,
            targetPitch,
            ref focusPitchVelocity,
            smoothingSeconds,
            Mathf.Infinity,
            Time.unscaledDeltaTime);

        if (focusLowPassFilter != null)
        {
            focusLowPassFilter.cutoffFrequency = currentFocusCutoffFrequency;
        }

        if (focusMusicSource != null)
        {
            focusMusicSource.pitch = currentFocusPitch;
        }

        RefreshMusicTargets();
    }

    private void UpdateMusicMixing()
    {
        if (walkingMusicSource != null)
        {
            walkingMusicSource.volume = Mathf.SmoothDamp(
                walkingMusicSource.volume,
                targetWalkingVolume,
                ref walkingVolumeVelocity,
                musicFadeDuration,
                Mathf.Infinity,
                Time.unscaledDeltaTime);
        }

        if (focusMusicSource != null)
        {
            focusMusicSource.volume = Mathf.SmoothDamp(
                focusMusicSource.volume,
                targetFocusMusicVolume,
                ref focusMusicVolumeVelocity,
                musicFadeDuration,
                Mathf.Infinity,
                Time.unscaledDeltaTime);
        }
    }

    private float ResolveFeedbackSmoothingSeconds(float attentionValue)
    {
        if (attentionValue >= highAttentionThreshold)
        {
            return Mathf.Max(0.05f, highBandSmoothingSeconds);
        }

        if (attentionValue >= lowAttentionThreshold)
        {
            return Mathf.Max(0.05f, midBandSmoothingSeconds);
        }

        return Mathf.Max(0.05f, lowBandSmoothingSeconds);
    }

    private float ResolveFocusPitch(float attentionValue, float normalizedAttention)
    {
        if (attentionValue <= 0f)
        {
            return Mathf.Lerp(lowAttentionPitch, midAttentionPitch, normalizedAttention);
        }

        if (attentionValue < lowAttentionThreshold)
        {
            var lowNormalized = Mathf.InverseLerp(0f, lowAttentionThreshold, attentionValue);
            return Mathf.Lerp(lowAttentionPitch, midAttentionPitch, lowNormalized);
        }

        if (attentionValue < highAttentionThreshold)
        {
            var midNormalized = Mathf.InverseLerp(lowAttentionThreshold, highAttentionThreshold, attentionValue);
            return Mathf.Lerp(midAttentionPitch, 1.03f, midNormalized);
        }

        var highNormalized = Mathf.InverseLerp(highAttentionThreshold, 100f, attentionValue);
        return Mathf.Lerp(1.03f, highAttentionPitch, highNormalized);
    }
}
