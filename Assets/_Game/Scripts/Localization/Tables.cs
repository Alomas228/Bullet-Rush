using System.Collections.Generic;

/// <summary>
/// Таблицы переводов. Ключи в трёх наборах должны совпадать один в один.
/// </summary>
public static class Tables
{
    private static Dictionary<string, string> _ru;
    private static Dictionary<string, string> _tr;
    private static Dictionary<string, string> _en;

    public static Dictionary<string, string> Get(LangCode code)
    {
        switch (code)
        {
            case LangCode.Ru:
                return Ru();

            case LangCode.Tr:
                return Tr();

            default:
                return En();
        }
    }

    private static Dictionary<string, string> Ru()
    {
        if (_ru != null)
            return _ru;

        var t = new Dictionary<string, string>
        {
            // ---------- Загрузка ----------
            { "boot.loading", "Загрузка" },

            // ---------- Главное меню ----------
            { "menu.play", "ИГРАТЬ" },
            { "menu.upgrades", "Улучшения" },
            { "menu.equipment", "Снаряжение" },
            { "menu.maps", "Карты" },
            { "menu.profile", "Профиль" },
            { "menu.tips", "Подсказки" },
            { "menu.level", "УРОВЕНЬ {0}" },
            { "menu.xp", "{0} / {1} XP" },
            { "menu.tutorial_replay", "ПРОЙТИ ОБУЧЕНИЕ ЗАНОВО" },
            { "menu.tutorial_reset", "Обучение сброшено — начнётся при следующем нажатии «Играть»" },
            { "eq.back", "НАЗАД" },
            { "menu.level_up", "УРОВЕНЬ {0} → УРОВЕНЬ {1}" },
            { "menu.player_xp", "Опыт игрока +{0}" },
            { "menu.coins_earned", "Монет получено: {0}" },
            { "menu.wave_reached", "Волна: {0}" },
            { "menu.kills", "Убийств: {0}" },
            { "menu.score", "Счёт: {0}" },

            // ---------- Пауза ----------
            { "pause.title", "Пауза" },
            { "pause.resume", "Продолжить" },
            { "pause.restart", "Рестарт" },
            { "pause.menu", "Меню" },
            { "pause.settings", "Настройки" },

            // ---------- Настройки ----------
            { "settings.title", "Настройки" },
            { "settings.sfx", "Громкость эффектов" },
            { "settings.music", "Громкость музыки" },
            { "settings.ui", "Громкость интерфейса" },
            { "settings.shake_damage", "Тряска при уроне" },
            { "settings.shake_fire", "Тряска при стрельбе" },
            { "settings.reset", "Сбросить" },
            { "settings.close", "Закрыть" },
            { "settings.language", "Язык" },
            { "settings.language_auto", "Автоматически" },

            // ---------- HUD ----------
            { "hud.score", "СЧЁТ: {0}" },
            { "hud.wave", "ВОЛНА: {0}" },
            { "hud.record", "рекорд" },
            { "hud.wave_short", "волна" },
            { "hud.coins", "монеты" },
            { "hud.combo", "КОМБО" },
            { "hud.hud_strip_wave", "ВОЛНА" },
            { "hud.hud_strip_time", "ВРЕМЯ" },
            { "hud.boss", "БОСС" },
            { "hud.boss_phase", "{0} · ФАЗА {1}" },
            { "hud.crit", "КРИТ " },
            { "hud.wave_left", "ВОЛНА: {0} · ОСТАЛОСЬ: {1}" },
            { "hud.wave_complete", "Волна завершена" },

            // ---------- Волны ----------
            { "wave.title", "ВОЛНА" },
            { "wave.number", "ВОЛНА {0}" },
            { "wave.number_subtitle", "ВОЛНА {0} · {1}" },
            { "wave.prepare", "ПРИГОТОВЬСЯ!" },
            { "wave.complete", "ВОЛНА ЗАВЕРШЕНА" },
            { "wave.archetype_swarm", "РОЙ" },
            { "wave.archetype_siege", "ОСАДА" },
            { "wave.archetype_hunt", "ВЫЛАЗКА" },
            { "wave.mod_fast_assault", "НАЛЁТ" },
            { "wave.mod_ranged_assault", "ОБСТРЕЛ" },
            { "wave.mod_elite_hunt", "ОХОТА" },
            { "wave.mod_ambush", "ЗАХОД С ФЛАНГОВ" },
            { "wave.mod_danger_zone", "МИНЫ" },
            { "wave.mod_last_stand", "ПРИКРЫТИЕ" },
            { "wave.event_ambush", "ЗАСАДА!" },
            { "wave.event_surge", "РЫВОК!" },
            { "wave.event_danger", "ОПАСНОСТЬ!" },

            // ---------- Личный рекорд ----------
            { "pb.menu", "ЛИЧНЫЙ РЕКОРД — ВОЛНА {0}" },
            { "pb.hud_line", "Волна {0} / Рекорд {1}" },
            { "pb.progress", "{2}" },
            { "pb.approach", "{2} ВОЛНЫ ДО ТВОЕГО РЕКОРДА" },
            { "pb.approach_one", "{2} ВОЛНА ДО ТВОЕГО РЕКОРДА" },
            { "pb.on_record", "ЭТО ТВОЯ РЕКОРДНАЯ ВОЛНА — ДЕРЖИСЬ!" },
            { "pb.new_record", "НОВЫЙ ЛИЧНЫЙ РЕКОРД!" },

            // ---------- Игровое завершение ----------
            { "over.title", "ИГРА ОКОНЧЕНА" },
            { "over.level", "УРОВЕНЬ {0}" },
            { "over.xp_progress", "{0} / {1} XP" },
            { "over.player_xp", "ОПЫТ ИГРОКА +{0}" },
            { "over.level_up", "УРОВЕНЬ {0} → УРОВЕНЬ {1}" },
            { "over.menu", "МЕНЮ" },
            { "over.restart", "РЕСТАРТ" },
            { "over.x2_coins", "x2 МОНЕТЫ" },
            { "over.revive", "ВОЗРОДИТЬСЯ" },

            // ---------- Результаты забега ----------
            { "rank.s_plus", "ИДЕАЛЬНЫЙ ЗАБЕГ" },
            { "rank.s", "ЛЕГЕНДАРНЫЙ" },
            { "rank.a", "ПРЕВОСХОДНЫЙ" },
            { "rank.b", "ПРЕКРАСНЫЙ" },
            { "rank.c", "ХОРОШО" },
            { "rank.f", "ПОПРОБУЙ ЕЩЁ" },

            { "results.score", "Счёт: {0}" },
            { "results.style_bonus", "+Бонус за стиль: {0}" },
            { "results.total", "Итого: {0}" },

            { "stat.kills", "Убийства: {0}" },
            { "stat.waves", "Волны: {0}" },
            { "stat.time", "Время: {0}" },
            { "stat.max_combo", "Макс. комбо: {0}" },
            { "stat.perfect_waves", "Идеальные волны: {0}" },
            { "stat.critical_hits", "Критические попадания: {0}" },
            { "stat.abilities_used", "Способности: {0}" },
            { "stat.dash_dodges", "Уклонения рывком: {0}" },
            { "stat.multi_kills", "Массовые убийства: {0}" },
            { "stat.avg_kill_time", "Среднее время убийства: {0}" },
            { "stat.fastest_kill", "Самое быстрое убийство: {0}" },
            { "stat.time_to_first_kill", "До первого убийства: {0}" },
            { "stat.time_to_first_boss", "До первого босса: {0}" },
            { "stat.best_wave_clear", "Лучшая зачистка волны: {0}" },
            { "stat.damage_efficiency", "Эффективность урона: {0}x" },
            { "stat.no_time", "--:--" },

            // ---------- Бонусы ----------
            { "bonus.combo50", "Комбо 50x" },
            { "bonus.combo30", "Комбо 30x" },
            { "bonus.combo20", "Комбо 20x" },
            { "bonus.combo10", "Комбо 10x" },
            { "bonus.enemy_kill", "Убийство врага" },
            { "bonus.dash_dodge", "Уклонение рывком" },

            // ---------- Улучшения ----------
            { "upg.title", "УЛУЧШЕНИЯ" },
            { "upg.subtitle", "Усиль своего героя" },
            { "upg.level", "УРОВЕНЬ {0} / {1}" },
            { "upg.current", "СЕЙЧАС" },
            { "upg.next", "ДАЛЬШЕ" },
            { "upg.buy", "УЛУЧШИТЬ" },
            { "upg.maxed", "МАКСИМАЛЬНЫЙ УРОВЕНЬ" },
            { "upg.refresh", "ОБНОВИТЬ" },
            { "upg.refresh_cost", "{0} монет" },
            { "upg.back", "НАЗАД" },
            { "upg.dash", "— (макс)" },

            { "cat.damage", "УРОН" },
            { "cat.health", "ЗДОРОВЬЕ" },
            { "cat.speed", "СКОРОСТЬ" },
            { "cat.critical", "КРИТ. ШАНС" },

            { "stat_upg.damage.name", "Урон" },
            { "stat_upg.damage.desc", "Увеличивает урон всего оружия." },
            { "stat_upg.damage.format", "+{0}% к урону" },
            { "stat_upg.damage.short", "+{0}%" },

            { "stat_upg.fire_rate.name", "Скорострельность" },
            { "stat_upg.fire_rate.desc", "Оружие стреляет чаще." },
            { "stat_upg.fire_rate.format", "+{0}% к скорострельности" },
            { "stat_upg.fire_rate.short", "+{0}%" },

            { "stat_upg.move_speed.name", "Скорость бега" },
            { "stat_upg.move_speed.desc", "Позволяет быстрее уворачиваться." },
            { "stat_upg.move_speed.format", "+{0}% к скорости бега" },
            { "stat_upg.move_speed.short", "+{0}%" },

            { "stat_upg.max_health.name", "Запас здоровья" },
            { "stat_upg.max_health.desc", "Больше HP в каждом забеге." },
            { "stat_upg.max_health.format", "+{0}% к максимуму здоровья" },
            { "stat_upg.max_health.short", "+{0}%" },

            { "stat_upg.health_regen.name", "Регенерация" },
            { "stat_upg.health_regen.desc", "Здоровье восстанавливается само." },
            { "stat_upg.health_regen.format", "+{0} HP/с" },
            { "stat_upg.health_regen.short", "+{0} HP/с" },

            { "stat_upg.critical_chance.name", "Шанс крита" },
            { "stat_upg.critical_chance.desc", "Шанс нанести критический урон." },
            { "stat_upg.critical_chance.format", "+{0}% к шансу крита" },
            { "stat_upg.critical_chance.short", "+{0}%" },

            { "stat_upg.critical_damage.name", "Сила крита" },
            { "stat_upg.critical_damage.desc", "Критический удар бьёт сильнее." },
            { "stat_upg.critical_damage.format", "+{0}x к силе крита" },
            { "stat_upg.critical_damage.short", "+{0}x" },

            { "stat_upg.projectile_speed.name", "Скорость снарядов" },
            { "stat_upg.projectile_speed.desc", "Пуля летит до цели быстрее." },
            { "stat_upg.projectile_speed.format", "+{0}% к скорости снарядов" },
            { "stat_upg.projectile_speed.short", "+{0}%" },

            { "stat_upg.dash_cooldown.name", "Перезарядка рывка" },
            { "stat_upg.dash_cooldown.desc", "Рывок (Space) восстанавливается быстрее." },
            { "stat_upg.dash_cooldown.format", "-{0}% к перезарядке рывка" },
            { "stat_upg.dash_cooldown.short", "-{0}%" },

            { "stat_upg.ability_cooldown.name", "Перезарядка способностей" },
            { "stat_upg.ability_cooldown.desc", "Бомба и щит восстанавливаются быстрее." },
            { "stat_upg.ability_cooldown.format", "-{0}% к перезарядке способностей" },
            { "stat_upg.ability_cooldown.short", "-{0}%" },

            { "upg.no_bonus", "Без бонуса" },
            { "upg.max_level", "Максимальный уровень" },
            { "upg.next_gain", "Дальше: {0}" },
            { "upg.dash_marker", "—" },
            { "upg.level_static", "УРОВЕНЬ 1 / 10" },
            { "upg.desc_static", "Увеличивает урон всего оружия." },

            // ---------- Улучшения за забег ----------
            { "runup.level_of", "Уровень {0} из {1}" },

            { "runup.crit_chance.name", "Шанс крита" },
            { "runup.crit_chance.desc", "Шанс критического удара растёт на {0} за уровень." },

            { "runup.crit_damage.name", "Сила крита" },
            { "runup.crit_damage.desc", "Множитель урона критического удара растёт на {0} за уровень." },

            { "runup.damage.name", "Урон" },
            { "runup.damage.desc", "Урон всего оружия растёт на {0} за уровень." },

            { "runup.fire_rate.name", "Скорострельность" },
            { "runup.fire_rate.desc", "Скорострельность растёт на {0} за уровень." },

            { "runup.max_health.name", "Запас здоровья" },
            { "runup.max_health.desc", "Максимальное здоровье растёт на {0} за уровень." },

            { "runup.move_speed.name", "Скорость бега" },
            { "runup.move_speed.desc", "Скорость передвижения растёт на {0} за уровень." },

            { "runup.pierce.name", "Пробитие" },
            { "runup.pierce.desc", "Снаряды пробивают на {0} врагов больше." },

            { "runup.projectile_count.name", "Снаряды" },
            { "runup.projectile_count.desc", "Число снарядов за выстрел растёт на {0} за уровень. У дробовиков есть потолок." },

            { "runup.projectile_speed.name", "Скорость снарядов" },
            { "runup.projectile_speed.desc", "Скорость снарядов растёт на {0} за уровень." },

            { "runup.regen.name", "Регенерация" },
            { "runup.regen.desc", "Здоровье восстанавливается на {0} в секунду." },

            { "runup.burning.name", "Горение" },
            { "runup.burning.desc", "Попадания поджигают врага. С уровнем растут шанс, урон и время горения. Смерть горящего врага поджигает соседей." },

            { "runup.bleeding.name", "Кровотечение" },
            { "runup.bleeding.desc", "Попащения вызывают кровотечение. Пока кровь идёт, каждое новое попадание складывает урон." },

            { "runup.ricochet.name", "Рикошет" },
            { "runup.ricochet.desc", "Снаряды иногда отскакивают и ищут новую цель. С уровнем растут шанс, число отскоков и радиус поиска." },

            { "runup.lifesteal.name", "Вампиризм" },
            { "runup.lifesteal.desc", "Возвращает вам {0} нанесённого урона здоровьем." },

            { "runup.explosion.name", "Взрывные снаряды" },
            { "runup.explosion.desc", "Попадания взрываются и задевают соседей. С уровнем растут урон и радиус взрыва." },

            { "runup.chain_lightning.name", "Цепная молния" },
            { "runup.chain_lightning.desc", "Попадания бьют молнией по соседним целям. С уровнем растут шанс, урон и число целей." },

            { "synergy.firestorm.name", "Огненный шторм" },
            { "synergy.firestorm.short", "Шторм" },
            { "synergy.firestorm.desc", "Три стака горения — и огонь перерастает во взрывы." },

            { "synergy.bloodlust.name", "Жажда крови" },
            { "synergy.bloodlust.short", "Кровь" },
            { "synergy.bloodlust.desc", "Кровотечение и вампиризм вместе: раны копятся, урон возвращается здоровьем." },

            { "synergy.stormcaller.name", "Призыв бури" },
            { "synergy.stormcaller.short", "Буря" },
            { "synergy.stormcaller.desc", "Цепная молния плюс тяжёлый урон: разряд бьёт чаще и злее." },

            { "synergy.bulwark.name", "Бастион" },
            { "synergy.bulwark.short", "Бастион" },
            { "synergy.bulwark.desc", "Здоровье и регенерация держат строй, а щит добивает защиту." },

            { "synergy.ripple.name", "Рикошетный залп" },
            { "synergy.ripple.short", "Рикошет" },
            { "synergy.ripple.desc", "Снаряды ищут новые цели: залп становится шире и глубже." },

            { "synergy.chaos.name", "Хаос" },
            { "synergy.chaos.short", "Хаос" },
            { "synergy.chaos.desc", "Четыре разных семейства — билд выходит из-под контроля и выигрывает от этого." },

            { "synergy.demolitionist.name", "Сапёр" },
            { "synergy.demolitionist.short", "Сапёр" },
            { "synergy.demolitionist.desc", "Взрывные снаряды и бомба: каждая цель становится зарядом." },

            { "synergy.discovered", "СИНЕРГИЯ ОТКРЫТА: {0}" },

            // ---------- Снаряжение ----------
            { "eq.title", "СНАРЯЖЕНИЕ" },
            { "eq.subtitle", "Собери боевой набор" },
            { "eq.tab_weapon", "ОРУЖИЕ" },
            { "eq.tab_ability", "СПОСОБНОСТИ" },
            { "eq.tab_clothing", "ОДЕЖДА" },
            { "eq.empty_slot", "не выбрано" },
            { "eq.item", "Предмет" },
            { "eq.clothing_type", "Одежда" },
            { "eq.empty_state", "В этой категории пока нет предметов." },
            { "eq.level_chip", "УР. {0}" },
            { "eq.page_counter", "{0:00} / {1:00}" },

            { "eq.type_rifle", "Винтовка" },
            { "eq.type_shotgun", "Дробовик" },
            { "eq.type_smg", "Пистолет-пулемёт" },

            { "eq.ability_bomb", "Бомба [E]" },
            { "eq.ability_shield", "Щит [Q]" },

            { "rarity.common", "Обычное" },
            { "rarity.uncommon", "Необычное" },
            { "rarity.rare", "Редкое" },
            { "rarity.epic", "Эпическое" },
            { "rarity.legendary", "Легендарное" },

            { "eq.state_locked_level", "ОТКРОЕТСЯ НА УРОВНЕ {0}" },
            { "eq.state_not_owned", "НЕ КУПЛЕНО" },
            { "eq.state_equipped", "СНАРЯЖЕНО" },
            { "eq.state_owned", "КУПЛЕНО" },

            { "eq.action_locked", "ЗАКРЫТО" },
            { "eq.action_buy", "КУПИТЬ" },
            { "eq.action_unequip", "СНЯТЬ" },
            { "eq.action_equip", "СНАРЯДИТЬ" },

            { "eq.stat_damage", "Урон: {0}" },
            { "eq.stat_firerate", "Темп: {0}/с" },
            { "eq.stat_projectiles", "Снарядов: {0}" },
            { "eq.stat_pierce", "Пробитие: {0}" },
            { "eq.stat_burst", "Очередь: {0}" },
            { "eq.stat_crit", "Крит: +{0}%" },
            { "eq.stat_score", "Очки: +{0}%" },
            { "eq.stats_static", "Урон: 5\nТемп: 3/с" },
            { "eq.level_chip_static", "УР. 1" },

            // ---------- Карты ----------
            { "maps.title", "ВЫБОР КАРТЫ" },
            { "maps.subtitle", "Мир уедет и приедет новый — арена та же, механики те же" },
            { "maps.selected", "ВЫБРАНО" },
            { "maps.default_name", "Карта {0}" },
            { "maps.back", "НАЗАД" },

            // ---------- Лидерборд ----------
            { "lb.loading", "ЗАГРУЗКА..." },
            { "lb.empty", "ТАБЛИЦА ПУСТА" },
            { "lb.no_auth", "ВОЙДИТЕ, ЧТОБЫ УВИДЕТЬ СВОЙ РАНГ" },
            { "lb.row", "{0}. {1}  -  {2}" },
            { "lb.combo", "КОМБО" },

            // ---------- Обучение ----------
            { "tut.skip", "ПРОПУСТИТЬ" },
            { "tut.kill_one", "УБЕЙ ЕГО!" },
            { "tut.mobs", "БЫСТРЫЕ НАБЕГАЮТ, СТРЕЛКИ БЬЮТ ИЗДАЛЕКА.\nНЕ СТОЙ НА МЕСТЕ!" },
            { "tut.dodge", "УКЛОНЯЙСЯ ОТ СНАРЯДОВ!" },
            { "tut.upgrade", "ТЕПЕРЬ ВЫБЕРИ ОДНО УЛУЧШЕНИЕ — ОНО УСИЛИТ ТЕБЯ В ЭТОМ ЗАБЕГЕ.\nЦВЕТ НАЗВАНИЯ = РЕДКОСТЬ:\nСЕРЫЙ — ОБЫЧНОЕ · ЗЕЛЁНЫЙ — НЕОБЫЧНОЕ · СИНИЙ — РЕДКОЕ\nФИОЛЕТОВЫЙ — ЭПИЧЕСКОЕ · ОРАНЖЕВЫЙ — ЛЕГЕНДАРНОЕ" },
            { "tut.step", "ШАГ {0}/{1}" },

            // ---------- Карты (названия) ----------
            { "map.les", "Лес" },
            { "map.gory", "Горы" },
            { "map.gorod", "Город" },
            { "map.pustynya", "Пустыня" },
            { "map.plyazh", "Пляж" },
            { "map.kosmos", "Космос" },

            // ---------- Оружие (названия) ----------
            { "wpn.basic_rifle", "Basic Rifle" },
            { "wpn.burst_rifle", "Burst Rifle" },
            { "wpn.golden_rifle", "Golden Rifle" },
            { "wpn.hunter_rifle", "Hunter Rifle" },
            { "wpn.plasma_rifle", "Plasma Rifle" },
            { "wpn.assassin_smg", "Assassin SMG" },
            { "wpn.basic_smg", "Basic SMG" },
            { "wpn.lightning_smg", "Lightning SMG" },
            { "wpn.minigun", "Minigun" },
            { "wpn.twin_smg", "Twin SMG" },
            { "wpn.basic_shotgun", "Basic Shotgun" },
            { "wpn.double_shotgun", "Double Shotgun" },
            { "wpn.heavy_shotgun", "Heavy Shotgun" },
            { "wpn.inferno_shotgun", "Inferno Shotgun" },
            { "wpn.void_shotgun", "Void Shotgun" },

            // ---------- Оружие (описания) ----------
            { "wpn.basic_rifle.desc", "Надёжная винтовка на все случаи жизни." },
            { "wpn.burst_rifle.desc", "Короткая очередь за секунду — враг не успевает уйти." },
            { "wpn.golden_rifle.desc", "Позолоченная винтовка. Стреляет так же, выглядит лучше." },
            { "wpn.hunter_rifle.desc", "Снайперская винтовка для далёких целей." },
            { "wpn.plasma_rifle.desc", "Стреляет раскалённой плазмой." },
            { "wpn.assassin_smg.desc", "Убийственный автомат для ближнего боя." },
            { "wpn.basic_smg.desc", "Автомат с высокой скорострельностью." },
            { "wpn.lightning_smg.desc", "Пули разряжаются электричеством." },
            { "wpn.minigun.desc", "Ураган свинца. Не останавливайся." },
            { "wpn.twin_smg.desc", "Два ствола — двойной шквал огня." },
            { "wpn.basic_shotgun.desc", "Классический дробовик на все случаи жизни." },
            { "wpn.double_shotgun.desc", "Заряжается сразу за оба ствола." },
            { "wpn.heavy_shotgun.desc", "Магнум-дробовик. Бьёт совсем рядом." },
            { "wpn.inferno_shotgun.desc", "Картечь поджигает всё, чего коснётся." },
            { "wpn.void_shotgun.desc", "Дробь пронзает врагов насквозь." },

            // ---------- Способности ----------
            { "abi.bomb.name", "Бомба" },
            { "abi.bomb.desc", "Мощный взрыв вокруг персонажа. Наносит урон всем врагам поблизости." },
            { "abi.bomb.stats", "Урон: 5 • Радиус: 5 • Кулдаун: 8 с" },
            { "abi.shield.name", "Щит" },
            { "abi.shield.desc", "Временный защитный барьер, который поглощает урон несколько секунд." },
            { "abi.shield.stats", "Длительность: 2.5 с • Кулдаун: 12 с" },

            // ---------- Одежда ----------
            { "clo.camo.name", "Камуфляж" },
            { "clo.camo.desc", "Почти сливается с окружением. Почти." },
            { "clo.classic.name", "Классик" },
            { "clo.classic.desc", "Оригинальное белое обмундирование. Ничего не улучшает, зато бесплатно." },
            { "clo.neon.name", "Неон" },
            { "clo.neon.desc", "Яркий неоновый цвет. Видно издалека — и врагам тоже." },

            // ---------- Ежедневные награды ----------
            { "daily.title", "ЕЖЕДЕЛЬНЫЕ НАГРАДЫ" },
            { "daily.day", "День {0}" },
            { "daily.claim", "ПОЛУЧИТЬ" },
            { "daily.claimed", "ПОЛУЧЕНО" },
            { "daily.locked", "ЗАКРЫТО" },
            { "daily.coins", "{0} монет" },
            { "daily.xp", "{0} XP" },
        };

        _ru = t;
        return t;
    }

