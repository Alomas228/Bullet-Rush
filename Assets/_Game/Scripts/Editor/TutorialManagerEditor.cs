#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

/// <summary>
/// Кастомный инспектор TutorialManager: показывает статус обучения
/// и даёт кнопки сброса / отметки «пройдено» без запуска игры.
/// </summary>
[CustomEditor(typeof(TutorialManager))]
public class TutorialManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(12f);
        EditorGUILayout.LabelField(
            "Обучение",
            EditorStyles.boldLabel
        );

        bool completed = TutorialManager.IsCompleted;

        EditorGUILayout.HelpBox(
            completed
                ? "Статус: пройдено"
                : "Статус: НЕ пройдено",
            completed
                ? MessageType.Info
                : MessageType.Warning
        );

        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.enabled = completed;

            if (GUILayout.Button("Сбросить обучение"))
            {
                TutorialManager.IsCompleted = false;
                GUI.FocusControl(null);
            }

            GUI.enabled = true;

            if (!completed &&
                GUILayout.Button("Отметить пройденным"))
            {
                TutorialManager.IsCompleted = true;
                GUI.FocusControl(null);
            }
        }

        if (completed)
        {
            EditorGUILayout.HelpBox(
                "После сброса игрок при следующем нажатии «Играть» " +
                "пройдёт обучение заново.",
                MessageType.None
            );
        }
    }
}

#endif
