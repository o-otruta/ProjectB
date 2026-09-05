# Аудит проекта ProjectB

## Общая оценка

Архитектурный «скелет» здоровый: есть DI (VContainer) с разделением на Root/Game/MainMenu-скоупы, данные вынесены в ScriptableObject'ы, везде используется `ObjectPool<T>`, есть слой мета-прогрессии. Но реализация — типичный MVP-прототип: много рантайм-хаков (`Shader.Find`, `CreatePrimitive`), утечек, дублирования и «мёртвых» фич, которые уже видны игроку.

Ниже — по убыванию критичности.

---

## 🔴 Критические баги

### 1. ~~`ArenaGenerator` ломает весь рандом игры~~ — ✅ *Исправлено*
[Assets/_Scripts/Arena/ArenaGenerator.cs](Assets/_Scripts/Arena/ArenaGenerator.cs)

> **Решение:** Генерация арены полностью переведена на изолированный экземпляр `System.Random`. Глобальный `UnityEngine.Random` больше не сбрасывается, сид в `ArenaConfig.asset` по умолчанию сброшен на `0` (случайная генерация каждого забега), а метод `GenerateArena(int? overrideSeed)` позволяет передавать конкретный сид при необходимости.

```csharp
if (_config.Seed != 0) Random.InitState(_config.Seed);
```

`Random.InitState` сеет **глобальный** `UnityEngine.Random`. После генерации арены детерминированными становятся: выбор типа врага, точки спавна, шанс элиты, дроп монет (20%), выбор карточек при левелапе, разброс фаерболов. Каждый забег будет идентичным.

**Фикс:** использовать локальный `System.Random` или `Random.State` со снимком/восстановлением:
```csharp
var prev = Random.state;
Random.InitState(_config.Seed);
// ... генерация
Random.state = prev;
```

