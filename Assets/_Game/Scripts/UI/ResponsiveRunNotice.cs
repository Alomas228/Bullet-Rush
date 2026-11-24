using UnityEngine;

// Reward notifications must not hide the mobile aim stick or ability buttons.
public sealed class ResponsiveRunNotice : MonoBehaviour
{
    private bool? touch, portrait;
    private void OnEnable() => Reflow();
    private void LateUpdate() => Reflow();

    private void Reflow()
    {
        bool mobile = MobileControls.IsTouchDevice;
        bool vertical = ResponsiveUi.Portrait;
        if (touch == mobile && portrait == vertical) return;
        touch = mobile; portrait = vertical;
        RectTransform rect = (RectTransform)transform;
        Vector2 anchor = mobile ? new Vector2(.5f,0) : new Vector2(1,0);
        UiArtboard.Place(rect, anchor, anchor,
            mobile ? new Vector2(0,vertical ? 630 : 215) : new Vector2(-18,78), new Vector2(448,104));
    }
}
