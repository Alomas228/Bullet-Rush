using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static GameplayJourney;

// Isolated top-down practice board. Reads WeaponData; never modifies equipment,
// currencies, mastery, the run's random stream or the combat arena.
public sealed class WeaponRangePreview : MonoBehaviour
{
    private sealed class Target
    {
        public RectTransform rect; public TMP_Text label;
        public float health, maxHealth, burn, burnTime, respawn;
    }
    private sealed class Shot
    {
        public RectTransform rect; public Vector2 position, velocity;
        public float damage; public int pierce;
        public readonly HashSet<Target> hit = new HashSet<Target>();
    }
    private WeaponData data;
    private RectTransform board, shooter;
    private TMP_Text totals;
    private readonly List<Target> targets = new List<Target>();
    private readonly List<Shot> shots = new List<Shot>();
    private readonly Stack<RectTransform> freeShots = new Stack<RectTransform>();
    private readonly System.Random random = new System.Random(713);
    private float cycle, volleyTimer, distance = 1, totalDamage, elapsed;
    private int remainingVolleys;
    private Vector2 origin, aim = Vector2.right;
    public void Build(RectTransform parent, WeaponData weapon)
    {
        StopPreview(); data = weapon; totalDamage = elapsed = cycle = 0; remainingVolleys = 0;
        var hint = ArcadeUiFactory.Label(ArcadeUiFactory.At(parent, "RangeHint", Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, new Vector2(700, 95)),
            T("Проба базовой стрельбы без улучшений. Наводи мышью, удерживай ЛКМ внутри поля. WASD — движение. Колесо — дистанция. Мишени восстанавливаются; награды не начисляются.",
                "Base-weapon practice without upgrades. Aim with the mouse; hold left click inside the board. WASD moves; wheel changes distance. Targets reset; no rewards are earned.",
                "Geliştirmesiz temel silah denemesi. Fareyle nişan al; alan içinde sol tıkı tut. WASD hareket, tekerlek mesafe. Hedefler yenilenir; ödül verilmez."),
            ArcadeKitAssets.Body, 17, ArcadeUiTheme.TextSecondary, TextAlignmentOptions.TopLeft);
        hint.gameObject.AddComponent<LayoutElement>().preferredHeight = 95;
        board = ArcadeUiFactory.At(parent, "PracticeBoard", Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, new Vector2(700, 280));
        board.gameObject.AddComponent<LayoutElement>().preferredHeight = 280;
        ArcadeUiFactory.Image(board, null, new Color(0.035f, 0.065f, 0.09f));
        board.gameObject.AddComponent<RectMask2D>();
        shooter = Dot(board, "Shooter", new Color(0.2f, 0.8f, 1), new Vector2(18, 18));
        origin = new Vector2(-240, 0);
        for (int i = 0; i < 4; i++)
        {
            var target = new Target { maxHealth = i == 3 ? 3000 : 650 };
            target.health = target.maxHealth;
            target.rect = Dot(board, "Target", i == 3 ? new Color(1, 0.6f, 0.2f) : new Color(0.3f, 0.75f, 0.5f), i == 3 ? new Vector2(32, 32) : new Vector2(24, 24));
            target.label = ArcadeUiFactory.Label(ArcadeUiFactory.At(target.rect, "HP", new Vector2(0.5f, 1), new Vector2(0.5f, 0), new Vector2(0, 4), new Vector2(90, 24)), "",
                ArcadeKitAssets.BodyBold, 12, Color.white, TextAlignmentOptions.Center);
            targets.Add(target);
        }
        totals = ArcadeUiFactory.Label(ArcadeUiFactory.At(parent, "RangeTotals", Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, new Vector2(700, 35)), "",
            ArcadeKitAssets.Body, 17, ArcadeUiTheme.TextPrimary, TextAlignmentOptions.Left);
        totals.gameObject.AddComponent<LayoutElement>().preferredHeight = 35;
    }
    public void StopPreview()
    {
        data = null;
        foreach (var shot in shots) if (shot.rect != null) Destroy(shot.rect.gameObject);
        foreach (var rect in freeShots) if (rect != null) Destroy(rect.gameObject);
        shots.Clear(); freeShots.Clear(); targets.Clear(); board = null;
    }
    private void Update()
    {
        if (board == null || !board.gameObject.activeInHierarchy || data == null) return;
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        elapsed += dt; cycle -= dt;
        Vector2 half = board.rect.size * 0.5f;
        if (Keyboard.current != null)
        {
            var k = Keyboard.current;
            origin += new Vector2((k.dKey.isPressed ? 1 : 0) - (k.aKey.isPressed ? 1 : 0), (k.wKey.isPressed ? 1 : 0) - (k.sKey.isPressed ? 1 : 0)) * 90 * dt;
        }
        origin.x = Mathf.Clamp(origin.x, -half.x + 15, half.x - 15); origin.y = Mathf.Clamp(origin.y, -half.y + 15, half.y - 15);
        shooter.anchoredPosition = origin;
        bool held = false;
        if (Mouse.current != null)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(board, Mouse.current.position.ReadValue(), null, out var point);
            if (board.rect.Contains(point))
            {
                aim = (point - origin).normalized; held = Mouse.current.leftButton.isPressed;
                distance = Mathf.Clamp(distance + Mouse.current.scroll.ReadValue().y * 0.001f, 0.5f, 1.4f);
            }
        }
        if (held && cycle <= 0 && remainingVolleys == 0)
        {
            cycle = data.GetFireCycleDuration(1);
            remainingVolleys = data.SpecialType == WeaponSpecialType.Burst ? data.ShotsPerBurst : data.SpecialType == WeaponSpecialType.DoubleShot ? 2 : 1;
            volleyTimer = 0;
        }
        volleyTimer -= dt;
        while (remainingVolleys > 0 && volleyTimer <= 0)
        {
            Fire(); remainingVolleys--;
            volleyTimer += data.SpecialType == WeaponSpecialType.Burst ? data.GetBurstInterval(1) : data.GetDoubleShotInterval(1);
        }
        for (int i = 0; i < targets.Count; i++)
        {
            var target = targets[i];
            target.rect.anchoredPosition = new Vector2(Mathf.Clamp((40 + (i % 3) * 65) * distance, -half.x + 30, half.x - 30), i == 3 ? -85 : 35);
            target.respawn -= dt;
            if (target.health <= 0 && target.respawn <= 0) { target.health = target.maxHealth; target.burn = target.burnTime = 0; }
            if (target.burnTime > 0 && target.health > 0) { Hit(target, target.burn * dt); target.burnTime -= dt; }
            target.rect.gameObject.SetActive(target.health > 0);
            target.label.text = Mathf.CeilToInt(Mathf.Max(0, target.health)) + " HP";
        }
        for (int i = shots.Count - 1; i >= 0; i--)
        {
            var shot = shots[i]; Vector2 before = shot.position; shot.position += shot.velocity * dt;
            bool remove = Mathf.Abs(shot.position.x) > half.x || Mathf.Abs(shot.position.y) > half.y;
            foreach (var target in targets)
            {
                if (target.health <= 0 || shot.hit.Contains(target)) continue;
                Vector2 segment = shot.position - before;
                float t = segment.sqrMagnitude < 0.001f ? 0 : Mathf.Clamp01(Vector2.Dot(target.rect.anchoredPosition - before, segment) / segment.sqrMagnitude);
                if ((target.rect.anchoredPosition - (before + segment * t)).sqrMagnitude > 18 * 18) continue;
                shot.hit.Add(target); Hit(target, shot.damage);
                if (data.BurnDamagePerSecond > 0) { target.burn = data.BurnDamagePerSecond; target.burnTime = data.BurnDuration; }
                if (random.NextDouble() < data.LightningChance)
                {
                    int count = 0;
                    foreach (var other in targets)
                        if (other != target && other.health > 0 && count < data.LightningTargets &&
                            Vector2.Distance(other.rect.anchoredPosition, target.rect.anchoredPosition) <= data.LightningRange * 18)
                        { Hit(other, data.LightningDamage); count++; }
                }
                if (shot.pierce-- <= 0) { remove = true; break; }
            }
            shot.rect.anchoredPosition = shot.position;
            if (remove) { shot.rect.gameObject.SetActive(false); freeShots.Push(shot.rect); shots.RemoveAt(i); }
        }
        totals.text = T("Нанесённый урон: ", "Damage dealt: ", "Verilen hasar: ") + Mathf.RoundToInt(totalDamage) + "    " +
            T("Базовый цикл: ", "Base cycle: ", "Temel döngü: ") + data.GetFireCycleDuration(1).ToString("0.00") + " s";
    }
    private void Hit(Target target, float damage)
    {
        if (target.health <= 0) return;
        totalDamage += Mathf.Min(target.health, damage); target.health -= damage;
        if (target.health <= 0) target.respawn = 1.5f;
    }
    private void Fire()
    {
        for (int i = 0; i < data.ProjectileCount; i++)
        {
            if (shots.Count >= 200) break;
            float angle = ((float)random.NextDouble() * 2 - 1) * data.SpreadAngle * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(aim.x * Mathf.Cos(angle) - aim.y * Mathf.Sin(angle), aim.x * Mathf.Sin(angle) + aim.y * Mathf.Cos(angle));
            bool critical = random.NextDouble() < data.CriticalChanceBonus;
            var rect = freeShots.Count > 0 ? freeShots.Pop() : Dot(board, "Shot", Color.yellow, new Vector2(6, 3));
            rect.gameObject.SetActive(true); rect.anchoredPosition = origin;
            shots.Add(new Shot { rect = rect, position = origin, velocity = dir * data.ProjectileSpeed * 18,
                damage = data.Damage * (critical ? 2 + data.CriticalDamageBonus : 1), pierce = data.PierceCount });
        }
        origin -= aim * data.RecoilForce * 10;
    }
    private static RectTransform Dot(Transform parent, string name, Color color, Vector2 size)
    {
        var rect = ArcadeUiFactory.At(parent, name, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, size);
        ArcadeUiFactory.Image(rect, null, color).raycastTarget = false; return rect;
    }
    private void OnDestroy() => StopPreview();
}