### 2. Волна может никогда не закончиться
[Assets/_Scripts/Enemies/WaveManager.cs](Assets/_Scripts/Enemies/WaveManager.cs#L154-L166)

`CheckWaveEnd()` срабатывает только по `OnDied`. Если враг застрял за стеной, вылетел за арену или потерял таргет — волна висит бесконечно. Нет таймаута волны, нет принудительной телепортации/деспавна «потерянных» врагов.

Плюс: после смерти героя враги продолжают жить и обновляться в `Update()` (спавн останавливается, но пул не чистится).

### 3. `Projectile` без максимального времени жизни
[Assets/_Scripts/Combat/Projectile.cs](Assets/_Scripts/Combat/Projectile.cs#L24-L42)

`EnemyProjectile` и `AbilityProjectile` имеют `MAX_LIFETIME` (6с / 8с), а основной снаряд героя — нет. Если цель телепортируется/выключается нештатно (без `activeInHierarchy == false`), снаряд летит вечно и не возвращается в пул → пул исчерпывается.

### 4. `LaserTurret`: двойной `Release` в пул
[Assets/_Scripts/Abilities/Active/LaserTurret.cs](Assets/_Scripts/Abilities/Active/LaserTurret.cs#L60-L65)

```csharp
if (Time.time >= spawnTime + lifetime)
{
    pool.Release(this);
    return;
}
```

Нет флага `isReturned`. При `collectionCheck: true` (а он включён в [LaserTurretAbility.cs](Assets/_Scripts/Abilities/Active/LaserTurretAbility.cs#L59)) повторный `Release` кинет `InvalidOperationException`. `OnDisable` не гарантирует, что `Update` не выполнится ещё раз в этом же кадре при некоторых порядках.

### 5. Утечка материалов на врагах
[Assets/_Scripts/Enemies/EnemyBase.cs](Assets/_Scripts/Enemies/EnemyBase.cs#L58-L70)

`enemyRenderer.material` (не `sharedMaterial`) создаёт **копию материала на каждый инстанс**. Это: (а) утечка памяти — копии не уничтожаются, (б) полный слом batching/GPU instancing — каждый враг = свой draw call. На мобилке при 100+ врагах это убивает FPS.

**Фикс:** `MaterialPropertyBlock` + `Renderer.SetPropertyBlock`.

### 6. `ShootingEnemy`: пул снарядов на каждого врага
[Assets/_Scripts/Enemies/ShootingEnemy.cs](Assets/_Scripts/Enemies/ShootingEnemy.cs#L27-L70)

Каждый инстанс стрелка создаёт **свой** `ObjectPool` + свой GameObject-контейнер `EnemyProjectiles_{EntityId}`, который никогда не удаляется. За длинный забег в сцене накапливаются сотни осиротевших контейнеров с пулами.

**Фикс:** один общий пул на тип снаряда, владеет `WaveManager` или отдельный `ProjectileService`.

### 7. `CameraController.AdjustAspect()` — накопительный баг для ортокамеры
[Assets/_Scripts/Core/CameraController.cs](Assets/_Scripts/Core/CameraController.cs#L69-L74)

```csharp
float defaultOrthoSize = cam.orthographicSize;   // это НЕ дефолт, это текущее значение
cam.orthographicSize = defaultOrthoSize * (targetAspect / currentAspect);
```

При каждом повторном вызове (поворот экрана, сплит-скрин, изменение окна) размер умножается заново. Перспективная ветка сделана правильно через кэшированный `defaultFOV` — ортографическую забыли.

Там же: `smoothTime = 0f` — `SmoothDamp` вырождается в мгновенный снап, комментарий «плавное следование» врёт.

### 8. `DailyBonusManager` — нет защиты и нет проверки границ
[Assets/_Scripts/Meta/DailyBonusManager.cs](Assets/_Scripts/Meta/DailyBonusManager.cs#L64-L80)

- `rewardData.rewards[currentDay - 1]` без проверки `rewards.Length >= 7` → `IndexOutOfRangeException`.
- Нет защиты от перевода часов назад/вперёд: игрок меняет дату → фармит бонус бесконечно.
- Нет сброса стрика при пропуске дня (сравнивается только «сегодня != последний claim»). Пропустил неделю — продолжил с 5-го дня.

---

## 🟠 Мёртвые и незаконченные фичи (видны игроку)

| Место | Проблема |
|---|---|
| [StatUpgradeCardData.cs](Assets/_Scripts/LevelUp/StatUpgradeCardData.cs#L31-L56) | Карточки `MoveSpeed`, `GlobalDamage`, `Armor` **ничего не делают** — только `Debug.Log`. Игрок тратит выбор впустую. |
| [HeroAbilities.cs](Assets/_Scripts/Abilities/HeroAbilities.cs#L69) | `OnAbilitiesChanged()` — пустая заглушка. Пассивки (`damageMultiplierBonus`, `cooldownReductionBonus`, `moveSpeedBonus` из `PassiveAbilityData`) **никогда не применяются**. |
| [HeroAbilities.cs](Assets/_Scripts/Abilities/HeroAbilities.cs#L36-L67) | Нет трекинга уровня способности. `AchievementManager.OnAbilityUpgraded` существует, но **не вызывается ниоткуда** → ачивки на макс. уровень недостижимы. |
| [GameManager.cs](Assets/_Scripts/Core/GameManager.cs#L140-L147) | `GoToMenu()` и `ReviveHero()` — `Debug.Log("TODO")`. Из Game Over выйти нельзя. |
| [SaveManager.cs](Assets/_Scripts/Meta/SaveManager.cs#L163-L167) | `unlockedAbilityIds` пишется (дейли-бонусом), но **нигде не читается** — разблокированные способности не попадают в пул карт. |
| [HeroMovement.cs](Assets/_Scripts/Player/HeroMovement.cs) | `moveSpeed` считается один раз в `Start()` — изменить в рантайме невозможно (отсюда и мёртвая карточка MoveSpeed). |
| `AddAbility` | При заполненных слотах/дубликате — **тихий no-op**. Игрок выбрал карту, ничего не произошло, карта потрачена. |

---

## 🟡 Производительность (критично для мобилки / 60 FPS)

### Сохранения в горячем пути
[Assets/_Scripts/Meta/SaveManager.cs](Assets/_Scripts/Meta/SaveManager.cs#L63-L79)

`Save()` = `JsonUtility.ToJson` + `PlayerPrefs.SetString` + **`PlayerPrefs.Save()`** (синхронная запись файла на диск). Вызывается на **каждой** мутации:

`RunStatistics.AddKill()` → `AchievementManager.OnEnemyKilled()` → `UpdateAchievementProgress()` → `Save()`

То есть **запись на диск на каждый убитый враг**. На Android это гарантированные фризы во время боя. Плюс `OnDataChanged` дёргает перерисовку UI.

**Фикс:** флаг `isDirty` + флаш в `OnApplicationPause`/`OnApplicationFocus`/по таймеру/на конец забега.

### `Debug.Log` в игровом цикле
202 совпадения по логам. Особенно:
- [HeroHealth.cs](Assets/_Scripts/Player/HeroHealth.cs#L55) — лог на каждый удар
- [ShootingEnemy.cs](Assets/_Scripts/Enemies/ShootingEnemy.cs#L121) — лог на каждый выстрел каждого стрелка (+ конкатенация строк = GC allocs)
- [MainMenuUI.cs](Assets/_Scripts/UI/MainMenuUI.cs), [MainMenuLifetimeScope.cs](Assets/_Scripts/Core/MainMenuLifetimeScope.cs)

`Debug.Log` в Development-сборках не вырезается автоматически. Нужен обёрточный `GameLog` с `[System.Diagnostics.Conditional("ENABLE_LOGS")]`.

### `Shader.Find` + `new Material` в рантайме
9 мест: `BlackHoleAbility`, `IceAuraAbility`, `LaserTurret(Ability)`, `Boomerang/Fireball/ShooterAbility`, `ArenaGenerator`.

Проблемы: (1) `Shader.Find` медленный, (2) шейдер может быть **вырезан из билда** (не в Always Included) → розовые объекты только на девайсе, (3) каждый `new Material` — утечка. Часть помечена `TODO(Post-MVP)`, но это блокер релиза.

### Физика каждый кадр
- [HeroExperience.cs](Assets/_Scripts/LevelUp/HeroExperience.cs#L61-L74) — `OverlapSphereNonAlloc` (буфер 64) **каждый кадр**, плюс `TryGetComponent` × 2 на каждый хит.
- [BlackHoleAbility.cs](Assets/_Scripts/Abilities/Active/BlackHoleAbility.cs) — оверлап с буфером 100 каждый кадр.
- [BoomerangProjectile.cs](Assets/_Scripts/Abilities/Active/BoomerangProjectile.cs#L66-L88) — оверлап на каждый бумеранг каждый кадр.

Достаточно частоты 5–10 Гц с расфазировкой (как уже сделано в `EnemyBase` — там подход правильный).

### Мелочи с большим суммарным эффектом
- [EnemyBase.cs](Assets/_Scripts/Enemies/EnemyBase.cs#L136) — `LayerMask.GetMask("Obstacles")` (строковый lookup) внутри цикла движения; надо кэшировать статикой.
- `XpCrystal`/`CoinPickup` крутят `Update()` даже когда лежат неподвижно.
- Движение врагов через `transform.position +=` при наличии коллайдеров → пересчёт физики (лучше `Rigidbody.MovePosition` или `isKinematic` + правильные слои).
- `MetaUpgradeManager.GetTotalBonus` — линейный проход по всем апгрейдам на каждый вызов; `AchievementManager` — `List.Find` вместо `Dictionary`.

---

## 🔵 Архитектурные проблемы

### 1. ~~Service Locator внутри ScriptableObject'ов~~ ✅ **Исправлено**
- ~~[CardData.cs](Assets/_Scripts/LevelUp/CardData.cs), [StatUpgradeCardData.cs](Assets/_Scripts/LevelUp/StatUpgradeCardData.cs#L22), [AbilityCardData.cs](Assets/_Scripts/LevelUp/AbilityCardData.cs), [GameOverUI.cs](Assets/_Scripts/UI/GameOverUI.cs): `public override void ApplyEffect(IObjectResolver resolver)` и `resolver.Resolve<GameManager>()`.~~
- Карточки улучшений (`CardData`, `StatUpgradeCardData`, `AbilityCardData`, `AbilityModifierCardData`) очищены от зависимостей VContainer и превращены в чистые ScriptableObject данных (Data-Driven).
- Создан сервис [UpgradeApplier.cs](Assets/_Scripts/LevelUp/UpgradeApplier.cs), зарегистрированный в `GameLifetimeScope` со `Scoped` временем жизни. Сервис явно принимает через конструктор все подсистемы героя (`HeroHealth`, `HeroMovement`, `HeroExperience`, `HeroCombat`, `HeroAbilities`) и применяет эффекты карт через типобезопасный `switch`.
- Добавлены публичные методы для динамического изменения скорости героя (`HeroMovement.IncreaseMoveSpeed`) и множителя урона оружия (`HeroCombat.IncreaseDamageMultiplier` + `WeaponController.DamageMultiplier`).
- [UpgradeManager.cs](Assets/_Scripts/LevelUp/UpgradeManager.cs) очищен от `IObjectResolver`; `HeroAbilities` и `UpgradeApplier` внедряются явно через `[Inject] Construct`.
- В проекте не осталось ни одного обращения к `IObjectResolver` в рантайм-коде.
- Ликвидирован скрытый Service Locator / Ambient Context `GameEventBus.Current`: статическое поле `Current` полностью удалено из `GameEventBus.cs`. Все компоненты героя (`HeroExperience`, `HeroHealth`, `HeroAbilities`, `HeroEconomy`), `GameManager`, а также враги (`EnemyBase` через `Initialize` из `WaveManager`) получают шину исключительно через DI. В проекте действительно 0 вызовов сервис-локаторов и 0 глобального статического доступа к зависимостям.

### 2. ~~Часть систем вне DI~~ ✅ **Исправлено**
- ~~`HeroCombat` имеет `[Inject] Construct(HeroHealth, MetaUpgradeManager)`, но не зарегистрирован в `GameLifetimeScope.cs`~~: `HeroCombat`, `CameraController` и `ArenaGenerator` зарегистрированы в [GameLifetimeScope.cs](Assets/_Scripts/Core/GameLifetimeScope.cs). В [CameraController.cs](Assets/_Scripts/Core/CameraController.cs) добавлен `[Inject] Construct(HeroMovement)` для динамического связывания с целью. Теперь мета-бонусы к урону и смерть героя корректно обрабатываются в `HeroCombat`.

### 3. ~~Ручная инъекция в главном меню~~ ✅ **Исправлено**
- ~~[MainMenuLifetimeScope.cs](Assets/_Scripts/Core/MainMenuLifetimeScope.cs) — компоненты и регистрируются через `RegisterComponentInHierarchy`, **и** отдельно ищутся через `FindAnyObjectByType` + `resolver.Inject`.~~ Удалена избыточная ручная инъекция и спам-логи; VContainer автоматически резолвит и инжектит компоненты сцены.

### 4. Нет Assembly Definitions
Ни одного `.asmdef` — весь код в `Assembly-CSharp`. Любая правка одной строки = полная перекомпиляция всего проекта (включая редакторные тулзы). При 100+ скриптах и VContainer это уже ощутимо, дальше будет хуже. Минимум: `ProjectB.Core`, `ProjectB.Gameplay`, `ProjectB.Meta`, `ProjectB.UI`, `ProjectB.Editor`.

### 5. Дублирование кода
| Пара | Что дублируется |
|---|---|
| `XpManager` ↔ `CoinManager` | Практически идентичные пулы + fallback-материал через temp-примитив |
| `XpCrystal` ↔ `CoinPickup` | Логика магнита, сбора, `TryGetComponent` на герое |
| `IceAuraAbility.CreateVisual()` ↔ `BlackHoleAbility.CreateVisual()` | Копипаста создания визуала + материала |
| `FireballAbility` / `ShooterAbility` / `BoomerangAbility` — `CreateProjectilePrefab()` | Одинаковый fallback-блок |

Просится общий `PickupBase` + `PooledSpawner<T>` и хелпер для fallback-визуала.

### 6. ~~Нет событийной шины~~ ✅ **Исправлено**
- ~~Менеджеры держат прямые ссылки друг на друга (`EnemyBase` тащит `XpManager`, `CoinManager`, `RunStatistics` через 7-параметровый `Initialize`). Сигнатура уже неуправляемая — при добавлении дропа/статистики будет 9 параметров.~~
- Внедрён легковесный zero-allocation `GameEventBus` ([GameEventBus.cs](Assets/_Scripts/Core/Events/GameEventBus.cs)) со struct-событиями ([GameEvents.cs](Assets/_Scripts/Core/Events/GameEvents.cs)): `EnemyDiedEvent`, `WaveCompletedEvent`, `HeroLeveledUpEvent`, `AbilityUnlockedEvent`, `HeroDiedEvent`, `CoinCollectedEvent`.
- Создан сервис [EnemyDeathHandler.cs](Assets/_Scripts/Enemies/EnemyDeathHandler.cs), зарегистрированный как `IStartable`/`IDisposable` EntryPoint в [GameLifetimeScope.cs](Assets/_Scripts/Core/GameLifetimeScope.cs). Он слушает `EnemyDiedEvent` и централизованно управляет спавном кристаллов XP, монет, статистикой и ачивками.
- Сигнатура `EnemyBase.Initialize` и `ShootingEnemy.Initialize` сокращена с 7 до 4 параметров (`data, target, pool, difficulty`). Полностью убраны ссылки на `XpManager`, `CoinManager`, `RunStatistics`.
- `WaveManager` больше не инжектирует менеджеры лута и статистики ради пересылки во врагов, а публикует `WaveCompletedEvent`.
- `RunStatistics` отвязана от `AchievementManager`.

### 7. Магические числа
- ~~`EnemyBase.ContactRadius = 1.0f`, дроп монет 20%, элита `×3 HP / ×2 dmg` — захардкожены, не в `EnemyData`/`WaveConfig`.~~ ✅ **Исправлено:** вынесены в `EnemyData` (`contactRadius`, `coinDropChance`, `coinDrop`) и `WaveConfig` (`eliteHpMultiplier`, `eliteDamageMultiplier`).
- [MainMenuUI.cs](Assets/_Scripts/UI/MainMenuUI.cs) — индексы табов 0–4 числами.
- ~~[VirtualJoystick.cs](Assets/_Scripts/UI/VirtualJoystick.cs) — `handleLimit = 100f` в сырых пикселях, без учёта DPI/CanvasScaler → на разных экранах разная чувствительность.~~ ✅ **Исправлено:** лимит хода динамически рассчитывается из радиуса фона (`autoHandleLimit`), добавлены настройки `handleRange`, `deadZone`, опциональная DPI-нормализация (`normalizeWithDpi`), а `CanvasScaler` в `Gameplay.unity` и инструментах настройки сцен переведен в `ScaleWithScreenSize` (1080×1920, match 0.5).

---

## 🟣 Расхождения с GDD

| GDD / IMPLEMENTATION_PLAN | Реальность |
|---|---|
| Формула XP: `baseXP * level^1.5` | [HeroExperience.cs](Assets/_Scripts/LevelUp/HeroExperience.cs#L100-L103): `level^1.05` — кривая почти линейная, к 30 уровню разница ~×10. Игрок будет левелапиться непрерывно. |
| 2 карточки + 1 за рекламу | [UpgradeManager.cs](Assets/_Scripts/LevelUp/UpgradeManager.cs#L15): `maxCardsToOffer = 3`, рекламная карта не реализована |
| Индикатор волны в HUD (п. 1.5) | Не сделано |
| Секретные предметы (п. 1.10) | Не сделано |
| Мета-бонус `StartLevel` | Поднимает `CurrentLevel`, но **не вызывает `OnLevelUp`** → стартовых карт игрок не получает, апгрейд ощущается как штраф (XP до след. уровня выше, бонусов ноль) |

---

## 🟤 Сохранения / безопасность

- **PlayerPrefs без подписи и шифрования.** JSON лежит в реестре/plist открытым текстом — монеты и мета-апгрейды правятся за минуту. Минимум: HMAC-подпись с солью в бинарнике.
- `MigrateIfNeeded()` пустая при `saveVersion = 1` — первое же изменение схемы сломает сейвы существующих игроков. Нужен хотя бы каркас с проверкой версии и бэкапом.
- [SerializableDictionary.cs](Assets/_Scripts/Meta/SerializableDictionary.cs): `OnBeforeSerialize()` пустой (полагается на то, что списки уже синхронны), дубликаты ключей **молча теряются** при десериализации, `Remove` перестраивает весь индекс.
- Нет обработки повреждённого сейва иначе как «создать новый» — прогресс игрока молча обнулится.

---

## Приоритет работ

**Сейчас (блокеры):**
1. `Random.InitState` в `ArenaGenerator`
2. Батчинг сохранений (`isDirty` + флаш) — иначе фризы в бою
3. `enemyRenderer.material` → `MaterialPropertyBlock`
4. Общий пул снарядов вместо пула-на-врага в `ShootingEnemy`
5. Таймаут волны в `WaveManager`
6. `MAX_LIFETIME` для `Projectile`, guard на `Release` в `LaserTurret`

**До первого билда на девайс:**
7. Убрать все `Shader.Find`/`new Material` → префабы + сериализованные материалы
8. Условная компиляция логов
9. Дожать или **скрыть** мёртвые карточки (MoveSpeed / GlobalDamage / Armor)
10. Реализовать `GoToMenu()` — сейчас из Game Over нет выхода
11. Починить `AdjustAspect` для ортокамеры, защита дейли-бонуса от перевода часов

**Следующая итерация:**
12. Ввести `.asmdef`
13. Убрать `IObjectResolver` из `CardData`
14. `OnAbilitiesChanged` + уровни способностей
15. Разжать дублирование XP/Coin, дедуп визуалов способностей
16. Привести XP-формулу и количество карт к GDD

---

Готов начать чинить — скажи, с какого пункта (или дай сразу блок «блокеры», сделаю всё подряд).
