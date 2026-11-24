using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public sealed partial class ArcadeMenuSkin
{
    private void BuildTopBar(RectTransform parent)
    {
        RectTransform record = ArcadeUiFactory.At(
            parent,
            "RecordChip",
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -38f),
            new Vector2(430f, 66f)
        );
        ArcadeUiFactory.Image(
            record,
            ArcadeKitAssets.RoundRect,
            ArcadeUiTheme.Alpha(ArcadeUiTheme.Panel, 0.95f),
            true
        );
        AddShadow(record, new Vector2(0f, -4f), 0.3f);

        RectTransform trophy = ArcadeUiFactory.At(
            record,
            "Trophy",
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(34f, 0f),
            new Vector2(32f, 32f)
        );
        ArcadeUiFactory.Image(trophy, ArcadeKitAssets.Icon("trophy"), ArcadeUiTheme.Gold);

        recordValueText = ArcadeUiFactory.Label(
            record,
            "RecordValue",
            "",
            ArcadeKitAssets.BodyBold,
            21f,
            ArcadeUiTheme.TextPrimary,
            TextAlignmentOptions.Left,
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(78f, 10f),
            new Vector2(330f, 26f)
        );

        TMP_Text recordHint = ArcadeUiFactory.Label(
            record,
            "RecordHint",
            "",
            ArcadeKitAssets.BodyBold,
            12f,
            ArcadeUiTheme.TextSecondary,
            TextAlignmentOptions.Left,
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(78f, -14f),
            new Vector2(330f, 18f),
            true
        );
        AddLangBinding(recordHint, () => Lang.GetOr("menu.skin.record", "ЛИЧНЫЙ РЕКОРД"));

        BuildTopRight(parent);
    }

    private void BuildTopRight(RectTransform parent)
    {
        RectTransform gear = ArcadeUiFactory.At(
            parent,
            "SettingsButton",
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-36f, -38f),
            new Vector2(58f, 58f)
        );
        Button gearButton = ArcadeUiFactory.Button(
            gear,
            ArcadeKitAssets.RoundRect,
            ArcadeUiTheme.Alpha(ArcadeUiTheme.PanelRaised, 0.94f),
            null,
            ArcadeKitAssets.Body,
            16f,
            ArcadeUiTheme.TextPrimary,
            OpenSettings
        );
        gearButton.name = "GearButton";

        RectTransform gearIcon = ArcadeUiFactory.At(
            gear,
            "Icon",
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(28f, 28f)
        );
        ArcadeUiFactory.Image(gearIcon, ArcadeKitAssets.Icon("gear"), ArcadeUiTheme.Gold);

        RectTransform coins = ArcadeUiFactory.At(
            parent,
            "CoinsPill",
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-106f, -38f),
            new Vector2(246f, 58f)
        );
        ArcadeUiFactory.Image(
            coins,
            ArcadeKitAssets.RoundRect,
            ArcadeUiTheme.Alpha(ArcadeUiTheme.Panel, 0.95f),
            true
        );
        AddShadow(coins, new Vector2(0f, -3f), 0.3f);

        RectTransform coinIcon = ArcadeUiFactory.At(
            coins,
            "CoinIcon",
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(26f, 0f),
            new Vector2(30f, 30f)
        );
        ArcadeUiFactory.Image(coinIcon, ArcadeKitAssets.Coin, Color.white);

        coinsValueText = ArcadeUiFactory.Label(
            coins,
            "CoinsValue",
            "0",
            ArcadeKitAssets.BodyBold,
            22f,
            ArcadeUiTheme.TextPrimary,
            TextAlignmentOptions.Left,
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(66f, 0f),
            new Vector2(120f, 30f)
        );

        RectTransform plus = ArcadeUiFactory.At(
            coins,
            "PlusButton",
            new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(-12f, 0f),
            new Vector2(38f, 38f)
        );
        ArcadeUiFactory.Button(
            plus,
            ArcadeKitAssets.Knob,
            ArcadeUiTheme.Orange,
            null,
            ArcadeKitAssets.Body,
            16f,
            ArcadeUiTheme.TextDark,
            ClaimBonusAd
        );

        RectTransform plusIcon = ArcadeUiFactory.At(
            plus,
            "Icon",
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(18f, 18f)
        );
        ArcadeUiFactory.Image(plusIcon, ArcadeKitAssets.Icon("plus"), ArcadeUiTheme.TextDark);


    }

    private void BuildLeftRail(RectTransform parent)
    {
        dailyCard = BuildRailCard(
            parent,
            "DailyCard",
            () => Lang.GetOr("menu.skin.daily", "ЕЖЕДНЕВНЫЙ БОНУС"),
            () => Lang.GetOr("menu.skin.daily_sub", "ЗАБРАТЬ НАГРАДУ"),
            "gift",
            ArcadeUiTheme.Gold,
            new Vector2(44f, -150f),
            OpenDaily
        );

        upgradesCard = BuildRailCard(
            parent,
            "UpgradesCard",
            () => Lang.GetOr("menu.upgrades", "УЛУЧШЕНИЯ"),
            () => Lang.GetOr("menu.skin.upgrades_sub", "ПОСТОЯННЫЕ УЛУЧШЕНИЯ"),
            "cart",
            ArcadeUiTheme.Cyan,
            new Vector2(44f, -270f),
            OpenUpgrades
        );

        equipmentCard = BuildRailCard(
            parent,
            "EquipmentCard",
            () => Lang.GetOr("menu.equipment", "СНАРЯЖЕНИЕ"),
            () => Lang.GetOr("menu.skin.equipment_sub", "ОРУЖИЕ И СПОСОБНОСТИ"),
            "shield",
            ArcadeUiTheme.Purple,
            new Vector2(44f, -390f),
            OpenEquipment
        );


    }

    private RailCard BuildRailCard(
        RectTransform parent,
        string name,
        Func<string> title,
        Func<string> subtitle,
        string icon,
        Color accent,
        Vector2 position,
        UnityAction onClick)
    {
        RailCard card = new RailCard();

        RectTransform rect = ArcadeUiFactory.At(
            parent,
            name,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            position,
            new Vector2(360f, 104f)
        );

        card.Root = rect.gameObject;
        card.Button = ArcadeUiFactory.Button(
            rect,
            ArcadeKitAssets.RoundRect,
            ArcadeUiTheme.Alpha(ArcadeUiTheme.Panel, 0.94f),
            null,
            ArcadeKitAssets.Body,
            16f,
            ArcadeUiTheme.TextPrimary,
            onClick
        );
        card.Background = rect.GetComponent<Image>();
        AddShadow(rect, new Vector2(0f, -4f), 0.32f);

        RectTransform iconTile = ArcadeUiFactory.At(
            rect,
            "IconTile",
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(22f, 0f),
            new Vector2(64f, 64f)
        );
        ArcadeUiFactory.Image(
            iconTile,
            ArcadeKitAssets.IconTile,
            ArcadeUiTheme.Alpha(accent, 0.3f),
            true
        );

        card.Icon = ArcadeUiFactory.Icon(
            iconTile,
            "Icon",
            icon,
            accent,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(34f, 34f)
        );

        card.Title = ArcadeUiFactory.Label(
            rect,
            "Title",
            "",
            ArcadeKitAssets.BodyBold,
            21f,
            ArcadeUiTheme.TextPrimary,
            TextAlignmentOptions.Left,
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(100f, 12f),
            new Vector2(150f, 28f),
            true
        );

        card.Subtitle = ArcadeUiFactory.Label(
            rect,
            "Subtitle",
            "",
            ArcadeKitAssets.Body,
            14f,
            ArcadeUiTheme.TextSecondary,
            TextAlignmentOptions.Left,
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(100f, -16f),
            new Vector2(150f, 22f),
            true
        );

        RectTransform badge = ArcadeUiFactory.At(
            rect,
            "Badge",
            new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(-14f, 0f),
            new Vector2(84f, 30f)
        );
        ArcadeUiFactory.Image(badge, ArcadeKitAssets.RoundRect, ArcadeUiTheme.Gold, true);
        card.BadgeLabel = ArcadeUiFactory.Label(
            badge,
            "Label",
            "NEW",
            ArcadeKitAssets.BodyBold,
            13f,
            ArcadeUiTheme.TextDark,
            TextAlignmentOptions.Center,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(80f, 26f),
            true
        );
        card.Badge = badge.gameObject;

        AddLangBinding(card.Title, title);
        AddLangBinding(card.Subtitle, subtitle);

        return card;
    }

    private void BuildRightRail(RectTransform parent)
    {
        BuildWeaponCard(parent);
        BuildPlayerCard(parent);
    }

    private void BuildWeaponCard(RectTransform parent)
    {
        RectTransform card = ArcadeUiFactory.At(
            parent,
            "WeaponCard",
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-44f, -150f),
            new Vector2(396f, 252f)
        );
        weaponCardRoot = card.gameObject;
        ArcadeUiFactory.Image(
            card,
            ArcadeKitAssets.RoundRect,
            ArcadeUiTheme.Alpha(ArcadeUiTheme.Panel, 0.96f),
            true
        );
        AddShadow(rect: card, offset: new Vector2(0f, -5f), alpha: 0.36f);

        RectTransform tag = ArcadeUiFactory.At(
            card,
            "Tag",
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(16f, -16f),
            new Vector2(238f, 30f)
        );
        ArcadeUiFactory.Image(tag, ArcadeKitAssets.RoundRect, ArcadeUiTheme.Orange, true);
        weaponTagText = ArcadeUiFactory.Label(
            tag,
            "Label",
            "",
            ArcadeKitAssets.BodyBold,
            12f,
            ArcadeUiTheme.TextDark,
            TextAlignmentOptions.Center,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(230f, 26f),
            true
        );

        RectTransform badge = ArcadeUiFactory.At(
            card,
            "Badge",
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-16f, -16f),
            new Vector2(72f, 30f)
        );
        ArcadeUiFactory.Image(badge, ArcadeKitAssets.RoundRect, ArcadeUiTheme.Gold, true);
        ArcadeUiFactory.Label(
            badge,
            "Label",
            "FREE",
            ArcadeKitAssets.BodyBold,
            13f,
            ArcadeUiTheme.TextDark,
            TextAlignmentOptions.Center,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(68f, 26f)
        );
        weaponBadge = badge.gameObject;

        RectTransform iconGlow = ArcadeUiFactory.At(
            card,
            "IconGlow",
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-70f, -104f),
            new Vector2(160f, 160f)
        );
        ArcadeUiFactory.Image(
            iconGlow,
            ArcadeKitAssets.Glow,
            ArcadeUiTheme.Alpha(ArcadeUiTheme.Gold, 0.22f)
        );

        RectTransform icon = ArcadeUiFactory.At(
            card,
            "Icon",
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-70f, -100f),
            new Vector2(96f, 96f)
        );
        weaponIconImage = ArcadeUiFactory.Image(icon, ArcadeKitAssets.Coin, Color.white);

        weaponTitleText = ArcadeUiFactory.Label(
            card,
            "Title",
            "",
            ArcadeKitAssets.BodyBold,
            25f,
            ArcadeUiTheme.TextPrimary,
            TextAlignmentOptions.Left,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(18f, -66f),
            new Vector2(200f, 34f),
            true
        );

        weaponHintText = ArcadeUiFactory.Label(
            card,
            "Hint",
            "",
            ArcadeKitAssets.Body,
            15f,
            ArcadeUiTheme.TextSecondary,
            TextAlignmentOptions.TopLeft,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(18f, -104f),
            new Vector2(200f, 44f)
        );

        RectTransform action = ArcadeUiFactory.At(
            card,
            "Action",
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(18f, 18f),
            new Vector2(220f, 56f)
        );
        weaponActionButton = ArcadeUiFactory.Button(
            action,
            ArcadeKitAssets.RoundRect,
            ArcadeUiTheme.Gold,
            "",
            ArcadeKitAssets.Display,
            22f,
            ArcadeUiTheme.TextDark,
            ClaimWeaponAd
        );
        weaponButtonLabel = action.Find("Label").GetComponent<TMP_Text>();
    }

    private void BuildPlayerCard(RectTransform parent)
    {
        RectTransform card = ArcadeUiFactory.At(
            parent,
            "PlayerCard",
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-44f, -414f),
            new Vector2(396f, 214f)
        );
        playerCardRoot = card.gameObject;
        ArcadeUiFactory.Button(
            card,
            ArcadeKitAssets.RoundRect,
            ArcadeUiTheme.Alpha(ArcadeUiTheme.PanelRaised, 0.96f),
            null,
            ArcadeKitAssets.Body,
            16f,
            ArcadeUiTheme.TextPrimary,
            OpenStats
        );
        AddShadow(card, new Vector2(0f, -5f), 0.36f);

        RectTransform hex = ArcadeUiFactory.At(
            card,
            "Avatar",
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(20f, -22f),
            new Vector2(86f, 96f)
        );
        ArcadeUiFactory.Image(hex, ArcadeKitAssets.HexFill, ArcadeUiTheme.Panel);
        RectTransform avatarRing = ArcadeUiFactory.At(
            card,
            "AvatarRing",
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(20f, -22f),
            new Vector2(94f, 104f)
        );
        ArcadeUiFactory.Image(avatarRing, ArcadeKitAssets.HexRing, ArcadeUiTheme.Gold);
        RectTransform avatarIcon = ArcadeUiFactory.At(
            card,
            "AvatarIcon",
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(42f, -49f),
            new Vector2(42f, 42f)
        );
        ArcadeUiFactory.Image(avatarIcon, ArcadeKitAssets.Icon("person"), ArcadeUiTheme.Gold);

        playerNameText = ArcadeUiFactory.Label(
            card,
            "Name",
            "",
            ArcadeKitAssets.BodyBold,
            22f,
            ArcadeUiTheme.TextPrimary,
            TextAlignmentOptions.Left,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(126f, -30f),
            new Vector2(250f, 28f),
            true
        );

        playerLevelText = ArcadeUiFactory.Label(
            card,
            "Level",
            "",
            ArcadeKitAssets.BodyBold,
            15f,
            ArcadeUiTheme.TextPrimary,
            TextAlignmentOptions.Left,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(126f, -60f),
            new Vector2(250f, 22f),
            true
        );

        RectTransform bar = ArcadeUiFactory.At(
            card,
            "XpBar",
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(126f, -90f),
            new Vector2(248f, 12f)
        );
        playerXpBar = ArcadeUiFactory.Bar(
            bar,
            0f,
            false,
            ArcadeUiTheme.BackgroundDeep,
            ArcadeUiTheme.Gold
        );

        playerXpText = ArcadeUiFactory.Label(
            card,
            "XpText",
            "",
            ArcadeKitAssets.Body,
            14f,
            ArcadeUiTheme.TextSecondary,
            TextAlignmentOptions.Left,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(126f, -108f),
            new Vector2(250f, 22f)
        );
        playerXpText.color = ArcadeUiTheme.TextSecondary;

        RectTransform statsHint = ArcadeUiFactory.At(
            card,
            "StatsHint",
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(20f, 16f),
            new Vector2(300f, 30f)
        );
        ArcadeUiFactory.Image(
            statsHint,
            ArcadeKitAssets.Icon("chart"),
            ArcadeUiTheme.Cyan,
            false
        );
        statsHint.sizeDelta = new Vector2(22f, 22f);
        statsHint.anchoredPosition = new Vector2(31f, 21f);

        TMP_Text statsLink = ArcadeUiFactory.Label(
            card,
            "StatsLink",
            "",
            ArcadeKitAssets.BodyBold,
            15f,
            ArcadeUiTheme.TextSecondary,
            TextAlignmentOptions.Left,
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(54f, 8f),
            new Vector2(300f, 26f),
            true
        );
        AddLangBinding(statsLink, () => Lang.GetOr("menu.skin.stats", "СТАТИСТИКА"));
    }

    private void BuildCenter(RectTransform parent)
    {
        var topLeft = new Vector2(0, 1);
        var brand = ArcadeUiFactory.At(parent, "Brand", topLeft, topLeft,
            new Vector2(40, -32), new Vector2(286, 80));
        ArcadeUiFactory.Image(brand, ArcadeKitAssets.CutPanel, ArcadeUiTheme.Ink, true);
        var logo = ArcadeUiFactory.Label(ArcadeUiFactory.Stretch(brand, "Wordmark", 12, 6, 12, 6),
            "BULLET <color=#E9C794>RUSH</color>", ArcadeKitAssets.Display, 36,
            MenuSheet.Paper, TextAlignmentOptions.Center);
        logo.enableWordWrapping = false;

        RectTransform mapChip = ArcadeUiFactory.At(
            parent,
            "MapChip",
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(-171f, -140f),
            new Vector2(326f, 68f)
        );
        ArcadeUiFactory.Button(
            mapChip,
            ArcadeKitAssets.RoundRect,
            ArcadeUiTheme.Alpha(ArcadeUiTheme.Panel, 0.95f),
            null,
            ArcadeKitAssets.Body,
            16f,
            ArcadeUiTheme.TextPrimary,
            OpenMaps
        );

        RectTransform mapIcon = ArcadeUiFactory.At(
            mapChip,
            "MapIcon",
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(28f, 0f),
            new Vector2(28f, 28f)
        );
        ArcadeUiFactory.Image(mapIcon, ArcadeKitAssets.Icon("target"), ArcadeUiTheme.Green);

        mapNameText = ArcadeUiFactory.Label(
            mapChip,
            "MapName",
            "",
            ArcadeKitAssets.BodyBold,
            20f,
            ArcadeUiTheme.TextPrimary,
            TextAlignmentOptions.Left,
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(64f, -9f),
            new Vector2(240f, 34f)
        );

        TMP_Text change = ArcadeUiFactory.Label(
            mapChip,
            "Change",
            "",
            ArcadeKitAssets.BodyBold,
            13f,
            ArcadeUiTheme.TextPrimary,
            TextAlignmentOptions.Left,
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(64f, 16f),
            new Vector2(110f, 24f),
            true
        );
        AddLangBinding(change, () => Lang.GetOr("menu.skin.change", "СМЕНИТЬ"));

        RectTransform modifier = ArcadeUiFactory.At(
            parent,
            "ModifierCard",
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(171f, -140f),
            new Vector2(326f, 68f)
        );
        modifierButton = ArcadeUiFactory.Button(
            modifier,
            ArcadeKitAssets.RoundRect,
            ArcadeUiTheme.Alpha(ArcadeUiTheme.PanelRaised, 0.96f),
            null,
            ArcadeKitAssets.Body,
            16f,
            ArcadeUiTheme.TextPrimary,
            CycleModifier
        );
        AddShadow(modifier, new Vector2(0f, -4f), 0.32f);

        RectTransform modifierIcon = ArcadeUiFactory.At(
            modifier,
            "Icon",
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(36f, 0f),
            new Vector2(30f, 30f)
        );
        ArcadeUiFactory.Image(modifierIcon, ArcadeKitAssets.Icon("flame"), ArcadeUiTheme.Orange);

        TMP_Text modifierLabel = ArcadeUiFactory.Label(
            modifier,
            "Caption",
            "",
            ArcadeKitAssets.BodyBold,
            12f,
            ArcadeUiTheme.TextSecondary,
            TextAlignmentOptions.Left,
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(66f, 16f),
            new Vector2(300f, 20f),
            true
        );
        AddLangBinding(modifierLabel, () => Lang.GetOr("menu.skin.modifier", "МОДИФИКАТОР"));

        modifierValueText = ArcadeUiFactory.Label(
            modifier,
            "Value",
            "",
            ArcadeKitAssets.BodyBold,
            20f,
            ArcadeUiTheme.TextPrimary,
            TextAlignmentOptions.Left,
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(66f, -12f),
            new Vector2(210f, 28f),
            true
        );

        RectTransform nextIcon = ArcadeUiFactory.At(
            modifier,
            "Next",
            new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(-24f, 0f),
            new Vector2(22f, 22f)
        );
        ArcadeUiFactory.Image(nextIcon, ArcadeKitAssets.Icon("arrow"), ArcadeUiTheme.Gold);

        RectTransform play = ArcadeUiFactory.At(
            parent,
            "PlayButton",
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, -236f),
            new Vector2(668f, 92f)
        );
        playButton = ArcadeUiFactory.Button(
            play,
            ArcadeKitAssets.ButtonOrange,
            ArcadeUiTheme.Gold,
            "",
            ArcadeKitAssets.Display,
            44f,
            ArcadeUiTheme.TextDark,
            Play
        );
        AddShadow(play, new Vector2(0f, -7f), 0.4f);

        TMP_Text playLabel = play.Find("Label").GetComponent<TMP_Text>();
        playLabel.characterSpacing = 6f;
        playLabel.fontStyle = FontStyles.UpperCase;
        AddLangBinding(playLabel, () => Lang.GetOr("menu.play", "ИГРАТЬ"));

        RectTransform modeRow = ArcadeUiFactory.At(
            parent,
            "GameModeRow",
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, -66f),
            new Vector2(470f, 48f)
        );

        RectTransform pve = ArcadeUiFactory.At(
            modeRow,
            "PveButton",
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0f),
            new Vector2(220f, 48f)
        );
        pveModeButton = ArcadeUiFactory.Button(
            pve,
            ArcadeKitAssets.RoundRect,
            ArcadeUiTheme.Green,
            "",
            ArcadeKitAssets.BodyBold,
            16f,
            ArcadeUiTheme.TextDark,
            SelectPveMode
        );
        pveModeLabel = pve.transform.Find("Label").GetComponent<TMP_Text>();
        AddShadow(pve, new Vector2(0f, -4f), 0.3f);

        RectTransform pvp = ArcadeUiFactory.At(
            modeRow,
            "PvpButton",
            new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(0f, 0f),
            new Vector2(220f, 48f)
        );
        pvpModeButton = ArcadeUiFactory.Button(
            pvp,
            ArcadeKitAssets.RoundRect,
            ArcadeUiTheme.Alpha(ArcadeUiTheme.Panel, 0.94f),
            "",
            ArcadeKitAssets.BodyBold,
            16f,
            ArcadeUiTheme.TextPrimary,
            SelectPvpMode
        );
        pvpModeLabel = pvp.transform.Find("Label").GetComponent<TMP_Text>();
        AddShadow(pvp, new Vector2(0f, -4f), 0.3f);

    }

    private void BuildBottom(RectTransform parent)
    {
        const float tileWidth = 178f;
        const float gap = 10f;

        float total = tileWidth * 6f + gap * 5f;
        float start = -total * 0.5f + tileWidth * 0.5f;

        tipsTile = BuildStatTile(
            parent,
            "TipsTile",
            () => Lang.GetOr("menu.skin.tips", "КАК ИГРАТЬ"),
            "book",
            ArcadeUiTheme.Green,
            new Vector2(start, 46f),
            tileWidth,
            OpenTips
        );

        missionsTile = BuildStatTile(
            parent,
            "MissionsTile",
            () => Lang.GetOr("menu.skin.missions", "ЗАДАНИЯ"),
            "scroll",
            ArcadeUiTheme.Gold,
            new Vector2(start + (tileWidth + gap) * 1f, 46f),
            tileWidth,
            OpenMissions
        );

        achievementsTile = BuildStatTile(
            parent,
            "AchievementsTile",
            () => Lang.GetOr("menu.skin.achievements", "ДОСТИЖЕНИЯ"),
            "trophy",
            ArcadeUiTheme.Purple,
            new Vector2(start + (tileWidth + gap) * 2f, 46f),
            tileWidth,
            OpenAchievements
        );

        masteryTile = BuildStatTile(
            parent,
            "MasteryTile",
            () => Lang.GetOr("progression.mastery.button", "МАСТЕРСТВО"),
            "sword",
            ArcadeUiTheme.Orange,
            new Vector2(start + (tileWidth + gap) * 3f, 46f),
            tileWidth,
            OpenMastery
        );

        BuildStatTile(
            parent,
            "LeaderboardsTile",
            () => Lang.GetOr("menu.leaderboard.title", "РЕЙТИНГИ"),
            "trophy",
            ArcadeUiTheme.Red,
            new Vector2(start + (tileWidth + gap) * 4f, 46f),
            tileWidth,
            OpenLeaderboards
        );

        StatTile journeyTile = BuildStatTile(
            parent,
            "JourneyTile",
            () => GameplayJourney.T("ПУТЬ БОЙЦА", "FIGHTER'S JOURNEY", "SAVAŞÇININ YOLU"),
            "book",
            ArcadeUiTheme.Gold,
            new Vector2(start + (tileWidth + gap) * 5f, 46f),
            tileWidth,
            () => GameplayJourney.Instance?.UI?.OpenJournal()
        );
        journeyTile.Badge.SetActive(false);
        AddLangBinding(journeyTile.Value, () => GameplayJourney.RankName(
            GameplayJourney.Instance != null ? GameplayJourney.Instance.Data.rank : 0));

        BuildLangPills(
            parent,
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(40f, 34f),
            0.95f
        );

        versionText = ArcadeUiFactory.Label(
            parent,
            "Version",
            "",
            ArcadeKitAssets.Body,
            13f,
            ArcadeUiTheme.TextMuted,
            TextAlignmentOptions.Left,
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(40f, 8f),
            new Vector2(160f, 22f)
        );
        versionText.text = "v" + Application.version;

        RectTransform exit = ArcadeUiFactory.At(
            parent,
            "ExitButton",
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(-40f, 34f),
            new Vector2(156f, 48f)
        );
        exitButton = ArcadeUiFactory.Button(
            exit,
            ArcadeKitAssets.RoundRect,
            ArcadeUiTheme.Alpha(ArcadeUiTheme.PanelRaised, 0.94f),
            "",
            ArcadeKitAssets.BodyBold,
            16f,
            ArcadeUiTheme.TextSecondary,
            ExitGame
        );

        RectTransform exitIcon = ArcadeUiFactory.At(
            exit,
            "Icon",
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(24f, 0f),
            new Vector2(22f, 22f)
        );
        ArcadeUiFactory.Image(exitIcon, ArcadeKitAssets.Icon("exit"), ArcadeUiTheme.Red);

        TMP_Text exitLabel = exit.Find("Label").GetComponent<TMP_Text>();
        exitLabel.alignment = TextAlignmentOptions.Right;
        AddLangBinding(exitLabel, () => Lang.GetOr("menu.skin.exit", "ВЫХОД"));
    }

    private StatTile BuildStatTile(
        RectTransform parent,
        string name,
        Func<string> title,
        string icon,
        Color accent,
        Vector2 position,
        float width,
        UnityAction onClick)
    {
        StatTile tile = new StatTile();

        RectTransform rect = ArcadeUiFactory.At(
            parent,
            name,
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            position,
            new Vector2(width, 98f)
        );

        tile.Root = rect.gameObject;
        tile.Button = ArcadeUiFactory.Button(
            rect,
            ArcadeKitAssets.RoundRect,
            ArcadeUiTheme.Alpha(ArcadeUiTheme.Panel, 0.94f),
            null,
            ArcadeKitAssets.Body,
            16f,
            ArcadeUiTheme.TextPrimary,
            onClick
        );
        tile.Background = rect.GetComponent<Image>();
        AddShadow(rect, new Vector2(0f, -4f), 0.3f);

        RectTransform iconTile = ArcadeUiFactory.At(
            rect,
            "IconTile",
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(10f, 16f),
            new Vector2(30f, 30f)
        );
        ArcadeUiFactory.Image(
            iconTile,
            ArcadeKitAssets.IconTile,
            ArcadeUiTheme.Alpha(accent, 0.2f),
            true
        );
        ArcadeUiFactory.Icon(
            iconTile,
            "Icon",
            icon,
            accent,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(20f, 20f)
        );

        tile.Title = ArcadeUiFactory.Label(
            rect,
            "Title",
            "",
            ArcadeKitAssets.BodyBold,
            17f,
            ArcadeUiTheme.TextPrimary,
            TextAlignmentOptions.Left,
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(46f, 14f),
            new Vector2(width - 52f, 32f),
            true
        );
        tile.Title.enableAutoSizing = true;
        tile.Title.fontSizeMin = 13f;
        tile.Title.fontSizeMax = 17f;

        tile.Value = ArcadeUiFactory.Label(
            rect,
            "Value",
            "",
            ArcadeKitAssets.BodyBold,
            11f,
            ArcadeUiTheme.TextSecondary,
            TextAlignmentOptions.Left,
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(14f, -16f),
            new Vector2(width - 28f, 24f),
            true
        );
        tile.Value.enableAutoSizing = true;
        tile.Value.fontSizeMin = 11f;
        tile.Value.fontSizeMax = 14f;

        RectTransform badge = ArcadeUiFactory.At(
            rect,
            "Badge",
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-10f, -10f),
            new Vector2(22f, 22f)
        );
        ArcadeUiFactory.Image(badge, ArcadeKitAssets.Spark, ArcadeUiTheme.Red);
        tile.Badge = badge.gameObject;

        AddLangBinding(tile.Title, title);

        return tile;
    }

    private void BuildLangPills(
        RectTransform parent,
        Vector2 anchor,
        Vector2 pivot,
        Vector2 position,
        float alpha)
    {
        BuildLangPill(parent, anchor, pivot, position, LangCode.Ru, "RU", alpha);
        BuildLangPill(
            parent,
            anchor,
            pivot,
            position + new Vector2(76f, 0f),
            LangCode.En,
            "EN",
            alpha
        );
        BuildLangPill(
            parent,
            anchor,
            pivot,
            position + new Vector2(152f, 0f),
            LangCode.Tr,
            "TR",
            alpha
        );
    }

    private void BuildLangPill(
        RectTransform parent,
        Vector2 anchor,
        Vector2 pivot,
        Vector2 position,
        LangCode code,
        string text,
        float alpha)
    {
        RectTransform rect = ArcadeUiFactory.At(
            parent,
            "Lang" + text,
            anchor,
            pivot,
            position,
            new Vector2(68f, 38f)
        );
        Image background = ArcadeUiFactory.Image(
            rect,
            ArcadeKitAssets.RoundRect,
            ArcadeUiTheme.Alpha(ArcadeUiTheme.PanelRaised, alpha),
            true
        );
        background.raycastTarget = true;

        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        button.transition = Selectable.Transition.None;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(() => Lang.SetUserLanguage(code));
        rect.gameObject.AddComponent<ArcadeHoverLift>().Bind(background, 1.04f, 0.1f);

        TMP_Text label = ArcadeUiFactory.Label(
            rect,
            "Label",
            text,
            ArcadeKitAssets.BodyBold,
            15f,
            ArcadeUiTheme.TextSecondary,
            TextAlignmentOptions.Center,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(64f, 30f)
        );

        langPills.Add(new LangPill
        {
            Background = background,
            Label = label,
            Code = code
        });
    }

    private void BuildToast()
    {
        RectTransform host = transform as RectTransform;

        if (host == null)
            return;

        RectTransform rect = ArcadeUiFactory.Stretch(
            host,
            "ArcadeSkinToast",
            0f,
            0f,
            0f,
            0f
        );
        ResponsiveUi.BindSafeArea(rect);

        toastGroup = rect.gameObject.AddComponent<CanvasGroup>();
        toastGroup.alpha = 0f;
        toastGroup.blocksRaycasts = false;
        toastGroup.interactable = false;

        RectTransform card = ArcadeUiFactory.At(
            rect,
            "ToastCard",
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 190f),
            new Vector2(500f, 66f)
        );
        ArcadeUiFactory.Image(
            card,
            ArcadeKitAssets.RoundRect,
            ArcadeUiTheme.Alpha(ArcadeUiTheme.BackgroundDeep, 0.96f),
            true
        );
        AddShadow(card, new Vector2(0f, -5f), 0.4f);

        toastText = ArcadeUiFactory.Label(
            card,
            "Text",
            "",
            ArcadeKitAssets.BodyBold,
            19f,
            ArcadeUiTheme.GoldBright,
            TextAlignmentOptions.Center,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(470f, 40f),
            true
        );

        toastRoot = rect.gameObject;
        toastRoot.SetActive(false);
    }

    private static void AddShadow(RectTransform rect, Vector2 offset, float alpha)
    {
        Image image = rect.GetComponent<Image>();

        if (image == null)
            return;

        Shadow shadow = rect.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, alpha);
        shadow.effectDistance = offset;
        shadow.useGraphicAlpha = true;
    }

    private static void AddShadowText(TMP_Text text, Vector2 offset, float alpha)
    {
        Shadow shadow = text.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, alpha);
        shadow.effectDistance = offset;
        shadow.useGraphicAlpha = true;
    }
}