    private static Dictionary<string, string> Tr()
    {
        if (_tr != null)
            return _tr;

        var t = new Dictionary<string, string>
        {
            // ---------- Загрузка ----------
            { "boot.loading", "Yükleniyor" },

            // ---------- Ana menü ----------
            { "menu.play", "OYNA" },
            { "menu.upgrades", "Yükseltmeler" },
            { "menu.equipment", "Teçhizat" },
            { "menu.maps", "Haritalar" },
            { "menu.profile", "Profil" },
            { "menu.tips", "İpuçları" },
            { "menu.level", "SEVİYE {0}" },
            { "menu.xp", "{0} / {1} XP" },
            { "menu.tutorial_replay", "EĞİTİMİ TEKRAR OYNA" },
            { "menu.tutorial_reset", "Eğitim sıfırlandı — bir sonraki «Oyna»ya basışta başlayacak" },
            { "eq.back", "GERİ" },
            { "menu.level_up", "SEVİYE {0} → SEVİYE {1}" },
            { "menu.player_xp", "Oyuncu XP +{0}" },
            { "menu.coins_earned", "Kazanılan madençi: {0}" },
            { "menu.wave_reached", "Dalga: {0}" },
            { "menu.kills", "Kısım: {0}" },
            { "menu.score", "Skor: {0}" },

            // ---------- Duraklat ----------
            { "pause.title", "Duraklatıldı" },
            { "pause.resume", "Devam Et" },
            { "pause.restart", "Yeniden Başla" },
            { "pause.menu", "Menü" },
            { "pause.settings", "Ayarlar" },

            // ---------- Ayarlar ----------
            { "settings.title", "Ayarlar" },
            { "settings.sfx", "Efekt sesi" },
            { "settings.music", "Müzik" },
            { "settings.ui", "Arayüz sesi" },
            { "settings.shake_damage", "Hasar alırken sarsıntı" },
            { "settings.shake_fire", "Ateş ederken sarsıntı" },
            { "settings.reset", "Sıfırla" },
            { "settings.close", "Kapat" },
            { "settings.language", "Dil" },
            { "settings.language_auto", "Otomatik" },

            // ---------- HUD ----------
            { "hud.score", "SKOR: {0}" },
            { "hud.wave", "DALGA: {0}" },
            { "hud.record", "rekor" },
            { "hud.wave_short", "dalga" },
            { "hud.coins", "jeton" },
            { "hud.combo", "KOMBO" },
            { "hud.hud_strip_wave", "DALGA" },
            { "hud.hud_strip_time", "SÜRE" },
            { "hud.boss", "PATRON" },
            { "hud.boss_phase", "{0} · FAZ {1}" },
            { "hud.crit", "KRİTİK " },
            { "hud.wave_left", "DALGA: {0} · KALAN: {1}" },
            { "hud.wave_complete", "Dalga tamamlandı" },

            // ---------- Dalgalar ----------
            { "wave.title", "DALGA" },
            { "wave.number", "DALGA {0}" },
            { "wave.number_subtitle", "DALGA {0} · {1}" },
            { "wave.prepare", "HAZIR OL!" },
            { "wave.complete", "DALGA TAMAMLANDI" },
            { "wave.archetype_swarm", "SÜRÜ" },
            { "wave.archetype_siege", "KUŞATMA" },
            { "wave.archetype_hunt", "BASKIN" },
            { "wave.mod_fast_assault", "HÜCUM" },
            { "wave.mod_ranged_assault", "MENZİL" },
            { "wave.mod_elite_hunt", "AV" },
            { "wave.mod_ambush", "YAN SALDIRI" },
            { "wave.mod_danger_zone", "MAYINLAR" },
            { "wave.mod_last_stand", "SON DURUŞ" },
            { "wave.event_ambush", "AMBUSH!" },
            { "wave.event_surge", "DALGA!" },
            { "wave.event_danger", "TEHLİKE!" },

            // ---------- Kisisel rekor ----------
            { "pb.menu", "KİŞİSEL REKOR — DALGA {0}" },
            { "pb.hud_line", "Dalga {0} / Rekor {1}" },
            { "pb.progress", "{2}" },
            { "pb.approach", "REKORUNU GEÇMEK İÇİN {2} DALGA" },
            { "pb.approach_one", "REKORUNU GEÇMEK İÇİN {2} DALGA" },
            { "pb.on_record", "REKOR DALGANDASIN — DAYAN!" },
            { "pb.new_record", "YENİ KİŞİSEL REKOR!" },

            // ---------- Oyun sonu ----------
            { "over.title", "OYUN BİTTİ" },
            { "over.level", "SEVİYE {0}" },
            { "over.xp_progress", "{0} / {1} XP" },
            { "over.player_xp", "OYUNCU XP +{0}" },
            { "over.level_up", "SEVİYE {0} → SEVİYE {1}" },
            { "over.menu", "MENÜ" },
            { "over.restart", "YENİDEN BAŞLA" },
            { "over.x2_coins", "x2 JETON" },
            { "over.revive", "YAŞA" },

            // ---------- Sonuçlar ----------
            { "rank.s_plus", "KUSURSUZ KOŞU" },
            { "rank.s", "EFSANEVİ" },
            { "rank.a", "MÜKEMMEL" },
            { "rank.b", "HARİKA" },
            { "rank.c", "İYİ" },
            { "rank.f", "TEKRAR DENE" },

            { "results.score", "Skor: {0}" },
            { "results.style_bonus", "+Stil bonusu: {0}" },
            { "results.total", "Toplam: {0}" },

            { "stat.kills", "Öldürmeler: {0}" },
            { "stat.waves", "Dalgalar: {0}" },
            { "stat.time", "Süre: {0}" },
            { "stat.max_combo", "Maks. kombo: {0}" },
            { "stat.perfect_waves", "Kusursuz dalgalar: {0}" },
            { "stat.critical_hits", "Kritik isabet: {0}" },
            { "stat.abilities_used", "Kullanılan yetenekler: {0}" },
            { "stat.dash_dodges", "Atılma kaçınmaları: {0}" },
            { "stat.multi_kills", "Çoklu öldürmeler: {0}" },
            { "stat.avg_kill_time", "Ortalama öldürme süresi: {0}" },
            { "stat.fastest_kill", "En hızlı öldürme: {0}" },
            { "stat.time_to_first_kill", "İlk öldürmeye kadar: {0}" },
            { "stat.time_to_first_boss", "İlk patrona kadar: {0}" },
            { "stat.best_wave_clear", "En iyi dalga temizliği: {0}" },
            { "stat.damage_efficiency", "Hasar verimliliği: {0}x" },
            { "stat.no_time", "--:--" },

            // ---------- Bonuslar ----------
            { "bonus.combo50", "Kombo 50x" },
            { "bonus.combo30", "Kombo 30x" },
            { "bonus.combo20", "Kombo 20x" },
            { "bonus.combo10", "Kombo 10x" },
            { "bonus.enemy_kill", "Düşman öldürüldü" },
            { "bonus.dash_dodge", "Atılma kaçınması" },

            // ---------- Yükseltmeler ----------
            { "upg.title", "YÜKSELTMELER" },
            { "upg.subtitle", "Kahramanını güçlendir" },
            { "upg.level", "SEVİYE {0} / {1}" },
            { "upg.current", "ŞİMDİ" },
            { "upg.next", "SONRA" },
            { "upg.buy", "YÜKSELT" },
            { "upg.maxed", "MAKSİMUM SEVİYE" },
            { "upg.refresh", "YENİLE" },
            { "upg.refresh_cost", "{0} jeton" },
            { "upg.back", "GERİ" },
            { "upg.dash", "— (maks)" },

            { "cat.damage", "HASAR" },
            { "cat.health", "CAN" },
            { "cat.speed", "HIZ" },
            { "cat.critical", "KRİTİK ŞANS" },

            { "stat_upg.damage.name", "Hasar" },
            { "stat_upg.damage.desc", "Tüm silahların hasarını artırır." },
            { "stat_upg.damage.format", "Hasar +%{0}" },
            { "stat_upg.damage.short", "+%{0}" },

            { "stat_upg.fire_rate.name", "Atış Hızı" },
            { "stat_upg.fire_rate.desc", "Silah daha sık ateş eder." },
            { "stat_upg.fire_rate.format", "Atış hızı +%{0}" },
            { "stat_upg.fire_rate.short", "+%{0}" },

            { "stat_upg.move_speed.name", "Koşu Hızı" },
            { "stat_upg.move_speed.desc", "Daha hızlı kaçmana yardımcı olur." },
            { "stat_upg.move_speed.format", "Koşu hızı +%{0}" },
            { "stat_upg.move_speed.short", "+%{0}" },

            { "stat_upg.max_health.name", "Can Rezervi" },
            { "stat_upg.max_health.desc", "Her koşuda daha fazla can." },
            { "stat_upg.max_health.format", "Maks. can +%{0}" },
            { "stat_upg.max_health.short", "+%{0}" },

            { "stat_upg.health_regen.name", "Can Yenileme" },
            { "stat_upg.health_regen.desc", "Can kendiliğinden yenilenir." },
            { "stat_upg.health_regen.format", "+{0} can/sn" },
            { "stat_upg.health_regen.short", "+{0} can/sn" },

            { "stat_upg.critical_chance.name", "Kritik Şans" },
            { "stat_upg.critical_chance.desc", "Kritik hasar vurma şansı." },
            { "stat_upg.critical_chance.format", "Kritik şans +%{0}" },
            { "stat_upg.critical_chance.short", "+%{0}" },

            { "stat_upg.critical_damage.name", "Kritik Güç" },
            { "stat_upg.critical_damage.desc", "Kritik vuruş daha sert vurur." },
            { "stat_upg.critical_damage.format", "Kritik güç +{0}x" },
            { "stat_upg.critical_damage.short", "+{0}x" },

            { "stat_upg.projectile_speed.name", "Mermi Hızı" },
            { "stat_upg.projectile_speed.desc", "Mermi hedefe daha hızlı ulaşır." },
            { "stat_upg.projectile_speed.format", "Mermi hızı +%{0}" },
            { "stat_upg.projectile_speed.short", "+%{0}" },

            { "stat_upg.dash_cooldown.name", "Atılma Bekleme" },
            { "stat_upg.dash_cooldown.desc", "Atılma (Boşluk) daha hızlı dolar." },
            { "stat_upg.dash_cooldown.format", "Atılma bekleme -%{0}" },
            { "stat_upg.dash_cooldown.short", "-%{0}" },

            { "stat_upg.ability_cooldown.name", "Yetenek Bekleme" },
            { "stat_upg.ability_cooldown.desc", "Bomba ve kalkan daha hızlı dolar." },
            { "stat_upg.ability_cooldown.format", "Yetenek bekleme -%{0}" },
            { "stat_upg.ability_cooldown.short", "-%{0}" },

            { "upg.no_bonus", "Bonus yok" },
            { "upg.max_level", "Maksimum seviye" },
            { "upg.next_gain", "Sonra: {0}" },
            { "upg.dash_marker", "—" },
            { "upg.level_static", "SEVİYE 1 / 10" },
            { "upg.desc_static", "Tüm silahların hasarını artırır." },

            // ---------- Koşu yükseltmeleri ----------
            { "runup.level_of", "Seviye {0} / {1}" },

            { "runup.crit_chance.name", "Kritik Şansı" },
            { "runup.crit_chance.desc", "Kritik vuruş şansı her seviyede {0} artar." },

            { "runup.crit_damage.name", "Kritik Gücü" },
            { "runup.crit_damage.desc", "Kritik vuruşun hasar çarpanı her seviyede {0} artar." },

            { "runup.damage.name", "Hasar" },
            { "runup.damage.desc", "Tüm silahların hasarı her seviyede {0} artar." },

            { "runup.fire_rate.name", "Atış Hızı" },
            { "runup.fire_rate.desc", "Atış hızı her seviyede {0} artar." },

            { "runup.max_health.name", "Can Rezervi" },
            { "runup.max_health.desc", "Maksimum can her seviyede {0} artar." },

            { "runup.move_speed.name", "Koşu Hızı" },
            { "runup.move_speed.desc", "Hareket hızı her seviyede {0} artar." },

            { "runup.pierce.name", "Delme" },
            { "runup.pierce.desc", "Mermiler {0} düşman daha fazla deler." },

            { "runup.projectile_count.name", "Mermi Sayısı" },
            { "runup.projectile_count.desc", "Atış başına mermi sayısı her seviyede {0} artar. Pompalı silahlarda bir tavan vardır." },

            { "runup.projectile_speed.name", "Mermi Hızı" },
            { "runup.projectile_speed.desc", "Mermi hızı her seviyede {0} artar." },

            { "runup.regen.name", "Can Yenileme" },
            { "runup.regen.desc", "Can saniyede {0} yenilenir." },

            { "runup.burning.name", "Yakma" },
            { "runup.burning.desc", "İsabetler düşmanı tutuşturur. Seviye arttıkça şans, hasar ve süre büyür. Yanan düşman ölünce çevresini de tutuşturur." },

            { "runup.bleeding.name", "Kanama" },
            { "runup.bleeding.desc", "İsabetler kanama başlatır. Süre bitene kadar her yeni isabet hasarı üst üste bindirir." },

            { "runup.ricochet.name", "Sekme" },
            { "runup.ricochet.desc", "Mermiler bazen sekirip yeni hedef arar. Seviye arttıkça şans, sekme sayısı ve arama yarıçapı büyür." },

            { "runup.lifesteal.name", "Vampirizm" },
            { "runup.lifesteal.desc", "Verilen hasarın {0} kadarını can olarak geri verir." },

            { "runup.explosion.name", "Patlayıcı Mermiler" },
            { "runup.explosion.desc", "İsabetler patlar ve yanındakilere değer. Seviye arttıkça hasar ve patlama yarıçapı büyür." },

            { "runup.chain_lightning.name", "Zincirleme Yıldırım" },
            { "runup.chain_lightning.desc", "İsabetler yanındaki hedeflere yıldırım indirir. Seviye arttıkça şans, hasar ve hedef sayısı büyür." },

            { "synergy.firestorm.name", "Ateş Fırtınası" },
            { "synergy.firestorm.short", "Ateş" },
            { "synergy.firestorm.desc", "Üç yangın seviyesi — ve alev patlamalara dönüşür." },

            { "synergy.bloodlust.name", "Kan Açlığı" },
            { "synergy.bloodlust.short", "Kan" },
            { "synergy.bloodlust.desc", "Kanama ve vampirizm birlikte: yaralar birikir, hasar can olarak geri döner." },

            { "synergy.stormcaller.name", "Fırtına Çağırıcı" },
            { "synergy.stormcaller.short", "Fırtına" },
            { "synergy.stormcaller.desc", "Zincirleme yıldırım artı ağır hasar: şimşek daha sık ve daha sert iner." },

            { "synergy.bulwark.name", "Sur" },
            { "synergy.bulwark.short", "Sur" },
            { "synergy.bulwark.desc", "Can ve yenilenme hattı tutar, kalkan savunmayı tamamlar." },

            { "synergy.ripple.name", "Sekme Atışışı" },
            { "synergy.ripple.short", "Sekme" },
            { "synergy.ripple.desc", "Mermiler yeni hedefler bulur: atış daha geniş ve daha derin olur." },

            { "synergy.chaos.name", "Kaos" },
            { "synergy.chaos.short", "Kaos" },
            { "synergy.chaos.desc", "Dört farklı aile — yapı bozulur ve bundan kazançlı çıkar." },

            { "synergy.demolitionist.name", "İmha Uzmanı" },
            { "synergy.demolitionist.short", "İmha" },
            { "synergy.demolitionist.desc", "Patlayıcı mermiler artı bomba: her hedef bir şarjör olur." },

            { "synergy.discovered", "SİNERJİ AÇILDI: {0}" },

            // ---------- Teçhizat ----------
            { "eq.title", "TEÇHİZAT" },
            { "eq.subtitle", "Savaş teçhizatını topla" },
            { "eq.tab_weapon", "SİLAHLAR" },
            { "eq.tab_ability", "YETENEKLER" },
            { "eq.tab_clothing", "KIYAFET" },
            { "eq.empty_slot", "seçilmedi" },
            { "eq.item", "Eşya" },
            { "eq.clothing_type", "Kıyafet" },
            { "eq.empty_state", "Bu kategoride henüz eşya yok." },
            { "eq.level_chip", "SV. {0}" },
            { "eq.page_counter", "{0:00} / {1:00}" },

            { "eq.type_rifle", "Tüfek" },
            { "eq.type_shotgun", "Pompalı" },
            { "eq.type_smg", "Hafif Makinalı" },

            { "eq.ability_bomb", "Bomba [E]" },
            { "eq.ability_shield", "Kalkan [Q]" },

            { "rarity.common", "Yaygın" },
            { "rarity.uncommon", "Sıra dışı" },
            { "rarity.rare", "Nadir" },
            { "rarity.epic", "Destansı" },
            { "rarity.legendary", "Efsanevi" },

            { "eq.state_locked_level", "SEVİYE {0}'DE AÇILIR" },
            { "eq.state_not_owned", "SATIN ALINMADI" },
            { "eq.state_equipped", "KUŞANILDI" },
            { "eq.state_owned", "SATIN ALINDI" },

            { "eq.action_locked", "KİLİTLİ" },
            { "eq.action_buy", "SATIN AL" },
            { "eq.action_unequip", "ÇIKAR" },
            { "eq.action_equip", "KUŞAN" },

            { "eq.stat_damage", "Hasar: {0}" },
            { "eq.stat_firerate", "Atış hızı: {0}/sn" },
            { "eq.stat_projectiles", "Mermi: {0}" },
            { "eq.stat_pierce", "Delme: {0}" },
            { "eq.stat_burst", "Seri: {0}" },
            { "eq.stat_crit", "Kritik: +%{0}" },
            { "eq.stat_score", "Skor: +%{0}" },
            { "eq.stats_static", "Hasar: 5\nAtış hızı: 3/sn" },
            { "eq.level_chip_static", "SV. 1" },

            // ---------- Haritalar ----------
            { "maps.title", "HARİTA SEÇİMİ" },
            { "maps.subtitle", "Dünya değişecek, savaş alanı aynı" },
            { "maps.selected", "SEÇİLDİ" },
            { "maps.default_name", "Harita {0}" },
            { "maps.back", "GERİ" },

            // ---------- Sıralama tablosu ----------
            { "lb.loading", "YÜKLENİYOR..." },
            { "lb.empty", "SIRA TABLOSU BOŞ" },
            { "lb.no_auth", "SIRANI GÖRMEK İÇİN GİRİŞ YAP" },
            { "lb.row", "{0}. {1}  -  {2}" },
            { "lb.combo", "KOMBO" },

            // ---------- Eğitim ----------
            { "tut.skip", "ATLA" },
            { "tut.kill_one", "ONUNU ÖLDÜR!" },
            { "tut.mobs", "HIZLILAR GELİYOR, NIŞANCILAR UZAKTAN ATEŞ EDİYOR.\nYERİNDE DURMA!" },
            { "tut.dodge", "MERMİLERDEN KAÇ!" },
            { "tut.upgrade", "ŞİMDİ BİR YÜKSELTME SEÇ — BU KOŞUDA SENİ GÜÇLENDİRECEK.\nİSİM RENGİ = NADİRLİK:\nGRİ — YAYGIN · YEŞİL — SIRA DIŞI · MAVİ — NADİR\nMOR — DESTANSI · TURUNCU — EFSANEVİ" },
            { "tut.step", "ADIM {0}/{1}" },

            // ---------- Harita adları ----------
            { "map.les", "Orman" },
            { "map.gory", "Dağlar" },
            { "map.gorod", "Şehir" },
            { "map.pustynya", "Çöl" },
            { "map.plyazh", "Sahil" },
            { "map.kosmos", "Uzay" },

            // ---------- Silah adları ----------
            { "wpn.basic_rifle", "Basic Rifle" },
            { "wpn.burst_rifle", "Burst Rifle" },
            { "wpn.golden_rifle", "Golden Rifle" },
            { "wpn.hunter_rifle", "Hunter Rifle" },
            { "wpn.plasma_rifle", "Plasma Rifle" },
            { "wpn.assassin_smg", "Assassin SMG" },
            { "wpn.basic_smg", "Basic SMG" },
            { "wpn.lightning_smg", "Lightning SMG" },
            { "wpn.minigun", "Minigun" },
            { "wpn.twin_smg", "Twin SMG" },
            { "wpn.basic_shotgun", "Basic Shotgun" },
            { "wpn.double_shotgun", "Double Shotgun" },
            { "wpn.heavy_shotgun", "Heavy Shotgun" },
            { "wpn.inferno_shotgun", "Inferno Shotgun" },
            { "wpn.void_shotgun", "Void Shotgun" },

            // ---------- Silah açıklamaları ----------
            { "wpn.basic_rifle.desc", "Her durum için güvenilir tüfek." },
            { "wpn.burst_rifle.desc", "Saniyede kısa bir seri — hedef kaçamaz." },
            { "wpn.golden_rifle.desc", "Altın kaplama tüfek. Aynı ateş eder, daha iyi görünür." },
            { "wpn.hunter_rifle.desc", "Uzak hedefler için keskin nişancı tüfeği." },
            { "wpn.plasma_rifle.desc", "Kızgın plazma yayar." },
            { "wpn.assassin_smg.desc", "Yakın dövüş için ölümcül otomatik." },
            { "wpn.basic_smg.desc", "Yüksek atış hızlı otomatik." },
            { "wpn.lightning_smg.desc", "Mermiler elektrik yüklü." },
            { "wpn.minigun.desc", "Kurşun fırtınası. Durma." },
            { "wpn.twin_smg.desc", "İki namlu — çift ateş." },
            { "wpn.basic_shotgun.desc", "Her durum için klasik pompalı." },
            { "wpn.double_shotgun.desc", "İki namluyu birden doldurur." },
            { "wpn.heavy_shotgun.desc", "Magnum pompalı. Çok yakından vurur." },
            { "wpn.inferno_shotgun.desc", "Saçma her neye değerse yakar." },
            { "wpn.void_shotgun.desc", "Saçma düşmanların içinden geçer." },

            // ---------- Yetenekler ----------
            { "abi.bomb.name", "Bomba" },
            { "abi.bomb.desc", "Karakterin çevresinde güçlü bir patlama. Yakındaki tüm düşmanlara hasar verir." },
            { "abi.bomb.stats", "Hasar: 5 • Yarıçap: 5 • Bekleme: 8 sn" },
            { "abi.shield.name", "Kalkan" },
            { "abi.shield.desc", "Hasarı birkaç saniye boyunca emen geçici bir koruma bariyeri." },
            { "abi.shield.stats", "Süre: 2.5 sn • Bekleme: 12 sn" },

            // ---------- Kıyafetler ----------
            { "clo.camo.name", "Kamuflaj" },
            { "clo.camo.desc", "Neredeyse çevreye karışır. Neredeyse." },
            { "clo.classic.name", "Klasik" },
            { "clo.classic.desc", "Orijinal beyaz üniforma. Hiçbir şey iyileştirmez ama bedava." },
            { "clo.neon.name", "Neon" },
            { "clo.neon.desc", "Parlak neon renk. Uzaktan görülür — düşmanlar da görür." },

            // ---------- Günlük ödüller ----------
            { "daily.title", "GÜNLÜK ÖDÜLLER" },
            { "daily.day", "Gün {0}" },
            { "daily.claim", "AL" },
            { "daily.claimed", "ALINDI" },
            { "daily.locked", "KİLİTLİ" },
            { "daily.coins", "{0} jeton" },
            { "daily.xp", "{0} XP" },
        };

        _tr = t;
        return t;
    }

