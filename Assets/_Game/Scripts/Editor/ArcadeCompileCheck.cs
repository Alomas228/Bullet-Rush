#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

public static class ArcadeCompileCheck
{
    public static void Run()
    {
        Debug.Log("ARCADE_COMPILE_CHECK_OK");

        if (Application.isBatchMode)
            EditorApplication.Exit(0);
    }
}

#endif

