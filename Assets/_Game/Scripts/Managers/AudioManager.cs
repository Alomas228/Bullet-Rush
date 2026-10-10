using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// Приоритет звука. Когда все голоса заняты, вытесняется голос
/// с наименьшим приоритетом: важные звуки не глохнут под массой
/// одинаковых попаданий/подборов.
public enum SfxPriority
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Library")]
    [SerializeField] private SFXLibrary sfxLibrary;

    [Header("Music")]
    [SerializeField] private AudioClip mainMusic;
    [SerializeField] private AudioClip bossMusic;

    [Header("Settings")]
    [Range(0f, 1f)]
    [SerializeField] private float sfxVolume = 1f;
    [Range(0f, 1f)]
    [SerializeField] private float uiVolume = 0.8f;
    [Range(0f, 1f)]
    [SerializeField] private float musicVolume = 0.5f;

    [Header("Voice Pool")]
    [Tooltip("Сколько SFX могут звучать одновременно. Каждый голос — " +
        "отдельный AudioSource, поэтому лимит реальных голосов движка " +
        "больше не упирается в один общий источник.")]
    [Range(4, 64)]
    [SerializeField] private int sfxVoices = 24;

    [Tooltip("Минимальный интервал между повторами одного клипа для " +
        "Medium. Защищает от «стены» одинаковых звуков при высокой " +
        "плотности врагов.")]
    [Range(0f, 0.3f)]
    [SerializeField] private float repeatCooldownMedium = 0.035f;

    [Tooltip("Минимальный интервал между повторами одного клипа для " +
        "Low (попадания, рикошеты, подборы).")]
    [Range(0f, 0.3f)]
    [SerializeField] private float repeatCooldownLow = 0.09f;

    [Header("Spatial")]
    [Tooltip("Позиционные SFX дальше этого расстояния от слушателя не " +
        "воспроизводятся и не занимают голоса (0 — без ограничения).")]
    [SerializeField] private float maxSfxDistance = 28f;

    [Tooltip("С какой дистанции позиционный звук начинает стихать.")]
    [SerializeField] private float falloffStartDistance = 8f;

    private const float MinPitch = 0.9f;
    private const float MaxPitch = 1.1f;

    private AudioSource musicSource;

    private AudioSource[] voices;
    private int[] voicePriorities;
    private float[] voiceEndTimes;

    private readonly Dictionary<AudioClip, float> lastPlayTimes =
        new Dictionary<AudioClip, float>();

    private Transform listenerTransform;
    private Transform cameraTransform;
    private float nextListenerSearchTime;
    private bool listenerSearched;

    private Coroutine fadeCoroutine;

    public SFXLibrary SFXLibrary => sfxLibrary;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        ApplyLowLatencyDspBuffer();

        BuildVoicePool();

        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = true;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        ApplyPersistedVolumes();

        if (mainMusic != null)
            PlayMusic(mainMusic);
    }

    private void BuildVoicePool()
    {
        int count = Mathf.Clamp(sfxVoices, 4, 64);

        voices = new AudioSource[count];
        voicePriorities = new int[count];
        voiceEndTimes = new float[count];

        for (int i = 0; i < count; i++)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();

            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = Mathf.Max(1f, falloffStartDistance);
            source.maxDistance = Mathf.Max(maxSfxDistance, 1f);

            voices[i] = source;
        }
    }

    private void ApplyLowLatencyDspBuffer()
    {
        AudioConfiguration config = AudioSettings.GetConfiguration();

        if (config.dspBufferSize == 256)
            return;

        config.dspBufferSize = 256;

        if (!AudioSettings.Reset(config))
        {
            Debug.LogWarning(
                "AudioManager: не удалось понизить DSP буфер до 256 сэмплов."
            );
        }
    }

    private void ApplyPersistedVolumes()
    {
        if (SettingsManager.Instance == null)
            return;

        sfxVolume = SettingsManager.Instance.SfxVolume;
        uiVolume = SettingsManager.Instance.UiVolume;
        musicVolume = SettingsManager.Instance.MusicVolume;

        if (musicSource != null)
            musicSource.volume = musicVolume;
    }

    public void PlaySFX(
        AudioClip clip,
        float volumeScale = 1f,
        SfxPriority priority = SfxPriority.Medium)
    {
        PlayVoice(
            clip,
            sfxVolume,
            volumeScale,
            priority,
            Vector3.zero,
            false,
            false
        );
    }

    public void PlaySFXAt(
        AudioClip clip,
        Vector3 position,
        float volumeScale = 1f,
        SfxPriority priority = SfxPriority.Medium)
    {
        PlayVoice(
            clip,
            sfxVolume,
            volumeScale,
            priority,
            position,
            true,
            false
        );
    }

    public void PlaySFXVariation(
        AudioClip clip,
        float volumeScale = 1f,
        SfxPriority priority = SfxPriority.Medium)
    {
        PlayVoice(
            clip,
            sfxVolume,
            volumeScale,
            priority,
            Vector3.zero,
            false,
            true
        );
    }

    public void PlaySFXVariationAt(
        AudioClip clip,
        Vector3 position,
        float volumeScale = 1f,
        SfxPriority priority = SfxPriority.Medium)
    {
        PlayVoice(
            clip,
            sfxVolume,
            volumeScale,
            priority,
            position,
            true,
            true
        );
    }

    public void PlayUI(
        AudioClip clip,
        float volumeScale = 1f)
    {
        PlayVoice(
            clip,
            uiVolume,
            volumeScale,
            SfxPriority.High,
            Vector3.zero,
            false,
            false
        );
    }

    public void StopAllSFX()
    {
        if (voices == null)
            return;

        for (int i = 0; i < voices.Length; i++)
        {
            if (voices[i] == null)
                continue;

            voices[i].Stop();
            voices[i].clip = null;
            voicePriorities[i] = 0;
            voiceEndTimes[i] = 0f;
        }

        lastPlayTimes.Clear();
    }

    private void PlayVoice(
        AudioClip clip,
        float masterVolume,
        float volumeScale,
        SfxPriority priority,
        Vector3 position,
        bool positional,
        bool randomizePitch)
    {
        if (clip == null || voices == null || voices.Length == 0)
            return;

        if (masterVolume <= 0f || volumeScale <= 0f)
            return;

        if (positional && !IsWithinRange(position))
            return;

        float now = Time.unscaledTime;

        if (!CanRepeat(clip, priority, now))
            return;

        int index = AcquireVoice(priority);

        if (index < 0)
            return;

        AudioSource source = voices[index];

        if (source == null)
            return;

        source.Stop();
        source.clip = clip;
        source.pitch = randomizePitch
            ? Random.Range(MinPitch, MaxPitch)
            : 1f;
        source.priority = ToUnityPriority(priority);
        source.spatialBlend = positional ? 1f : 0f;

        if (positional)
        {
            source.minDistance = Mathf.Max(1f, falloffStartDistance);
            source.maxDistance = Mathf.Max(maxSfxDistance, 1f);
            source.transform.position = position;
        }

        source.volume = Mathf.Max(
            0f,
            masterVolume * GetClipVolume(clip) * volumeScale
        );

        source.Play();

        voicePriorities[index] = (int)priority;
        voiceEndTimes[index] = now +
            clip.length / Mathf.Max(0.01f, Mathf.Abs(source.pitch));
        lastPlayTimes[clip] = now;
    }

    private bool CanRepeat(
        AudioClip clip,
        SfxPriority priority,
        float now)
    {
        float cooldown = 0f;

        switch (priority)
        {
            case SfxPriority.Low:
                cooldown = repeatCooldownLow;
                break;

            case SfxPriority.Medium:
                cooldown = repeatCooldownMedium;
                break;
        }

        if (cooldown <= 0f)
            return true;

        if (lastPlayTimes.TryGetValue(clip, out float last) &&
            now - last < cooldown)
        {
            return false;
        }

        return true;
    }

    private int AcquireVoice(SfxPriority priority)
    {
        int candidateIndex = -1;
        int candidatePriority = int.MaxValue;
        float candidateEndTime = float.MaxValue;

        for (int i = 0; i < voices.Length; i++)
        {
            AudioSource source = voices[i];

            if (source == null || !source.isPlaying)
                return i;

            float endTime = voiceEndTimes[i];

            if (voicePriorities[i] < candidatePriority ||
                (voicePriorities[i] == candidatePriority &&
                 endTime < candidateEndTime))
            {
                candidatePriority = voicePriorities[i];
                candidateEndTime = endTime;
                candidateIndex = i;
            }
        }

        if (candidateIndex < 0)
            return -1;

        // Голос с меньшим приоритетом, чем у нового звука, не трогаем —
        // новый звук просто пропускается.
        if (candidatePriority < (int)priority)
            return -1;

        return candidateIndex;
    }

    private static int ToUnityPriority(SfxPriority priority)
    {
        switch (priority)
        {
            case SfxPriority.Critical:
                return 32;

            case SfxPriority.High:
                return 64;

            case SfxPriority.Medium:
                return 128;

            default:
                return 192;
        }
    }

    private bool IsWithinRange(Vector3 position)
    {
        if (maxSfxDistance <= 0f)
            return true;

        Vector3 listener = GetListenerPosition();
        float sqrDistance = (position - listener).sqrMagnitude;

        return sqrDistance <= maxSfxDistance * maxSfxDistance;
    }

    private Vector3 GetListenerPosition()
    {
        if (listenerTransform == null)
            listenerTransform = ResolveListener();

        if (listenerTransform != null)
            return listenerTransform.position;

        if (cameraTransform == null && !listenerSearched)
        {
            Camera main = Camera.main;

            if (main != null)
                cameraTransform = main.transform;
        }

        return cameraTransform != null
            ? cameraTransform.position
            : Vector3.zero;
    }

    private Transform ResolveListener()
    {
        listenerSearched = true;

        if (Time.unscaledTime < nextListenerSearchTime)
            return null;

        nextListenerSearchTime = Time.unscaledTime + 1f;

        AudioListener listener =
            FindAnyObjectByType<AudioListener>();

        return listener != null ? listener.transform : null;
    }

    private float GetClipVolume(AudioClip clip)
    {
        if (sfxLibrary == null)
            return 1f;

        return sfxLibrary.GetClipVolume(clip);
    }

    public void PlayMusic(AudioClip clip)
    {
        if (clip == null)
            return;

        if (musicSource.clip == clip && musicSource.isPlaying)
            return;

        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);

        fadeCoroutine = StartCoroutine(
            CrossfadeMusic(clip)
        );
    }

    public void StopMusic()
    {
        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);

        fadeCoroutine = StartCoroutine(FadeOutMusic());
    }

    private IEnumerator CrossfadeMusic(AudioClip newClip)
    {
        if (musicSource.isPlaying)
        {
            float fadeTime = 0.5f;
            float timer = 0f;

            while (timer < fadeTime)
            {
                timer += Time.unscaledDeltaTime;
                musicSource.volume =
                    Mathf.Lerp(musicVolume, 0f, timer / fadeTime);
                yield return null;
            }
        }

        musicSource.clip = newClip;
        musicSource.Play();

        float fadeInTimer = 0f;

        while (fadeInTimer < 0.5f)
        {
            fadeInTimer += Time.unscaledDeltaTime;
            musicSource.volume =
                Mathf.Lerp(0f, musicVolume, fadeInTimer / 0.5f);
            yield return null;
        }

        musicSource.volume = musicVolume;
        fadeCoroutine = null;
    }

    private IEnumerator FadeOutMusic()
    {
        float fadeTime = 0.5f;
        float timer = 0f;

        while (timer < fadeTime)
        {
            timer += Time.unscaledDeltaTime;
            musicSource.volume =
                Mathf.Lerp(musicSource.volume, 0f, timer / fadeTime);
            yield return null;
        }

        musicSource.Stop();
        musicSource.volume = musicVolume;
        fadeCoroutine = null;
    }

    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
    }

    public void SetUiVolume(float volume)
    {
        uiVolume = Mathf.Clamp01(volume);
    }

    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        musicSource.volume = musicVolume;
    }

    public void SwitchToBossMusic()
    {
        PlayMusic(bossMusic);
    }

    public void SwitchToMainMusic()
    {
        PlayMusic(mainMusic);
    }
}