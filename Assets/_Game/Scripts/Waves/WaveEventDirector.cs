using System.Collections;
using UnityEngine;

// Случайные события посреди волны: вынуждают игрока двигаться,
// а не стоять на месте. Одно событие на волну, типы не повторяются
// подряд из-за страховки от однообразия.
public class WaveEventDirector : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnemySpawner enemySpawner;
    [SerializeField] private WaveUI waveUI;

    [Header("Scheduling")]
    [Tooltip("С какой волны начинаются события.")]
    [SerializeField] private int minEventWave = 4;
    [Tooltip("Вероятность события в пригодной волне. Ниже 0.5 — чтобы больше половины волн оставались без события и игрок мог выдохнуть.")]
    [Range(0f, 1f)]
    [SerializeField] private float eventChancePerWave = 0.45f;
    [Tooltip("Минимальная задержка события после начала волны.")]
    [SerializeField] private float eventMinDelay = 6f;
    [Tooltip("Максимальная задержка события после начала волны.")]
    [SerializeField] private float eventMaxDelay = 14f;
    [Tooltip("Сколько волн подряд может пройти без события, прежде чем событие станет обязательным.")]
    [SerializeField] private int eventRepeatLimit = 3;
    [Tooltip("Минимальный отдых между событиями в секундах. Чинит главную проблему: волны могут идти по 15-20 секунд, и при шансе 0.55 событие успевало попадать и в предыдущую, и в следующую волну подряд.")]
    [SerializeField] private float eventCooldownSeconds = 20f;
    [Tooltip("Сколько волн подряд может идти с событием. Дальше — обязательная передышка, чтобы событие осталось событием, а не фоном.")]
    [Range(1, 4)]
    [SerializeField] private int maxConsecutiveEventWaves = 2;

    [Header("Ambush Event")]
    [Tooltip("Базовое число рванувших к игроку врагов.")]
    [SerializeField] private int ambushBaseCount = 4;
    [Tooltip("Добавляется за каждые N волн (начиная с минимальной).")]
    [SerializeField] private int ambushCountPerWave = 3;
    // Дистанция выросла с 9-13 до 13-17: раньше амбуш появлялся
    // практически на экране и без подсказки, что игрок успевает
    // среагировать. Теперь это «вне поля зрения, но рядом».
    [SerializeField] private float ambushMinDistance = 13f;
    [SerializeField] private float ambushMaxDistance = 17f;
    [Tooltip("Потолок числа врагов в амбуше.")]
    [SerializeField] private int ambushMaxCount = 8;
    [Tooltip("Пауза между врагами внутри амбуша. Пачка, появляющаяся в один кадр, нечитаема: игрок не может ни уклониться, ни выбрать цель.")]
    [SerializeField] private float ambushStagger = 0.16f;

    [Header("Rush Event")]
    [Tooltip("Стенда с одного фланга.")]
    [SerializeField] private int rushBaseCount = 6;
    [SerializeField] private int rushCountPerWave = 3;
    [Tooltip("Ширина дуги, из которой выходит пачка (градусы).")]
    [SerializeField] private float rushArcDegrees = 70f;
    [Tooltip("Потолок числа врагов в пачке.")]
    [SerializeField] private int rushMaxCount = 12;
    [Tooltip("Пауза между первой и второй половиной пачки.")]
    [SerializeField] private float rushStagedDelay = 1.4f;

    [Header("Danger Zone Event")]
    [SerializeField] private float zoneWarning = 1.4f;
    [SerializeField] private float zoneRadius = 3f;
    [SerializeField] private float zoneDamagePerSecond = 3f;
    [SerializeField] private float zoneDuration = 4f;
    [SerializeField] private float zoneGrowMultiplier = 1.8f;
    [Tooltip("Начиная с этой волны событие ставит сразу две зоны.")]
    [SerializeField] private int zoneSecondFromWave = 8;
    [Tooltip("Потолок числа зон за одно событие.")]
    [SerializeField] private int zoneMaxCount = 3;
    [Tooltip("Ближняя граница расстояния от игрока до центра зоны.")]
    [SerializeField] private float zoneSpreadMin = 5f;
    [Tooltip("Дальняя граница расстояния от игрока до центра зоны.")]
    [SerializeField] private float zoneSpreadMax = 9f;

    [Header("Presentation")]
    [SerializeField] private float bannerDuration = 1.6f;

    private enum WaveEventType
    {
        Ambush,
        DangerZone,
        Rush
    }

    private WaveEventType lastEventType;
    private bool waveRunning;
    private Coroutine scheduleCoroutine;

    // Сколько волн подряд прошло без события. Нужно, чтобы тихие
    // волны не копились: иначе после серии спокойных волн событие
    // выпадает на самую напряжённую и удваивает давление.
    private int wavesSinceEvent;

    // Сколько волн подряд уже было с событием. Второй ограничитель
    // против спама: даже при высоком шансе третья волна подряд
    // события не получает.
    private int consecutiveEventWaves;

    // Когда последнее событие реально запустилось. Пауза в
    // секундах нужна потому, что счётчик волн не защищает от
    // коротких волн: событие могло попасть в конец одной волны и
    // в начало следующей.
    private float lastEventTime = -999f;

    // Было ли событие именно на текущей волне.
    private bool eventFiredThisWave;

    public void Initialize(
        EnemySpawner spawner,
        WaveUI ui)
    {
        enemySpawner = spawner;
        waveUI = ui;
    }

    // Вызывается менеджером волн при старте волны.
    // Акцент волны сужает пул событий: если волна уже несёт свой
    // характер (заход с флангов, охота на элиту), событие не должно
    // дублировать его вторым тем же приёмом.
    public void OnWaveStarted(
        int wave,
        bool allowEvents,
        WaveModifier modifier = WaveModifier.None)
    {
        Stop();

        eventFiredThisWave = false;

        if (!allowEvents || wave < minEventWave)
            return;

        // Событие не может идти раньше, чем истёк отдых после
        // предыдущего. Короткая волна не должна успевать выдать
        // два события подряд.
        float sinceLast =
            Time.time - lastEventTime;

        if (sinceLast < Mathf.Max(eventCooldownSeconds, 0f))
            return;

        // Серия событий подряд — это уже не акцент, а фон.
        if (consecutiveEventWaves >=
            Mathf.Max(maxConsecutiveEventWaves, 1))
        {
            return;
        }

        waveRunning = true;

        scheduleCoroutine = StartCoroutine(
            ScheduleEvent(wave, modifier)
        );
    }

    // Вызывается при завершении волны.
    public void OnWaveEnded()
    {
        waveRunning = false;

        // Волна засчитывается как «тихая», если события на ней не
        // было. Раньше счётчик рос только когда шанс не сработал,
        // и волны, где событие отменилось из-за тесноты арены,
        // в счёт не попадали — обязательное событие потом
        // срывалось на самой напряжённой волне.
        if (!eventFiredThisWave)
        {
            wavesSinceEvent++;

            // Счётчик серии обнуляется на тихой волне. Без этого он
            // рос только вверх и однажды навсегда упирался в
            // maxConsecutiveEventWaves: после двух event-волн
            // OnWaveStarted отсекался на этой проверке и события
            // больше не появлялись до конца забега.
            consecutiveEventWaves = 0;
        }

        eventFiredThisWave = false;

        CancelSchedule();
    }

    // Полная остановка (меню, стоп игры).
    public void Stop()
    {
        waveRunning = false;

        CancelSchedule();
    }

    public void ResetRun()
    {
        wavesSinceEvent = 0;
        consecutiveEventWaves = 0;
        eventFiredThisWave = false;
        lastEventTime = -999f;
        lastEventType = WaveEventType.Ambush;

        Stop();
    }

    private void CancelSchedule()
    {
        if (scheduleCoroutine != null)
        {
            StopCoroutine(scheduleCoroutine);
            scheduleCoroutine = null;
        }
    }

    private IEnumerator ScheduleEvent(
        int wave,
        WaveModifier modifier)
    {
        // Слишком долгий шанс без событий — событие становится
        // обязательным: пауза нужна, но четыре подряд «обычные»
        // волны читаются как отсутствие содержимого.
        bool forced =
            wavesSinceEvent >= Mathf.Max(eventRepeatLimit, 1);

        if (!forced && Random.value > eventChancePerWave)
            yield break;

        float delay =
            Random.Range(
                eventMinDelay,
                eventMaxDelay
            );

        yield return new WaitForSeconds(delay);

        if (!CanRunEvent())
            yield break;

        WaveEventType type =
            PickEventType(modifier);

        yield return RunEvent(type, wave);
    }

    private bool CanRunEvent(
        WaveEventType type = default,
        int wave = 0,
        bool checkRoom = false)
    {
        if (!waveRunning)
            return false;

        if (GameStateManager.Instance != null &&
            GameStateManager.Instance.CurrentState !=
            GameState.Playing)
        {
            return false;
        }

        // Событие бессмысленно, если волна почти закончилась
        // и живых врагов уже нет.
        if (Enemy.AliveCount <= 0)
            return false;

        if (checkRoom &&
            enemySpawner != null &&
            !enemySpawner.HasRoomForEvent(
                GetPlannedCount(type, wave)))
        {
            return false;
        }

        return true;
    }

    // Сколько врагов (или зон) событие добавит на арену. Нужно
    // до запуска, чтобы проверить потолок живых и не превратить
    // событие в добивающий удар по уже перегруженной арене.
    private int GetPlannedCount(
        WaveEventType type,
        int wave)
    {
        switch (type)
        {
            case WaveEventType.Ambush:
                return Mathf.Min(
                    ambushBaseCount +
                    Mathf.Max(wave - minEventWave, 0) /
                    Mathf.Max(ambushCountPerWave, 1),
                    ambushMaxCount
                );

            case WaveEventType.Rush:
                return Mathf.Min(
                    rushBaseCount +
                    Mathf.Max(wave - minEventWave, 0) /
                    Mathf.Max(rushCountPerWave, 1),
                    rushMaxCount
                );

            case WaveEventType.DangerZone:
                return GetZoneCount(wave);
        }

        return 0;
    }

    private int GetZoneCount(int wave)
    {
        int zoneCount =
            wave >= zoneSecondFromWave
                ? 2
                : 1;

        return Mathf.Min(zoneCount, zoneMaxCount);
    }

    // Акцент волны больше не диктует единственное событие. Раньше
    // связка была жёсткой («дальники → мины», «охота → амбуш»),
    // и четыре волны из восьми всегда играли одинаково: игрок знал
    // по номеру волны, что его ждёт. Теперь акцент только запрещает
    // несовместимые типы, а выбор идёт между оставшимися.
    private WaveEventType PickEventType(
        WaveModifier modifier)
    {
        WaveEventType[] all =
        {
            WaveEventType.Ambush,
            WaveEventType.DangerZone,
            WaveEventType.Rush
        };

        // Совместимые с акцентом волны типы — это пул, из которого идёт
        // выбор. Два подряд одинаковых события читаются как повтор,
        // а не как разнообразие, поэтому прошлый тип по возможности
        // исключается — но если других вариантов нет, повтор всё
        // равно лучше, чем отмена события.
        WaveEventType[] pool =
            new WaveEventType[all.Length];

        int count = 0;

        for (int i = 0; i < all.Length; i++)
        {
            if (IsCompatible(all[i], modifier))
                pool[count++] = all[i];
        }

        // Страховка на будущее: если правила запретят все три типа,
        // событие всё равно должно состояться.
        if (count <= 0)
        {
            lastEventType = all[Random.Range(0, all.Length)];

            return lastEventType;
        }

        int freshCount = 0;

        for (int i = 0; i < count; i++)
        {
            if (pool[i] != lastEventType)
                freshCount++;
        }

        bool avoidLast =
            freshCount > 0;

        int candidates =
            avoidLast ? freshCount : count;

        int pick =
            Random.Range(0, candidates);

        for (int i = 0; i < count; i++)
        {
            if (avoidLast && pool[i] == lastEventType)
                continue;

            if (pick == 0)
            {
                lastEventType = pool[i];

                return lastEventType;
            }

            pick--;
        }

        lastEventType = pool[0];

        return lastEventType;
    }

    // Правила совместимости. Смысл каждого запрета — не дать
    // событию удвоить то, чему волна уже посвящена, и не дать
    // ему отнять внимание у одиночной цели.
    private static bool IsCompatible(
        WaveEventType type,
        WaveModifier modifier)
    {
        switch (type)
        {
            case WaveEventType.Ambush:
                // Амбуш — самый дешёвый по вниманию и самый
                // массовый приём: 4-8 врагов вокруг игрока.
                //  - «заход с флангов» уже делает то же самое;
                //  - «охота на элиту» рассчитана на одну цель —
                //    восемь рядовых врагов её просто закрывают;
                //  - «последний рубеж» выходит с края арены, и
                //    второй выход с другой стороны превращает
                //    его в хаос без выхода.
                return
                    !modifier.Has(WaveModifier.Ambush) &&
                    !modifier.Has(WaveModifier.EliteHunt) &&
                    !modifier.Has(WaveModifier.LastStand);

            case WaveEventType.Rush:
                // Навал — давление с одного направления.
                //  - «последний рубеж» уже идёт с края арены;
                //  - «заход с флангов» — то же давление, только
                //    с двух сторон.
                return
                    !modifier.Has(WaveModifier.LastStand) &&
                    !modifier.Has(WaveModifier.Ambush);

            case WaveEventType.DangerZone:
                // Мины — единственное событие, которое не добавляет
                // врагов, а меняет геометрию боя. Оно не дублирует
                // ни один акцент, поэтому совместимо со всеми, и
                // в том числе с «опасными зонами»: там просто
                // ставится одна зона вместо двух.
                return true;
        }

        return true;
    }

    // Если совместимых типов не оказалось (страховка на будущее),
    // берём тот, который был реже всего.
    private WaveEventType PickLeastRecent(
        WaveEventType[] all)
    {
        return all[
            (int)lastEventType + 1 >= all.Length
                ? 0
                : (int)lastEventType + 1
        ];
    }

    private IEnumerator RunEvent(
        WaveEventType type,
        int wave)
    {
        // Повторная проверка в момент запуска: между выбором типа и
        // его показом проходит несколько секунд, и арена за это время
        // могла забиться. Событие в таком случае просто не идёт —
        // переносить его на середину волны опаснее, чем пропустить.
        if (!CanRunEvent(type, wave, true))
            yield break;

        wavesSinceEvent = 0;
        consecutiveEventWaves++;
        eventFiredThisWave = true;
        lastEventTime = Time.time;

        switch (type)
        {
            case WaveEventType.Ambush:
                RunAmbush(wave);
                break;

            case WaveEventType.Rush:
                yield return RunRush(wave);
                break;

            case WaveEventType.DangerZone:
                SpawnDangerZones(wave);
                break;
        }

        yield return new WaitForSeconds(bannerDuration);

        if (waveUI != null)
            waveUI.Hide();
    }

    private void RunAmbush(int wave)
    {
        int count =
            ambushBaseCount +
            Mathf.Max(wave - minEventWave, 0) /
            Mathf.Max(ambushCountPerWave, 1);

        count = Mathf.Min(count, ambushMaxCount);

        if (enemySpawner != null)
        {
            enemySpawner.SpawnAmbush(
                count,
                ambushMinDistance,
                ambushMaxDistance,
                true,
                Mathf.Max(ambushStagger, 0f)
            );
        }

        ShowBanner(Lang.Get("wave.event_ambush"));
    }

    // Пачка приходит двумя волнами с одного и того же фланга:
    // первая обозначает направление, вторая приходит, пока игрок
    // ещё разворачивается. Одна волна читается как обычный спавн,
    // две с одним флангом — как давление, которому надо отступать.
    private IEnumerator RunRush(int wave)
    {
        int count =
            rushBaseCount +
            Mathf.Max(wave - minEventWave, 0) /
            Mathf.Max(rushCountPerWave, 1);

        count = Mathf.Min(count, rushMaxCount);

        int firstHalf =
            Mathf.Max(Mathf.CeilToInt(count * 0.5f), 1);

        float centerAngle =
            Random.Range(0f, 360f);

        if (enemySpawner != null)
        {
            enemySpawner.SpawnRushPack(
                firstHalf,
                rushArcDegrees,
                wave,
                centerAngle
            );
        }

        ShowBanner(Lang.Get("wave.event_surge"));

        yield return new WaitForSeconds(rushStagedDelay);

        // Пачка бьёт в два приёма. Если к началу второго приёма
        // арена забилась, доводить пачку нельзя: игрок получит
        // третью волну давления поверх уже неразрешимого боя.
        if (!CanRunEvent(WaveEventType.Rush, wave, true))
            yield break;

        if (enemySpawner != null)
        {
            enemySpawner.SpawnRushPack(
                count - firstHalf,
                rushArcDegrees,
                wave,
                centerAngle
            );
        }
    }

    // Одна зона читается как «осторожно». Две-три, разнесённые по
    // разным направлениям от игрока, — как участок, который надо
    // пересекать по маршруту. Разносить обязательно: две зоны в
    // одной точке — это одна зона вдвое шире, и игрок просто
    // обходит её стороной.
    private void SpawnDangerZones(int wave)
    {
        PlayerHealth playerHealth =
            PlayerHealth.Instance;

        if (playerHealth == null)
            return;

        int zoneCount =
            GetZoneCount(wave);

        float baseAngle =
            Random.Range(0f, 360f);

        for (int i = 0; i < zoneCount; i++)
        {
            PlayerHealth anchor =
                PlayerHealth.Instance;

            if (anchor == null)
                return;

            // Раскладываем зоны ровно по кольцу вокруг игрока: выбор
            // случайной точки рядом с игроком дважды даёт две зоны в
            // одном месте — игрок не увидит второй и посчитает, что
            // событие сработало один раз.
            float angle =
                Mathf.Repeat(
                    baseAngle +
                    (float)i / zoneCount * 360f +
                    Random.Range(-20f, 20f),
                    360f
                );

            float distance =
                Random.Range(zoneSpreadMin, zoneSpreadMax);

            Vector3 position =
                anchor.transform.position +
                new Vector3(
                    Mathf.Cos(angle * Mathf.Deg2Rad) * distance,
                    0f,
                    Mathf.Sin(angle * Mathf.Deg2Rad) * distance
                );

            position.y = 0.02f;

            GameObject zoneObject =
                new GameObject("HazardZone_Event");

            zoneObject.transform.position = position;
            zoneObject.transform.rotation = Quaternion.identity;

            HazardZone zone =
                zoneObject.AddComponent<HazardZone>();

            zone.Initialize(
                zoneWarning,
                zoneRadius,
                zoneDamagePerSecond,
                zoneDuration,
                zoneGrowMultiplier
            );
        }

        ShowBanner(Lang.Get("wave.event_danger"));
    }

    private void ShowBanner(string label)
    {
        if (waveUI != null)
            waveUI.ShowEvent(label);
    }
}