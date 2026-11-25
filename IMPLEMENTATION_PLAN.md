# План реализации: ProjectB

> **Единый роадмап проекта.** Ссылки: GDD — [CONTEXT.md](./CONTEXT.md), детальный технический аудит (v3) и ТЗ спринтов 12, 14–18 — [AUDIT.md](./AUDIT.md).
> **Статус:** фазы 0–4 (v1 MVP), спринты 1–11 (v2 Expansion), 13 (UI-редизайн), 14.1 (адаптивный фундамент) и 15.1–15.5 (стабилизация и техдолг) завершены на коммите `66073eb`. Актуальный порядок релизов (Web → Google Play) и открытые спринты 12, 14–18 см. ниже и в [AUDIT.md §5.2](./AUDIT.md).
>
> Легенда: `[x]` — сделано, `[ ]` — открыто, `[~]` — частично.

---

## Фаза 0: Настройка проекта
- [x] Создать Unity 6 проект с URP (Unity `6000.5.5f1`, URP `17.5.0`)
- [x] Настроить адаптивную ориентацию: базовый портрет (`9:16`..`9:21`) + ландшафт для Web/планшетов через `OrientationPolicy` (обновлено в 14.1 / Решении №8)
- [x] Настроить целевую платформу: Android, API 34+
- [x] Настроить Quality Settings под мобилку (отключить лишнее, 60 FPS target)
- [x] Установить New Input System (`1.19.0`)
- [x] Установить и настроить VContainer (`1.19.0`, `RootLifetimeScope` → `MainMenuLifetimeScope` / `GameLifetimeScope`)
- [x] Разбить код на asmdef-сборки: `ProjectB.Data`, `ProjectB.Meta`, `ProjectB.Runtime`, `ProjectB.Editor` + `ProjectB.Tests.EditMode` / `ProjectB.Tests.PlayMode`
- [x] Создать структуру папок:
  ```
  ArtSources/Blender/      (.blend-исходники воксельных моделей, вне Assets)
  Assets/
  ├── _Scripts/
  │   ├── Abilities/       (Active/, Data/ — активные/пассивные способности, синергии)
  │   ├── Ads/             (AdManager, Mock/AdMob провайдеры)
  │   ├── Arena/           (генерация арены, бочки, алтари, секреты, магнит)
  │   ├── Combat/          (оружие, снаряды, урон)
  │   ├── Core/            (GameManager, DI Scopes, RunStatistics, Events/)
  │   ├── Data/            (Combat/, Enemies/, Meta/ — ScriptableObject-конфиги)
  │   ├── Editor/          (Debug/, Generators/, SetupTools/ — инструменты и Sprint*SetupTool)
  │   ├── Enemies/         (враги, элиты, боссы, WaveManager, события волн)
  │   ├── LevelUp/         (XP, карточки, UpgradeManager)
  │   ├── Meta/            (сохранения, мета-апгрейды, ачивки, слоты, Созвездия, дейли)
  │   ├── Player/          (движение, здоровье, ульта, HeroRuntimeStats)
  │   ├── UI/              (HUD, меню, экраны)
  │   └── Visuals/         (VFX, числа урона, VoxelMeshAnimator)
  ├── _Prefabs/            (Abilities, Arena, Core, Decorations, Enemies, Heroes, Obstacles, Pickups, Projectiles, Secrets, UI)
  ├── _ScriptableObjects/  (Abilities/, Bosses/, Cards/, Enemies/, Heroes/, Synergies/, Talents/, Weapons/ + конфиги)
  ├── _Scenes/             (MainMenu, Gameplay)
  ├── _Materials/
  ├── _Shaders/            (VoxelLit.shader)
  ├── _VoxelModels/        (.obj модели по папкам)
  ├── _Audio/              (Music, SFX)
  ├── _UI/
  ├── _Tests/              (EditMode, PlayMode)
  ├── Resources/           (Achievements/, MetaUpgrades/, CardCatalog, ComboConfig, HeroCatalog, PickupConfig, RunRulesConfig, SecretConfig, SynergyCatalog, TalentTree, AudioCatalog, AdConfig, DailyBonusRewardData, VContainerSettings)
  └── Settings/            (URP, Input System, ProjectB_PostFX Volume Profile)
  ```
- [x] Создать сцены: `MainMenu` (меню + все мета-экраны), `Gameplay` *(отдельная `MetaScreen` не понадобилась — мета-экраны живут в `MainMenu`)*
- [x] Настроить Git (`.gitignore` для Unity, исключены локальные сборки)

---

## Фаза 1: Ядро геймплея (v1 MVP)

### 1.1 Камера
- [x] Создать камеру top-down 45° (portrait, 9:16)
- [x] Камера следует за героем (SmoothDamp)
- [x] Проверить, что камера корректно работает на разных aspect ratio (9:16, 9:19.5, 9:20)

### 1.2 Герой — движение
- [x] Создать префаб героя (визуал — дочерний `Visuals` с воксельной моделью)
- [x] Виртуальный джойстик по центру снизу
- [x] Движение героя по XZ плоскости через Rigidbody
- [x] Скорость движения вынести в ScriptableObject `HeroData`
- [x] Анимация поворота в сторону движения (lerp)

