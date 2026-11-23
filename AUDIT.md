# Полный аудит проекта ProjectB (v2), оценка идеи/стилистики и Мастер-план разработки

**Рабочее название:** `ProjectB` (*Swarm Hero: Voxel Survivor*)  
**Актуальный срез репозитория:** коммит `9e2c3a3` (все 6 спринтов плана v1 выполнены)  
**Движок и рендер:** Unity `6000.5.5f1` (Unity 6.5), URP `17.5.0`  
**Архитектура и стек:** VContainer `1.19.0`, New Input System `1.19.0`, uGUI + TextMeshPro, ScriptableObject Data-Driven, 4 asmdef-сборки (`Data`, `Meta`, `Runtime`, `Editor`) + EditMode/PlayMode тесты  
**Платформа:** Android (API 34+, Portrait Only `9:16`–`9:20`, 60 FPS), далее iOS  
**Назначение документа:** Актуальный технический, визуальный и геймдизайнерский аудит + согласованный с автором пошаговый **Мастер-план (Спринты 7–12)** для продолжения разработки ИИ-агентом (Gemini).

---

## 1. Executive Summary: где проект находится сейчас

За Спринты 1–6 проект вырос в **полноценный, технически чистый вертикальный Voxel Survivor** с сильным инженерным фундаментом и богатой базой контента:

### Что уже работает на высоком уровне
- **Ядро и производительность (Mobile 60 FPS):** Zero-GC боевой цикл (подтверждено `Sprint6PlayModeStressTests.cs` на 120 врагах), chunk-culled GPU Instancing для 50 000 объектов травы ([ArenaDecorationRenderer.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/Arena/ArenaDecorationRenderer.cs)), пулы для врагов, снарядов, XP, монет и чисел урона, пакетное сохранение через `IsDirty` ([SaveManager.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/Meta/SaveManager.cs)), чистый DI-граф VContainer (`RootLifetimeScope` → `MainMenuLifetimeScope` / `GameLifetimeScope`).
- **Контентная база (уже больше изначального MVP):**
  - **4 героя:** `Knight`, `Mage`, `Ranger`, `Necromancer` со своими воксельными моделями, стартовым оружием, пассивками и ультами.
  - **21 способность:** 13 активных (`Orbital Sword`, `Shooter`, `Fireball`, `Ice Aura`, `Boomerang`, `Laser Turret`, `Black Hole`, `Chain Lightning`, `Poison Flask`, `Guardian Shields`, `Dragon Whelp`, `Arcane Tornado`, `Seismic Hammer`) + 8 пассивных + 8 парных синергий + 60+ карточек модификаторов.
  - **Бестиарий:** 6 обычных врагов с фазовым вводом по волнам (`Slime`, `GoblinShooter`, `SkeletonRunner`, `BatShooter`, `BombSlime`, `OrcCharger`), 3 архетипа элит (`Berserker`, `Juggernaut`, `HasteAura`), мини-боссы каждые 5 волн, 3 уникальных босса (`Stone Golem` W25, `Necromancer` W50, `Frost Lich` W75).
  - **Мета и мир:** расширяемые слоты `3+3 → 6+6` с заделом под дерево талантов ([AbilitySlotService.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/Meta/AbilitySlotService.cs) / `IAbilitySlotBonusProvider`), 21 ачивка, 8 мета-апгрейдов, 7-дневный Daily Bonus, 3 секретных предмета с компасом, взрывные бочки, Vacuum Magnet, Challenge Shrine, `AdManager` (Mock/AdMob), 20 EditMode-наборов тестов + PlayMode стресс-тест.

### Сводная оценка текущего состояния (v2)

| Направление | Оценка | Статус | Главный фокус улучшений |
|---|:---:|:---:|---|
| **1. Идея и Core Loop** | **8.2 / 10** | 🟢 Отличная база | Перевести Синергии из «стат-пачек» в реальные реакции (в духе *Divinity: Original Sin 2*); добавить **волны знакомства** с новыми врагами и события волн |
| **2. Стилистика и Воксельный арт** | **7.0 / 10** | 🟡 Хорошая база | Заменить runtime-примитивы (`Cube/Cylinder/Sphere`) у способностей Спринта 3, аур и щита Рыцаря на воксельные модели; починить белый Hit-Flash шейдером `VoxelLit`. *(16×16 иконки карточек ОК на текущий этап; 3D-рендер всех иконок — позже)* |
| **3. Производительность (Mobile)** | **9.0 / 10** | 🟢 Эталон | Сохранить 0 байт GC при добавлении separation врагов и реактивных синергий; очистить `DefaultVolumeProfile.asset` и рантайм `Debug.Log` |
| **4. Баланс и Боевой Feel** | **6.5 / 10** | 🟠 Нужен инструмент | Враги слипаются в одну точку (нет separation); нет раздельного HP/Damage скейлинга и симулятора баланса для длинной мобильной прогрессии; один `sfx_boss_spawn` играет на 7 разных событий |
| **5. Мета-прогрессия и Retention** | **7.0 / 10** | 🟡 Хорошо для D1 | Плоский список из 8 мета-апгрейдов → добавить **Дерево Созвездий** (радиальное, со свободным панорамированием во все стороны) с **Keystone-перками** + **Post-Run DPS Breakdown** |
| **6. UX / Гигиена проекта** | **7.4 / 10** | 🟡 Почти релиз | Выход через `Pause → Quit to Menu` молча сжигает монеты забега; компас не видит Алтарь и Магнит; устаревшие `CONTEXT.md` / `IMPLEMENTATION_PLAN.md` |

> **Зафиксированные решения пользователя (ОБЯЗАТЕЛЬНО соблюдать при разработке):**
> 1. **Без потолка `maxLevel` у способностей:** бесконечная прокачка оставлена осознанно ради длинной мобильной прогрессии — НЕ добавлять жёсткий кап уровней.
> 2. **Чистые Реактивные Синергии (без плоских стат-баффов) + перенос из карточек в Дерево Созвездий:** убрать из `SynergyCardData` плоские стат-пачки (`+Count/+Damage`); каждая синергия — это **чистая системная/стихийная реакция** (как в *Divinity: Original Sin 2*). Источник активации проектируется через интерфейс `ISynergyUnlockProvider`: **основное направление — разблокировка синергий как узлов в Радиальном Дереве Созвездий с авто-активацией в забеге при сборе пары способностей** (в Спринте 8 механики работают сразу при сборе пары, а в Спринте 11 привязываются к узлам дерева — или, если пользователь передумает, легко возвращаются в карточки).
> 3. **Волны «знакомства» с новыми мобами (без UI-подсказок):** в волне первого появления нового типа врага просто **~50% мобов — это новый тип**, без баннеров и текстовых подсказок — игрок оценивает врага естественно в бою.
> 4. **Иконки `16×16` пока оставить:** пиксель-арт иконки карточек допустимы на текущем этапе; унификация всех UI-иконок через 3D-рендеры в Blender (включая замену AI-иконок нижнего меню `Play.png` и др.) переносится в пост-основной арт-полиш.
> 5. **Радиальное Дерево Созвездий:** ноды расходятся во все стороны от центрального узла, с обязательным **плавным панорамированием во все стороны** и удобным управлением одной рукой.
> 6. **Без пунктирного кольца под элитами** (убрано в `43b8ca3`) — выделять элит через rim-подсветку шейдера.
> 7. **Биомы и боевой AdMob SDK / релизный `.aab` отложены** в раздел Pre-Release / Post-Launch.

