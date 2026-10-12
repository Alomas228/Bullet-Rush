#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

/// <summary>
/// Кастомный инспектор XpManager: показывает кнопку сброса постоянного
/// прогресса (XP, уровень, монеты) без запуска игры.
/// </summary>
[CustomEditor(typeof(XpManager))]
public class XpManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(12f);
        EditorGUILayout.LabelField(
            "Прогресс игрока",
            EditorStyles.boldLabel
        );

        EditorGUILayout.HelpBox(
            "Сбрасывает постоянный XP, уровень и монеты " +
            "(ключ PlayerPrefs \"ArcadeSurvivor.Global\").",
            MessageType.None
        );

        if (GUILayout.Button("Сбросить прогресс (XP / уровень / монеты)"))
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Сброс прогресса",
                "Сбросить постоянный XP, уровень и монеты игрока?",
                "Сбросить",
                "Отмена"
            );

            if (confirmed)
            {
                ((XpManager)target).ResetProgress();
                GUI.FocusControl(null);
            }
        }
    }
}

#endif