### 1.3 Герой — автоатака
- [x] Создать ScriptableObject `WeaponData` (урон, скорость атаки, дальность, тип снаряда, AoE, пробитие)
- [x] Герой автоматически атакует ближайшего врага
- [x] Поиск цели: Physics.OverlapSphere → ближайший враг (NonAlloc)
- [x] Базовый снаряд + Object pooling (`ObjectPool<T>`)
- [x] Снаряд летит к цели → наносит урон → возвращается в пул
- [x] Визуальная обратная связь: белый хит-флеш через шейдер `VoxelLit`

### 1.4 Враги — базовые
- [x] Создать ScriptableObject `EnemyData` (HP, скорость, урон при касании, XP-дроп, модель)
- [x] AI: steering к герою + Zero-GC separation толпы (`EnemySeparationSystem`)
- [x] При касании героя → урон герою (кулдаун на урон)
- [x] Система здоровья врага (`IDamageable` интерфейс)
- [x] Смерть врага: дроп XP-кристалла + монет → возврат в пул
- [x] Object pooling для врагов

### 1.5 Спавнер волн
- [x] Создать `WaveManager`
- [x] Спавн врагов за пределами камеры (рандомная точка на кольце вокруг героя)
- [x] Нарастающая сложность: больше врагов + раздельный скейлинг HP и урона
- [x] Конфигурация волн через ScriptableObject `WaveConfig` (фазовое расписание врагов `enemySchedule`)
- [x] Индикатор текущей волны на HUD
- [x] Пауза между волнами (2 сек), таймаут волны 30 сек

### 1.6 XP и Levelup
- [x] XP-кристалл (воксельная модель, object pooling)
- [x] Магнит: кристаллы притягиваются к герою при приближении (настраиваемый радиус)
- [x] XP-бар на HUD (верх экрана)
- [x] При заполнении XP-бара → levelup → пауза геймплея
- [x] Формула XP: `baseXP * (level ^ 1.12)` (`baseXP = 9`, степень настраивается в `HeroExperience`)

### 1.7 Система карточек (2+1)
- [x] Создать ScriptableObject `CardData` (иконка, название, описание, тип, эффект, редкость)
- [x] Пул всех возможных карточек с весами редкости `Common 50 / Rare 30 / Epic 15 / Legendary 5`
- [x] При levelup: 2 случайные карточки из пула (без дубликатов)
- [x] UI панель выбора: 2 карточки + 3-я за рекламу
- [x] При нажатии на карточку → применить эффект → закрыть панель → продолжить геймплей
- [x] Типы эффектов карточек:
  - [x] Добавить новую способность (`AbilityCardData`, 27 шт.)
  - [x] Улучшить параметр способности (`AbilityModifierCardData`, 63 шт.)
  - [x] Глобальный бафф героя (`StatUpgradeCardData`, 3 шт.)
  - [x] Улучшение оружия (`WeaponUpgradeCardData`, 3 шт.)
- [x] Фильтрация: модификаторы только для полученных способностей, новые способности — только при свободном слоте
- [x] Реролл: 1 бесплатный за забег (+ талант), далее — за рекламу

### 1.8 Процедурная арена
- [x] Квадратная арена `500×500` (`ArenaConfig`)
- [x] Процедурный пол + GPU-instanced трава (50 000 травинок, chunk culling)
- [x] Стены/границы арены
- [x] Процедурная расстановка 180–190 препятствий без наложений (`Physics.CheckSphere`) + 1 400–1 800 декоративных пропов
- [x] Препятствия имеют коллайдеры (герой и враги не проходят)
- [x] Seed-based генерация (одинаковый seed → одинаковая арена)

### 1.9 Здоровье героя и смерть
- [x] HP-бар героя на HUD
- [x] Пост-хит i-frames (`0.18s`), вспышка модели, красная виньетка урона
- [x] При HP ≤ 0 → экран «Game Over»
- [x] На экране Game Over: монеты, волна, кнопки «Заново» / «В меню»
- [x] Воскрешение за рекламу (1 раз за забег)

### 1.10 Секретные предметы на арене
- [x] Создать `InteractableItem` (коллайдер-триггер + логика подбора)
- [x] Спавнер предметов (макс 1 за забег, вдалеке от центра — `SecretItemSpawner`)
- [x] Сундук (много монет сразу), древний свиток (мгновенный levelup), загадочный кристалл (случайная редкая+ способность)
- [x] Обновление статистики для ачивок при подборе (`FindSecretItem`)
- [x] Визуальные эффекты (световой столб-маяк, вращение/покачивание, партиклы при подборе)
- [x] **Сделать секрет редким и убрать его из компаса** — выполнено в 15.3 (`SecretConfig`, компас без секрета, цели ачивок `2 / 1 / 1`)

---

## Фаза 2: Способности и боссы (v1 MVP)

