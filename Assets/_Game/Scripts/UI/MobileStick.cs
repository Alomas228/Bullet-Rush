using UnityEngine;
using UnityEngine.UI;

public sealed class MobileStick : MonoBehaviour
{
    private RectTransform rect, knob, surface;
    private Image knobImage, outline;
    private CanvasGroup visuals;
    private Vector2 homePosition, touchOrigin;
    private Color idleColor;
    private bool highlighted;
    public int PointerId { get; private set; } = int.MinValue;
    public bool IsHeld => PointerId != int.MinValue;
    public Vector2 Value { get; private set; }

    public void Bind(RectTransform handle,Image rim)
    {
        rect=(RectTransform)transform;
        surface=rect.parent as RectTransform;
        homePosition=rect.anchoredPosition;
        knob=handle;
        outline=rim;
        knobImage=knob.GetComponent<Image>();
        if(knobImage!=null) idleColor=knobImage.color;
        visuals=gameObject.AddComponent<CanvasGroup>();
        visuals.interactable=false;
        visuals.blocksRaycasts=false;
        RefreshVisibility();
    }

    public void SetHome(Vector2 position,float scale)
    {
        homePosition=position;
        rect.localScale=Vector3.one*scale;
        ResetStick();
    }

    private void RefreshVisibility()
    {
        if(visuals!=null) visuals.alpha=IsHeld || highlighted?1f:.28f;
    }

    public void SetTutorialHighlight(bool enabled)
    {
        if(outline==null || highlighted==enabled) return;
        highlighted=enabled;
        outline.color=enabled?new Color32(255,202,94,255):new Color32(236,224,190,115);
        RefreshVisibility();
    }

    public bool TryCapture(int id,Vector2 screenPoint)
    {
        if(IsHeld || rect==null || surface==null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(surface,screenPoint,null,out touchOrigin)) return false;
        // The finger starts at zero regardless of where on its screen half it lands.
        // Keep a portion of the visual inside the safe area near the very edge.
        float margin=rect.rect.width*rect.localScale.x*.18f;
        Rect bounds=surface.rect;
        float x=Mathf.Clamp(touchOrigin.x,bounds.xMin+Mathf.Min(margin,bounds.width*.5f),bounds.xMax-Mathf.Min(margin,bounds.width*.5f));
        float y=Mathf.Clamp(touchOrigin.y,bounds.yMin+Mathf.Min(margin,bounds.height*.5f),bounds.yMax-Mathf.Min(margin,bounds.height*.5f));
        rect.localPosition=new Vector3(x,y,rect.localPosition.z);
        PointerId=id;
        if(knobImage!=null) knobImage.color=new Color32(94,115,109,245);
        RefreshVisibility();
        UpdateContact(screenPoint);
        return true;
    }

    public void UpdateContact(Vector2 screenPoint)
    {
        if(!IsHeld || rect==null || surface==null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(surface,screenPoint,null,out Vector2 local)) return;
        float radius=Mathf.Max(1f,rect.rect.width*.33f);
        float scaledRadius=Mathf.Max(.01f,radius*rect.localScale.x);
        Vector2 raw=Vector2.ClampMagnitude((local-touchOrigin)/scaledRadius,1f);
        if(knob!=null) knob.anchoredPosition=raw*radius;
        float magnitude=raw.magnitude;
        Value=magnitude>.10f?raw.normalized*((magnitude-.10f)/.90f):Vector2.zero;
    }

    public void ResetStick()
    {
        PointerId=int.MinValue; Value=Vector2.zero;
        if(rect!=null) rect.anchoredPosition=homePosition;
        if(knob!=null) knob.anchoredPosition=Vector2.zero;
        if(knobImage!=null) knobImage.color=idleColor;
        RefreshVisibility();
    }
    private void OnDisable()=>ResetStick();
}
