using UnityEngine;
using System.Collections;

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

    private AudioSource sfxSource;
    private AudioSource musicSource;

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

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;

        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = true;
    }

    private void Start()
    {
        ApplyPersistedVolumes();

        if (mainMusic != null)
            PlayMusic(mainMusic);
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

    public void PlaySFX(AudioClip clip)
    {
        if (clip == null)
            return;

        sfxSource.PlayOneShot(
            clip,
            sfxVolume * GetClipVolume(clip)
        );
    }

    public void PlaySFX(AudioClip clip, float volumeScale)
    {
        if (clip == null)
            return;

        sfxSource.PlayOneShot(
            clip,
            sfxVolume * GetClipVolume(clip) * volumeScale
        );
    }

    public void PlaySFXVariation(AudioClip clip)
    {
        if (clip == null)
            return;

        sfxSource.pitch = Random.Range(0.9f, 1.1f);
        sfxSource.PlayOneShot(
            clip,
            sfxVolume * GetClipVolume(clip)
        );
        sfxSource.pitch = 1f;
    }

    public void PlayUI(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null)
            return;

        sfxSource.PlayOneShot(
            clip,
            uiVolume * GetClipVolume(clip) * volumeScale
        );
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