### 2.1 Система способностей — архитектура
- [x] Базовый класс `AbilityBase` (MonoBehaviour), разделение на `ActiveAbility` и `PassiveAbility`
- [x] ScriptableObject `AbilityData` (имя, описание, тип, префаб, статы, редкость, `unlockedByDefault`)
- [x] `HeroAbilities` на герое — списки активных и пассивных способностей, прокачка без `maxLevel`
- [x] Object pooling для снарядов способностей
- [x] Система модификаторов `ApplyModifier(ModifierType, value)`
- [x] Слоты способностей `2+2 → 6+6` (`AbilitySlotService` + `IAbilitySlotBonusProvider`; база снижена с 3+3 в 12.7)

### 2.2 Типы способностей
- [x] **Орбитальные** (Orbital Sword, Guardian Shields)
- [x] **Стрелковые** (Shooter, Fireball, Boomerang, Chain Lightning, Buzzsaw Chakram)
- [x] **Ауры** (Ice Aura, Black Hole)
- [x] **Миньоны** (Dragon Whelp)
- [x] **Стационарные / зоны** (Laser Turret, Poison Flask, Arcane Tornado, Shadow Decoy, Skyfall Spire, Seismic Hammer, Glacial Shatter)

### 2.3 Способности (27 = 17 активных + 10 пассивных)
- [x] **Common:** Orbital Sword, Shooter, Guardian Shields; пассивки Battle Fury, Iron Skin, Titan Heart
- [x] **Rare:** Fireball, Ice Aura, Boomerang, Chain Lightning, Poison Flask, Buzzsaw Chakram, Glacial Shatter; пассивки Swift Boots, Area Tome, Chrono Tome, Vitality Regen, Golden Idol
- [x] **Epic:** Laser Turret, Dragon Whelp, Arcane Tornado, Skyfall Spire, Shadow Decoy; пассивки Sniper Lens, Vampiric Fang
- [x] **Legendary:** Black Hole, Seismic Hammer

### 2.4 Карточки и модификаторы
- [x] `AbilityCardData` — выдача новой способности
- [x] `AbilityModifierCardData` — точечная прокачка (урон, количество, радиус, кулдаун и т.д.)
- [x] `StatUpgradeCardData` — глобальные статы (Global Damage, Max HP, Magnet)
- [x] `WeaponUpgradeCardData` — Multishot, Fire Rate, Power
- [x] Динамический пул в `UpgradeManager` (одноразовые карты удаляются после взятия)

### 2.5 Синергии способностей
- [x] `SynergyData` (пара требуемых способностей) + `SynergyCatalog`
- [x] ~~Карточки синергий в пуле LevelUp~~ → заменено реактивными синергиями с авто-активацией (Спринт 8)
- [x] 12 реактивных синергий, открываются узлами Дерева Созвездий (Спринты 8 и 11)

### 2.6 Боссы
- [x] Базовый класс `BossBase : EnemyBase` + `BossData`
- [x] Мини-босс каждые 5 волн: `×5 HP`, `×2` урона, `×1.85` размер, дроп кристаллов, монет, Осколка и шанса Vacuum Magnet
- [x] Боссы каждые 25 волн с уникальными паттернами:
  - [x] W25 — Stone Golem (`GolemBoss`): телеграфируемый AoE-удар, призыв слизней
  - [x] W50 — Necromancer (`NecromancerBoss`): телепорт, веер снарядов, призыв скелетов
  - [x] W75 — Frost Lich (`FrostLichBoss`): ледяные шипы, Blizzard с безопасной зоной
  - [x] W100 — Demon Lord (`DemonLordBoss`): 2 фазы, Enrage при `<50% HP`
- [x] HP-бар босса (`BossHealthBarUI`)
- [x] Босс-фаза: обычные враги не спавнятся, нет пропуска волны по таймауту

### 2.7 Элитные враги
- [x] Элиты с волны 10, шанс `+5%` за волну (максимум `25%`)
- [x] 3 архетипа: Berserker, Juggernaut, Haste Aura
- [x] Выделение rim-подсветкой шейдера `VoxelLit` (без наземных колец)

---

## Фаза 3: Мета-прогрессия и UI (v1 MVP)

### 3.1 Система сохранений
- [x] `SaveData` (монеты, Осколки Боссов, уровни апгрейдов, таланты, прогресс ачивок, разблокировки, статистика), `saveVersion = 2`
- [x] Сериализация: JSON → PlayerPrefs, пакетное сохранение (`IsDirty`)
- [x] Автосохранение после каждого забега, при выходе в меню из паузы (`AbandonRunAndGoToMenu`) и при выходе из игры
- [x] Загрузка при старте приложения

### 3.2 Перманентная мета-прокачка (за монеты)
- [x] ScriptableObject `MetaUpgradeData` (имя, описание, базовая стоимость, множитель, эффект за уровень, макс уровень)
- [x] 8 апгрейдов: Hero HP, Hero Damage, Movement Speed, Magnet Radius, Starting Level, XP Bonus, Ability Damage, Crit Damage
- [x] Стоимость: `baseCost * (multiplier ^ currentLevel)`
- [x] `MetaUpgradeManager` применяет модификаторы к герою при старте забега
- [x] UI экран прокачки

