using UnityEngine;
using UnityEngine.UI;

public class UiSfxHook : MonoBehaviour
{
    [Tooltip("Включать ли звук нажатия по кнопкам. Удобно для отладки.")]
    [SerializeField] private bool enabledByDefault = true;

    private void Awake()
    {
        if (!enabledByDefault)
            return;

        Button[] buttons =
            FindObjectsByType<Button>(
                FindObjectsInactive.Include
            );

        foreach (Button button in buttons)
        {
            if (button == null)
                continue;

            button.onClick.AddListener(PlayUiClick);
        }
    }

    private void PlayUiClick()
    {
        AudioManager audio = AudioManager.Instance;

        if (audio == null)
            return;

        SFXLibrary sfx = audio.SFXLibrary;

        if (sfx != null && sfx.UiClick != null)
            audio.PlayUI(sfx.UiClick);
    }
}