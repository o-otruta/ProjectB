# Полный аудит проекта ProjectB, оценка концепции/стилистики и Мастер-план расширения контента

**Рабочее название:** `ProjectB` (*Swarm Hero: Voxel Survivor*)  
**Движок и рендер:** Unity `6000.5.5f1` (Unity 6.5), URP `17.5.0`  
**Стек:** VContainer `1.19.0`, New Input System `1.19.0`, uGUI + TextMeshPro, ScriptableObject Data-Driven  
**Целевая платформа:** Android (API 34+, Portrait Only `9:16` – `9:20`, 60 FPS), далее iOS  
**Назначение документа:** Технический и геймдизайнерский аудит актуального состояния репозитория + пошаговый план разработки и контент-экспансии для ИИ-агента (Gemini).

---

## 1. Executive Summary (Текущее состояние проекта)

За последние спринты проект совершил качественный скачок от сырого прототипа к функциональному вертикальному рогалику-автобатлеру (закрыты основные блокеры из первого аудита):
- **Производительность и ядро:** 50 000 объектов травы переведены на chunk-culled GPU Instancing ([ArenaDecorationRenderer.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/Arena/ArenaDecorationRenderer.cs)), сохранения в бою переведены на пакетный `IsDirty`-режим без дискового I/O на каждый килл ([SaveManager.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/Meta/SaveManager.cs)), внедрён общий пул вражеских снарядов ([EnemyProjectilePool.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/Combat/EnemyProjectilePool.cs)), все 36 воксельных материалов используют `enableInstancing: 1`, а смена цвета врагов переведена на `MaterialPropertyBlock` ([EnemyBase.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/Enemies/EnemyBase.cs)).
- **Геймплейные системы MVP:** работают 7 активных и 4 пассивные способности, 3 парные синергии ([SynergyData.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/LevelUp/SynergyData.cs)), единая модель статов забега ([HeroRuntimeStats.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/Player/HeroRuntimeStats.cs)), мини-боссы каждые 5 волн и 2 босса каждые 25 волн (`Stone Golem`, `Necromancer`), 3 секретных предмета на арене ([SecretItemSpawner.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/Arena/SecretItemSpawner.cs)), 15 ачивок с реальными анлоками, формат карточек `2 бесплатные + 1 за рекламу` ([CardSelectionUI.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/LevelUp/CardSelectionUI.cs)), `AdManager` и базовый Game Feel ([VfxFeedbackManager.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/Visuals/VfxFeedbackManager.cs), [AudioManager.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/Core/AudioManager.cs)).
- **Воксельный пайплайн:** интегрирована 21 воксельная 3D-модель с покадровым zero-GC аниматором ([VoxelMeshAnimator.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/Visuals/VoxelMeshAnimator.cs)) и покрытие базовыми EditMode-тестами (`Assets/_Tests/EditMode/`).

### Сводная таблица оценок текущей версии

| Направление | Было | Стало | Статус | Что мешает выйти на уровень коммерческого релиза |
|---|:---:|:---:|:---:|---|
| **1. Идея и Core Loop** | 6.0 / 10 | **8.0 / 10** | 🟢 Сильная база | Малая глубина прокачки внутри забега (у 9 из 11 скиллов всего 1 карта-модификатор, нет `maxLevel` и эволюций скиллов) |
| **2. Стилистика и Воксельный арт** | 4.5 / 10 | **7.0 / 10** | 🟡 Хорошо, но неровно | Однотонный плоский пол арены без текстуры/тайлинга, `0` 2D-иконок во всех SO (`icon: {fileID: 0}`), цилиндры-заглушки у аур/щита, невидимый взрыв `Fireball` |
| **3. Производительность (Mobile 60 FPS)** | 3.5 / 10 | **8.0 / 10** | 🟢 Стабильно | На `LaserTurret.prefab` остался импортированный `.obj`-материал без GPU Instancing; лишние тяжёлые пакеты в `manifest.json`; мусор в `DefaultVolumeProfile.asset` |
| **4. Контент и Бестиарий** | 4.0 / 10 | **6.0 / 10** | 🟡 Требует расширения | В `WaveManager` реально спавнятся только 2 врага (`Slime` и `Bat`), готовый `GoblinShooter` забыт; герой `Mage` — клон `Knight` по оружию и ульте; нет фазового расписания волн |
| **5. Архитектура, DI и Тесты** | 6.0 / 10 | **8.5 / 10** | 🟢 Чисто | Нет `CooldownReduction` и `Crit` в `HeroRuntimeStats`; часть `.asset` хранит старый `Assembly-CSharp::` идентификатор; нет UI-тостов для секретов и синергий |
| **6. UI / UX и Полировка** | 4.5 / 10 | **6.8 / 10** | 🟡 Функционально | Карточки LevelUp и ачивки пустые без иконок; нет анимации появления карт (scale bounce); нет стрелки-компаса к секретам на арене 500×500; нет паузы в бою |

---

## 2. Оценка идеи, рыночного потенциала и стилистики

### 2.1. Оценка концепции (Play Market + TikTok / Shorts)
Идея **вертикального 3D Voxel Survivor / Auto-battler** в духе *Megabonk* — попадание в одну из самых виральных ниш мобильного рынка:
1. **Идеальный форм-фактор для мобильных и UGC:**
   - **Одноручное управление (One-Thumb Gameplay):** виртуальный джойстик + одна кнопка ульты под большой палец правой руки позволяют играть в транспорте и на ходу.
   - **Нативный вертикальный формат `9:16`:** любой записанный экран геймплея без монтажа и рамок готов к публикации в TikTok, YouTube Shorts и Reels.
2. **Высококонверсионная F2P-монетизация без Paywall:**
   - Механика **«2 бесплатные карточки + 3-я за Rewarded Ad»** в момент LevelUp работает на пике интереса игрока (когда в 3-м слоте выпадает `Epic` или `Legendary` карта либо нужная Синергия, opt-in rate превышает 50%).
