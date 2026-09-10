using UnityEngine;
using TMPro;

public class DamageNumber : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text damageText;

    [Header("Settings")]
    [SerializeField] private float lifetime = 0.7f;
    [SerializeField] private float moveSpeed = 2f;

    [Header("Critical")]
    [SerializeField] private float criticalScale = 1.4f;

    private float timer;
    private Camera mainCamera;

    public void Initialize(
        float damage,
        bool isCritical)
    {
        if (damageText != null)
        {
            damageText.text =
                Mathf.RoundToInt(damage).ToString();

            if (isCritical)
            {
                damageText.text =
                    "CRIT " + damageText.text;

                damageText.transform.localScale *=
                    criticalScale;
            }
        }
    }

    private void Awake()
    {
        mainCamera = Camera.main;
    }

    private void Update()
    {
        if (mainCamera != null)
        {
            transform.LookAt(
                mainCamera.transform
            );

            transform.Rotate(
                0f,
                180f,
                0f
            );
        }

        transform.position +=
            Vector3.up *
            moveSpeed *
            Time.deltaTime;

        timer += Time.deltaTime;

        if (timer >= lifetime)
            Destroy(gameObject);
    }
}