### 3.3 Система ачивок
- [x] ScriptableObject `AchievementData` + `AchievementManager`
- [x] Условия: `ReachWave`, `TotalKills`, `DefeatBoss`, `MaxAbilityLevel`, `CollectAbilitiesInRun`, `NoDamageWaves`, `FindSecretItem`, `ClearShrines`, `ReachCombo`
- [x] Награды: `UnlockAbility`, `UnlockHero`, `GiveCoins`, `UnlockActiveSlot`, `UnlockPassiveSlot`
- [x] Персистентный прогресс в `SaveData`
- [x] Нотификация (`AchievementPopupUI` с очередью) + список на Game Over с человекочитаемыми наградами
- [x] 27 ачивок

### 3.4 Разблокировка контента
- [x] Заблокированные способности (`unlockedByDefault = false`): Laser Turret, Black Hole, Vitality Regen, Dragon Whelp, Arcane Tornado, Seismic Hammer, Sniper Lens, Golden Idol, Vampiric Fang, Skyfall Spire, Shadow Decoy
- [x] Заблокированные герои: Mage, Ranger, Necromancer *(Paladin и Engineer удалены после 12.7)*
- [x] Заблокированное не попадает в пул карточек / выбор героя
- [x] Экран коллекции способностей (`AbilitiesScreenUI`) со статусом и нужной ачивкой

### 3.5 Главное меню
- [x] Нижнее меню с вкладками: «Играть», «Прокачка» (+ вход в Созвездия), «Способности», «Ачивки», «Бонус»; попап «Настройки»
- [x] Отображение монет
- [x] Выбор героя с 3D-превью (`HeroSelectionUI`)
- [x] Бейдж новых ачивок, индикатор доступного ежедневного бонуса

### 3.6 UI экрана ачивок
- [x] Список со scroll, иконка, название, описание, прогресс-бар, награда
- [x] Статусы: locked / in progress / completed
- [x] При клике на завершённую — попап с разблокированным (`AchievementRewardPopupUI`)

### 3.7 HUD (в забеге)
- [x] HP-бар, XP-бар, уровень, номер волны, монеты
- [x] Иконки активных и пассивных способностей с уровнем (`AbilityHudUI`)
- [x] Джойстик, кнопка ульты (`UltimateButtonUI`), пауза (`PauseMenuUI`)
- [x] Комбо-счётчик (`ComboHudUI`), баннер событий волн (`WaveEventBannerUI`), виньетка урона (`DamageVignetteUI`)
- [x] Multi-POI компас (`SecretCompassIndicatorUI`)
- [ ] DPS-метр в забеге (опционально; пост-забеговый Damage Breakdown уже есть)

### 3.8 Экран Game Over
- [x] Статистика забега: волна, убийства, время, монеты, Осколки, лучшее комбо
- [x] Damage Breakdown (до 12 источников, прокрутка)
- [x] Кнопки «x2 монеты (реклама)», «Воскреснуть (реклама)», «В меню», «Заново»
- [x] Ачивки, выполненные за забег

### 3.9 Экран выбора карточки (levelup)
- [x] Панель поверх геймплея (геймплей на паузе)
- [x] 2 бесплатные карточки + 3-я за рекламу, кнопка реролла
- [x] Иконка, название, описание, цвет рамки по редкости, метка рекламы
- [x] Анимация появления (scale bounce)

### 3.10 Экран ежедневного бонуса
- [x] 7 ячеек, трекинг дней через `DateTime`, защита от перевода часов
- [x] Новый цикл после 7-го дня с наградами `×1.2` (до 5 циклов)
- [x] Награды-монеты `100…700` (тип «способность» поддержан, но не используется в конфиге)
- [x] Кнопка «Удвоить за рекламу»
- [x] Подсвеченный текущий день, галочки на собранных, затемнённые будущие

---

## Фаза 4: Реклама, полировка и оптимизация (v1 MVP)

### 4.1 Реклама (`AdManager`)
- [x] `AdManager` с `IAdProvider`, `MockAdProvider` и адаптером `AdMobAdProvider` под `#if GOOGLE_MOBILE_ADS`
- [x] Rewarded: 3-я карточка, реролл, x2 монеты, воскрешение, дейли x2
- [x] Interstitial между забегами (реализовано, отключено через `runsPerInterstitial = 0`)
- [x] Если реклама не загрузилась → кнопка неактивна
- [x] Лимит: `10` за сессию, `20` в день
- [ ] Боевой SDK и Ad Unit IDs — см. Фазу 8 (Pre-Release)

### 4.2 Баланс (первичный)
- [x] Кривая сложности первых 50 волн, XP-формула, стоимость мета-апгрейдов, длительность забега
- [x] Editor-симулятор `BalanceCurveSimulator` (HP/урон врагов и боссов на волнах 1–100)
- [ ] Финальный баланс-пасс — см. Спринт 12

### 4.3 Полировка геймплея
- [x] Плавная камера, screen shake, hit-stop (`0.03s`) на смерти босса и взрыве бочки
- [x] Числа урона (pooled `TextMeshProUGUI`, под модальным UI, скрываются на паузе / levelup / Game Over)
- [x] Particle effects: смерть врага, подбор XP/монет, levelup, ульта, секрет, синергии, Overdrive
- [x] Синтезированные `.wav` SFX через `AudioManager` + `AudioCatalog` (попадания, смерти, взрывы, молния, крит, синергии, алтарь, комбо, UI)
- [x] Музыка: меню, геймплей, босс; приглушение `-50%` на паузе / levelup

