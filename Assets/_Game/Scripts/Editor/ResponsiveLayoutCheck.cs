#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

/// <summary>Fast regression check for the layout rules; no scene or asset changes.</summary>
public static class ResponsiveLayoutCheck
{
    [MenuItem("BulletRush/Tests/Responsive UI")]
    public static void Run()
    {
        var sizes = new[]
        {
            new Vector2(1920,1080), new Vector2(1024,768), new Vector2(2560,1080),
            new Vector2(1920,1200), new Vector2(1280,1024), new Vector2(1080,1080),
            new Vector2(844,390), new Vector2(390,844), new Vector2(768,1024),
            new Vector2(320,240), new Vector2(5120,1440)
        };
        int checks = 0;
        foreach (Vector2 screen in sizes)
        {
            foreach (bool inset in new[] { false, true })
            {
                // Simulate a cutout/gesture area on both landscape and portrait.
                Vector2 safePixels = screen - (inset ? new Vector2(screen.x*.10f,screen.y*.07f) : Vector2.zero);
                bool vertical = safePixels.y > safePixels.x;
                Vector2 reference = ResponsiveUi.Reference(new Vector2(1920,1080), vertical);
                float scaler = ResponsiveUi.Fit(screen, reference, true);
                Vector2 available = safePixels / scaler;
                var designs = new[]
                {
                    vertical ? new Vector2(1000,1494) : new Vector2(1520,864), // shops
                    vertical ? new Vector2(940,1264) : new Vector2(1520,650), // results
                    new Vector2(1136,774), new Vector2(646,460), new Vector2(1040,634),
                    new Vector2(884,644), new Vector2(920,520), new Vector2(1492,846)
                };
                foreach (Vector2 design in designs)
                {
                    float scale = ResponsiveUi.Fit(available, design);
                    AssertFits(design*scale, available, "modal");
                    checks++;
                }
                Vector2 campDesign = vertical ? new Vector2(780,1360) : new Vector2(1320,1080);
                float camp = ResponsiveUi.Fit(available, campDesign, true);
                AssertFits((vertical ? new Vector2(760,1340) : new Vector2(1274,1080))*camp, available, "camp");
                float sticks = Mathf.Min(1.22f,Mathf.Min(available.x/980f,available.y/720f));
                AssertFits(new Vector2(780,690)*sticks,available,"mobile controls");
                Vector2 choiceArea = available - new Vector2(40,186);
                Vector2 choiceDesign = vertical ? new Vector2(680,1050) : new Vector2(1440,810);
                AssertFits(choiceDesign*ResponsiveUi.Fit(choiceArea,choiceDesign,true),choiceArea,"wave choices");
                checks += 3;
            }
        }
        Debug.Log($"RESPONSIVE_UI_CHECK_OK: {checks} bounds checks; {sizes.Length} screen sizes, with and without safe insets.");
    }

    private static void AssertFits(Vector2 content, Vector2 area, string name)
    {
        if (content.x > area.x + .02f || content.y > area.y + .02f || content.x <= 0 || content.y <= 0)
            throw new InvalidOperationException($"{name}: {content} exceeds {area}");
    }
}
#endif
