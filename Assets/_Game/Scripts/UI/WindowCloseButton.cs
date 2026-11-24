using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class WindowCloseButton : MonoBehaviour
{
    private static readonly List<WindowCloseButton> open = new List<WindowCloseButton>();
    private Button button;
    private static int handledFrame = -1;

    public static void Create(RectTransform panel, UnityAction close)
    {
        var rect = ArcadeUiFactory.At(panel, "Close", Vector2.one, Vector2.one, Vector2.zero, Vector2.one);
        var bg = ArcadeUiFactory.Image(rect, ArcadeKitAssets.RoundRect, MenuSheet.Recess, true);
        bg.raycastTarget = true;
        var control = rect.gameObject.AddComponent<Button>();
        control.targetGraphic = bg;
        control.onClick.AddListener(close);
        Attach(panel, control);
    }

    // Keep scene callbacks on existing BackButtons, including the menu's own navigation.
    public static void Attach(RectTransform panel, Button control)
    {
        if (control == null) return;
        var rect = (RectTransform)control.transform;
        rect.SetParent(panel, false);
        rect.localScale = Vector3.one;
        UiArtboard.Place(rect, Vector2.one, Vector2.one, new Vector2(-18,-18), new Vector2(48,48));
        rect.SetAsLastSibling();
        foreach (Transform child in rect) child.gameObject.SetActive(false);
        var bg = control.GetComponent<Image>();
        MenuSheet.Surface(bg, MenuSheet.Recess, true);
        control.targetGraphic = bg;
        control.transition = Selectable.Transition.ColorTint;
        var mark = control.GetComponent<WindowCloseButton>();
        if (mark == null)
        {
            mark = control.gameObject.AddComponent<WindowCloseButton>();
            Stroke(rect, "Slash",45);
            Stroke(rect, "Backslash",-45);
        }
        else
        {
            rect.Find("Slash").gameObject.SetActive(true);
            rect.Find("Backslash").gameObject.SetActive(true);
        }
        mark.button = control;
        control.gameObject.SetActive(true);
    }

    private static void Stroke(RectTransform rect, string name, float angle)
    {
        var line = ArcadeUiFactory.At(rect, name, Vector2.one * .5f, Vector2.one * .5f,
            Vector2.zero, new Vector2(22,4));
        line.localRotation = Quaternion.Euler(0,0,angle);
        ArcadeUiFactory.Image(line, null, MenuSheet.Ink);
    }

    private void OnEnable() => open.Add(this);
    private void OnDisable() => open.Remove(this);

    private void Update()
    {
        if (PauseManager.Instance != null && PauseManager.Instance.isActiveAndEnabled) return;
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && (!JourneyUI.IsBlocking || JourneyUI.IsJournalOpen)) CloseTop();
    }

    public static bool CloseTop()
    {
        if (handledFrame == Time.frameCount) return true;
        WindowCloseButton top = null;
        int highest = int.MinValue;
        foreach (var item in open)
        {
            if (item == null || item.button == null || !item.button.IsInteractable()) continue;
            var canvas = item.button.GetComponentInParent<Canvas>();
            int order = canvas == null ? 0 : canvas.sortingOrder;
            if (order < highest) continue;
            highest = order;
            top = item;
        }
        if (top == null) return false;
        handledFrame = Time.frameCount;
        top.button.onClick.Invoke();
        return true;
    }
}
