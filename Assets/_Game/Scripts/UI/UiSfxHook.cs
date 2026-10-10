using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

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

            if (button.GetComponent<UiSfxPointerDown>() != null)
                continue;

            button.gameObject.AddComponent<UiSfxPointerDown>();
        }
    }
}

public class UiSfxPointerDown : MonoBehaviour, IPointerDownHandler
{
    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        if (button != null && !button.IsInteractable())
            return;

        AudioManager audio = AudioManager.Instance;

        if (audio == null)
            return;

        SFXLibrary sfx = audio.SFXLibrary;

        if (sfx != null && sfx.UiClick != null)
            audio.PlayUI(sfx.UiClick);
    }
}
