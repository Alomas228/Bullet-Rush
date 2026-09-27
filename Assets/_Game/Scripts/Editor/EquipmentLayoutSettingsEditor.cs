#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

/// <summary>
/// Инспектор настроек размеров. Кроме самих полей показывает,
/// влезает ли ряд карточек и содержимое карточки, — иначе
/// переполнение видно только в play mode.
/// </summary>
[CustomEditor(typeof(EquipmentLayoutSettings))]
public class EquipmentLayoutSettingsEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var layout = (EquipmentLayoutSettings)target;

        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField(
            "Проверка влезания",
            EditorStyles.boldLabel
        );

        DrawCheck(
            layout.BandFits,
            "Полосы экрана",
            $"Полоса вкладок заканчивается на " +
            $"{layout.topBarHeight + layout.tabBarHeight:0}, " +
            $"ряд начинается на {layout.cardAreaTop:0}. " +
            "Сверху полосы накладываются друг на друга.",
            "Порядок полос правильный."
        );

        DrawCheck(
            layout.RowFits,
            "Ряд карточек по ширине",
            $"Ряд занимает {layout.RowWidth:0} px, доступно " +
            $"{layout.AvailableWidth:0} px. " +
            "Уменьши cardWidth, cardGap, cardsPerPage или rowPadding.",
            $"Ряд из {layout.cardsPerPage} карточек влезает " +
            $"в {layout.AvailableWidth:0} px."
        );

        DrawCheck(
            layout.CardFits,
            "Содержимое карточки по высоте",
            $"Строки занимают {layout.ContentHeight:0} px, " +
            $"карточка {layout.cardHeight:0} px. " +
            "Уменьши высоты строк или увеличь cardHeight.",
            $"Строки занимают {layout.ContentHeight:0} из " +
            $"{layout.cardHeight:0} px, запас " +
            $"{layout.cardHeight - layout.ContentHeight:0} px."
        );

        if (layout.CardFits && layout.RowFits && layout.BandFits)
        {
            EditorGUILayout.HelpBox(
                "Всё влезает в " +
                $"{layout.referenceWidth:0}x{layout.referenceHeight:0}.",
                MessageType.Info
            );
        }

        EditorGUILayout.Space();

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Сбросить к значениям по умолчанию"))
            {
                Undo.RecordObject(layout, "Reset Equipment Layout");

                layout.ApplyDefaults();

                EditorUtility.SetDirty(layout);
            }

            if (GUILayout.Button("Пересобрать экран"))
                EquipmentUIBuilder.Build();
        }

        EditorGUILayout.HelpBox(
            "Правки применяются после пересборки: билдер пересоздаёт " +
            "панель целиком. Сам экран в сцене не обновляется сам.",
            MessageType.None
        );
    }

    private static void DrawCheck(
        bool ok,
        string title,
        string problem,
        string okText)
    {
        EditorGUILayout.HelpBox(
            $"{title}: {(ok ? "ок" : "не влезает")}\n" +
            (ok ? okText : problem),
            ok ? MessageType.None : MessageType.Warning
        );
    }
}

#endif
