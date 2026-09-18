using UnityEngine;
using YG;

public class GetAuthorize : MonoBehaviour
{
    private void OnEnable()
    {
        YG2.onGetSDKData += OnAuth;

        if (YG2.isSDKEnabled)
            OnAuth();
    }

    private void OnDisable()
    {
        YG2.onGetSDKData -= OnAuth;
    }

    private void OnAuth()
    {
        if (YG2.player.auth)
        {
            Debug.Log(
                $"[YG2] Player: {YG2.player.name} ({YG2.player.id})"
            );
        }
        else
        {
            Debug.Log("[YG2] Player unauthorized");
        }
    }

    /// <summary>Вызывается по кнопке «Войти» из UI (не насильно!).</summary>
    public void OpenAuthDialog()
    {
        YG2.OpenAuthDialog();
    }
}