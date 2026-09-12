using UnityEngine;
using UnityEngine.UI;

public class SubPanelUI : MonoBehaviour
{
    [Tooltip("Кнопка «Назад» на панели. Возвращает в главное меню.")]
    [SerializeField] private Button backButton;

    private void Awake()
    {
        if (backButton != null)
            backButton.onClick.AddListener(OnBackClicked);
    }

    private void OnDestroy()
    {
        if (backButton != null)
            backButton.onClick.RemoveListener(OnBackClicked);
    }

    private void OnBackClicked()
    {
        if (MainMenuUI.Instance != null)
            MainMenuUI.Instance.CloseSubPanels();
        else
            gameObject.SetActive(false);
    }
}