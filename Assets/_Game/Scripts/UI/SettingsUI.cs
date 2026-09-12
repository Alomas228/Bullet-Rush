using UnityEngine;
using UnityEngine.UI;

public class SettingsUI : MonoBehaviour
{
    [Header("Sliders")]
    [Tooltip("Громкость эффектов (выстрелы, попадания и т.д.).")]
    [SerializeField] private Slider sfxSlider;
    [Tooltip("Громкость музыки.")]
    [SerializeField] private Slider musicSlider;
    [Tooltip("Громкость UI-звуков (нажатия кнопок).")]
    [SerializeField] private Slider uiSlider;

    [Header("Toggles")]
    [Tooltip("Тряска экрана при получении урона.")]
    [SerializeField] private Toggle screenShakeToggle;
    [Tooltip("Тряска экрана при выстреле из оружия.")]
    [SerializeField] private Toggle fireShakeToggle;

    [Header("Buttons")]
    [SerializeField] private Button resetButton;

    private void Awake()
    {
        if (sfxSlider != null)
            sfxSlider.onValueChanged.AddListener(
                value => SettingsManager.Instance?.SetSFXVolume(value)
            );

        if (musicSlider != null)
            musicSlider.onValueChanged.AddListener(
                value => SettingsManager.Instance?.SetMusicVolume(value)
            );

        if (uiSlider != null)
            uiSlider.onValueChanged.AddListener(
                value => SettingsManager.Instance?.SetUiVolume(value)
            );

        if (screenShakeToggle != null)
            screenShakeToggle.onValueChanged.AddListener(
                value => SettingsManager.Instance?.SetScreenShake(value)
            );

        if (fireShakeToggle != null)
            fireShakeToggle.onValueChanged.AddListener(
                value => SettingsManager.Instance?.SetFireShake(value)
            );

        if (resetButton != null)
            resetButton.onClick.AddListener(ResetAll);
    }

    private void Start()
    {
        Refresh();
    }

    private void OnEnable()
    {
        Refresh();
    }

    private void Refresh()
    {
        SettingsManager settings = SettingsManager.Instance;

        if (settings == null)
            return;

        if (sfxSlider != null)
            sfxSlider.SetValueWithoutNotify(settings.SfxVolume);

        if (musicSlider != null)
            musicSlider.SetValueWithoutNotify(settings.MusicVolume);

        if (uiSlider != null)
            uiSlider.SetValueWithoutNotify(settings.UiVolume);

        if (screenShakeToggle != null)
            screenShakeToggle.SetIsOnWithoutNotify(
                settings.ScreenShakeEnabled
            );

        if (fireShakeToggle != null)
            fireShakeToggle.SetIsOnWithoutNotify(
                settings.FireShakeEnabled
            );
    }

    private void ResetAll()
    {
        if (SettingsManager.Instance == null)
            return;

        SettingsManager.Instance.ResetToDefaults();
        Refresh();
    }
}