3. **Чего сейчас не хватает идее для удержания (Retention D1/D7) и TikTok-виральности:**
   - **Слабая визуальная эскалация («От новичка до Бога за 60 секунд»):** в лучших представителях жанра прокачка способности меняет её **поведение и визуальный масштаб** (1 фаербол → веер из 5 огромных метеоров с огненными лужами; 2 орбитальных меча → двойное огненное кольцо из 8 клинков). Сейчас у большинства способностей карточка модификатора даёт только плоские `+5 Damage`, что не меняет картинку на экране.
   - **Отсутствие моментов катарсиса (Dopamine Spikes):** нужны кристаллы-пылесосы (*Vacuum Magnet*, стягивающие 200 кристаллов со всей карты в одну точку), взрывные бочки на арене, числа критического урона с анимацией и алтари риска.
   - **Плоская драматургия волн:** когда с 1-й по 50-ю волну на игрока идут одни и те же `Slime` и `Bat`, исчезает чувство прогрессии внутри забега. Каждые 3–4 волны должен меняться состав роя и появляться новый тактический вызов.

### 2.2. Оценка стилистики и арт-дирекции (Voxel Art)
Выбор **MagicaVoxel + покадровый `VoxelMeshAnimator` + Emissive Glow** — отличное техническое и художественное решение:
- **Плюсы текущей реализации:**
  - Чёткие воксельные силуэты (`Knight`, `Mage`, `Goblin`, `Golem`, `Necromancer`, `Turret`, сундук, свиток, кристалл) с двухматериальной схемой (`Base_Mat` + `Glow_Mat` с `_EmissionColor: 1.8`).
  - Стоп-моушен анимация (`8–12 FPS` у пеших юнитов, `24 FPS` у крыльев мыши) создаёт узнаваемый «игрушечный / настольный» шарм и обходится почти бесплатно для мобильного CPU/GPU.
  - Разлёт врагов на воксельные кубики (`SharedBurstParticles` в `VfxFeedbackManager`) отлично поддерживает воксельную физику мира.
