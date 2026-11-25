using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using System.Collections.Generic;

[DefaultExecutionOrder(-150)]
public class UiSfxHook : MonoBehaviour
{
    [Tooltip("Включать ли звук нажатия по кнопкам. Удобно для отладки.")]
    [SerializeField] private bool enabledByDefault = true;

    private readonly List<RaycastResult> hits = new List<RaycastResult>(24);
    private PointerEventData pointer;
    private EventSystem currentSystem;

    private void Update()
    {
        // Only the persistent AudioManager's hook handles input; scene hooks may still exist.
        if (!enabledByDefault || AudioManager.Instance == null || gameObject != AudioManager.Instance.gameObject) return;
        EventSystem system = EventSystem.current;
        if (system == null) return;
        Vector2? position = null;
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            position = Touchscreen.current.primaryTouch.position.ReadValue();
        else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            position = Mouse.current.position.ReadValue();
        if (position.HasValue)
        {
            if (pointer == null || currentSystem != system) { currentSystem = system; pointer = new PointerEventData(system); }
            pointer.Reset(); pointer.position = position.Value;
            hits.Clear(); system.RaycastAll(pointer, hits);
            foreach (var hit in hits)
            {
                if (!(hit.module is GraphicRaycaster)) continue;
                Play(hit.gameObject.GetComponentInParent<Selectable>());
                break;
            }
            return;
        }
        bool submit = Keyboard.current != null && (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame)
            || Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame;
        if (submit && system.currentSelectedGameObject != null)
            Play(system.currentSelectedGameObject.GetComponentInParent<Selectable>());
    }

    private static void Play(Selectable selectable)
    {
        if (selectable == null || !selectable.IsInteractable() || !selectable.isActiveAndEnabled) return;
        string key = selectable is Toggle || selectable is Slider || selectable is Dropdown ? "ui_select" : "ui_press";
        string name = selectable.name.ToLowerInvariant();
        if (name.Contains("close") || name.Contains("back") || name.Contains("cancel")) key = "ui_back";
        else if (name.Contains("buy") || name.Contains("claim") || name.Contains("equip") || name.Contains("action") || name.Contains("confirm")) key = "ui_confirm";
        else if (name.Contains("tab") || name.Contains("card") || name.Contains("select") || name.Contains("next") || name.Contains("prev")) key = "ui_select";
        AudioManager.Instance?.PlayUiInteraction(key);
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
            audio.PlayUiInteraction("ui_press");
    }
}
