using UnityEngine;
using YG;

public class GetAuthorize : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {   
        YG2.onGetSDKData += OnAuth;
        if(!YG2.player.auth)
        {
            YG2.OpenAuthDialog();
        }
        else
        {
            print(YG2.player.name);
            print(YG2.player.id);
        }
    }

    private void OnAuth()
    {
        print(YG2.player.name);
        print(YG2.player.id);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