### 4.4 Воксельные ассеты
- [x] Герои: Knight, Mage, Ranger, Necromancer
- [x] Враги: Slime, Goblin, Skeleton, Bat, Bomb Slime, Orc Charger, Shield Orc, Void Acolyte, Gold Goblin
- [x] Боссы: Golem, Necromancer, Frost Lich, Demon Lord
- [x] Способности и снаряды: Orbital Sword, Boomerang, Fireball, Shooter, Turret, Guardian Shield, Poison Flask/Puddle, Dragon Whelp, Voxel Tornado, Buzzsaw Chakram, Skyfall Spire, Shadow Decoy, Seismic / Ice Aura / Black Hole кольца, Aegis Shield, Ranger Arrow, Soul Bolt
- [x] Пикапы и секреты: XP-кристалл, монета, Vacuum Magnet, сундук, свиток, кристалл
- [x] Объекты арены: дуб, сосна, пень, бревно, валуны, руины, кусты, грибы, цветы, трава, бочка, алтарь
- [x] Шейдер `ProjectB/VoxelLit` (`_HitFlash`, `_RimColor`, `_HeightAO`)

### 4.5 Оптимизация
- [x] Object pooling для всех часто создаваемых объектов
- [x] Zero-GC боевой цикл (PlayMode стресс-тест на 120 врагах)
- [x] Стабильные 60 FPS при 100+ врагах (в редакторе)
- [x] GPU Instancing для врагов и декораций, SRP Batcher-совместимый шейдер
- [ ] Culling: не обновлять врагов за пределами расширенной зоны камеры
- [ ] Профилирование на реальном Android-устройстве — см. Фазу 8

---

## Фаза 5: v2 Expansion — Спринты 1–11 ✅

Детальные ТЗ спринтов 7–11 — в [AUDIT.md](./AUDIT.md), раздел 5.

- [x] **Спринт 1** — Goblin Shooter, Mage + Meteor Shower, VFX-фидбек, воксельные иконки (`492bad3`)
- [x] **Спринт 2** — 11 модификаторов способностей, UX карточек, реролл, меню паузы (`c70e077`)
- [x] **Спринт 3** — 6 активных + 4 пассивных способности, crit/CDR, расширяемые слоты `3+3 → 6+6`, Debug Cheat Menu (`8e65ef3`, `7e51155`, `fe1bb56`)
- [x] **Спринт 4** — фазовое расписание волн, Bomb Slime / Orc Charger, 3 архетипа элит, Frost Lich (`736a1ab`, `5a3b682`)
- [x] **Спринт 5** — Ranger и Necromancer, взрывные бочки, Vacuum Magnet, Challenge Shrine (`4bb0858`, `d1070a7`)
- [x] **Спринт 6** — Zero-GC боевой цикл и стресс-тесты (`9e2c3a3`)
- [x] **Спринт 7** — separation врагов, волны знакомства, безопасный выход из паузы, Data-Driven ульты, CDR, i-frames, раздельные кривые баланса, чистка техдолга (`2ff7a28`, `4e6f474`, `b08aa7e`)
- [x] **Спринт 8** — 8 реактивных синергий, Overdrive Lv 10/20/30+, комбо, Damage Breakdown (`35afd58`)
- [x] **Спринт 9** — шейдер `VoxelLit`, воксельные модели вместо примитивов, hit-stop, виньетка, Multi-POI компас, 10 новых SFX + музыка босса (`7a52678`)
- [x] **Спринт 10** — события волн, Demon Lord (W100), Shield Bearer / Void Summoner / Gold Goblin, живая арена (`fca8971`)
- [x] **Спринт 11** — Дерево Созвездий (34 узла, 3 Keystone) + Осколки Боссов, Paladin и Engineer, карточки оружия, 4 способности + 2 пассивки + 4 синергии (`833b422`)

---

## Фаза 6: Спринты 12–13 — Баланс, FTUE, локализация, UI-редизайн, стресс-тест v2

### Спринт 12 — Экономика и баланс, FTUE, локализация EN/UK/RU, стресс-тест v2