    private static Dictionary<string, string> En()
    {
        if (_en != null)
            return _en;

        var t = new Dictionary<string, string>
        {
            // ---------- Loading ----------
            { "boot.loading", "Loading" },

            // ---------- Main menu ----------
            { "menu.play", "PLAY" },
            { "menu.upgrades", "Upgrades" },
            { "menu.equipment", "Equipment" },
            { "menu.maps", "Maps" },
            { "menu.profile", "Profile" },
            { "menu.tips", "Tips" },
            { "menu.level", "LEVEL {0}" },
            { "menu.xp", "{0} / {1} XP" },
            { "menu.tutorial_replay", "REPLAY TUTORIAL" },
            { "menu.tutorial_reset", "Tutorial reset — it will start on the next Play" },
            { "eq.back", "BACK" },
            { "menu.level_up", "LEVEL {0} → LEVEL {1}" },
            { "menu.player_xp", "Player XP +{0}" },
            { "menu.coins_earned", "Coins earned: {0}" },
            { "menu.wave_reached", "Wave: {0}" },
            { "menu.kills", "Kills: {0}" },
            { "menu.score", "Score: {0}" },

            // ---------- Pause ----------
            { "pause.title", "Paused" },
            { "pause.resume", "Resume" },
            { "pause.restart", "Restart" },
            { "pause.menu", "Menu" },
            { "pause.settings", "Settings" },

            // ---------- Settings ----------
            { "settings.title", "Settings" },
            { "settings.sfx", "Sound effects" },
            { "settings.music", "Music" },
            { "settings.ui", "Interface sounds" },
            { "settings.shake_damage", "Screen shake on damage" },
            { "settings.shake_fire", "Screen shake on fire" },
            { "settings.reset", "Reset" },
            { "settings.close", "Close" },
            { "settings.language", "Language" },
            { "settings.language_auto", "Automatic" },

            // ---------- HUD ----------
            { "hud.score", "SCORE: {0}" },
            { "hud.wave", "WAVE: {0}" },
            { "hud.record", "record" },
            { "hud.wave_short", "wave" },
            { "hud.coins", "coins" },
            { "hud.combo", "COMBO" },
            { "hud.hud_strip_wave", "WAVE" },
            { "hud.hud_strip_time", "TIME" },
            { "hud.boss", "BOSS" },
            { "hud.boss_phase", "{0} · PHASE {1}" },
            { "hud.crit", "CRIT " },
            { "hud.wave_left", "WAVE: {0} · LEFT: {1}" },
            { "hud.wave_complete", "Wave complete" },

            // ---------- Waves ----------
            { "wave.title", "WAVE" },
            { "wave.number", "WAVE {0}" },
            { "wave.number_subtitle", "WAVE {0} · {1}" },
            { "wave.prepare", "GET READY!" },
            { "wave.complete", "WAVE COMPLETE" },
            { "wave.archetype_swarm", "SWARM" },
            { "wave.archetype_siege", "SIEGE" },
            { "wave.archetype_hunt", "HUNT" },
            { "wave.mod_fast_assault", "ONSLAUGHT" },
            { "wave.mod_ranged_assault", "LONG RANGE" },
            { "wave.mod_elite_hunt", "ELITE HUNT" },
            { "wave.mod_ambush", "FLANK" },
            { "wave.mod_danger_zone", "MINES" },
            { "wave.mod_last_stand", "COVER" },
            { "wave.event_ambush", "AMBUSH!" },
            { "wave.event_surge", "SURGE!" },
            { "wave.event_danger", "DANGER!" },

            // ---------- Personal best ----------
            { "pb.menu", "PERSONAL BEST — WAVE {0}" },
            { "pb.hud_line", "Wave {0} / PB {1}" },
            { "pb.progress", "{2}" },
            { "pb.approach", "{2} WAVES TO BEAT YOUR RECORD" },
            { "pb.approach_one", "{2} WAVE TO BEAT YOUR RECORD" },
            { "pb.on_record", "THIS IS YOUR RECORD WAVE — HOLD ON!" },
            { "pb.new_record", "NEW PERSONAL BEST!" },

            // ---------- Game over ----------
            { "over.title", "GAME OVER" },
            { "over.level", "LEVEL {0}" },
            { "over.xp_progress", "{0} / {1} XP" },
            { "over.player_xp", "PLAYER XP +{0}" },
            { "over.level_up", "LEVEL {0} → LEVEL {1}" },
            { "over.menu", "MENU" },
            { "over.restart", "RESTART" },
            { "over.x2_coins", "x2 COINS" },
            { "over.revive", "REVIVE" },

            // ---------- Run results ----------
            { "rank.s_plus", "PERFECT RUN" },
            { "rank.s", "LEGENDARY" },
            { "rank.a", "EXCELLENT" },
            { "rank.b", "GREAT" },
            { "rank.c", "GOOD" },
            { "rank.f", "TRY AGAIN" },

            { "results.score", "Score: {0}" },
            { "results.style_bonus", "+Style Bonus: {0}" },
            { "results.total", "Total: {0}" },

            { "stat.kills", "Kills: {0}" },
            { "stat.waves", "Waves: {0}" },
            { "stat.time", "Time: {0}" },
            { "stat.max_combo", "Max Combo: {0}" },
            { "stat.perfect_waves", "Perfect Waves: {0}" },
            { "stat.critical_hits", "Critical Hits: {0}" },
            { "stat.abilities_used", "Abilities Used: {0}" },
            { "stat.dash_dodges", "Dash Dodges: {0}" },
            { "stat.multi_kills", "Multi-Kills: {0}" },
            { "stat.avg_kill_time", "Avg Kill Time: {0}" },
            { "stat.fastest_kill", "Fastest Kill: {0}" },
            { "stat.time_to_first_kill", "Time to First Kill: {0}" },
            { "stat.time_to_first_boss", "Time to First Boss: {0}" },
            { "stat.best_wave_clear", "Best Wave Clear: {0}" },
            { "stat.damage_efficiency", "Damage Efficiency: {0}x" },
            { "stat.no_time", "--:--" },

            // ---------- Bonuses ----------
            { "bonus.combo50", "Combo 50x" },
            { "bonus.combo30", "Combo 30x" },
            { "bonus.combo20", "Combo 20x" },
            { "bonus.combo10", "Combo 10x" },
            { "bonus.enemy_kill", "Enemy kill" },
            { "bonus.dash_dodge", "Dash Dodge" },

            // ---------- Upgrades ----------
            { "upg.title", "UPGRADES" },
            { "upg.subtitle", "Strengthen your hero" },
            { "upg.level", "LEVEL {0} / {1}" },
            { "upg.current", "CURRENT" },
            { "upg.next", "NEXT" },
            { "upg.buy", "UPGRADE" },
            { "upg.maxed", "MAX LEVEL" },
            { "upg.refresh", "REROLL" },
            { "upg.refresh_cost", "{0} coins" },
            { "upg.back", "BACK" },
            { "upg.dash", "— (max)" },

            { "cat.damage", "DAMAGE" },
            { "cat.health", "HEALTH" },
            { "cat.speed", "SPEED" },
            { "cat.critical", "CRIT CHANCE" },

            { "stat_upg.damage.name", "Damage" },
            { "stat_upg.damage.desc", "Increases the damage of all weapons." },
            { "stat_upg.damage.format", "+{0}% damage" },
            { "stat_upg.damage.short", "+{0}%" },

            { "stat_upg.fire_rate.name", "Fire Rate" },
            { "stat_upg.fire_rate.desc", "Weapons fire more often." },
            { "stat_upg.fire_rate.format", "+{0}% fire rate" },
            { "stat_upg.fire_rate.short", "+{0}%" },

            { "stat_upg.move_speed.name", "Run Speed" },
            { "stat_upg.move_speed.desc", "Lets you dodge faster." },
            { "stat_upg.move_speed.format", "+{0}% run speed" },
            { "stat_upg.move_speed.short", "+{0}%" },

            { "stat_upg.max_health.name", "Health Pool" },
            { "stat_upg.max_health.desc", "More HP in every run." },
            { "stat_upg.max_health.format", "+{0}% max health" },
            { "stat_upg.max_health.short", "+{0}%" },

            { "stat_upg.health_regen.name", "Regeneration" },
            { "stat_upg.health_regen.desc", "Health restores on its own." },
            { "stat_upg.health_regen.format", "+{0} HP/s" },
            { "stat_upg.health_regen.short", "+{0} HP/s" },

            { "stat_upg.critical_chance.name", "Crit Chance" },
            { "stat_upg.critical_chance.desc", "Chance to land critical damage." },
            { "stat_upg.critical_chance.format", "+{0}% crit chance" },
            { "stat_upg.critical_chance.short", "+{0}%" },

            { "stat_upg.critical_damage.name", "Crit Power" },
            { "stat_upg.critical_damage.desc", "Critical hits hit harder." },
            { "stat_upg.critical_damage.format", "+{0}x crit power" },
            { "stat_upg.critical_damage.short", "+{0}x" },

            { "stat_upg.projectile_speed.name", "Projectile Speed" },
            { "stat_upg.projectile_speed.desc", "Bullets reach the target faster." },
            { "stat_upg.projectile_speed.format", "+{0}% projectile speed" },
            { "stat_upg.projectile_speed.short", "+{0}%" },

            { "stat_upg.dash_cooldown.name", "Dash Recharge" },
            { "stat_upg.dash_cooldown.desc", "Dash (Space) recharges faster." },
            { "stat_upg.dash_cooldown.format", "-{0}% dash recharge" },
            { "stat_upg.dash_cooldown.short", "-{0}%" },

            { "stat_upg.ability_cooldown.name", "Ability Recharge" },
            { "stat_upg.ability_cooldown.desc", "Bomb and shield recharge faster." },
            { "stat_upg.ability_cooldown.format", "-{0}% ability recharge" },
            { "stat_upg.ability_cooldown.short", "-{0}%" },

            { "upg.no_bonus", "No bonus" },
            { "upg.max_level", "Max level" },
            { "upg.next_gain", "Next: {0}" },
            { "upg.dash_marker", "—" },
            { "upg.level_static", "LEVEL 1 / 10" },
            { "upg.desc_static", "Increases the damage of all weapons." },

            // ---------- Run upgrades ----------
            { "runup.level_of", "Level {0} / {1}" },

            { "runup.crit_chance.name", "Crit Chance" },
            { "runup.crit_chance.desc", "Critical hit chance grows by {0} per level." },

            { "runup.crit_damage.name", "Crit Power" },
            { "runup.crit_damage.desc", "Critical hit damage multiplier grows by {0} per level." },

            { "runup.damage.name", "Damage" },
            { "runup.damage.desc", "Damage of all weapons grows by {0} per level." },

            { "runup.fire_rate.name", "Fire Rate" },
            { "runup.fire_rate.desc", "Fire rate grows by {0} per level." },

            { "runup.max_health.name", "Health Pool" },
            { "runup.max_health.desc", "Max health grows by {0} per level." },

            { "runup.move_speed.name", "Run Speed" },
            { "runup.move_speed.desc", "Move speed grows by {0} per level." },

            { "runup.pierce.name", "Pierce" },
            { "runup.pierce.desc", "Bullets pierce {0} more enemies." },

            { "runup.projectile_count.name", "Projectile Count" },
            { "runup.projectile_count.desc", "Bullets per shot grow by {0} per level. Shotguns have a hard cap." },

            { "runup.projectile_speed.name", "Projectile Speed" },
            { "runup.projectile_speed.desc", "Projectile speed grows by {0} per level." },

            { "runup.regen.name", "Regeneration" },
            { "runup.regen.desc", "Restores {0} health per second." },

            { "runup.burning.name", "Burning" },
            { "runup.burning.desc", "Hits set enemies on fire. Each level raises chance, burn damage and duration. A burning enemy that dies ignites its neighbours." },

            { "runup.bleeding.name", "Bleeding" },
            { "runup.bleeding.desc", "Hits cause bleeding. Every new hit adds to the damage while the bleed lasts." },

            { "runup.ricochet.name", "Ricochet" },
            { "runup.ricochet.desc", "Bullets sometimes bounce and look for a new target. Each level raises chance, bounces and search radius." },

            { "runup.lifesteal.name", "Lifesteal" },
            { "runup.lifesteal.desc", "Returns {0} of dealt damage to you as health." },

            { "runup.explosion.name", "Explosive Rounds" },
            { "runup.explosion.desc", "Hits explode and hit nearby enemies. Each level raises damage and blast radius." },

            { "runup.chain_lightning.name", "Chain Lightning" },
            { "runup.chain_lightning.desc", "Hits arc lightning to nearby enemies. Each level raises chance, damage and target count." },

            { "synergy.firestorm.name", "Firestorm" },
            { "synergy.firestorm.short", "Firestorm" },
            { "synergy.firestorm.desc", "Three stacks of burn, and the fire outgrows into explosions." },

            { "synergy.bloodlust.name", "Bloodlust" },
            { "synergy.bloodlust.short", "Bloodlust" },
            { "synergy.bloodlust.desc", "Bleeding and lifesteal together: wounds stack while damage comes back as health." },

            { "synergy.stormcaller.name", "Stormcaller" },
            { "synergy.stormcaller.short", "Stormcaller" },
            { "synergy.stormcaller.desc", "Chain lightning plus heavy damage: the bolt strikes harder and more often." },

            { "synergy.bulwark.name", "Bulwark" },
            { "synergy.bulwark.short", "Bulwark" },
            { "synergy.bulwark.desc", "Health and regen hold the line, and the shield finishes the job." },

            { "synergy.ripple.name", "Ricochet Volley" },
            { "synergy.ripple.short", "Volley" },
            { "synergy.ripple.desc", "Bullets look for new targets: the volley becomes wider and deeper." },

            { "synergy.chaos.name", "Chaos" },
            { "synergy.chaos.short", "Chaos" },
            { "synergy.chaos.desc", "Four different families: the build breaks shape and wins from it." },

            { "synergy.demolitionist.name", "Demolitionist" },
            { "synergy.demolitionist.short", "Demolitionist" },
            { "synergy.demolitionist.desc", "Explosive rounds plus the bomb: every target becomes a charge." },

            { "synergy.discovered", "SYNERGY UNLOCKED: {0}" },

            // ---------- Equipment ----------
            { "eq.title", "EQUIPMENT" },
            { "eq.subtitle", "Build your combat loadout" },
            { "eq.tab_weapon", "WEAPONS" },
            { "eq.tab_ability", "ABILITIES" },
            { "eq.tab_clothing", "CLOTHING" },
            { "eq.empty_slot", "not selected" },
            { "eq.item", "Item" },
            { "eq.clothing_type", "Clothing" },
            { "eq.empty_state", "There are no items in this category yet." },
            { "eq.level_chip", "LV. {0}" },
            { "eq.page_counter", "{0:00} / {1:00}" },

            { "eq.type_rifle", "Rifle" },
            { "eq.type_shotgun", "Shotgun" },
            { "eq.type_smg", "SMG" },

            { "eq.ability_bomb", "Bomb [E]" },
            { "eq.ability_shield", "Shield [Q]" },

            { "rarity.common", "Common" },
            { "rarity.uncommon", "Uncommon" },
            { "rarity.rare", "Rare" },
            { "rarity.epic", "Epic" },
            { "rarity.legendary", "Legendary" },

            { "eq.state_locked_level", "UNLOCKS AT LEVEL {0}" },
            { "eq.state_not_owned", "NOT OWNED" },
            { "eq.state_equipped", "EQUIPPED" },
            { "eq.state_owned", "OWNED" },

            { "eq.action_locked", "LOCKED" },
            { "eq.action_buy", "BUY" },
            { "eq.action_unequip", "UNEQUIP" },
            { "eq.action_equip", "EQUIP" },

            { "eq.stat_damage", "Damage: {0}" },
            { "eq.stat_firerate", "Rate: {0}/s" },
            { "eq.stat_projectiles", "Projectiles: {0}" },
            { "eq.stat_pierce", "Pierce: {0}" },
            { "eq.stat_burst", "Burst: {0}" },
            { "eq.stat_crit", "Crit: +{0}%" },
            { "eq.stat_score", "Score: +{0}%" },
            { "eq.stats_static", "Damage: 5\nRate: 3/s" },
            { "eq.level_chip_static", "LV. 1" },

            // ---------- Maps ----------
            { "maps.title", "MAP SELECT" },
            { "maps.subtitle", "The world changes, the arena and mechanics stay the same" },
            { "maps.selected", "SELECTED" },
            { "maps.default_name", "Map {0}" },
            { "maps.back", "BACK" },

            // ---------- Leaderboard ----------
            { "lb.loading", "LOADING..." },
            { "lb.empty", "LEADERBOARD IS EMPTY" },
            { "lb.no_auth", "SIGN IN TO SEE YOUR RANK" },
            { "lb.row", "{0}. {1}  -  {2}" },
            { "lb.combo", "COMBO" },

            // ---------- Tutorial ----------
            { "tut.skip", "SKIP" },
            { "tut.kill_one", "KILL IT!" },
            { "tut.mobs", "FAST ONES ARE COMING, SNIPERS SHOOT FROM FAR.\nDON'T STAND STILL!" },
            { "tut.dodge", "DODGE THE BULLETS!" },
            { "tut.upgrade", "NOW PICK ONE UPGRADE — IT WILL POWER YOU UP FOR THIS RUN.\nNAME COLOUR = RARITY:\nGREY — COMMON · GREEN — UNCOMMON · BLUE — RARE\nPURPLE — EPIC · ORANGE — LEGENDARY" },
            { "tut.step", "STEP {0}/{1}" },

            // ---------- Map names ----------
            { "map.les", "Forest" },
            { "map.gory", "Mountains" },
            { "map.gorod", "City" },
            { "map.pustynya", "Desert" },
            { "map.plyazh", "Beach" },
            { "map.kosmos", "Space" },

            // ---------- Weapon names ----------
            { "wpn.basic_rifle", "Basic Rifle" },
            { "wpn.burst_rifle", "Burst Rifle" },
            { "wpn.golden_rifle", "Golden Rifle" },
            { "wpn.hunter_rifle", "Hunter Rifle" },
            { "wpn.plasma_rifle", "Plasma Rifle" },
            { "wpn.assassin_smg", "Assassin SMG" },
            { "wpn.basic_smg", "Basic SMG" },
            { "wpn.lightning_smg", "Lightning SMG" },
            { "wpn.minigun", "Minigun" },
            { "wpn.twin_smg", "Twin SMG" },
            { "wpn.basic_shotgun", "Basic Shotgun" },
            { "wpn.double_shotgun", "Double Shotgun" },
            { "wpn.heavy_shotgun", "Heavy Shotgun" },
            { "wpn.inferno_shotgun", "Inferno Shotgun" },
            { "wpn.void_shotgun", "Void Shotgun" },

            // ---------- Weapon descriptions ----------
            { "wpn.basic_rifle.desc", "A dependable rifle for any situation." },
            { "wpn.burst_rifle.desc", "A short burst per second — the enemy has no time to leave." },
            { "wpn.golden_rifle.desc", "A gilded rifle. Fires the same, looks better." },
            { "wpn.hunter_rifle.desc", "A sniper rifle for distant targets." },
            { "wpn.plasma_rifle.desc", "Fires scorching plasma." },
            { "wpn.assassin_smg.desc", "A deadly automatic weapon for close quarters." },
            { "wpn.basic_smg.desc", "A high-rate-of-fire automatic weapon." },
            { "wpn.lightning_smg.desc", "Bullets crackle with electricity." },
            { "wpn.minigun.desc", "A hurricane of lead. Never stop." },
            { "wpn.twin_smg.desc", "Two barrels — double the firepower." },
            { "wpn.basic_shotgun.desc", "A classic shotgun for any situation." },
            { "wpn.double_shotgun.desc", "Reloads both barrels at once." },
            { "wpn.heavy_shotgun.desc", "A magnum shotgun. Hits point blank." },
            { "wpn.inferno_shotgun.desc", "The buckshot sets whatever it touches on fire." },
            { "wpn.void_shotgun.desc", "Buckshot punches straight through enemies." },

            // ---------- Abilities ----------
            { "abi.bomb.name", "Bomb" },
            { "abi.bomb.desc", "A powerful blast around the character. Damages all nearby enemies." },
            { "abi.bomb.stats", "Damage: 5 • Radius: 5 • Cooldown: 8 s" },
            { "abi.shield.name", "Shield" },
            { "abi.shield.desc", "A temporary barrier that absorbs damage for a few seconds." },
            { "abi.shield.stats", "Duration: 2.5 s • Cooldown: 12 s" },

            // ---------- Clothing ----------
            { "clo.camo.name", "Camo" },
            { "clo.camo.desc", "Almost blends into the surroundings. Almost." },
            { "clo.classic.name", "Classic" },
            { "clo.classic.desc", "The original white uniform. Improves nothing, but it's free." },
            { "clo.neon.name", "Neon" },
            { "clo.neon.desc", "Bright neon colour. Visible from afar — and to enemies." },

            // ---------- Daily Rewards ----------
            { "daily.title", "DAILY REWARDS" },
            { "daily.day", "Day {0}" },
            { "daily.claim", "CLAIM" },
            { "daily.claimed", "CLAIMED" },
            { "daily.locked", "LOCKED" },
            { "daily.coins", "{0} coins" },
            { "daily.xp", "{0} XP" },
        };

        _en = t;
        return t;
    }
}
