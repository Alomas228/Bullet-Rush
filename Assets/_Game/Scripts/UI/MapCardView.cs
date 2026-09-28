using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Карточка выбора карты на панели «Карты».
///
/// Композиция — мини-версия карты: фон = цвет неба (фона камеры),
/// снизу полоса земли (цвет подложки), по центру иконка/диск,
/// под ним название. Выбранная карта подсвечивается чипом
/// «ВЫБРАНО» и тёмной рамкой.
///
/// Данные не лежат в полях: их передаёт MapSelectionUI через
/// <see cref="Bind"/>. Клики раздаёт MapSelectionUI.
/// </summary>
public class MapCardView : MonoBehaviour
{
    [Header("Frame")]
    [SerializeField] private Image frameImage;
    [SerializeField] private Image backgroundImage;

    [Header("World preview")]
    [SerializeField] private Image landImage;
    [SerializeField] private Image iconDisc;
    [SerializeField] private Image iconImage;

    [Header("Text")]
    [SerializeField] private TMP_Text nameText;

    [Header("State")]
    [Tooltip("Чип «ВЫБРАНО». Показывается/прячется целиком.")]
    [SerializeField] private RectTransform selectedChip;

    [Tooltip("Кнопка на всю карточку.")]
    [SerializeField] private Button selectButton;

    [Tooltip("Ставится в true, когда на карточку повешен обработчик.")]
    [SerializeField] private bool wired;

    private const float SelectedScale = 1.03f;

    public Button SelectButton => selectButton;

    public bool Wired => wired;

    public void MarkWired()
    {
        wired = true;
    }

    /// <summary>
    /// Заполняет карточку данными карты. Иконку берёт из GameMap,
    /// а если её нет — рисует цветной диск цвета подложки.
    /// </summary>
    public void Bind(GameMap map)
    {
        if (map == null)
            return;

        Color ground = GroundColor(map);
        Color sky = map.skyColor;

        if (backgroundImage != null)
            backgroundImage.color = sky;

        if (landImage != null)
            landImage.color = ground;

        Color discColor =
            Color.Lerp(ground, Color.white, 0.35f);

        if (iconDisc != null)
            iconDisc.color = discColor;

        if (iconImage != null)
        {
            bool hasIcon = map.icon != null;

            iconImage.gameObject.SetActive(hasIcon);

            if (hasIcon)
            {
                iconImage.sprite = map.icon;
                iconImage.color = Color.white;
            }
        }

        if (nameText != null)
        {
            nameText.text = string.IsNullOrEmpty(map.mapName)
                ? map.name
                : map.mapName;

            // На тёмной земле (космос) — светлый текст, иначе тёмный.
            float luminance =
                ground.r * 0.299f +
                ground.g * 0.587f +
                ground.b * 0.114f;

            nameText.color = luminance < 0.45f
                ? Color.white
                : MapSelectionTheme.TextPrimary;
        }

        SetSelected(false);
    }

    public void SetSelected(bool value)
    {
        transform.localScale =
            Vector3.one * (value ? SelectedScale : 1f);

        if (frameImage != null)
        {
            frameImage.color = value
                ? MapSelectionTheme.FrameSelected
                : MapSelectionTheme.FrameIdle;
        }

        if (selectedChip != null &&
            selectedChip.gameObject.activeSelf != value)
        {
            selectedChip.gameObject.SetActive(value);
        }
    }

    /// <summary>
    /// Представительный цвет карты для полосы земли и диска:
    /// берётся из _BaseColor материала подложки, если он задан.
    /// </summary>
    public static Color GroundColor(GameMap map)
    {
        Color fallback = new Color(0.6f, 0.6f, 0.6f);

        if (map == null)
            return fallback;

        if (map.groundMaterial != null &&
            map.groundMaterial.HasProperty("_BaseColor"))
        {
            return map.groundMaterial.GetColor("_BaseColor");
        }

        return fallback;
    }
}