- [x] **12.1. Экономика и баланс по модели (B1, B2, B3):** расширить `BalanceCurveSimulator` (экономика дохода vs цены меты, DPS-модель, частота LevelUp); перенастроить `MetaUpgradeData`, дропы `EnemyData`, `WaveConfig` и XP-кривую; зафиксировать таблицы «до/после» в `Docs/Balance/`.
- [x] **12.2. Онбординг (FTUE):** первый забег (`totalRuns == 0`) — подсказка движения, подсветка первого LevelUp и первой готовой ульты (через минимальный `LocalizationService`), облегчённые волны 1–2 (`WaveConfig.ftueWaves`), бейдж-подсказка в меню после 1-й смерти (по минимальной цене из `MetaUpgradeData`).
- [x] **12.3. Локализация EN + UK + RU:** `LocalizationService` (`Resources/Localization/{en,uk,ru}.json`, Zero-GC кэш), `LocalizedText` на статичные тексты + локализационные ключи для SO, проверка кириллицы/украинских букв `Ґґ Єє Іі Її` в атласах, переключатель языка в `SettingsPopupUI`.
- [ ] **12.4. Стресс-тест v2** (`ReleasePlayModeStressTests`, на устройстве после Спринта 17): W50+, `maxActiveEnemies` врагов + separation + `VoxelLit` + 6 способностей с синергиями и Overdrive + событие + Demon Lord — `< 2.5 мс` боевой логики, `0 B` GC, p95 `< 16.6 мс`, отчёт в `Docs/Perf/`.
- [x] **12.5. Синхронизация документации:** `CONTEXT.md` и `IMPLEMENTATION_PLAN.md` приведены к реальному составу проекта (обновлено и в 15.5).
- [x] **12.6. Автоматизация баланса (RunTelemetry + BalanceBot):** *(только `UNITY_EDITOR || DEVELOPMENT_BUILD`)*
  - `RunTelemetry` пишет `waves.csv` / `runs.csv` / `cards.csv` (длительность волн, урон по герою с источником, DPS по источникам, LevelUp, выбор карт, монеты/Осколки); ручной режим — кнопка в Debug Cheat Menu → `persistentDataPath/BalanceLogs/`.
  - `BalanceBot`: кайтинг с уклонением от снарядов, телеграфов и зон, авто-ульта, стратегии карт `Random` / `GreedyDamage` / `FixedBuild`, мета-профили `Fresh` / `Mid` / `Full`, изоляция `SaveData` (in-memory хранилище + бэкап).
  - Пакетный запуск: окно `ProjectB/Debug/Balance Bot` или `[Explicit]` PlayMode-тест `BalanceBot` (env `BALANCE_BOT_*`) → `BalanceReports/<дата>/summary.md`.
  - Ускорение — фиксированный шаг `Time.captureDeltaTime = 1/60` (симуляция как на 60 FPS, кадры — максимально быстро). Legacy `timeScale 6×` занижал силу бота (медиана W13 против W16 на 1×).
- [~] **12.7. Анти-кайт ребаланс** *(по данным BalanceBot: бег по кругу почти не наказывался, на W25 бои с Golem длились до 21 мин, к W16 у игрока 5 активных слотов)*:
  - `WaveConfig`: спавн по ходу движения героя (`forwardSpawnChance 0.45`, конус ±60°) и перенос врагов, оставшихся за экраном позади, вперёд (`relocateTrailingEnemies`).
  - Стрелки: дальность `8 → 11`, снаряд `6 → 9`, упреждение `WeaponData.leadFactor = 0.6`, дистанция стрельбы Goblin `5.5 → 8.5`, Bat `5 → 8`; перезарядка `2 → 3 с` (после прогона Goblin и Bat убивали 12 из 36 ботов).
  - Боссы: ярость по таймеру (`BossData.enrageAfterSeconds 75`, рост каждые 30 с), Golem — прыжок к герою (`leap*`) и зоны обломков с замедлением (`rubble*`). Ограничение арены — не делаем (решение автора).
  - Слоты `2+2 → 6+6`: ранние ачивки W5 / 250 убийств; далее Golem / W25, ключевые узлы Созвездий, W50 / W35.
  - Бот идёт на босса через 20 с боя; застрявшие забеги исключены из медиан `summary.md`.
  - Прогон «до» — `BalanceReports/baseline_before_rebalance/`. Прогон «после» (Fresh, 36 забегов): застрявших забегов `5 → 0`, самый долгий бой с Golem `21 → 7.5 мин`, медиана W17 → W16, нижняя четверть W16 → W12, Golem с Fresh-профилем не убит ни разу (0 / 3).
  - Осталось: прогон Fresh + Mid с новой перезарядкой стрелков — проверить ранние смерти и проходимость Golem с метой.

### Спринт 13 — UI/UX редизайн (параллельно со Спринтом 12; ТЗ и аудит экранов — [AUDIT.md](./AUDIT.md), разделы 3.8 и «Спринт 13»)

> 13.1 выполнить **до** 12.2–12.3: FTUE и локализация опираются на новую дизайн-систему и шрифт с кириллицей.

- [x] **13.1. Дизайн-система:**
  - `UiTheme` (SO: палитра, цвета редкостей, типографика, отступы, длительности);
  - шрифты с кириллицей вместо `LiberationSans`;
  - 9-slice UI-кит в `SpriteAtlas`, иконки валют как `TMP Sprite Asset`;
  - компоненты `ThemedButton` / `CurrencyLabel` / `BadgeUI` / `ProgressBarUI` / `UiTween` / `ScreenTransition`;
  - editor-инструмент `UiThemeApplier`.
- [x] **13.2. Главное меню:** *(портрет; в ландшафте пока только экран Play — остальное в 14.4)*
  - непрозрачные экраны и выключение 3D-превью вне вкладки Play;
  - шапка с монетами и Осколками;
  - редизайн Play, нижних вкладок, Upgrades (+ баннер Созвездий), Abilities (фильтры, чипы синергий), Achievements, Daily Bonus, Constellation, Settings.
