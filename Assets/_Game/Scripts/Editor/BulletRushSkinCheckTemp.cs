using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class BulletRushSkinCheckTemp
{
    const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
        var manager = UnityEngine.Object.FindAnyObjectByType<UpgradeManager>(FindObjectsInactive.Include);
        typeof(UpgradeManager).GetProperty("Instance").SetValue(null, manager);
        var player = UnityEngine.Object.FindAnyObjectByType<PlayerEquipmentApplier>(FindObjectsInactive.Include);
        var ui = UnityEngine.Object.FindAnyObjectByType<EquipmentUI>(FindObjectsInactive.Include);
        player.GetType().GetMethod("Awake", Private).Invoke(player, null);
        var filter = player.GetComponent<MeshFilter>();
        var original = filter.sharedMesh;
        var renderer = player.GetComponent<MeshRenderer>();
        var block = new MaterialPropertyBlock();
        var catalog = manager.GetAvailableClothing();
        if (catalog.Count != 19) throw new Exception("Invalid catalog count");
        var stateType = typeof(EquipmentManager).GetNestedType("EquipmentData",BindingFlags.NonPublic);
        var state = Activator.CreateInstance(stateType,true);
        var staticPrivate = BindingFlags.NonPublic | BindingFlags.Static;
        typeof(EquipmentManager).GetField("data",staticPrivate).SetValue(null,state);
        typeof(EquipmentManager).GetField("loaded",staticPrivate).SetValue(null,true);
        var equipped = stateType.GetField("equippedClothing");
        var owned = (List<string>)stateType.GetField("ownedClothing").GetValue(state);
        ui.GetType().GetMethod("EnsureBuilt",Private).Invoke(ui,null);
        ui.GetType().GetMethod("SelectTab",Private).Invoke(ui,new object[]{2});
        var slots=(List<EquipmentCardView>)typeof(EquipmentUI).GetField("cardSlots",Private).GetValue(ui);
        var detail=(EquipmentCardView)typeof(EquipmentUI).GetField("detail",Private).GetValue(ui);
        var glyph=typeof(EquipmentCardView).GetField("glyph",Private);
        for(int i=0;i<catalog.Count;i++)
        {
            var data=catalog[i];
            if(data == null || data.Preview==null) throw new Exception("Missing preview "+i);
            if(data.name!="Clothing_Neon" && (!EquipmentManager.IsOwned(data) || !EquipmentManager.IsUnlocked(data) || data.Price!=0))
                throw new Exception("Unavailable skin "+i);
            ui.GetType().GetMethod("GoToPage",Private).Invoke(ui,new object[]{i/4});
            slots[i%4].CardButton.onClick.Invoke();
            if(((Image)glyph.GetValue(slots[i%4])).sprite!=data.Preview || ((Image)glyph.GetValue(detail)).sprite!=data.Preview)
                throw new Exception("Wrong preview "+data.name);
            owned.Add(data.ClothingName);
            equipped.SetValue(state,data.ClothingName);
            player.GetType().GetMethod("ApplyClothing",Private).Invoke(player,null);
            renderer.GetPropertyBlock(block);
            if(filter.sharedMesh!=(data.BodyMesh!=null ? data.BodyMesh : original) || block.GetTexture("_BaseMap")!=data.SkinTexture)
                throw new Exception("Wrong outfit "+data.name);
        }
        Debug.Log("BULLETRUSH_SKIN_CHECK_OK 19 player outfits and shop previews");
    }
}
