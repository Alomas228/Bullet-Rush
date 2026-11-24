using UnityEngine;

// Real edge anchors for the PVP health bars; move the timer above them on a narrow screen.
public sealed class ResponsiveMatchStrip : MonoBehaviour
{
    private RectTransform strip, player, playerBar, enemy, enemyBar, status;
    private float lastWidth = -1;
    private bool lastTouch;

    public void Bind(RectTransform left, RectTransform leftBar, RectTransform right, RectTransform rightBar,
        RectTransform timer)
    {
        strip = (RectTransform)transform;
        player = left; playerBar = leftBar; enemy = right; enemyBar = rightBar; status = timer;
        Reflow();
    }

    private void LateUpdate() => Reflow();

    private void Reflow()
    {
        if (strip == null) return;
        float width = ((RectTransform)strip.parent).rect.width;
        bool touch = MobileControls.IsTouchDevice;
        if (width <= 0 || (Mathf.Approximately(width,lastWidth) && touch == lastTouch)) return;
        lastWidth = width; lastTouch = touch;
        float left = touch ? 142 : 24, right = 24;
        bool compact = width < 1360;
        float barWidth = Mathf.Min(340, (width-left-right-32)*.5f);
        strip.offsetMin = new Vector2(touch ? 132 : 0, -(compact ? 152 : 112));
        strip.offsetMax = Vector2.zero;
        UiArtboard.Place(player, Vector2.up, Vector2.up, new Vector2(left,compact ? -72 : -30), new Vector2(barWidth,38));
        UiArtboard.Place(playerBar, Vector2.up, Vector2.up, new Vector2(left,compact ? -119 : -78), new Vector2(barWidth,14));
        UiArtboard.Place(enemy, Vector2.one, Vector2.one, new Vector2(-right,compact ? -72 : -30), new Vector2(barWidth,38));
        UiArtboard.Place(enemyBar, Vector2.one, Vector2.one, new Vector2(-right,compact ? -119 : -78), new Vector2(barWidth,14));
        var top = new Vector2(.5f,1);
        UiArtboard.Place(status, top, top, new Vector2(0,compact ? -18 : -42),
            new Vector2(Mathf.Min(520,width-left-right-24),38));
        foreach (RectTransform rect in new[] { player, enemy, status })
        {
            if (!rect.TryGetComponent<TMPro.TMP_Text>(out var label)) continue;
            label.enableAutoSizing = true;
            label.fontSizeMin = 13;
            label.fontSizeMax = 18;
            label.overflowMode = TMPro.TextOverflowModes.Ellipsis;
        }
    }
}