- [x] **13.3. HUD:**
  - портрет + толстый HP-бар с ghost-trail, XP-бар;
  - показ только открытых слотов, рамки редкости и Overdrive;
  - компас вне зоны HUD, иконка ульты с радиальным кулдауном;
  - скрытие HUD под модальными окнами.
- [x] **13.4. Модальные окна:** *(LevelUp — в обеих ориентациях; Pause / Game Over в ландшафте — 14.4)*
  - LevelUp — вертикальный стек из 3 карточек с рамками редкости;
  - Pause — сводка забега и текущий билд;
  - Game Over — `NEW BEST!`, count-up наград, авто-высота breakdown, перестроенные кнопки;
  - попапы и тосты.
- [x] **13.5. Game Feel UI:** *(без опциональной вибрации)* клик-SFX, переходы, count-up, пульс бейджей.
- [x] **13.6. Адаптивность и производительность:** `9:16` / `9:19.5` / `9:21` + ландшафт (`16:9 / 16:10 / 21:9 / 4:3`), `SafeAreaFitter` везде, единый `SafeAreaHUD` канвас (`9` HUD draw calls ≤ 10 в `1920x1080`, 0 B GC).
- [x] **13.7. Тесты `Sprint13UiTests`** + скриншоты «до / после» всех экранов в `Screenshots/Sprint13/`.

---

## Фаза 7: Открытые задачи и техдолг (закрыто в 15.3–15.4)

- [x] **Секретные предметы — редкий шанс и без компаса (15.3):** `SecretConfig` (`10%` + `+20%` Созвездия), секрет убран из `SecretCompassIndicatorUI`, цели ачивок пересчитаны (`2 / 1 / 1`).
- [x] **Удалить легаси-синергии в карточках (15.4):** удалены 8 ассетов `SynergyCardData` и обходы `is SynergyCardData`.
- [x] **Data-Driven комбо и правила забега (15.4):** `ComboConfig`, `RunRulesConfig`, `PickupConfig`, `CardCatalog` в `Resources/`, инъекция через `GameLifetimeScope`.
- [ ] **Culling врагов** за пределами расширенной зоны камеры (поднять при нехватке запаса на 12.4).
- [ ] **DPS-метр в HUD** (опционально).

---

## Фаза 8: Спринты 14–18 (Стабилизация, Web-релиз и Google Play)

> Полные ТЗ и критерии приёмки каждого подпункта — в [AUDIT.md](./AUDIT.md), раздел 6.

### Спринт 15 — Стабилизация, жизненный цикл и техдолг (P0/P1)
- [x] **15.1.** Авто-пауза при сворачивании (`OnApplicationPause`/`OnApplicationFocus`) + чекпоинт забега и восстановление при `runInProgress`.
- [x] **15.2.** Глобальный лимит врагов (`WaveConfig.maxActiveEnemies = 300`, долг волны) + безопасный фолбэк кольца спавна + слияние пикапов при `> 300` (`PickupConfig`).
- [x] **15.3.** `SecretConfig`, исключение секрета из компаса, перебаланс ачивок (`2 / 1 / 1`).
- [x] **15.4.** Data-Driven конфиги через VContainer DI (`CardCatalog`, `RunRulesConfig`, `ComboConfig`, `PickupConfig`, `SecretConfig`), scoped `EnemyRuntimeModifiers`, чистка легаси и `[Obsolete]` на Setup-инструментах, раскладка `_ScriptableObjects/`, `ProjectB_PostFX`.
- [x] **15.5.** Синхронизация документации (`AGENTS.md`, `CONTEXT.md`, `IMPLEMENTATION_PLAN.md`).
- [ ] **15.6.** CI (GitHub Actions + GameCI + бейдж в README) — *опционально / по решению автора*.
- [ ] **15.7.** Демо на itch.io + сбор списка тестеров + TikTok-дневник разработки.
- [ ] **15.8.** Приёмка Спринта 15.

### Спринт 14 — Мультиплатформа и Web-билд (РЕЛИЗ 1)
- [x] **14.1.** Адаптивный фундамент (`LayoutModeService`, `InputModeService`, `AdaptiveCanvasScaler`, `ResponsiveRect`, `AdaptiveLayoutGroup`, `OrientationPolicy`).
- [x] **14.2.** Камера по короткой стороне (`CameraConfig`), радиус спавна от проекции фрустума + кламп компаса к HUD.
- [x] **14.3.** Мультиввод (`InputActionAsset`, `MoveInput`-агрегатор для Touch + клавиатуры + геймпада, подсказки и системная кнопка Back).
- [x] **14.4.** Ландшафтные раскладки всех экранов меню, HUD, Pause и Game Over; прогон `9:21 / 4:3 / 16:10 / 21:9` и замер draw calls (13.6 — `9 <= 10`).
- [~] **14.5.** Web-билд (Poki или CrazyGames): *(Basic Launch WebGL готов: `15.91 МБ`, `5` файлов без DoNotShip, Chrome смоук-тест 119.5 FPS и 0 ошибок; далее замер Firefox/Edge/моб. Chrome и Фаза 5 — CrazyGames SDK)* Build Profile `Web`, Quality `Web`, браузерный lifecycle, `IAdProvider` / `IPlatformEvents` / `ISaveStorage`.
- [ ] **14.6.** Дешёвые порты после Google Play (Samsung Galaxy Store, TapTap, Steam; Amazon — бэклог).
- [ ] **14.7.** Тесты `Sprint14MultiplatformTests`, скриншоты и смоук-тест Web-билда.

