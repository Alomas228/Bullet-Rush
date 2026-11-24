using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using static GameplayJourney;

public sealed class ProtectedCache : CombatMarkSource
{
    private EnemySpawner spawner;
    private Action complete;
    private readonly List<Enemy> guards = new List<Enemy>();
    private bool engaged, resolved;
    private float expires = 22, nextHint;
    public void Initialize(EnemySpawner source, Action done)
    {
        spawner = source; complete = done;
        Enemy.OnEnemyKilled += Killed;
        Instance?.Notify(T("Защищённый тайник: подойди и нажми E. 6 стражей → выбор улучшения после волны.",
            "Protected cache: approach and press E. 6 guards → an extra upgrade choice after the wave.",
            "Korumalı sandık: yaklaş ve E'ye bas. 6 muhafız → dalga sonunda ek geliştirme seçimi."));
    }
    private void Update()
    {
        if (resolved || Instance == null || GameStateManager.Instance == null || GameStateManager.Instance.CurrentState != GameState.Playing) return;
        if (engaged) return;
        expires -= Time.deltaTime;
        if (expires <= 0) { Finish(); return; }
        if (PlayerHealth.Instance == null || (PlayerHealth.Instance.transform.position - transform.position).sqrMagnitude > 9) return;
        if (Time.time >= nextHint)
        { nextHint = Time.time + 3; Instance.Notify(T("[E] Тайник: 6 стражей → 1 выбор улучшения", "[E] Cache: 6 guards → 1 upgrade choice", "[E] Sandık: 6 muhafız → 1 geliştirme seçimi")); }
        if (Time.timeScale <= 0 || JourneyUI.IsBlocking || Keyboard.current == null || !Keyboard.current.eKey.wasPressedThisFrame) return;
        Instance.UI.ShowChoice(T("ВСКРЫТЬ ТАЙНИК?", "OPEN THE CACHE?", "SANDIK AÇILSIN MI?"),
            T("Появятся 6 дополнительных противников. Победи стражей и получи дополнительный выбор из трёх улучшений после зачистки волны. Отказ ничего не отнимает.",
                "6 extra enemies will spawn. Defeat the guards to earn an additional three-card upgrade choice after clearing the wave. Declining has no penalty.",
                "6 ek düşman doğacak. Muhafızları yenerek dalga sonunda üç karttan ek geliştirme seçimi kazan. Reddetmenin cezası yok."),
            new[] { T("Вызвать стражей", "Challenge guards", "Muhafızlara meydan oku"), T("Пройти мимо", "Walk away", "Vazgeç") },
            choice => { if (this == null || resolved) return; if (choice == 0) Activate(); else Finish(); });
    }
    private void Activate()
    {
        if (spawner == null || !spawner.HasRoomForEvent(6))
        { Instance?.Notify(T("Слишком тесно — сначала расчисти арену", "Too crowded — clear some enemies first", "Çok kalabalık — önce alanı temizle")); return; }
        engaged = true;
        for (int i = 0; i < 6; i++)
        {
            Vector3 position = spawner.GetPlayerRingSpawnPosition();
            Enemy enemy = spawner.SpawnEnemyAtPosition(i == 0 ? EnemyType.Tank : i % 2 == 0 ? EnemyType.Fast : EnemyType.Normal, position, 0.8f);
            if (enemy != null) { guards.Add(enemy); enemy.MarkCacheGuard(); }
        }
        if (guards.Count == 0) Finish();
        else Instance?.Notify(T("У стражей золотые щитки", "Look for the guards with gold badges", "Muhafızlarda altın kalkan rozeti var"));
    }
    internal override void Draw(CombatMarkBatch batch)
    {
        Vector3 position = transform.position;
        position.y -= .4f;
        batch.Stamp(CombatMarkShape.Cache, Matrix4x4.TRS(position, Quaternion.Euler(0,-18,0), Vector3.one));
    }
    private void Killed(Enemy enemy)
    {
        if (!engaged || resolved) return;
        guards.Remove(enemy);
        guards.RemoveAll(e => e == null || e.IsDead);
        if (guards.Count > 0) return;
        Instance?.CacheWon(); Finish();
    }
    private void Finish() { if (resolved) return; resolved = true; complete?.Invoke(); Destroy(gameObject); }
    private void OnDestroy() { Enemy.OnEnemyKilled -= Killed; }
}