---

## 2. Оценка идеи, рыночного потенциала и стилистики

### 2.1. Оценка идеи: от «survivor с цифрами» к системному «D:OS2-хаосу»
Формула **Portrait 3D Voxel Survivor + Auto-Battler** отлично подходит для Google Play и вертикального органического контента (TikTok / Shorts / Reels). Чтобы игра выделялась среди десятков клонов *Survivor.io*, у неё должны появиться **4 фирменных столпа**:

1. **Стихийные и тактические реакции (как в *Divinity: Original Sin 2*):**  
   Вместо скучных `+15% урона` синергии физически связывают способности друг с другом: `Fireball` поджигает ядовитую лужу `Poison Flask`, превращая её в бушующий напалм; `Fireball` по врагу в `Ice Aura` создаёт взрыв пара с отбрасыванием; две `Laser Turret` натягивают между собой лазерный забор; `Chain Lightning` по замороженному врагу раскалывает его ледяной шрапнелью. Это мгновенно даёт глубину билдостроения и идеальные «вау»-моменты для видео.
2. **Осмысленная бесконечная прокачка (Overdrive-пороги Lv 10 / Lv 20):**  
   Поскольку `maxLevel` отсутствует, достижение вех `Lv 10` и `Lv 20` (и далее каждые `+10` уровней) визуально усиливает способность (увеличение модели/эмиссии + разовая веха *OVERDRIVE* / *AWAKENED* / *MYTHIC*), превращая долгий прогресс в зрелищную эволюцию.
3. **Ритм и драматургия забега (Intro Waves + Wave Events + Combo):**  
   - **Волны знакомства (Intro Waves):** в волне дебюта нового врага примерно **половина волны (`~50%`)** состоит из этого нового архетипа — без всплывающих баннеров и текстовых подсказок, игрок сам замечает и изучает нового врага прямо в бою.
   - **Волновые события (Wave Events):** каждые 3–4 волны — всплеск напряжения (*Swarm Rush*, *Gold Goblin*, *Blood Moon*, *Elite Ambush*) + прыгающий счётчик **Combo (`x30`, `x50`, `x100`)**.
4. **Длинная мета-прогрессия — Радиальное Дерево Созвездий (`Constellation Talent Tree`):**  
   Звёздная карта, расходящаяся от центрального ядра во все стороны с плавным перетаскиванием пальцем и зумом, где мелкие узлы ведут к **узлам Синергий** (разблокирующим стихийные комбо навсегда с авто-активацией в забеге!), **Keystone-перкам** и дополнительным слотам способностей через `IAbilitySlotBonusProvider`.

### 2.2. Оценка стилистики и визуального языка (Voxel Art)
- **Сильная сторона:** воксельные 3D-модели Спринтов 1/4/5 (4 героя, враги, 3 босса, деревья, бочка, алтарь, секреты) с двухматериальной схемой (`Base_Mat` + `Glow_Mat`) и стоп-моушен аниматором `VoxelMeshAnimator` создают приятный «игрушечно-диорамный» стиль.
- **Текущие визуальные разрывы:**
  1. **Примитивы Unity в бою:** способности Спринта 3 (`DragonWhelp`, `GuardianShield`, `PoisonFlask`, `PoisonPuddle`, `VoxelTornado`, `SeismicHammer`), ауры (`IceAura`, `BlackHole`) и купол Рыцаря (`Hero.prefab`) всё ещё построены на runtime `CreatePrimitive(Cube/Cylinder/Sphere)`. Это самый заметный визуальный долг в геймплее.
  2. **Сломанный Hit-Flash и отсутствие Rim-выделения:** стандартный `URP/Lit` не умеет вспыхивать белым через `_BaseColor` и не выделяет силуэт элит в плотной толпе.
  3. **Разнородность UI-иконок (отложенный арт-долг):** иконки нижнего меню (`Play.png`, `Abilities.png`, `Upgrade.png`) — AI-сгенерированные 3D-картинки, а карточки используют процедурный `16×16` пиксель-арт. На текущем этапе `16×16` иконки карточек **оставляем без изменений** (они функциональны); позже, на этапе финального арт-полиша, всю линейку UI-иконок (включая табы меню) стоит перерендерить в едином стиле прямо в Blender из `.blend`-исходников.

---

## 3. Технический и геймдизайнерский аудит текущей кодовой базы

Подтверждённые баги, проблемы баланса и техдолг на коммите `9e2c3a3`:

### 3.1. Слипание толпы врагов в одну точку (P0)
- **Локация:** [EnemyBase.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/Enemies/EnemyBase.cs#L455-L502)
- **Проблема:** Враги двигаются прямым сдвигом `transform.position += ...` без расталкивания соседей. Десятки мобов быстро схлопываются в одну стопку на одной точке — рой выглядит бедно, и AoE бьёт всех как одну цель.
- **Решение:** Zero-GC **Spatial Grid Separation** (решётка `2×2м` с фиксированными буферами в `WaveManager`, обновление раз в 2 кадра, мягкое расталкивание в радиусе `0.85м` с учётом `ObstacleMask`).

### 3.2. Синергии: баг накрутки уровней и отсутствие реальных реакций (P0)
- **Локация:** [UpgradeApplier.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/LevelUp/UpgradeApplier.cs#L109-L123), [HeroAbilities.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/Abilities/HeroAbilities.cs#L114-L153), `Assets/_ScriptableObjects/Cards/SynergyCard_*.asset`
- **Проблема:**
  1. `ApplySynergyCard()` вызывает `heroAbilities.UpgradeAbility(...)` для каждого бонуса в списке, накручивая скиллам `+2..+4` лишних уровня за одну карту (ложно триггеря ачивки `MaxAbilityLevel5` / `SlotPassive2_AbilityLvl8`).
  2. Все 8 синергий — просто списки плоских стат-баффов без уникальной механики; к тому же вылавливание синергии отдельной карточкой в пуле LevelUp создаёт тройной рандом.
- **Решение:** **Убрать стат-пачки из синергий полностью** и убрать карточки синергий из пула LevelUp. Реактивные синергии (Раздел 4.1) активируются **автоматически в забеге при сборе пары способностей** (если синергия разблокирована через `ISynergyUnlockProvider` — целевое место разблокировки: **узлы Дерева Созвездий**).

### 3.3. Потеря прогресса забега при выходе через Pause Menu (P0)
- **Локация:** [PauseMenuUI.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/UI/PauseMenuUI.cs#L114-L125), [GameManager.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/Core/GameManager.cs#L224-L228)
- **Проблема:** Монеты забега лежат в `RunStatistics.CoinsEarned` и пишутся в `SaveData` только при смерти (`HandleHeroDeath`). Нажатие **Quit to Menu** в паузе без предупреждения сжигает все собранные монеты, киллы и рекордную волну.
- **Решение:** Добавить `GameManager.AbandonRunAndGoToMenu()` с сохранением `RunResult` + окно подтверждения в `PauseMenuUI`.

### 3.4. Сломанный Hit-Flash врагов и отсутствие защиты/фидбека при уроне по герою (P0)
- **Локация:** [EnemyBase.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/Enemies/EnemyBase.cs#L31), [Knight_Mat.mat](file:///d:/UnityProjects/ProjectB/Assets/_Materials/Voxels/Knight_Mat.mat#L131-L135), [HeroHealth.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/Player/HeroHealth.cs#L92-L109)
- **Проблема:**
  1. `EnemyBase` применяет `HitFlashColor = (1, 0.92, 0.85)` в `_BaseColor` — в `URP/Lit` это умножает текстуру и слегка затемняет врага вместо яркой белой вспышки.
  2. У героя нет коротких пост-хит **i-frames** (`0.18s`), вспышки модели и красной виньетки на HUD — одновременный контакт с несколькими врагами может мгновенно убить героя за 1 кадр.
- **Решение:** Кастомный шейдер `ProjectB/VoxelLit` с `_HitFlash` (`0..1` lerp к белому) + `_RimColor` для элит + `0.18s` i-frames в `HeroHealth` + красная пульсирующая рамка на HUD.

### 3.5. Инструментарий для будущего баланса и отсутствие волн знакомства (P0/P1)
- **Локация:** [WaveConfig.asset](file:///d:/UnityProjects/ProjectB/Assets/_ScriptableObjects/WaveConfig.asset), [WaveManager.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/Enemies/WaveManager.cs#L494-L528)
- **Проблема:**
  1. Когда на волне 3/5/7/10/14 открывается новый моб, он просто подмешивается по весу в общий рой — нет **волны «знакомства»**, где ~50% врагов являются новым типом, чтобы игрок успел его заметить и оценить.
  2. В `WaveConfig` один общий линейный `difficultyPerWave = 0.1` одновременно скейлит и HP, и урон врагов. Для настройки длинной мобильной прогрессии без `maxLevel` нужно разделить кривые HP и Damage (редактируемые `AnimationCurve` или формула с порогами в `WaveConfig`) + **EditMode-симулятор баланса** (таблица расчётного HP/DPS по волнам 1–100) и **Post-Run DPS Breakdown**, чтобы балансировать по реальным цифрам, а не вслепую.
  3. На Волне 100 нет 4-го босса (цикл возвращается к Голему), а у `Frost Lich` (W75) нет ачивки.

### 3.6. Несогласованность CDR, хардкод в `HeroUltimate` и автоаим по Бочкам (P1)
- **Локация:** [HeroUltimate.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/Player/HeroUltimate.cs#L17-L34), [WeaponController.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/Combat/WeaponController.cs#L113), [ExplosiveBarrel.prefab](file:///d:/UnityProjects/ProjectB/Assets/_Prefabs/Arena/ExplosiveBarrel.prefab#L14), [OrbitalCompanionAbility.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/Abilities/Active/OrbitalCompanionAbility.cs#L162)
- **Проблема:**
  1. Параметры ульт захардкожены в `private const` внутри `HeroUltimate.cs` вместо `HeroData` (нарушение Data-Driven правила `AGENTS.md`). Ульта Рыцаря `Aegis Shield` не даёт ударной волны отбрасывания и использует гладкую сферу Unity.
  2. `WeaponController` и `HeroUltimate` не учитывают `CooldownReductionMultiplier`.
  3. Автоатака стреляет в ближайшую `ExplosiveBarrel` (слой `Enemy`), даже если вокруг неё `0` врагов; а `OrbitalCompanionAbility` ищет `EnemyBase` вместо `IDamageable` и не подрывает бочки.

### 3.7. Визуальные/аудио дефекты, пустота арены и гигиена репозитория (P0/P1)
- **Z-fighting аур:** `IceAuraAbility` и `BlackHoleAbility` лежат в одной плоскости `y = 0` и кастят тени.
- **Арена и Компас:** на поле `500×500` всего `180–190` препятствий без `Physics.CheckSphere` (могут накладываться друг на друга), а бочки/алтарь/секреты спавнятся только в радиусе `<= 65м` от `(0,0)`. [SecretCompassIndicatorUI.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/UI/SecretCompassIndicatorUI.cs) не показывает `ChallengeShrine` и `VacuumMagnetPickup`.
- **Аудио-повторы:** `SfxType.BossSpawn` переиспользуется на 7 разных взрывах/событиях; нет отдельного трека босса и приглушения музыки на паузе.
- **UI мелочи:** `GameOverUI.FormatReward()` печатает сырые `rewardId` (`Unlocked: LaserTurret`, `Hero: ranger`); нет разбивки урона (**Post-Run DPS Breakdown**).
- **Мусор и техдолг:**
  - [DefaultVolumeProfile.asset](file:///d:/UnityProjects/ProjectB/Assets/Settings/DefaultVolumeProfile.asset) содержит 674 незакоммиченные строки лишних URP-компонентов (`MotionBlur`, `FilmGrain`, `PaniniProjection`, `LensDistortion`, `ChannelMixer`, `ColorLookup` с `active: 1`).
  - Мёртвые поля/ассеты: `WaveConfig.eliteAuraMaterial`, параметр `auraMaterial` в `EnemyBase.MakeElite()`, `Assets/_Materials/EliteAuraMat.mat`.
  - Локальный мусор: `test.apk`, `test_BackUpThisFolder_ButDontShipItWithYourGame/`, `ProjectB_BurstDebugInformation_DoNotShip/`, `ArtSources/Blender/Ranger.blend1`.
  - `37` ScriptableObjects со старым `Assembly-CSharp::`, `21` вызов `Debug.Log(...)` в рантайме, устаревшие `CONTEXT.md` и `IMPLEMENTATION_PLAN.md`.

---

## 4. Blueprint расширения контента и механик (V2 Content Expansion)

### 4.1. Чистые Реактивные Синергии (в духе *Divinity: Original Sin 2* — БЕЗ плоских стат-пачек!)
Каждая синергия **не добавляет плоские `+Damage/+Count`, не накручивает уровни и не занимает карточку LevelUp**. Вместо этого разблокированная в Дереве Созвездий синергия (`ISynergyUnlockProvider`) **автоматически активируется в забеге**, как только герой собирает обе нужные способности, включая **уникальную системную реакцию** между ними (12 синергий: 8 переработанных + 4 новых):

| ID | Название | Комбинация | Уникальная системная реакция на поле боя |
|---|---|---|---|
| `toxic_inferno` | **Toxic Inferno** | `PoisonAlchemist` + `Fireball` | Взрыв `Fireball` внутри или на краю `PoisonPuddle` **воспламеняет всю лужу**: она мгновенно детонирует (огненный взрыв `1.6x` урона) и превращается в **горящее напалмовое поле** (оранжево-зелёное пламя, `2x` урон тика до конца жизни) |
| `steam_blast` | **Steam Blast** | `Fireball` + `IceAura` | Попадание `Fireball` по любому замедленному врагу вызывает **Паровую Нову** (`4.2m` радиус): белое воксельное облако пара наносит `1.8x` бонусного AoE-урона и отбрасывает врагов от центра |
| `thunder_storm` | **Thunderstorm** | `ChainLightning` + `IceAura` | Молния, бьющая по замедленному врагу, **перегружает холод**: вызывает глубокую заморозку (`0.65s`, `90%` slow) и пускает от цели 2 дополнительных коротких ледяных разряда по соседям |
| `overcharged_grid` | **Overcharged Grid** | `LaserTurret` + `Shooter` | Все активные `LaserTurret` на расстоянии `<= 12м` соединяются **высоковольтной электродугой** (`LineRenderer`), режущей врагов на линии каждые `0.25s`; а снаряды `Shooter`, пролетающие рядом с турелью, заряжаются и получают пробитие (`pierce +2`) |
| `blade_storm` | **Blade Storm** | `orbital_small_sword` + `Boomerang` | Каждые `2.8s` (или при каждом `3-м` попадании бумеранга) орбитальные мечи **выстреливают призрачными копиями клинков** наружу по спирали на `7.5м`, пронзая толпу |
| `aegis_phalanx` | **Aegis Phalanx** | `orbital_small_sword` + `GuardianShield` | Каждый раз, когда `GuardianShield` отталкивает врага, между щитом и ближайшим `OrbitalSword` вспыхивает **ударная волна фаланги** (`2.4m` AoE-разрез + золотые искры) |
| `dragon_lord` | **Dragon Lord** | `MiniDragon` + `BattleFury` | Дракончики переходят в режим **Fury Breath**: их залп превращается в пробивающие (`pierce 3`) огненные шары, оставляющие короткие очаги горения под поражёнными врагами |
| `cataclysm_vortex` | **Cataclysm Vortex** | `BlackHole` + `VoxelTornado` | Каждый `VoxelTornado`, проходящий через радиус `BlackHole` (или при исчезновении), **дестабилизируется и разрывается** веером из `6` гравитационных осколков, наносящих AoE-урон по краям арены |
| `shatter_nova` *(NEW)* | **Shatter Nova** | `GlacialShatter` + `SeismicHammer` | Сейсмический удар по замороженным врагам вызывает **Хрупкий Раскол**: наносит `2.5x` урона и взрывает цель на 4 ледяных осколка по соседям |
| `ricochet_barrage` *(NEW)* | **Ricochet Barrage** | `BuzzsawChakram` + `CriticalLens` | Каждый **критический удар** чакрама немедленно порождает **дополнительный осколочный мини-диск** (2 рикошета) и ускоряет основной диск |
| `solar_eclipse` *(NEW)* | **Solar Eclipse** | `SkyfallSpire` + `BlackHole` | Когда `SkyfallSpire` бьёт рядом с героем/Чёрной дырой, он вызывает **затменный импульс**, стягивающий врагов в точку удара за `0.3s` до взрыва и оставляющий слепящий след |
| `phantom_legion` *(NEW)* | **Phantom Legion** | `ShadowDecoy` + `MiniDragon` | При детонации голограмма-приманка взрывается кольцом из **8 огненных шаров дракона**, расходящихся во все стороны |

### 4.2. Пороги Overdrive для безлимитной прокачки (без `maxLevel`)
Чтобы бесконечные уровни способностей оставались зрелищными и ощущались как эволюция:
- **Lv 1–9:** базовый вид способности.
- **Lv 10 — OVERDRIVE:** `+15%` визуальный масштаб снарядов/моделей + усиленная эмиссия + всплывающий золотой текст `"OVERDRIVE!"` + системный бонус (`+20%` скорости/дальности/радиуса).
- **Lv 20 — AWAKENED:** `+25%` масштаб + яркий шлейф + `+1` дополнительный снаряд/объект/прыжок + текст `"AWAKENED!"`.
- **Lv 30+ — MYTHIC (каждые +10 уровней):** мягкий визуальный импульс и рост масштаба урона без взрыва числа частиц на экране.

### 4.3. Волны «Знакомства» (Intro Waves), События Волн и Новые Враги / Босс W100

#### А. Волны «Знакомства» (`EnemyIntroWave`)
При первом достижении `minWave` нового архетипа врага эта волна автоматически работает как **волна знакомства** (без лишних UI-баннеров и текстовых подсказок):
- **Расписание:** W3 (`GoblinShooter`), W6 (`SkeletonRunner`, сдвинуть с W5 чтобы не пересекалось с мини-боссом), W7 (`BatShooter`), W11 (`BombSlime`, сдвинуть с W10), W14 (`OrcCharger`), W18 (`ShieldBearer`), W22 (`VoidSummoner`).
- **Механика:** на волне, где `entry.minWave == currentWave`, ровно **~50% врагов волны (`introSpawnShare = 0.5f`)** спавнятся именно этого нового архетипа, а остальные ~50% — обычный микс. Игрок сам замечает и оценивает нового врага прямо в бою.

#### Б. События Волн (`WaveEventManager`, каждые 3–4 обычные волны, не на Boss/Intro волнах)
| Событие | Длительность | Механика | Награда / Хук |
|---|---|---|---|
| **Swarm Rush** | 12с | Кольцо из 24–32 быстрых скелетов окружает героя на `r=12м` | Момент для ульты, бочки и комбо `x30+` |
| **Gold Goblin** | 18с | Золотой убегающий воксельный гоблин (`speed 4.8`) с золотым компасом | Роняет монеты при ударах + взрыв `40–80` монет + `Vacuum Magnet` при смерти |
| **Blood Moon** | 20с | Свет арены краснеет; враги быстрее на `+20%` | Все враги дропают **`x2 XP` и `x2 Coins`** |
| **Elite Ambush** | До зачистки | 3 элиты разных архетипов врываются треугольником | Последний дропает `+50 Coins` + `Vacuum Magnet` |

#### В. Новые Враги и 4-й Босс (Волна 100)
| Ассет (`EnemyData`) | Модель (`.blend` + `.obj`) | Поведение / Класс | Ввод (`minWave`) | Тактическая роль |
|---|---|---|---|---|
| `ShieldBearerData` | `ShieldOrc` (щит + топор) | `ShieldedEnemy : EnemyBase` | Волна 18 (Intro W18) | Снижает фронтальный урон (`±60°`) на `65%` — поощряет обход, орбитали и AoE |
| `VoidSummonerData` | `VoidAcolyte` (капюшон + сфера) | `SummonerEnemy : EnemyBase` | Волна 22 (Intro W22) | Держит `8м`, каждые `6s` призывает 2 мини-скелетов и лечит соседей на `15%` |
| `GoldGoblinData` | `GoldGoblin` (мешок золота) | `FleeingLootEnemy : EnemyBase` | Event (W4+) | Убегает от героя, сыпет монеты при каждом попадании |
| `DemonLordBossData` | `DemonLord` (рога + лавовый меч) | `DemonLordBoss : BossBase` | **Волна 100** | **2 фазы:** Ф1 — телеграфируемый `Fire Charge` + кольцо из 8 огненных шаров; Ф2 (`<50% HP` **ENRAGE**) — алое свечение, `+35%` скорости, 12 шаров + 5 метеоритных зон |

### 4.4. Новые Герои (до 6), Карты Оружия и Новые Способности
- **2 новых Героя (`HeroCatalog`):**
  - **`Paladin`** (анлок: `DefeatFrostLich`, W75): `120 HP`, Speed `4.8`, оружие *Holy Hammer* (пробивает `2` цели), пассивка `+25 Armor & +2 HP/s`, ульта *Divine Consecration* (золотой круг `5.5м` на `6s` — сжигает врагов и лечит героя).
  - **`Engineer`** (анлок: `Clear5Shrines` или `DefeatDemonLord`): `90 HP`, Speed `5.1`, оружие *Cluster Grenade* (AoE `2.2м`), пассивка `+20% Area & +12% CDR`, ульта *Mech Drop* (сбрасывает 3 усиленные авто-турели с ударной волной).
- **Карты базового оружия (`WeaponUpgradeCardData`):** `WeaponMod_Multishot` (`+1` снаряд веером), `WeaponMod_FireRate` (`+20%` скорострельности), `WeaponMod_Power` (`+30%` урона).
- **4 новых Активных и 2 Пассивных способности:**
  - `BuzzsawChakram` (рикошетящий воксельный диск, Rare, открыта со старта);
  - `SkyfallSpire` (небесный столб огня в самую плотную группу врагов, Epic, ачивка `Clear3Shrines`);
  - `GlacialShatter` (волна заморозки + взрыв замороженных врагов шрапнелью, Rare, открыта со старта);
  - `ShadowDecoy` (голограмма-приманка с взрывом через `3.5s`, Epic, ачивка `Combo50`);
  - `VampiricFang` (пассивка: `+2 HP` за каждые 8 убийств, Epic, ачивка `ReachWave35`);
  - `AreaTome` (пассивка: `+12%` глобальный радиус/площадь `GlobalAreaMultiplier`, Rare, открыта со старта).

### 4.5. Радиальное Дерево Созвездий (`Constellation Talent Tree`)
Реализует `IAbilitySlotBonusProvider` из [AbilitySlotService.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/Meta/AbilitySlotService.cs) **и `ISynergyUnlockProvider`** (разблокировка реактивных синергий прямо в дереве!) и хранится в `SaveData` (`saveVersion = 2`: `bossShards`, `unlockedTalentIds`):
- **UX навигации (Portrait One-Thumb):**
  - Большое полотно звёздной карты (`2200×2200`) внутри `RectMask2D` с **центральным Ядром (`Origin`) в `(0, 0)`**, от которого ветки расходятся во все стороны (равномерно по кругу + перекрёстные мостики).
  - **Свободное панорамирование пальцем во все стороны (360° Pan с инерцией и мягким отскоком от границ)** + **Zoom** (щипок двумя пальцами + компактные кнопки `+` / `−` / `⌖ Центр` в углу, чтобы удобно играть одной рукой).
  - Тап по любому узлу плавно подводит его в верхнюю половину экрана и открывает компактную нижнюю карточку с описанием, ценой и кнопкой **UNLOCK** (не загромождая карту).
  - Визуал связей: светящиеся линии созвездий (тусклые = закрыто, пульсирующие = можно открыть, золотые = открыто); **узлы Синергий** выделены бирюзово-фиолетовым ореолом, а **Keystone-ноды** — в `1.45×` крупнее с особой формой рамки.
- **Валюта:** **Boss Shards (Осколки Боссов)** — падают с мини-боссов (`+1`), боссов (`+5`), зачистки Алтарей (`+2`) + опция покупки узлов начального кольца за Монеты.
- **Структура узлов (статы + Синергии + 3 Keystone-перка):**
  - **Созвездие Ярости (Offense):** `+6% Crit` → `+10% Global Area` → *Узлы стихийных Синергий* (`Toxic Inferno`, `Dragon Lord`, `Blade Storm`, `Ricochet Barrage`) → `+15% Boss Damage` → `+10% CDR` → **Keystone *Overclocked Arsenal*** (`+1 Active Slot` через `IAbilitySlotBonusProvider` + `-8% CD`).
  - **Созвездие Бастиона (Defense):** `+15 Armor` → `+15% Healing` → *Узлы защитных/контроль Синергий* (`Steam Blast`, `Thunderstorm`, `Aegis Phalanx`, `Shatter Nova`) → `+0.15s i-frames` → `+25% Ult CDR` → **Keystone *Second Wind*** (1 раз за забег переживает смертельный удар с `30% HP`, `2.5s` неуязвимости и волной отбрасывания).
  - **Созвездие Алчности и Хаоса (Fortune & Arcane):** `+20% Coin Gain` → `+1 Free Reroll` → *Узлы арканных Синергий* (`Overcharged Grid`, `Cataclysm Vortex`, `Solar Eclipse`, `Phantom Legion`) → `Barrel Respawn +2` → `Secret & Shrine +30%` → **Keystone *Collector's Greed*** (`+1 Passive Slot` через `IAbilitySlotBonusProvider` + каждые 25 XP-кристаллов дают `+5` монет).

---

## 5. Пошаговый Мастер-план разработки для Gemini (Спринты 7–12)

> **Инструкция для ИИ-агента (Gemini):**
> 1. Выполняй спринты последовательно (`7 → 8 → 9 → 10 → 11 → 12`), отмечая завершённые пункты `[x]` и прогоняя тесты после каждого спринта.
> 2. **После ЛЮБОГО изменения C# кода** обязательно вызывай `UnityEditor.AssetDatabase.Refresh();` (через MCP/CLI) и проверяй отсутствие ошибок компиляции.
> 3. Соблюдай `AGENTS.md`: **Singleplayer Only**, **Portrait Only (`9:16`–`9:20`)**, **Object Pooling**, **Zero GC** в `Update`/`FixedUpdate`, визуал строго в дочернем `Visuals`, все балансные параметры в `ScriptableObject`.
> 4. **НЕ добавлять `maxLevel`**, **НЕ добавлять наземные пунктирные круги под элит**, **НЕ тратить время на перерисовку 16×16 иконок карточек** (они отложены в арт-полиш), **НЕ трогать Биомы и боевой AdMob SDK** до этапа Pre-Release.
> 5. Все новые воксельные модели сохранять с `.blend`-исходниками в `ArtSources/Blender/` + экспортировать `.obj` с shared-материалами `enableInstancing: 1`.
> 6. Коммиты предлагай только на одобрение пользователю, строго по **Conventional Commits**.

---

### Спринт 7: Критические фиксы, Separation врагов, Волны «Знакомства» и Фундамент Баланса (P0)
- [x] **7.1. Zero-GC Spatial Separation для роя врагов:**
  - Реализовать `EnemySeparationSystem` (пространственная сетка `2×2м` на фиксированных массивах без аллокаций, обновление в [WaveManager.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/Enemies/WaveManager.cs) раз в 2 кадра), мягко расталкивающий соседних `EnemyBase` в радиусе `0.85м` с учётом `ObstacleMask`, чтобы толпа не слипалась в одну точку.
- [x] **7.2. Волны «знакомства» с новыми врагами (`EnemyIntroWave`, без баннеров и подсказок):**
  - В [WaveConfig.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/Data/Enemies/WaveConfig.cs) добавить в `EnemyWaveEntry` поля `bool triggerIntroWave = true` и `float introSpawnShare = 0.5f` (половина мобов волны — новый тип).
  - Сдвинуть `minWave`, пересекающиеся с мини-боссами (`SkeletonRunner`: `minWave = 6`, `BombSlime`: `minWave = 11`).
  - В `WaveManager` на волне дебюта (`entry.minWave == currentWave`) гарантировать ~50% спавнов именно нового врага, без всплывающих подсказок и UI-баннеров.
- [x] **7.3. Фиксы критических багов (`PauseMenu`, `HeroUltimate`, `CDR`, `ExplosiveBarrel`, `i-frames`):**
  - Добавить `GameManager.AbandonRunAndGoToMenu()` с сохранением `RunResult` в `SaveManager` + подтверждение в [PauseMenuUI.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/UI/PauseMenuUI.cs).
  - Вынести все `private const` параметры ульт из [HeroUltimate.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/Player/HeroUltimate.cs#L17-L34) в [HeroData.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/Data/HeroData.cs) и заполнить 4 `HeroData`-ассета; добавить ульте Рыцаря (`Shield`) импульс отбрасывания (`4.5м`, `40` урона).
  - Применить `CooldownReductionMultiplier` в [WeaponController.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/Combat/WeaponController.cs#L113) и [HeroUltimate.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/Player/HeroUltimate.cs#L619).
  - В `WeaponController` и стрелковых скиллах не целиться в `ExplosiveBarrel`, если рядом с бочкой (`3.5м`) нет врагов; в [OrbitalCompanionAbility.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/Abilities/Active/OrbitalCompanionAbility.cs#L162) поддержать `IDamageable`.
  - Добавить `postHitInvulnerabilityDuration = 0.18f` в [HeroHealth.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/Player/HeroHealth.cs).
- [x] **7.4. Раздельные кривые баланса в `WaveConfig` + Инструмент-симулятор:**
  - В `WaveConfig` разделить скейлинг на `GetHpDifficultyMultiplier(int wave)` и `GetDamageDifficultyMultiplier(int wave)` (настраиваемые параметры/кривая, мягкие дефолты на данном этапе — точный тюнинг в Спринте 12 после внедрения всех синергий!).
  - Добавить Editor-инструмент `Assets/_Scripts/Editor/Debug/BalanceCurveSimulator.cs` (окно/логирование расчётных HP врагов и боссов на волнах 1, 5, 10, 25, 50, 75, 100).
- [x] **7.5. Очистка репозитория и техдолга:**
  - Очистить [DefaultVolumeProfile.asset](file:///d:/UnityProjects/ProjectB/Assets/Settings/DefaultVolumeProfile.asset) от 12 лишних URP-оверрайдов (оставить только `Bloom`, `Vignette`, `ColorAdjustments`).
  - Удалить мёртвые `WaveConfig.eliteAuraMaterial`, параметр `auraMaterial` в `EnemyBase.MakeElite()`, `Assets/_Materials/EliteAuraMat.mat`, `ArtSources/Blender/Ranger.blend1`, локальные сборочные файлы (`test.apk` и др., обновить `.gitignore`).
  - Пересохранить 37 ScriptableObjects с legacy `Assembly-CSharp::`; обернуть рантайм `Debug.Log` в `DevLog`.
- [x] **7.6. Тесты Спринта 7:** `Sprint7FixesAndSeparationTests.cs` (separation zero-GC + отсутствие слипания, Intro Wave ~50% спавн нового моба, сохранение монет при AbandonRun, CDR оружия/ульты, Data-Driven `HeroUltimate`, i-frames героя).

---

### Спринт 8: Реактивные Синергии (D:OS2-стиль, авто-активация), Overdrive (Lv 10 / Lv 20 / Lv 30+), Комбо и DPS Breakdown (P0/P1)
- [x] **8.1. Полная переработка Синергий в чистые боевые реакции (Раздел 4.1) с заделом под Дерево Созвездий:**
  - Создать интерфейс `ISynergyUnlockProvider` (`bool IsSynergyUnlocked(string synergyId)`): по умолчанию в Спринте 8 возвращает `true` (чтобы сразу тестировать и играть со всеми 8 реакциями), а в Спринте 11 реализуется через `TalentTreeManager` (синергии открываются узлами в Дереве Созвездий!).
  - В [HeroAbilities.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/Abilities/HeroAbilities.cs) добавить реестр активных синергий (`HasSynergy(string id)`) с **автоматической проверкой при добавлении любой способности**: если у героя собраны все `requiredAbilityIds` из `SynergyData` и синергия открыта провайдером — она **мгновенно активируется сама**, публикуя `SynergyActivatedEvent` (яркая вспышка `"SYNERGY: TOXIC INFERNO!"`), **без траты LevelUp и без накрутки уровней**.
  - Убрать стат-пачки (`abilityBonuses` / `UpgradeAbility` цикл) из [UpgradeApplier.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/LevelUp/UpgradeApplier.cs#L109-L136) и перестать подмешивать `SynergyCardData` в пул карточек [UpgradeManager.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/LevelUp/UpgradeManager.cs#L258-L270).
  - Реализовать все 8 системных реакций:
    1. `toxic_inferno`: взрыв `Fireball` поджигает `PoisonPuddle` (`Ignite()` → мгновенная детонация + пылающее поле `2x` урона).
    2. `steam_blast`: попадание `Fireball` по замедленной цели вызывает Паровую Нову (`4.2m`, `1.8x` урона, отбрасывание, белый пар).
    3. `thunder_storm`: `ChainLightning` по замедленной цели накладывает глубокую заморозку (`0.65s`) + 2 ледяных разряда по соседям.
    4. `overcharged_grid`: пары активных `LaserTurret` (`<= 12м`) соединяются лазерной дугой + снаряды `Shooter` получают пробитие, пролетая рядом с турелью.
    5. `blade_storm`: орбитальные мечи каждые `2.8s` выстреливают призрачными режущими копиями наружу.
    6. `aegis_phalanx`: отталкивание от `GuardianShield` вызывает AoE-вспышку фаланги (`2.4m`).
    7. `dragon_lord`: залпы `MiniDragon` становятся пробивающими (`pierce 3`) и оставляют короткие очаги горения.
    8. `cataclysm_vortex`: `VoxelTornado` в радиусе `BlackHole` (или на финише) выбрасывает 6 гравитационных осколков.
  - Обновить описания в `SynergyData` / коллекции способностей (`AbilitiesScreenUI`) с указанием комбинаций.
- [x] **8.2. Пороги Overdrive для безлимитной прокачки (каждые 10 уровней: Lv 10 / Lv 20 / Lv 30+):**
  - В [ActiveAbility.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/Abilities/ActiveAbility.cs) добавить хук `OnOverdriveTierReached(int tier)` на `Level == 10` (`OVERDRIVE!`), `Level == 20` (`AWAKENED!`) и каждые `+10` далее (`MYTHIC!`) — визуальный рост масштаба/эмиссии + системный бонус + событие `AbilityOverdriveEvent` в `VfxFeedbackManager`.
- [x] **8.3. Комбо-счётчик убийств (`RunComboTracker` + HUD):**
  - Отслеживать серию убийств (окно `3.0s`), вехи `x15`, `x30`, `x50`, `x100` дают временный бонус `+10..+30%` XP/монет и динамичный счётчик на HUD.
- [x] **8.4. `RunDamageTracker` и экран Post-Run DPS Breakdown в `GameOverUI`:**
  - В [RunStatistics.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/Core/RunStatistics.cs) добавить Zero-GC трекинг урона по источникам (`weapon`, `ultimate`, `ability.id`, `synergy.id`, `barrel`).
  - На [GameOverUI.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/UI/GameOverUI.cs) добавить секцию **Damage Breakdown** (топ источников: иконка, имя, полоска `%`, общий урон и DPS) и исправить `FormatReward()` (человекочитаемые названия вместо сырых `rewardId`).
- [x] **8.5. Тесты Спринта 8:** `Sprint8SynergiesAndDpsTests.cs` (отсутствие накрутки уровней, срабатывание `Toxic Inferno`, `Steam Blast`, `Overcharged Grid`, `Thunderstorm`, Overdrive каждые 10 уровней Lv 10 / Lv 20 / Lv 30+, комбо-окно, расчёт DPS Breakdown).

---

### Спринт 9: Визуальный и аудио-прорыв — Шейдер `VoxelLit`, Воксельные модели Спринта 3 и Боевой Juice (P1)
- [x] **9.1. Мобильный шейдер `Assets/_Shaders/VoxelLit.shader`:**
  - Написать URP HLSL-шейдер (`SRP Batcher` + `GPU Instancing` compatible, Forward + ShadowCaster) с:
    - `_HitFlash` (`0..1` per-instance через `MaterialPropertyBlock` — настоящий ослепительно-белый хит-флеш!);
    - `_RimColor` + `_RimPower` (чёткая цветная подсветка силуэта 3 архетипов Элит и Боссов без наземных кругов!);
    - `_HeightAO` (мягкое затемнение у основания модели в стиле MagicaVoxel).
  - Перевести материалы врагов и героев в `Assets/_Materials/Voxels/` на `ProjectB/VoxelLit`, подключить `_HitFlash` в [EnemyBase.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/Enemies/EnemyBase.cs) и [HeroHealth.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/Player/HeroHealth.cs).
- [x] **9.2. Воксельные 3D-модели вместо всех runtime `CreatePrimitive`:**
  - Смоделировать в Blender (`ArtSources/Blender/`) и экспортировать в `Assets/_VoxelModels/` (с `enableInstancing: 1`):
    - `DragonWhelp` (покадровые `.obj` с машущими крыльями для `DragonFollower`);
    - `GuardianShield` (воксельный щит с золотой окантовкой);
    - `PoisonFlask` (колба с неоновым зельем) + воксельные пузыри для `PoisonPuddle`;
    - `VoxelTornado` (цельная воксельная воронка) + воксельный купол Рыцаря `AegisShield` (вместо сферы в `Hero.prefab`);
    - Воксельные рунические кольца для `IceAura` (`y = 0.03`) и `BlackHole` (`y = 0.06`, `shadowCastingMode = Off` — 0 Z-fighting).
- [x] **9.3. HUD Juice, Multi-POI Compass и расширение Audio:**
  - Красная пульсирующая виньетка урона на HUD + микро **Hit-Stop** (`0.03s` через `WaitForSecondsRealtime` с защитой от конфликта с `Paused`/`LevelUp`) на смерти босса и взрыве бочки.
  - Расширить [SecretCompassIndicatorUI.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/UI/SecretCompassIndicatorUI.cs) до **Multi-POI Compass** (до 3 одновременных стрелок без GC: Секрет — фиолетовая, `ChallengeShrine` — алая, `VacuumMagnetPickup` — голубая, `GoldGoblin` — золотая).
  - Добавить 10 новых синтезированных `.wav` SFX в [AudioCatalog.asset](file:///d:/UnityProjects/ProjectB/Assets/Resources/AudioCatalog.asset) (`Explosion`, `LightningZap`, `CritHit`, `SynergyFanfare`, `PoisonSplash`, `ArrowWhoosh`, `IceShatter`, `BossDeath`, `ShrineStart`, `ComboMilestone`) + трек `BossMusic` и приглушение музыки (`-50%`) на паузе/левелапе в [AudioManager.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/Core/AudioManager.cs).
- [x] **9.4. Тесты Спринта 9:** `Sprint9VisualsAndAudioTests.cs` (валидация `VoxelLit` + instancing, Y-офсеты аур, замена примитивов, Multi-POI компас, проверка всех AudioClip).

---

### Спринт 10: События Волн, 4-й Босс (Волна 100), Новые Враги и Живая Арена (P1)
- [x] **10.1. Менеджер Событий Волн (`WaveEventManager` + `WaveEventBannerUI`):**
  - Реализовать 4 события из Раздела 4.3.Б (`Swarm Rush`, `Gold Goblin`, `Blood Moon`, `Elite Ambush`), запускаемые каждые 3–4 обычные волны (не перекрывая Intro/Boss волны), с баннером на HUD и золотым компасом на `GoldGoblin`.
- [x] **10.2. 3 новых архетипа врагов + 4-й Босс Волны 100 (`DemonLordBoss`):**
  - Воксельные `.blend` + `.obj` модели, префабы и `ScriptableObject`:
    - `ShieldedEnemy` (`ShieldBearerData`, W18 с Intro Wave — `-65%` фронтального урона);
    - `SummonerEnemy` (`VoidSummonerData`, W22 с Intro Wave — призыв и хил союзников);
    - `FleeingLootEnemy` (`GoldGoblinData`, для события `Gold Goblin`);
    - `DemonLordBoss : BossBase` (`DemonLordBossData`, **Волна 100** — 2 фазы с Enrage на `<50% HP`).
  - Добавить `DemonLordBossData` в `WaveConfig.bosses`, создать ачивки `DefeatFrostLich.asset` (W75) и `DefeatDemonLord.asset` (W100).
- [x] **10.3. «Живая» арена:**
  - В [ArenaGenerator.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/Arena/ArenaGenerator.cs) включить `Physics.CheckSphere` для препятствий (без наложений), добавить 2–3 новых декоративных пропа (грибы/цветы/руины через `ArenaDecorationRenderer`) и динамический респавн **2–3 новых взрывных бочек** + **Challenge Shrine** каждые 12 волн в радиусе `22–45м` от текущей позиции героя.
- [x] **10.4. Тесты Спринта 10:** `Sprint10EventsAndDemonLordTests.cs` (события волн, блок `ShieldedEnemy`, 2 фазы `DemonLordBoss`, overlap-free препятствия, динамический респавн бочек/алтарей).

---

### Спринт 11: Радиальное Дерево Созвездий, 2 Новых Героя (`Paladin`, `Engineer`) и Расширение Арсенала (P2)
- [x] **11.1. Валюта Boss Shards и Радиальное Дерево Созвездий (`TalentTreeManager` + `ConstellationTreeUI`):**
  - Обновить [SaveData.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/Meta/SaveData.cs) (`saveVersion = 2`: `bossShards`, `unlockedTalentIds`; миграция старых сейвов не нужна — по решению автора совместимость с v1 убрана); выдавать Boss Shards с мини-боссов (`+1`), боссов (`+5`) и Алтарей (`+2`).
  - Реализовать `TalentNodeData` (SO), `TalentTreeManager : IAbilitySlotBonusProvider, ISynergyUnlockProvider` (подключается и к [AbilitySlotService.cs](file:///d:/UnityProjects/ProjectB/Assets/_Scripts/Meta/AbilitySlotService.cs), и к разблокировке всех 12 реактивных синергий в `HeroAbilities`!), узлы статов + **узлы Синергий** + 3 **Keystone-перка** (*Overclocked Arsenal*, *Second Wind*, *Collector's Greed* из Раздела 4.5).
  - Построить **удобный 360° панорамируемый UI экран Созвездий** (свободный 1-пальцевый drag во все стороны с инерцией + pinch-zoom + кнопки `+`/`−`/`⌖ Центр` + нижняя шторка деталей узла при тапе).
- [x] **11.2. 2 новых Героя (`Paladin`, `Engineer`) и Карты Оружия (`WeaponUpgradeCardData`):**
  - Воксельные модели `Paladin`, `Engineer`, `HolyHammer`, `ClusterGrenade`; добавить `HeroUltimateType.Consecration` и `HeroUltimateType.TurretDrop`; зарегистрировать в `HeroCatalog.asset`.
  - Реализовать `WeaponUpgradeCardData` (`WeaponMod_Multishot`, `WeaponMod_FireRate`, `WeaponMod_Power`) с поддержкой мультивыстрела в `WeaponController`.
- [x] **11.3. 4 новых Активных, 2 Пассивных способности и 4 Реактивных Синергии (Разделы 4.1 и 4.4):**
  - `BuzzsawChakramAbility`, `SkyfallSpireAbility`, `GlacialShatterAbility`, `ShadowDecoyAbility`, пассивки `VampiricFang` и `AreaTome` (`GlobalAreaMultiplier`), плюс 4 чистых реактивных синергии (`Shatter Nova`, `Ricochet Barrage`, `Solar Eclipse`, `Phantom Legion`).
- [x] **11.4. Тесты Спринта 11:** `Sprint11ConstellationAndExpansionTests.cs` (SaveData v2: Boss Shards и узлы, `TalentTreeManager` + `IAbilitySlotBonusProvider`, Keystone `Second Wind`, панорамирование/клампинг координат, новые герои, скиллы и `GlobalAreaMultiplier`).

---

### Спринт 12: Финальный Баланс-Пасс, Onboarding (FTUE), Локализация (EN/RU), Стресс-тест v2 и Документация (P2/P3)
- [ ] **12.1. Осознанный Баланс-Пасс (по данным `BalanceCurveSimulator` и `DPS Breakdown`):**
  - Настроить раздельные HP/Damage кривые в `WaveConfig`, XP-кривую и экономику монет/Boss Shards под целевую формулу: первые забеги `2–5 мин` (W5–W12), долгий комфортный мета-прогресс на десятки часов без `maxLevel`, ощутимое напряжение орды без несправедливых ваншотов.
- [ ] **12.2. Лёгкий Onboarding (FTUE) и Локализация (EN + RU):**
  - Подсказка в первые 3 сек первого забега (`totalRuns == 0`) + подсветка первой готовой ульты; проверка `SafeAreaFitter` на `9:16`, `9:19.5`, `9:21`.
  - Лёгкий `LocalizationService` (словари EN/RU, переключатель языка в `SettingsPopupUI`).
- [ ] **12.3. Финальный PlayMode стресс-тест v2 и синхронизация документации:**
  - Обновить стресс-тест (`ReleasePlayModeStressTests.cs`): Волна 50+, 120 врагов + `EnemySeparationSystem` + `VoxelLit` + 6 скиллов + реактивные синергии + Overdrive + `WaveEvent` + `DemonLordBoss` — цель **< 2.5 мс/кадр (CPU)** и **0 байт GC**.
  - Синхронизировать [CONTEXT.md](file:///d:/UnityProjects/ProjectB/CONTEXT.md) и [IMPLEMENTATION_PLAN.md](file:///d:/UnityProjects/ProjectB/IMPLEMENTATION_PLAN.md) с реальным составом проекта.

---

## 6. Отложенный бэклог (Арт-полиш, Pre-Release и Post-Launch)

### 6.1. Опциональный Арт-полиш UI (когда геймплей и мета готовы)
- [ ] Унифицировать ВСЕ UI-иконки (включая замену AI-иконок нижнего меню `Play.png`, `Abilities.png`, `Upgrade.png` и `16×16` пиксель-арт иконок карточек) через единый headless Blender-скрипт 3D-рендера (`ArtSources/Blender/render_voxel_icons.py`) в `256×256` PNG под одним светом и углом.

### 6.2. Pre-Release Чеклист (Публикация в Google Play)
- [ ] Импортировать Google Mobile Ads Unity Plugin (`GOOGLE_MOBILE_ADS`), заполнить боевые Ad Unit IDs в [AdConfig.asset](file:///d:/UnityProjects/ProjectB/Assets/Resources/AdConfig.asset).
- [ ] Подключить Unity IAP (`Remove Ads` \$2.99, `Starter Pack` \$0.99) и GameAnalytics.
- [ ] Профилировать реальный `.apk`/`.aab` на физическом Android-устройстве (стабильные 60 FPS на Волне 50+).
- [ ] Store-ассеты: адаптивная иконка `512×512`, Feature Graphic `1024×500`, 5–6 вертикальных скриншотов + 20-сек геймплей-трейлер (также для TikTok/Shorts).
- [ ] Keystore, подписанный `.aab` (API 34+, IL2CPP ARM64), Privacy Policy / Data Safety / Content Rating → Internal / Open Testing.

### 6.3. Post-Launch идеи
- **Система Биомов (`Forest` → `Scorched Desert` → `Frozen Caverns` → `Volcanic Core`):** *(отложено по решению пользователя)*.
- **1-тап TikTok/UGC карточка:** красивый экспорт экрана Game Over с DPS Breakdown и собранными синергиями.
- **Скины героев** и **Ежедневный Челлендж-Забег** (общий `ArenaConfig.Seed` на день + модификаторы правил).
