using UnityEngine;

public sealed class ResponsiveResultsLayout : MonoBehaviour
{
    private RectTransform board, summary, report;
    private bool? portrait;

    public void Bind(RectTransform content, RectTransform result, RectTransform details)
    {
        board = content; summary = result; report = details;
        Reflow();
    }

    private void OnEnable() => Reflow();
    private void LateUpdate() => Reflow();

    private void Reflow()
    {
        if (board == null || summary == null || report == null) return;
        bool vertical = ResponsiveUi.Portrait;
        if (portrait == vertical) return;
        portrait = vertical;
        UiArtboard.Resize(board, vertical ? new Vector2(940,1264) : new Vector2(1520,650));
        summary.anchoredPosition = vertical ? new Vector2(0,310) : new Vector2(-294,0);
        report.anchoredPosition = vertical ? new Vector2(0,-310) : new Vector2(458,0);
    }
}
