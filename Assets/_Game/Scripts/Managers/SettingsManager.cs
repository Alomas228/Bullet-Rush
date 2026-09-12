using UnityEngine;

public class SettingsManager : MonoBehaviour
{
    [System.Serializable]
    private class SettingsData
    {
        public float sfxVolume = 1f;
        public float musicVolume = 0.5f;
        public float uiVolume = 0.8f;
        public bool screenShake = true;
        public bool fireShake = true;
    }

    public static SettingsManager Instance { get; private set; }

    private const string PrefsKey = "ArcadeSurvivor.Settings";

    private SettingsData data = new SettingsData();

    public float SfxVolume => data.sfxVolume;
    public float MusicVolume => data.musicVolume;
    public float UiVolume => data.uiVolume;
    public bool ScreenShakeEnabled => data.screenShake;
    public bool FireShakeEnabled => data.fireShake;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        Load();
        ApplyAll();
    }

    private void Load()
    {
        string json = PlayerPrefs.GetString(PrefsKey, string.Empty);

        if (string.IsNullOrEmpty(json))
            return;

        try
        {
            JsonUtility.FromJsonOverwrite(json, data);
        }
        catch
        {
            data = new SettingsData();
        }
    }

    private void Save()
    {
        PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }

    private void ApplyAll()
    {
        if (AudioManager.Instance == null)
            return;

        AudioManager.Instance.SetSFXVolume(data.sfxVolume);
        AudioManager.Instance.SetMusicVolume(data.musicVolume);
        AudioManager.Instance.SetUiVolume(data.uiVolume);
    }

    public void SetSFXVolume(float value)
    {
        data.sfxVolume = Mathf.Clamp01(value);
        Save();

        if (AudioManager.Instance != null)
            AudioManager.Instance.SetSFXVolume(data.sfxVolume);
    }

    public void SetMusicVolume(float value)
    {
        data.musicVolume = Mathf.Clamp01(value);
        Save();

        if (AudioManager.Instance != null)
            AudioManager.Instance.SetMusicVolume(data.musicVolume);
    }

    public void SetUiVolume(float value)
    {
        data.uiVolume = Mathf.Clamp01(value);
        Save();

        if (AudioManager.Instance != null)
            AudioManager.Instance.SetUiVolume(data.uiVolume);
    }

    public void SetScreenShake(bool enabled)
    {
        data.screenShake = enabled;
        Save();
    }

    public void SetFireShake(bool enabled)
    {
        data.fireShake = enabled;
        Save();
    }

    public void ResetToDefaults()
    {
        data = new SettingsData();
        Save();
        ApplyAll();
    }
}