- **Проблемы стилистики, которые портят впечатление прямо сейчас:**
  1. **«Пластиковый» однотонный пол (`FloorMat.mat`):** пол арены 500×500 — это стандартный `Plane` с заливкой `(0.2, 0.5, 0.2)` без сетки или пиксель-воксельного паттерна. На однотонном фоне теряется ощущение скорости движения героя (эффект «скольжения на месте»), а сцена выглядит как дебажный уровень.
  2. **Конфликт вокселей и гладких примитивов Unity:**
     - Ауры `IceAuraAbility` и `BlackHoleAbility` создаются через `GameObject.CreatePrimitive(PrimitiveType.Cylinder)` ([IceAuraAbility.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/Abilities/Active/IceAuraAbility.cs#L33), [BlackHoleAbility.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/Abilities/Active/BlackHoleAbility.cs#L50)) — это гладкие полупрозрачные блины, которые конфликтуют с кубической эстетикой.
     - Щит ульты `ShieldVisual` на `Hero.prefab` и `SlamTelegraph` на `GolemBoss.prefab` — гладкая сфера и цилиндр.
  3. **Пустой UI без иконок:** карточки выбора при LevelUp, слоты активных скиллов в HUD, экран коллекции способностей, экран мета-прокачки и список ачивок рендерятся **без единого спрайта-иконки** (везде `icon: {fileID: 0}`), полагаясь только на текстовые инициалы (`OS`, `FB`, `LT`). Для мобильной игры визуальное считывание карточки за 0.5 секунды по яркой воксельной иконке критично.
  4. **Пост-обработка и тени:** в `Mobile_RPAsset.asset` выключены мягкие тени (`m_SoftShadowsSupported: 0`), а `DefaultVolumeProfile.asset` содержит дефолтные нулевые настройки цветокоррекции и битые тестовые компоненты (`m_Script: {fileID: 0}`). Лёгкий `Vignette`, тёплый `ColorAdjustments` (чуть выше контраст и насыщенность) и аккуратный `Bloom` сразу свяжут воксельные модели, эмиссивные кристаллы и арену в цельную картинку.

---

## 3. Технический и геймплейный аудит текущей кодовой базы (Что нужно исправить)

Ниже перечислены **актуальные** баги, недоделки и расхождения с GDD, присутствующие в проекте на текущем коммите (`bef1364`).

### 3.1. Забытый враг `GoblinShooter` и дисбаланс старта забега
- **Локация:** [Gameplay.unity](file:///d:/UnityProjects/ProjectB/Assets/_Scenes/Gameplay.unity#L289-L291), `Assets/_Prefabs/Enemies/GoblinShooter.prefab`, [BatData.asset](file:///d:/UnityProjects/ProjectB/Assets/_ScriptableObjects/BatData.asset), [SlimeData.asset](file:///d:/UnityProjects/ProjectB/Assets/_ScriptableObjects/SlimeData.asset)
- **Суть проблемы:**
  1. Префаб `GoblinShooter.prefab` (с воксельной моделью и 4-кадровой анимацией ходьбы) полностью собран, но для него **не создан `GoblinData.asset`**, и он не добавлен в список `WaveManager.enemyTypes` в `Gameplay.unity`! В игре спавнятся только `SlimeData` и `BatData`.
  2. У `BatData.asset` выставлено `hp: 75` (против `hp: 18` у `SlimeData` и базового урона героя `10`). Так как `WaveManager.SpawnEnemy()` выбирает врага из списка случайно с 1-й волны, уже на первой секунде игры спавнятся толстые стреляющие мыши, требующие 8 попаданий.
- **Решение:** Создать `GoblinData.asset` (`hp: 28`, `speed: 2.8`, `stopDistance: 5.5`), снизить базовый `hp` у `BatData.asset` до `35–40` (или `speed: 3.4`), и добавить в `WaveConfig` структуру волновых пулов с полем `minWave`, чтобы враги вводились постепенно (Волна 1+: `Slime`, Волна 3+: `GoblinShooter`, Волна 6+: `BatShooter`).

### 3.2. Герой Маг (`MageData.asset`) — клон Рыцаря и сломанный анлок
- **Локация:** [MageData.asset](file:///d:/UnityProjects/ProjectB/Assets/_ScriptableObjects/MageData.asset), [HeroData.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/Data/HeroData.cs#L14-L17), [HeroUltimate.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/Player/HeroUltimate.cs#L119-L125), `Assets/Resources/Achievements/`
- **Суть проблемы:**
  1. В `MageData.asset` стоит `unlockedByDefault: 1` — Маг открыт сразу со старта игры, хотя в `HeroSelectionUI` заложена система блокировки героев через ачивки (`AchievementRewardType.UnlockHero`), а по GDD (`CONTEXT.md:203`) Маг должен открываться за достижение 25-й волны / победу над боссом. При этом ни одна из 15 ачивок не выдаёт `UnlockHero`!
  2. У Мага назначено то же самое `startingWeapon` (`WeaponData.asset`), что и у Рыцаря.
  3. В `HeroUltimateType` есть только одно значение `Shield = 0`. У Мага ульта названа `Arcane Barrier` (5с неуязвимости) — это тот же щит Рыцаря, а не боевая ульта `Meteor AoE` из GDD (`CONTEXT.md:225`).
- **Решение:** Выставить `MageData.unlockedByDefault = false`, привязать его разблокировку к ачивке `ReachWave25.asset` (`rewardType: UnlockHero`, `rewardId: mage`), создать для Мага `MageWeaponData.asset` и реализовать `HeroUltimateType.MeteorStrike` в `HeroUltimate.cs`.

### 3.3. Невидимый AoE-взрыв у `FireballProjectile`
- **Локация:** [FireballProjectile.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/Abilities/Active/FireballProjectile.cs#L51-L64), [VfxFeedbackManager.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/Visuals/VfxFeedbackManager.cs)
- **Суть проблемы:** В `FireballProjectile.HitTarget()` выполняется `Physics.OverlapSphereNonAlloc(transform.position, explosionRadius, ...)` и снаряд мгновенно выключается (`ReturnToPool()`). Никакого визуального эффекта взрыва по радиусу `3м` не происходит — игрок видит просто исчезновение шарика и не понимает, почему соседние враги получили урон.
- **Решение:** Публиковать событие/вызывать вспышку AoE-взрыва (кольцо воксельных огненных частиц радиусом `explosionRadius` + лёгкий `Shake` или расширяющаяся сфера/кольцо на 0.15с).

### 3.4. Секретные предметы на арене: нет UI-фидбека при подборе и нет индикатора направления
- **Локация:** [SecretItemSpawner.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/Arena/SecretItemSpawner.cs#L166-L201), [VfxFeedbackManager.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/Visuals/VfxFeedbackManager.cs#L306-L313)
- **Суть проблемы:**
  1. `SecretItemSpawner.HandleItemCollected()` формирует строку `rewardSummary` (`"+100 Coins"`, `"Free Level Up!"`, `"Unlocked: Fireball"`) и публикует `SecretItemCollectedEvent`, но **ни один UI-компонент не подписан на это событие**! Игрок подбирает сундук или загадочный кристалл и не видит на экране текста награды.
  2. На арене `500×500` спавнится всего 1 предмет на расстоянии `28–60м` от центра. Без экранного указателя (off-screen arrow indicator), появляющегося когда герой подходит на `35–45м`, игрок почти никогда не находит секрет осознанно.
- **Решение:** Показывать крупный всплывающий текст награды через `VfxFeedbackManager` / тост-панель при `SecretItemCollectedEvent` и добавить легковесный `SecretCompassIndicatorUI` (стрелка на краю экрана, когда секрет находится в радиусе 45м вне кадра).

### 3.5. Только 1 модификатор у большинства способностей и отсутствие `maxLevel`
- **Локация:** `Assets/_ScriptableObjects/Cards/*.asset`, [HeroAbilities.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/Abilities/HeroAbilities.cs#L88-L108), [UpgradeManager.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/LevelUp/UpgradeManager.cs#L210-L248)
- **Суть проблемы:**
  1. У `Shooter`, `Fireball`, `Boomerang`, `IceAura`, `BlackHole` и всех 4 пассивок есть только **одна** карточка улучшения (`+5 Damage` и т.п.). При этом в коде этих способностей уже реализована поддержка `ModifierType.Count` (мульти-снаряды), `ModifierType.Cooldown`, `ModifierType.Area` / `Radius`, `ModifierType.PierceCount`, `ModifierType.SlowAmount`, но **карточек для них не создано**!
  2. У способностей нет максимального уровня (`maxLevel = 5`). Игрок может брать `+5 Damage` до бесконечности (Lv 12+), засоряя пул карточек.
- **Решение:**
  - Добавить в `AbilityData` поле `public int maxLevel = 5;` и в `UpgradeManager.GetRandomCards()` отфильтровывать `AbilityModifierCardData`, если целевая способность уже достигла `Level >= Data.maxLevel`.
  - Расширить набор `AbilityModifierCardData` для всех способностей (на количество снарядов, радиус взрыва/ауры, кулдаун, пробитие).

### 3.6. Мелкие технические и визуальные дефекты в ассетах и префабах
- **`LaserTurret.prefab` без GPU Instancing:** В [LaserTurret.prefab](file:///d:/UnityProjects/ProjectB/Assets/_Prefabs/LaserTurret.prefab#L272) на `Visuals` назначен встроенный материал из `turret-0.obj` вместо shared-материала из `Assets/_Materials/Voxels/` с включённым GPU Instancing.
- **Грязный `localPosition` на `Slime.prefab`:** В [Slime.prefab](file:///d:/UnityProjects/ProjectB/Assets/_Prefabs/Enemies/Slime.prefab#L31) корень префаба имеет `m_LocalPosition: (-1.83432, 0, 4.11012)` вместо `(0, 0, 0)`, а также у слизня отсутствует анимация движения/прыжка (`VoxelMeshAnimator.enableBobbing = true`).
- **Битые sub-assets в `DefaultVolumeProfile.asset`:** В [DefaultVolumeProfile.asset](file:///d:/UnityProjects/ProjectB/Assets/Settings/DefaultVolumeProfile.asset#L47-L148) присутствуют 5 тестовых компонентов с `m_Script: {fileID: 0}` (`CopyPasteTestComponent2/3`, `OasisFogVolumeComponent` и др.).
- **Лишние пакеты в `Packages/manifest.json`:** Подключены неиспользуемые `com.unity.visualscripting`, `com.unity.timeline`, `com.unity.multiplayer.center`, `com.unity.ai.navigation`.
- **Отсутствие кнопки Паузы в бою:** В `GameManager` есть состояние `GameState.Paused` ([GameManager.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/Core/GameManager.cs#L17)), но в боевом HUD нет кнопки паузы (с возможностью открыть `SettingsPopupUI`, продолжить или выйти в меню).

---

## 4. План масштабного расширения контента (Content Expansion Blueprint)

Этот раздел содержит детальную спецификацию нового контента (герои, способности, модификаторы, синергии, враги, боссы, биомы и объекты арены). **Используй эти таблицы как прямое ТЗ для генерации кода и `ScriptableObject`-ассетов.**

### 4.1. Ростер Героев (4 уникальных героя с собственным оружием и ультами)

Необходимо расширить `HeroUltimateType` и `HeroPassiveType` в [HeroData.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/Data/HeroData.cs), чтобы каждый герой ощущался как уникальный класс:

| ID | Имя | Статус / Анлок | Базовые статы | Стартовое оружие (`WeaponData`) | Пассивка (`HeroPassiveType`) | Ульта (`HeroUltimateType`) |
|---|---|---|---|---|---|---|
| `knight` | **Knight** | Открыт со старта | `100 HP`, Speed `5.0` | `KnightLanceWeapon`: урон `12`, CD `0.55s`, дальность `7.5`, скорость `16` | **Iron Will:** `+100% Max HP` (стартует с 200 HP) | **Aegis Shield (`Shield`):** 5с неуязвимости + при активации отбрасывает врагов в радиусе 4м и наносит `30` урона (CD `25s`) |
| `mage` | **Mage** | Ачивка `Veteran` (Дойти до Волны 25) | `80 HP`, Speed `5.2` | `ArcaneWandWeapon`: урон `14`, CD `0.65s`, дальность `10`, мини-сплэш `1.5м` | **Arcane Mastery:** `+20% Global Damage` | **Meteor Shower (`MeteorStrike`):** в течение 3с обрушивает 6 воксельных метеоров по случайным врагам вокруг героя (радиус взрыва `3.5м`, урон `80` каждый, CD `22s`) |
| `ranger` | **Ranger** | Ачивка `Harvester` (5 000 киллов) | `90 HP`, Speed `5.6` | `RapidBowWeapon`: урон `9`, CD `0.32s`, дальность `11`, скорость `22` | **Windrunner:** `+25% Move Speed` | **Arrow Rain (`ArrowRain`):** 3 волны стрел сверху в радиусе `8м` вокруг героя (суммарно `150` урона всем врагам в зоне + замедление `50%` на `4s`, CD `20s`) |
| `necromancer` | **Necromancer** | Ачивка `Soul Vanquisher` (Победить Босса Некроманта, Волна 50) | `85 HP`, Speed `5.0` | `SoulBoltWeapon`: урон `11`, CD `0.5s`, дальность `9`, вампиризм/высокий магнит | **Soul Harvest:** `+35% XP & Magnet Radius` (`XpAndMagnetPercent`) | **Army of the Dead (`SoulNova`):** выпускает кольцо из 12 пробивающих черепов во все стороны (`60` урона) и исцеляет героя на `35% Max HP` (CD `24s`) |

---

### 4.2. Доработка существующих способностей (Модификаторы Lv 1–5 и потолок `maxLevel = 5`)

Добавить в каждую `AbilityCardData.unlockedModifiers` полноценный набор из 3–4 карточек модификаторов, чтобы прокачка меняла визуал и механику скилла:

| Способность | Существующие моды | Новые карточки модификаторов (добавить в `Assets/_ScriptableObjects/Cards/`) |
|---|---|---|
| **Orbital Sword** (`orbital_small_sword`) | `+10 Damage`, `+1 Sword` | `OrbitalSword_SpeedAndRadius`: `+40 Orbit Speed & +0.6 Radius` (Rare) |
| **Shooter** (`Shooter`) | `+5 Damage` | `ShooterMod_Count`: `+1 Projectile` (Rare); `ShooterMod_Cooldown`: `-0.18s Cooldown` (Common) |
| **Fireball** (`Fireball`) | `+5 Damage` | `FireballMod_Count`: `+1 Fireball` (Rare); `FireballMod_Area`: `+1.2m Explosion Radius` (Rare) |
| **Boomerang** (`Boomerang`) | `+5 Damage` | `BoomerangMod_Count`: `+1 Boomerang` (Rare); `BoomerangMod_Pierce`: `+3 Pierce & +2m Range` (Common) |
| **Ice Aura** (`IceAura`) | `+5 Damage` | `IceAuraMod_Radius`: `+1.0m Aura Radius` (Common); `IceAuraMod_Slow`: `+15% Slow Power` (Rare) |
| **Laser Turret** (`LaserTurret`) | `+5 DPS`, `-1s CD`, `+1s Dur`, `+1 Range` | Полный комплект уже есть (4 карточки) |
| **Black Hole** (`BlackHole`) | `+5 Damage` | `BlackHoleMod_Radius`: `+1.2m Pull Radius` (Epic); `BlackHoleMod_Force`: `+1.5 Pull Force` (Rare) |

---

### 4.3. Новые Способности (6 Активных + 4 Пассивных) и 5 Новых Синергий

#### А. Новые Активные Способности (`Assets/_Scripts/Abilities/Active/`)

| ID | Название | Редкость | Анлок | Механика и Визуал | Базовые статы | Модификаторы |
|---|---|---|---|---|---|---|
| `ChainLightning` | **Chain Lightning** | Rare | Открыта со старта | Бьёт ближайшего врага молнией (`LineRenderer` с 3–4 изломами) и мгновенно перескакивает на `bounceCount` соседних целей в радиусе `5м`. | Урон `14`, CD `1.4s`, Прыжков `3`, Дальность `9м` | `+6 Damage`, `+2 Bounces` (`Count`), `-0.25s Cooldown` |
| `PoisonAlchemist` | **Poison Flask** | Rare | Открыта со старта | Бросает воксельную колбу в скопление врагов. На земле остаётся кипящая зелёная зона на `4s`, наносящая DoT каждые `0.4s` и ослабляющая врагов. | Урон тика `6`, CD `2.5s`, Радиус лужи `2.5м`, Длительность `4s`, Колб `1` | `+4 Tick Damage`, `+1 Flask` (`Count`), `+1.0m Puddle Radius` (`Area`) |
| `GuardianShield` | **Guardian Shields** | Common | Открыта со старта | Воксельные щиты вращаются по внутреннему кольцу (`1.6м` от героя), наносят урон и **отталкивают врагов назад** (`ApplyPull` от героя). | Урон `12`, Щитов `2`, Скорость `150°/с`, Knockback `1.4м` | `+1 Shield` (`Count`), `+8 Damage`, `+0.5m Knockback` (`Area`) |
| `MiniDragon` | **Dragon Whelp** | Epic | Ачивка `Arsenal` (5 скиллов за забег) | **Следующий миньон (Follower):** летает за героем с плавным отставанием (`Vector3.SmoothDamp` + `enableBobbing`), каждые `1.2s` выпускает конус из 3 огненных снарядов в ближайшую цель. | Урон `12 x 3`, CD `1.2s`, Дальность `8м`, Драконов `1` | `+1 Dragon Whelp` (`Count`), `+6 Damage`, `-0.2s Cooldown` |
| `VoxelTornado` | **Arcane Tornado** | Epic | Ачивка `Exterminator` (1 000 киллов) | Выпускает блуждающий воксельный вихрь (вращающаяся спираль из кубов), который движется вперёд по синусоиде `4s`, стягивая врагов к своей оси и нанося периодический урон. | Урон тика `9` (раз в `0.25s`), CD `3.2s`, Радиус `2.2м`, Вихрей `1` | `+1 Tornado` (`Count`), `+5 Tick Damage`, `+1.2s Duration` |
| `EarthquakeSlam` | **Seismic Hammer** | Legendary | Ачивка `ReachWave50` (Волна 50) | Каждые `4s` создаёт вокруг героя мощную сейсмическую волну, которая подбрасывает воксельные осколки, наносит высокий AoE-урон и тормозит врагов на `80%` на `1.5s`. | Урон `45`, CD `4.0s`, Радиус `4.5м` | `+20 Damage`, `+1.2m Radius`, `-0.6s Cooldown` |

#### Б. Новые Пассивные Способности (расширение `PassiveEffectType` и `HeroRuntimeStats`)

| ID | Название | Редкость | Анлок | Эффект (`StatPassiveAbilityData`) | Карточка модификатора |
|---|---|---|---|---|---|
| `ChronoTome` | **Chrono Tome** | Rare | Открыта со старта | `CooldownReductionPercent`: ускоряет перезарядку всех активных способностей на `+10%` | `+8% Cooldown Reduction` (макс до `50%` CDR) |
| `CriticalLens` | **Sniper Lens** | Epic | Ачивка `Untouchable` (5 волн без урона) | `CritChancePercent`: даёт `+12%` шанс нанести **x2 критический урон** оружием и способностями (жёлто-оранжевые числа крита в `DamageNumberItem`) | `+8% Crit Chance` |
| `TitanHeart` | **Titan Heart** | Common | Открыта со старта | `MaxHpFlat`: увеличивает максимальное здоровье на `+40 HP` и мгновенно лечит на `40 HP` | `+30 Max HP` |
| `GoldenIdol` | **Golden Idol** | Rare | Ачивка `Treasure Hunter` (5 сундуков) | `CoinBonusPercent`: увеличивает количество получаемых монет с врагов и сундуков на `+25%` | `+20% Coin Gain` |

#### В. Новые Парные Синергии (`Assets/_ScriptableObjects/Synergies/`)

| ID Синергии | Название карточки | Требуемые способности | Редкость | Эффект синергии (`SynergyCardData`) |
|---|---|---|---|---|
| `thunder_storm` | **Synergy: Thunderstorm** | `ChainLightning` + `IceAura` | Legendary | Цепная молния получает `+3 Bounces` и `+12 Damage`; Ледяная аура получает `+1.5m Radius` и усиливает замедление на `+20%`. |
| `toxic_inferno` | **Synergy: Toxic Inferno** | `PoisonAlchemist` + `Fireball` | Epic | Колбы алхимика (`+1 Count`, `+1.2m Area`, `+8 Damage`) и Фаерболы (`+1 Count`, `+10 Damage`) образуют взрывной напалм. |
| `aegis_phalanx` | **Synergy: Aegis Phalanx** | `orbital_small_sword` + `GuardianShield` | Epic | `+2 Orbital Swords`, `+2 Guardian Shields`, `+60°/s` скорости вращения обоим кольцам и `+15 Armor` герою. |
| `dragon_lord` | **Synergy: Dragon Lord** | `MiniDragon` + `BattleFury` | Legendary | Призывает `+1 Dragon Whelp`, даёт `+10 Damage` драконам и `+25% Global Damage` герою. |
| `cataclysm_vortex` | **Synergy: Cataclysm Vortex** | `BlackHole` + `VoxelTornado` | Legendary | Чёрная дыра (`+2m Radius`, `+8 Damage`) и Торнадо (`+2 Count`, `+8 Damage`) превращают экран в гравитационный шторм. |

---

### 4.4. Расширение Бестиария, Фазового Расписания Волн и Боссов

#### А. Обычные враги (6 архетипов с разным тактическим поведением)

| Ассет (`EnemyData`) | Префаб / Модель | Поведение / Класс | HP | Скорость | Урон | С какой волны (`minWave`) | Особенность |
|---|---|---|---|---|---|---|---|
| `SlimeData` | `Slime.prefab` | `EnemyBase` (мили) | `18` | `3.0` | `8` | Волна 1 | Базовый роевой враг; добавить покачивание/прыжок через `VoxelMeshAnimator` |
| `GoblinData` *(создать)* | `GoblinShooter.prefab` | `ShootingEnemy` | `28` | `2.8` | `6` (снаряд) | Волна 3 | Останавливается на `5.5м` и кидает дротики/стрелы |
| `SkeletonData` *(новый)* | `SkeletonRunner.prefab` | `EnemyBase` (быстрый рой) | `22` | `4.3` | `10` | Волна 5 | Быстрый натиск группами по 4–6 шт., также призывается Некромантом |
| `BatData` | `BatShooter.prefab` | `ShootingEnemy` | `38` *(снизить с 75)* | `3.2` | `7` (снаряд) | Волна 7 | Летающий стрелок, держит дистанцию `5м` |
| `BombBugData` *(новый)* | `BombSlime.prefab` | `ExploderEnemy : EnemyBase` | `32` | `3.8` | `28` (взрыв) | Волна 10 | На дистанции `1.8м` останавливается, мигает красным `0.75s` с кругом-телеграфом `2.5м` и взрывается (нанося урон и герою, и врагам вокруг!) |
| `OrcChargerData` *(новый)* | `OrcCharger.prefab` | `ChargerEnemy : EnemyBase` | `85` | `2.4` (рывок `11.0`) | `18` | Волна 14 | На дистанции `7м` заряжает рывок `0.8s` (линия-телеграф) и несётся вперёд по прямой |

#### Б. Модификаторы Элитных Врагов (Волна 10+)
Расширить `EnemyBase.MakeElite()` тремя типами элитных префиксов (с визуальным тинтом и мини-аурой):
1. **Berserker Elite (Фиолетовый):** `x3 HP`, `x2 Damage`, `+25% Speed`.
2. **Juggernaut Elite (Золотисто-бронзовый):** `x4.5 HP`, увеличенный размер `x1.35`, иммунитет к притяжению (`pullResistance = 0.2f`).
3. **Frost/Haste Elite (Голубой):** `x3 HP`, ускоряет соседних обычных врагов в радиусе `3.5м` на `+20%`.

#### В. Новые Боссы (Волны 25, 50, 75, 100)
1. **Волна 25 — `Stone Golem` (`GolemBoss`):** уже реализован (AoE Slam с круговым телеграфом + призыв слизней).
2. **Волна 50 — `Necromancer` (`NecromancerBoss`):** уже реализован (телепортация + веер из 5 снарядов + призыв миньонов). *Заменить призываемых миньонов с летучих мышей на `SkeletonData` или `GoblinData`.*
3. **Волна 75 — `Frost Lich / Crystal Wyrm` (`FrostLichBoss : BossBase`, новый):**
   - **Механика 1 (Cross Ice Spikes):** рисует на земле крестообразный/звёздообразный телеграф из 4–6 линий (`LineRenderer`) на `1.0s`, затем по линиям пробивают ледяные шипы (`35` урона + замедление героя).
   - **Механика 2 (Blizzard Ring):** создаёт вокруг себя безопасный внутренний глаз бури (`3м`) и внешнее ледяное кольцо (`3..9м`), заставляя игрока подбежать вплотную.
4. **Волна 100 — `Infernal Demon Lord` (`DemonLordBoss : BossBase`, новый):**
   - Двухфазный босс: на `< 50% HP` входит в **Enrage** (меняет эмиссию на ярко-алую, скорость `+35%`, совмещает рывок `Charge` с расходящимся кольцом из 12 огненных шаров и метеоритными зонами).

---

### 4.5. Биомы (Арены) и Интерактивные Объекты на Карте

#### А. Система Биомов (`BiomeData` / расширение `ArenaConfig`)
Сделать арену выбираемой перед стартом забега (или автоматически сменяемой / открываемой через ачивки):
1. **Green Forest (Зелёный лес):** открыт по умолчанию. Трава, дубы, сосны, пни, валуны.
2. **Scorched Desert (Раскалённая пустыня):** открывается ачивкой `ReachWave25`. Песчано-золотистый пол с тёплым освещением, воксельные кактусы, песчаные скалы, сухие коряги и древние колонны.
3. **Frozen Caverns (Ледяные пещеры):** открывается ачивкой `ReachWave50`. Сине-голубой кристальный пол, ледяные сталагмиты, заснеженные валуны и светящиеся кристаллы.

#### Б. Новые Интерактивные Объекты Арены
1. **Взрывные воксельные бочки (`ExplosiveBarrel`):**
   - Спавнятся по 8–12 штук на арене при генерации (`ArenaGenerator`).
   - Имеют `1 HP` (реализуют `IDamageable` или триггерятся от удара/касания). При разрушении детонируют через `150` AoE-урона в радиусе `4м` с ярким воксельным взрывом и `CameraController.Shake()`, уничтожая целую пачку врагов.
2. **Кристалл-Пылесос (`VacuumMagnetPickup`):**
   - Выпадает с шансом `25%` из мини-боссов (каждые 5 волн) и `100%` из боссов.
   - При подборе вызывает `XpManager.MagnetizeAll(speed: 28f)` и `CoinManager.MagnetizeAll(speed: 28f)` — все кристаллы и монеты на всей арене устремляются к герою.
3. **Алтарь Испытания (`ChallengeShrine`):**
   - Спавнится 1 на арене. При входе в зону алтаря спавнит вокруг героя кольцо из 6 элитных врагов; после их убийства выдаёт **100 монет + гарантированный LevelUp**.

---

## 5. Пошаговый Мастер-план разработки для Gemini (Execution Roadmap)

> **Инструкция для ИИ-агента (Gemini):**
> - Выполняй план последовательно по спринтам.
> - После **каждого** изменения C# кода обязательно вызывай `UnityEditor.AssetDatabase.Refresh();` (через MCP/CLI) и проверяй отсутствие ошибок компиляции.
> - Соблюдай правила `AGENTS.md`: **Singleplayer Only**, **Portrait Only (9:16 – 9:20)**, **Object Pooling** для всех спавнящихся объектов, **Zero GC** в `Update`/`FixedUpdate`, разделение логики и дочернего объекта `Visuals`, все балансные числа — в `ScriptableObject`.
> - Коммиты предлагай строго по стандарту **Conventional Commits** (`feat(scope): ...`, `fix(scope): ...`, `perf(scope): ...`).

---

### Спринт 1: Исправление текущих недоделок, иконки и визуальная полировка арены (P0)
- [x] **1.1. Подключить забытого `GoblinShooter` и выровнять ранние волны:**
  - Создать `Assets/_ScriptableObjects/GoblinData.asset` (`ShootingEnemyData`: `hp: 28`, `speed: 2.8`, `contactDamage: 5`, `xpDrop: 2`, `coinDropChance: 0.25`, `modelPrefab: GoblinShooter.prefab`, `weaponData: EnemyShooterWeapon.asset`, `stopDistance: 5.5`).
  - В [BatData.asset](file:///d:/UnityProjects/ProjectB/Assets/_ScriptableObjects/BatData.asset) снизить `hp` с `75` до `38` и поднять `speed` до `3.2`.
  - Добавить `GoblinData.asset` в `WaveManager.enemyTypes` в [Gameplay.unity](file:///d:/UnityProjects/ProjectB/Assets/_Scenes/Gameplay.unity).
- [x] **1.2. Доделать героя Мага (`Mage`):**
  - В [MageData.asset](file:///d:/UnityProjects/ProjectB/Assets/_ScriptableObjects/MageData.asset) выставить `unlockedByDefault: 0`, `passiveValue: 20` (`+20% Global Damage`), `ultimateName: Meteor Shower`, `ultimateDescription: Calls down 6 explosive voxel meteors around you.`.
  - В [ReachWave25.asset](file:///d:/UnityProjects/ProjectB/Assets/Resources/Achievements/ReachWave25.asset) изменить награду на `rewardType: 2` (`UnlockHero`), `rewardId: mage`, `description: Reach Wave 25 to unlock the Mage hero`.
  - В [HeroData.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/Data/HeroData.cs) добавить `HeroUltimateType.MeteorStrike = 1` и реализовать в [HeroUltimate.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/Player/HeroUltimate.cs) спавн 6 AoE-ударов метеоров по врагам в радиусе `7м` с воксельными партиклами и камерой-шейком.
- [x] **1.3. Визуальный фидбек взрыва `Fireball` и подбора Секретов:**
  - В [FireballProjectile.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/Abilities/Active/FireballProjectile.cs) публиковать событие взрыва (или добавить `FireballExplodedEvent` в `GameEvents.cs`), по которому [VfxFeedbackManager.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/Visuals/VfxFeedbackManager.cs) эмитит кольцо из 14–18 оранжево-жёлтых воксельных кубов по радиусу `explosionRadius`.
  - В `VfxFeedbackManager.OnSecretItemCollected()` отображать крупный всплывающий текст `evt.RewardSummary` над героем, а также добавить на HUD стрелку-компас к активному секрету (`SecretItemSpawner.ActiveItem`), когда он ближе `45м` и находится за кадром.
- [x] **1.4. Генерация 2D-иконок для всех ScriptableObjects (`IconGenerator`):**
  - Написать Editor-утилиту `Assets/_Scripts/Editor/Generators/VoxelIconGenerator.cs`, которая генерирует чёткие цветные пиксель/воксель-иконки (`64x64` / `128x128` PNG в `Assets/_UI/Icons/Generated/`) и автоматически назначает `icon` во все `AbilityData`, `CardData`, `HeroData`, `AchievementData`, `MetaUpgradeData` и `DailyBonusRewardData`.
- [x] **1.5. Улучшение пола арены, префабов и URP Post-Processing:**
  - Сгенерировать тайловую воксельную шахматную текстуру травы (`FloorGrid.png`, `FilterMode.Point`) и подключить её в [FloorMat.mat](file:///d:/UnityProjects/ProjectB/Assets/_Materials/FloorMat.mat) с тайлингом по размеру арены, чтобы движение камеры отлично читалось.
  - На [LaserTurret.prefab](file:///d:/UnityProjects/ProjectB/Assets/_Prefabs/LaserTurret.prefab) заменить импортированный `.obj` материал на shared-материал с `enableInstancing = true`.
  - На [Slime.prefab](file:///d:/UnityProjects/ProjectB/Assets/_Prefabs/Enemies/Slime.prefab) сбросить корневой `m_LocalPosition` в `(0, 0, 0)` и добавить `VoxelMeshAnimator` с `enableBobbing = true` (`bobbingSpeed: 6`, `bobbingHeight: 0.12`).
  - Очистить [DefaultVolumeProfile.asset](file:///d:/UnityProjects/ProjectB/Assets/Settings/DefaultVolumeProfile.asset) от битых тестовых компонентов (`m_Script: {fileID: 0}`), включить лёгкую виньетку (`Vignette intensity: 0.22`) и `ColorAdjustments` (`contrast: 12`, `saturation: 10`).
  - Удалить неиспользуемые пакеты (`com.unity.visualscripting`, `com.unity.timeline`, `com.unity.multiplayer.center`, `com.unity.ai.navigation`) из [Packages/manifest.json](file:///d:/UnityProjects/ProjectB/Packages/manifest.json).

---

### Спринт 2: Глубина прокачки, потолок уровней (`maxLevel = 5`), новые модификаторы и UX карточек (P1)
- [ ] **2.1. Ввести `maxLevel` для способностей:** *(пропущено по требованию пользователя — потолок уровней не добавлялся; реализован только `HeroAbilities.GetAbilityLevel(string id)` для отображения уровней на карточках)*
  - В [AbilityData.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/Abilities/Data/AbilityData.cs) добавить `public int maxLevel = 5;`.
  - В [HeroAbilities.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/Abilities/HeroAbilities.cs) добавить метод `int GetAbilityLevel(string id)` и `bool IsAbilityMaxed(string id)`.
  - В [UpgradeManager.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/LevelUp/UpgradeManager.cs) исключать `AbilityModifierCardData` из `validPool`, если `heroAbilities.IsAbilityMaxed(modifierCard.targetAbilityId)`.
- [x] **2.2. Создать недостающие карточки модификаторов (Раздел 4.2):**
  - Сгенерировать через `AbilitiesGenerator` / Editor-скрипт 10 новых карточек модификаторов (`ShooterMod_Count`, `ShooterMod_Cooldown`, `FireballMod_Count`, `FireballMod_Area`, `BoomerangMod_Count`, `BoomerangMod_Pierce`, `IceAuraMod_Radius`, `IceAuraMod_Slow`, `BlackHoleMod_Radius`, `BlackHoleMod_Force`, `OrbitalSword_SpeedAndRadius`) и привязать их в `unlockedModifiers` соответствующих `AbilityCardData`.
- [x] **2.3. Полировка `CardSelectionUI`, кнопка Паузы и Реролл:**
  - Добавить в [CardSelectionUI.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/LevelUp/CardSelectionUI.cs) плавную анимацию появления карточек (`scale bounce` на `Time.unscaledDeltaTime`) и отображение текущего уровня улучшаемой способности на карточке (`Lv 2 → Lv 3` или `NEW!`).
  - Добавить кнопку **Паузы (`||`)** в верхний угол боевого HUD (открывает окно паузы с ползунками звука, кнопками `Resume` и `Quit to Menu`).

---

### Спринт 3: Расширение арсенала — 6 новых Активных, 4 Пассивных способности и 5 Синергий (P1)
- [x] **3.1. Расширить `HeroRuntimeStats` под новые механики:**
  - В [HeroRuntimeStats.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/Player/HeroRuntimeStats.cs) добавить свойства:
    - `CooldownReductionMultiplier` (от `0.5f` до `1.0f`, применяется ко всем `ActiveAbility`),
    - `CritChance` (`0..1f`) и `CritMultiplier` (`1.1f` базовый, с прокачкой через `CriticalLensMod_Multiplier` и мета-апгрейд `crit_multiplier`) с методом `RollDamage(float rawDamage, out bool isCrit)`,
    - `CoinMultiplier` (`1.0f+`, применяется в `HeroEconomy.AddCoin`).
  - Обновить `EnemyDamagedEvent` и [DamageNumberItem.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/Visuals/DamageNumberItem.cs) для поддержки флага `IsCrit` (увеличенный масштаб `1.45x` и ярко-оранжевый цвет).
- [x] **3.2. Реализовать 6 новых активных способностей (Раздел 4.3.А):**
  - `ChainLightningAbility` + `ChainLightningAbilityData`
  - `PoisonFlaskAbility` + `PoisonPuddle` (пулимый объект зоны) + `PoisonFlaskAbilityData`
  - `GuardianShieldAbility` (с knockback через `EnemyBase.ApplyPull`)
  - `MiniDragonAbility` + `DragonFollower` (первый миньон-спутник с `SmoothDamp` следованием и залпом огня)
  - `VoxelTornadoAbility` + `TornadoProjectile`
  - `SeismicHammerAbility`
- [x] **3.3. Создать 4 новые пассивные способности и 5 новых синергий (Разделы 4.3.Б и 4.3.В):**
  - Создать `.asset`-файлы для `ChronoTome`, `CriticalLens`, `TitanHeart`, `GoldenIdol` и их карточек.
  - Создать 5 новых `SynergyData` + `SynergyCardData` (`Thunderstorm`, `Toxic Inferno`, `Aegis Phalanx`, `Dragon Lord`, `Cataclysm Vortex`) и зарегистрировать их в `UpgradeManager.synergies` и `AbilityCatalog.asset`.
  - При взятии карточки `SynergyCardData` проигрывать особый VFX/SFX эволюции в `VfxFeedbackManager`.
- [x] **3.4. Расширяемые слоты способностей (3+3 базовых -> 6+6 через ачивки и интерфейс для дерева прокачки):**
  - Реализован [AbilitySlotService.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/Meta/AbilitySlotService.cs) с поддержкой `SaveData.bonusActiveSlots`/`bonusPassiveSlots`, `MetaUpgradeEffectType.BonusActiveSlots`/`BonusPassiveSlots` и расширяемого интерфейса `IAbilitySlotBonusProvider` для будущего дерева прокачки.
  - [HeroAbilities.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/Abilities/HeroAbilities.cs) и [AbilityHudUI.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/UI/AbilityHudUI.cs) ограничивают забег разблокированными слотами (по умолчанию `3` активных + `3` пассивных) и отображают заблокированные ячейки с иконкой замка.
  - Добавлены 6 ачивок расширения слотов (`UnlockActiveSlot` / `UnlockPassiveSlot`) в `Assets/Resources/Achievements/`.

---

### Спринт 4: Фазовое расписание волн, новые враги (`Skeleton`, `BombBug`, `OrcCharger`), элиты и боссы (P2)
- [ ] **4.1. Фазовая система волн в `WaveConfig` и `WaveManager`:**
  - Добавить в [WaveConfig.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/Data/Enemies/WaveConfig.cs) список `List<EnemyWaveEntry>` (`EnemyData enemy`, `int minWave`, `int maxWave`, `float spawnWeight`), сохранив обратную совместимость с `WaveManager.enemyTypes`.
- [ ] **4.2. Реализовать новые классы врагов и воксельные префабы (Раздел 4.4.А):**
  - `SkeletonRunner` (`SkeletonData.asset`, воксельная модель скелета, `minWave: 5`, также назначить как `minionData` в `NecromancerBossData.asset`).
  - `ExploderEnemy : EnemyBase` (`BombBugData.asset`, `minWave: 10`: телеграфируемый самоподрыв при сближении с героем).
  - `ChargerEnemy : EnemyBase` (`OrcChargerData.asset`, `minWave: 14`: телеграфируемый прямолинейный рывок).
- [ ] **4.3. Разнообразить Элитных врагов и добавить Босса Волны 75 (`FrostLichBoss`):**
  - Добавить 3 архетипа элит (`Berserker`, `Juggernaut`, `Haste Aura`) в `EnemyBase.MakeElite()`.
  - Реализовать `FrostLichBoss : BossBase` с линейными телеграфами ледяных шипов и добавить его в `WaveConfig.bosses`.

---

### Спринт 5: Новые Герои (`Ranger`, `Necromancer`), Интерактивная Арена и Биомы (P2)
- [ ] **5.1. Добавить героев `Ranger` и `Necromancer` (Раздел 4.1):**
  - Сгенерировать воксельные модели `Ranger` (с луком и плащом) и `NecromancerHero`, создать `RangerData.asset` и `NecromancerHeroData.asset`, добавить их в `HeroCatalog.asset` и реализовать ульты `HeroUltimateType.ArrowRain` и `HeroUltimateType.SoulNova`.
- [ ] **5.2. Интерактивные объекты арены (Раздел 4.5.Б):**
  - Реализовать `ExplosiveBarrel.cs` (воксельная бочка, детонирующая при попадании и наносящая AoE-урон врагам) и спавнить 8–12 бочек в `ArenaGenerator`.
  - Добавить метод `MagnetizeAll(float speed)` в `XpManager` и `CoinManager` (с трекингом активных пикапов в `HashSet`) и дроп `VacuumMagnetPickup` с мини-боссов и боссов.
  - Реализовать `ChallengeShrine.cs` (Алтарь Испытания).
- [ ] **5.3. Система Биомов (Лес, Пустыня, Ледяные пещеры):**
  - Добавить выбор открытого биома на экране старта в `MainMenu` (`Forest`, `Desert`, `FrozenCaves`) с соответствующими `ArenaConfig` (цветовая палитра пола, префабы препятствий и декораций).

---

### Спринт 6: Финальный баланс, автотесты и подготовка релизного AAB (P3)
- [ ] **6.1. Расширение EditMode и PlayMode тестов:**
  - Покрыть тестами новые ульты героев, фильтр `maxLevel` в `UpgradeManager`, расчёт критов и `CooldownReduction` в `HeroRuntimeStats`, фазовый выбор врагов по `minWave` в `WaveManager` и `MagnetizeAll` пикапов.
- [ ] **6.2. Профилирование на Android и финальная сборка:**
  - Прогнать стресс-тест на Волне 40+ (100+ активных врагов, 6 активных скиллов + синергии) с замером Frame Timing и GC Allocations (0 байт аллокаций в боевом цикле).
  - Подключить боевой Google Mobile Ads SDK (`GOOGLE_MOBILE_ADS`), настроить иконку приложения и собрать подписанный релизный `.aab`.