### Спринт 16 — Глубина забега (плотность, новые враги и Ascension)
- [x] **16.1.** Рост плотности, ускорение спавна (`0.50s → 0.16s`), перекрытие волн (`≤ 30%` живых), рост стай и смягчение HP-губки + «орды-коридоры» (`CorridorHorde`) каждые ~10 волн начиная с W19.
- [x] **16.2.** Три новых врага W30–W90: W32 `Splitter Slime`, W46 `Frost Wisp`, W61 `Siege Golemite` (двухфазный «Каменный панцирь → Ядро»), воксельные модели, префабы, волны знакомства и `EnemyData.icon` (`VoxelIconGenerator`).
- [x] **16.3.** Модификаторы волн (`WaveMutatorData` SO): `Fog`, `Elite Surge`, `Glass Horde`.
- [ ] **16.4.** Режим `Ascension A1–A10` (`AscensionLevelData` SO) после победы над Demon Lord (допустим перенос в Post-Launch).
- [ ] **16.5.** Тесты Спринта 16.

### Спринт 17 — Релизная готовность Google Play (P0)
- [ ] **17.0.** Закрытый тест Google Play (личный аккаунт: 15–20 тестеров с запасом, 14 дней активности + регулярные обновления сборки).
- [ ] **17.1.** Подписанный `.aab` (`Android-GooglePlay`), актуальный Target API Play Console, проверка 16 KB page size, SemVer + auto `versionCode`, отключение неиспользуемых модулей.
- [ ] **17.2.** Google Mobile Ads SDK (`GOOGLE_MOBILE_ADS`), UMP-согласие + кнопка privacy options в настройках, проверка всех rewarded-точек.
- [ ] **17.3.** Unity IAP (`Remove Ads` $2.99, `Starter Pack` $0.99), восстановление покупок и экран магазина в стиле `UiTheme`.
- [ ] **17.4.** Аналитика (`IAnalyticsService`) + таксономия событий (FTUE, забеги, реклама, покупки) и краш-репортинг (Cloud Diagnostics / Crashlytics).
- [ ] **17.5.** Материалы магазина (иконка 512×512, feature graphic 1024×500, скриншоты, трейлер, описания EN/UK/RU), Privacy Policy, Data Safety, платёжный профиль.
- [ ] **17.6.** Миграция сохранений (`SaveManager.Migrate`) и защита от порчи сейвов начиная с релиза 1.0.
- [ ] **17.7–17.8.** Профилирование на устройствах (эталон + 2020 г.), стресс-тест 12.4 и приёмка Google Play.

### Спринт 18 — Релизы, маркетинг-инструменты и LiveOps
- [ ] **18.0.** Веб-релиз 1 (Poki → при отказе CrazyGames), трафик из itch.io и TikTok.
- [ ] **18.1.** Карточка результата «Поделиться» (локальный рендер `1080×1920` на Game Over).
- [ ] **18.2.** Инструменты записи контента для TikTok / Shorts в dev-сборках (скрытие HUD, slow-mo, свободная камера).
- [ ] **18.3–18.4.** Опциональный Remote Config поверх SO-баланса и цикл итераций по дашборду метрик.
- [ ] **18.5.** Google Play Production (Релиз 2) и последующие порты 14.6.

---

## Порядок выполнения (из [AUDIT.md §5.2](./AUDIT.md))

```
Спринт 15 (15.1–15.5 ✅) → остаток 15 + itch.io демо / TikTok
    │
    ▼
Спринт 12 (12.1–12.3: баланс → FTUE → локализация EN / UK / RU)
    │
    ▼
Спринт 14 (14.2–14.5: камера/спавн, мультиввод, ландшафт, Web-билд) → РЕЛИЗ 1 (Web: Poki / CrazyGames)
    │
    ▼
Спринт 16 (плотность, 3 врага, мутаторы, Ascension) ↕ параллельно со Спринтом 17 (Google Play, SDK, IAP, закрытый тест)
    │
    ▼
12.4 Стресс-тест v2 на устройстве → РЕЛИЗ 2 Google Play Production (Спринт 18) → Порты (Galaxy Store, TapTap, Steam)
```

---

## Заметки

- **Каждый пункт = отдельный коммит** по Conventional Commits; коммиты — только с одобрения автора.
- **Тестировать на телефоне** после каждой фазы, не только в редакторе. После каждого спринта — EditMode-тесты (`Sprint*Tests.cs`).
- **ScriptableObjects для всего:** данные героев, врагов, способностей, карточек, волн, апгрейдов, талантов. Баланс меняется без перекомпиляции.
- **Зафиксированные решения** (без `maxLevel`, синергии без стат-пачек и карточек, без колец под элитами, иконки `16×16` пока остаются) — см. [CONTEXT.md](./CONTEXT.md).
- **Placeholder'ы — это нормально.** Лучше играбельный прототип с кубами, чем красивый неиграбельный продукт.
