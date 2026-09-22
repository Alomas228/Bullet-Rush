using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Кнопка «Пройти обучение заново» на панели Tips в меню.
/// Сбрасывает флаг пройденного обучения — в следующий раз при
/// нажатии «Играть» обучение запустится снова.
/// </summary>
public class TutorialReplayButton : MonoBehaviour
{
    [Tooltip("Текст-статус под кнопкой, куда выводится результат.")]
    [SerializeField] private TMP_Text statusText;

    [Tooltip("Цвет текста-статуса.")]
    [SerializeField] private Color statusColor =
        new Color(0.42f, 0.85f, 0.42f, 1f);

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();

        if (button != null)
            button.onClick.AddListener(OnReplayClicked);
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(OnReplayClicked);
    }

    private void OnReplayClicked()
    {
        TutorialManager.IsCompleted = false;

        if (statusText == null)
            return;

        statusText.text =
            "Обучение сброшено — начнётся при следующем нажатии «Играть»";

        statusText.color = statusColor;
    }
}