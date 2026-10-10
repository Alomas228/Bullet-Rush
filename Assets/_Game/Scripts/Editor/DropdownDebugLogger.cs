#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

[InitializeOnLoad]
public static class DropdownDebugLogger
{
    private static string lastSignature;
    private static bool loggedPlayStart;

    static DropdownDebugLogger()
    {
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying)
        {
            lastSignature = null;
            loggedPlayStart = false;
            return;
        }

        if (!loggedPlayStart)
        {
            loggedPlayStart = true;
            Debug.Log(Describe("PLAY START / dropdown state"));
        }

        GameObject popup = GameObject.Find("Dropdown List");
        if (popup == null)
            return;

        RectTransform rt = popup.transform as RectTransform;
        string signature = popup.GetInstanceID() + "|" + rt.anchoredPosition + "|" + rt.sizeDelta + "|" + rt.rect;
        if (signature == lastSignature)
            return;

        lastSignature = signature;

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("========== DROPDOWN POPUP OPENED ==========");
        sb.AppendLine("scene: " + SceneManager.GetActiveScene().path);

        Transform t = popup.transform;
        string chain = t.name;
        t = t.parent;
        while (t != null)
        {
            chain = t.name + " / " + chain;
            t = t.parent;
        }
        sb.AppendLine("popup chain: " + chain);
        sb.AppendLine($"popup parent: {(rt.parent != null ? rt.parent.name : "null")}");
        sb.AppendLine($"popup anchorMin={rt.anchorMin} anchorMax={rt.anchorMax} pivot={rt.pivot} anchoredPos={rt.anchoredPosition} sizeDelta={rt.sizeDelta} rect={rt.rect}");
        sb.AppendLine($"popup lossyScale={rt.lossyScale}");

        if (rt.parent is RectTransform prt)
            sb.AppendLine($"parent rect: anchorMin={prt.anchorMin} anchorMax={prt.anchorMax} pivot={prt.pivot} anchoredPos={prt.anchoredPosition} sizeDelta={prt.sizeDelta} rect={prt.rect} lossyScale={prt.lossyScale}");

        sb.AppendLine(Describe("dropdown state"));
        Debug.Log(sb.ToString());
    }

    private static string Describe(string header)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("========== " + header + " ==========");
        sb.AppendLine("scene: " + SceneManager.GetActiveScene().path);

        Object[] dropdowns = Object.FindObjectsOfType(typeof(TMP_Dropdown));
        sb.AppendLine("TMP_Dropdown count: " + dropdowns.Length);
        foreach (Object o in dropdowns)
        {
            if (!(o is TMP_Dropdown dd) || dd == null)
                continue;

            RectTransform drt = dd.transform as RectTransform;
            sb.AppendLine($"dropdown '{dd.name}' activeInHierarchy={dd.gameObject.activeInHierarchy} rect={drt.rect} parent={drt.parent?.name ?? "null"} value={dd.value} options={dd.options.Count}");

            if (dd.template == null)
            {
                sb.AppendLine("   template: NULL");
            }
            else
            {
                RectTransform trt = dd.template;
                sb.AppendLine($"   template '{dd.template.name}' parent={trt.parent?.name ?? "null"} activeInHierarchy={dd.template.gameObject.activeInHierarchy}");
                sb.AppendLine($"   template anchorMin={trt.anchorMin} anchorMax={trt.anchorMax} pivot={trt.pivot} anchoredPos={trt.anchoredPosition} sizeDelta={trt.sizeDelta} rect={trt.rect} lossyScale={trt.lossyScale}");
            }
        }
        return sb.ToString();
    }
}
#endif
