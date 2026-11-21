# Полный технический и архитектурный аудит проекта ProjectB

**Движок:** Unity `6000.5.5f1` (URP `17.5.0`)  
**Стек:** VContainer `1.19.0`, New Input System `1.19.0`, uGUI / TextMeshPro, ScriptableObject Data-Driven  
**Целевая платформа:** Android (Portrait only, 9:16 – 9:20, 60 FPS), далее iOS  
**Основание для сверки:** `CONTEXT.md` (GDD), `IMPLEMENTATION_PLAN.md`, `AGENTS.md`, исходный код, префабы, сцены, `.asset` и `ProjectSettings`

---

## Executive Summary

**Что уже хорошо:**
- Проект — живой играбельный прототип, а не набор заглушек: работает движение, адаптивная камера под вытянутые вертикальные экраны, автоатака, 7 активных способностей, волны с лишем и таймаутом, пулы объектов, воксельные модели с легковесным `VoxelMeshAnimator` (без `SkinnedMeshRenderer`), главное меню, сохранения, мета-апгрейды и ежедневный бонус.
- Грамотно настроена матрица коллизий слоёв (`Enemy`, `Hero`, `Obstacles`, `HeroProjectiles`, `EnemyProjectiles`, `Pickups`) и используется `VContainer` с корневым `RootLifetimeScope`.

**Главная проблема текущего состояния:**
В ходе быстрого прототипирования накопился серьёзный разрыв между кодом, префабами, `.asset`-файлами и GDD. Ряд пунктов, отмеченных выполненными в `IMPLEMENTATION_PLAN.md`, фактически сломан или не замкнут в игровой цикл, а несколько проблем в сценах и данных гарантированно обрушат FPS и загрузку на реальном Android-устройстве.

### Сводная таблица состояния

| Направление | Оценка | Статус | Ключевые проблемы |
|---|:---:|:---:|---|
| **Производительность (Mobile 60 FPS)** | 3.5 / 10 | 🔴 Критично | 50 000 `Instantiate` травы на старте, синхронный `PlayerPrefs.Save()` на каждый килл, static colliders у врагов, `pickupLayer = Everything`, `renderer.material`, `URP/Lit` в Always Included Shaders |
| **Геймплей и баги** | 5 / 10 | 🔴 Есть блокеры | Чёрная дыра тянет врагов в героя, `MagnetRadius` +15м вместо +15%, `StartLevel` без карт, off-by-one в DailyBonus UI, дрейф Y у мышей, мульти-снаряды исчезают, джиттер камеры |
| **Архитектура и DI** | 6 / 10 | ⚠️ Требует доработки | Разорванный мета-луп (`unlockedAbilityIds` не фильтрует карты), рассинхрон урона `HeroCombat`/`HeroAbilities`, мёртвые события `GameEventBus`, нет `.asmdef` и тестов |
| **Данные, префабы и баланс** | 4.5 / 10 | 🔴 Рассинхрон | Все 17 карт имеют `rarity: 0` (Common), дебажные значения в ассетах (`baseXP: 1`, `minWaveForElites: 1`, `explosionRadius: 10`), `HeroEconomy` отсутствует на `Hero.prefab` |
| **Настройки Android и правила (`AGENTS.md`)** | 5 / 10 | ⚠️ Нарушения | Разрешён Landscape (`defaultScreenOrientation: 4`), `AndroidTargetSdkVersion: 0`, нет `SafeArea` для вырезов, `.asset` внутри `_Scripts/`, мусор в `Resources/` |
| **Готовность к MVP (`CONTEXT.md`)** | 5 / 10 | 🟡 В процессе | Нет боссов/мини-боссов, секретов на арене, синергий, пассивных способностей, 2+1 слота карт, рекламы, звуков, чисел урона, экрана коллекции способностей |

---

## 1. Критические проблемы производительности (Mobile 60 FPS & RAM)

### 1.1. 50 000 инстансируемых GameObject травы на старте сцены
- **Локация:** `Assets/_ScriptableObjects/ArenaConfig.asset` (`MinDecorations: 50000`, `MaxDecorations: 50000`, `ArenaSize: 500`), `Assets/_Scripts/Arena/ArenaGenerator.cs:222-259`
- **Проблема:** `ArenaGenerator.GenerateDecorations()` синхронно в `Start()` выполняет 50 000 вызовов `Instantiate(prefab, ...)`. Это создаёт 50 000 объектов в иерархии (50k `Transform`, 50k `MeshFilter`, 50k `MeshRenderer`), вызывает многосекундный фриз загрузки, резкий скачок памяти и тяжёлый оверхед frustum culling / transform hierarchy на мобильном CPU.
- **Решение:** Для декораций такого масштаба использовать `Graphics.RenderMeshInstanced` (с разбиением арены на чанки по сетке и отсечением чанков вне камеры) либо объединять меши травы в чанки (`Mesh.CombineMeshes`). Как быстрый фикс до переработки — снизить количество декораций до разумного предела и включить GPU Instancing.

### 1.2. Синхронная запись на диск (`PlayerPrefs.Save()`) на каждое убийство врага
- **Локация:** `EnemyDeathHandler.cs:70` → `AchievementManager.cs:127-130, 30-48` → `SaveManager.cs:145-149, 63-79`
- **Проблема:** При смерти **каждого** врага запускается цепочка:
  1. `AchievementManager.AddProgress()` делает линейный поиск `_allAchievements.Find(a => a.id == id)` (аллокация делегата/замыкания на каждый килл).
  2. Вызывается `SaveManager.UpdateAchievementProgress()` → `SaveManager.Save()` → `JsonUtility.ToJson(Data)` (аллокация строки) + `PlayerPrefs.SetString()` + `PlayerPrefs.Save()` (синхронный дисковый I/O).
  3. При убийстве пачки из 20–30 врагов AoE-способностью в одном кадре выполняется 20–30 последовательных записей на диск.
  4. Дополнительно имеются каскадные двойные/тройные вызовы `Save()`:
     - `AchievementManager.AddProgress()` при разблокировке: `CompleteAchievement()` → `Save()`, `AddCoins()`/`UnlockAbility()` → `Save()`, затем `UpdateAchievementProgress()` → `Save()` (**3 записи подряд**).
     - `SaveManager.RecordRunResult()`: `AddCoins()` → `Save()`, затем в конце метода ещё раз `Save()` (**2 записи**).
     - `MetaUpgradeManager.TryBuyUpgrade()`: `SpendCoins()` → `Save()`, затем `SetMetaUpgradeLevel()` → `Save()` (**2 записи**).
     - `DailyBonusManager.ClaimReward()`: `AddCoins()`/`UnlockAbility()` → `Save()`, затем `saveManager.Save()` (**2 записи**).
- **Обратный риск на мобильных:** `SaveManager` подписывается только на `Application.quitting` (`SaveManager.cs:16`), которое на Android/iOS почти никогда не вызывается при сворачивании или убийстве фонового процесса ОС.
- **Решение:**
  - Хранить словарь `Dictionary<string, AchievementData>` вместо `List.Find`.
  - В бою накапливать изменения в памяти с флагом `isDirty`, а `Save()` вызывать пакетно: при завершении забега, при выходе в меню, при покупке/клейме (1 раз) и при `OnApplicationPause(true)` / `Application.focusChanged`.

### 1.3. Перемещение врагов как Static Colliders (разрушение PhysX BVH)
- **Локация:** `Assets/_Prefabs/Enemies/Slime.prefab`, `Assets/_Prefabs/Enemies/BatShooter.prefab`, `Assets/_Scripts/Enemies/EnemyBase.cs:154`
- **Проблема:** У префабов `Slime` и `BatShooter` есть `Collider` (`m_IsTrigger: 0`), но **отсутствует `Rigidbody`**. При этом `EnemyBase.UpdateMovement()` каждый кадр двигает их напрямую через `transform.position += ...`. В PhysX объект с коллайдером без `Rigidbody` считается статическим — его перемещение вынуждает физический движок перестраивать статические структуры broadphase на каждом шаге физики.
- **Решение:** Добавить на все движущиеся префабы врагов `Rigidbody` с `isKinematic = true` и `useGravity = false` (и сделать их коллайдеры триггерами, если физическое расталкивание не используется).

### 1.4. Неверная маска `pickupLayer = Everything` в `HeroExperience`
- **Локация:** `Assets/_Prefabs/Hero.prefab` (`HeroExperience.pickupLayer.m_Bits: 4294967295`), `Assets/_Scripts/LevelUp/HeroExperience.cs:63-79`
- **Проблема:** В `Hero.prefab` маска `pickupLayer` выставлена в `Everything` (`~0`) вместо слоя `Pickups` (слой 13, `m_Bits: 8192`). В результате каждый кадр `Physics.OverlapSphereNonAlloc(transform.position, MagnetRadius, magnetResults, pickupLayer)` собирает **все коллайдеры в радиусе** (пол, препятствия, самого героя, орбитальные мечи и всех врагов) и вызывает `TryGetComponent<XpCrystal>` + `TryGetComponent<CoinPickup>` на каждом. Хуже того: размер буфера `magnetResults` равен `64` — при скоплении врагов буфер забивается ими, и кристаллы/монеты **перестают притягиваться**.
- **Решение:** Выставить `pickupLayer` на `Hero.prefab` строго в слой `Pickups` и вынести проверку магнита на таймер (раз в 0.05–0.1 сек) с ранним выходом при `Time.timeScale == 0` или смерти героя.

### 1.5. Клонирование материалов, `Shader.Find` в рантайме и `URP/Lit` в `Always Included Shaders`
- **Локация:**
  - `EnemyBase.cs:57-68, 79-82` (`enemyRenderer.material.color = ...`)
  - `ShootingEnemy.cs:60` (т.к. у `EnemyShooterWeapon.asset` `projectilePrefab == null`)
  - `ArenaGenerator.cs:104, 112-114, 158, 163-165`
  - `ShooterAbility.cs:29-31`, `FireballAbility.cs:40-42`, `BoomerangAbility.cs:32-34`, `IceAuraAbility.cs:42-52`, `BlackHoleAbility.cs:41-51`, `LaserTurretAbility.cs:48-50`, `LaserTurret.cs:51-59`
  - `Assets/Editor/AddLitShaderFixer.cs` + `ProjectSettings/GraphicsSettings.asset:37`
- **Проблема:**
  1. Обращение к `renderer.material` и `new Material(...)` создаёт уникальные экземпляры материалов в памяти, отключает SRP Batcher/Instancing и плодит утечки (созданные материалы не удаляются в `OnDestroy`, включая `fallbackMaterial` в `XpManager.cs:30` и `CoinManager.cs:28`).
  2. Чтобы `Shader.Find("Universal Render Pipeline/Lit")` работал в билде, `AddLitShaderFixer` принудительно добавил `URP/Lit` в `Always Included Shaders`. Для Android это критический антипаттерн: Unity перестаёт вырезать неиспользуемые варианты `URP/Lit`, что резко увеличивает время сборки, размер APK и потребление памяти под шейдеры.
  3. На всех материалах в `Assets/_Materials/` и `Assets/_VoxelModels/` выключен `m_EnableInstancingVariants: 0`.
- **Решение:**
  - В `EnemyBase` перевести смену цвета элиты на `MaterialPropertyBlock` (`Shader.PropertyToID("_BaseColor")`) без обращения к `renderer.material`.
  - Назначить реальные префабы с shared-материалами для всех снарядов, аур и `EnemyShooterWeapon.asset`, удалить `Shader.Find` из рантайма и убрать `URP/Lit` из `Always Included Shaders` вместе с `AddLitShaderFixer.cs`.

### 1.6. Избыточные физические запросы, `GetComponent`, аллокации и `Debug.Log` в горячих циклах

| Скрипт и строки | Проблема | Как исправить |
|---|---|---|
| `OrbitalCompanionAbility.cs:91-157` | Вешает на каждый меч `SphereCollider` + kinematic `Rigidbody`, но `OnTrigger*` не использует — вместо этого каждый кадр (в т.ч. на паузе!) гоняет `OverlapSphereNonAlloc` + `GetComponent<EnemyBase>()` для **каждого** меча | Убрать лишние `Collider`/`Rigidbody` (или перейти на `OnTriggerStay`), добавить тик-таймер (0.05–0.1с) и ранний выход при `Time.deltaTime == 0` |
| `WeaponController.cs:65-73` | Если рядом нет врагов, `nextFireTime` не обновляется, и `OverlapSphereNonAlloc[200]` вызывается **каждый кадр** | Добавить интервал повторного сканирования при отсутствии цели (например, `0.15f`) и проверять `!enemy.IsDead` |
| `BlackHoleAbility.cs:95` | `OverlapSphereNonAlloc[100]` + `TryGetComponent<EnemyBase>` + `TryGetComponent<IDamageable>` вызываются **каждый кадр** | Обновлять список захваченных врагов по таймеру (раз в `0.1–0.15с`), а `EnemyBase` уже реализует `IDamageable` (не нужен второй `TryGetComponent`) |
| `LaserTurret.cs:109, 140, 149` | `OverlapSphereNonAlloc[50]` каждый кадр при поиске цели + два вызова `TryGetComponent<IDamageable>` каждый кадр на одну и ту же `currentTarget` | Искать цель раз в `0.2с`; закешировать `IDamageable currentTargetDamageable` при выборе цели |
| `Projectile.cs:47` | `target.TryGetComponent(out damageable)` выполняется каждый кадр на каждый летящий снаряд | Закешировать `IDamageable` один раз в `Initialize()` |
| `EnemyProjectile.cs:99, 119, 137, 142` | Делает `OverlapSphereNonAlloc` + `SphereCast` + `GetComponentInParent<EnemyBase>` + `GetComponentInParent<IDamageable>` каждый кадр | В одиночной игре герой один: попадание по герою проверять по `sqrMagnitude` до `heroTransform` (0 вызовов физики), а `SphereCast` делать только по маске `Obstacles` |
| `ShootingEnemy.cs:24, 28, 47, 55, 124, 166` | Активные `Debug.Log($"...")` с интерполяцией строк на каждый выстрел и спавн (GC-аллокации + тормоза вывода логов на Android) | Удалить боевые `Debug.Log` (также из `HeroHealth.cs:58, 91`, `SaveManager.cs:72`) |
| `WaveManager.cs:162, 201, 230, 293` | `new WaitForSeconds(waveConfig.spawnDelay)` аллоцируется на каждого заспавненного врага; `LayerMask.GetMask("Obstacles")` по строке в `GetValidSpawnPosition`; `activeEnemies.RemoveWhere(e => ...)` с лямбдой; `AddRange(HashSet)` | Закешировать `WaitForSeconds` и `obstacleMask`; итерироваться по `HashSet` циклом `foreach` без боксинга/лямбд |
| `EnemyBase.cs:135` | `LayerMask.GetMask("Obstacles")` вызывается по строке каждые 0.1с у каждого врага | Закешировать в `static readonly` / `Awake` |
| `XpCrystal.cs:23, 48`, `CoinPickup.cs:24, 49` | У всех лежащих на земле пикапов крутится `Update()`, а при подборе каждый раз вызывается `heroTarget.TryGetComponent<HeroExperience/HeroEconomy>` | Держать скрипт `enabled = false` до вызова `Magnetize()` (или обновлять летящие пикапы централизованно в менеджере); передавать ссылку на `HeroExperience`/`HeroEconomy` в `Initialize()` |

### 1.7. Индивидуальный пул снарядов на каждого `ShootingEnemy`
- **Локация:** `Assets/_Scripts/Enemies/ShootingEnemy.cs:33-82`
- **Проблема:** Каждый инстанс `ShootingEnemy` создаёт свой корневой объект `EnemyProjectiles_<id>` и свой `ObjectPool<EnemyProjectile>` (и не уничтожает контейнер в `OnDestroy`). 20 стреляющих врагов создают 20 пулов и 20 контейнеров в корне сцены.
- **Решение:** Вынести общий `EnemyProjectilePool` в отдельный сервис в `GameLifetimeScope`.

### 1.8. Раздувание APK через `Resources/` и отключённый GPU Instancing
- **Локация:** `Assets/TextMesh Pro/Examples & Extras/Resources/`
- **Проблема:** В Unity **все** файлы из любых папок `Resources` автоматически упаковываются в билд, даже если на них нет ссылок. В репозитории лежат примеры TextMeshPro с 6+ SDF-шрифтами, текстурными атласами и материалами внутри `Resources/`.
- **Решение:** Удалить `Assets/TextMesh Pro/Examples & Extras` (и `Assets/TutorialInfo`).

---

## 2. Критические и серьёзные баги (геймплей, физика, UI)

### 2.1. Легендарная способность «Чёрная дыра» убивает самого героя
- **Локация:** `Assets/_Scripts/Abilities/Active/BlackHoleAbilityData.cs:13-19`, `Assets/_Scripts/Abilities/Active/BlackHoleAbility.cs:93-113`
- **Суть бага:** `BlackHoleAbility` создаётся как дочерний объект героя в `(0, 0, 0)` и каждый кадр вызывает `enemy.ApplyPull(direction * currentPullForce * Time.deltaTime)` строго в сторону `transform.position` (позиции героя). В результате способность буквально затаскивает всех окрестных врагов в контактный радиус героя, где они мгновенно наносят ему урон. Дополнительно:
  - `direction` нормализуется до обнуления `direction.y = 0` (`BlackHoleAbility.cs:102-103`).
  - В центре дыры враги перелетают через `0` туда-обратно каждый кадр (вибрация).
  - `EnemyBase.ApplyPull()` двигает врага через `transform.position += pullDelta` сквозь препятствия и стены арены.
- **Решение:** Либо спавнить Чёрную дыру как отдельный временный объект на расстоянии от героя (в точке ближайшего врага / по кулдауну), либо останавливать притяжение на безопасной дистанции от героя (если она задумана как аура).

### 2.2. Ошибка единиц измерения у мета-апгрейда `MagnetRadius` (+15 метров вместо +15%)
- **Локация:** `Assets/Resources/MetaUpgrades/magnet_radius.asset` (`effectPerLevel: 15`, `maxLevel: 10`), `Assets/_Scripts/LevelUp/HeroExperience.cs:49-50`
- **Суть бага:** В GDD (`CONTEXT.md:174`) и ассете бонус задан в процентах (+15% за уровень), как и у остальных апгрейдов (`HeroHP`, `HeroDamage`, `HeroSpeed`, `XPBonus`, `AbilityDamage`). Однако `HeroExperience` вместо умножения на `(1f + magnetBonus / 100f)` делает:
  ```csharp
  MagnetRadius += magnetBonus; // 3 + 15 = 18м на 1 уровне, 153м на 10 уровне!
  ```
- **Решение:** Исправить на `MagnetRadius *= (1f + magnetBonus / 100f);`.

### 2.3. Мета-апгрейд `StartLevel` делает игру сложнее
- **Локация:** `Assets/_Scripts/LevelUp/HeroExperience.cs:52-53`
- **Суть бага:** При покупке `StartLevel` герой стартует с повышенным `CurrentLevel` (например, Lv 4), из-за чего возрастает порог `XPToNextLevel`, но событие `OnLevelUp` не вызывается и стартовые карточки не выдаются. Игрок платит 1000+ монет за то, чтобы дольше получать первый апгрейд.
- **Решение:** После старта (когда `UpgradeManager` уже подписан на `OnLevelUp`, либо через явный метод `GrantStartingLevelUps(int count)`) выдавать игроку `startLevelBonus` выборов карточек.

### 2.4. Off-by-one баг в UI ежедневного бонуса + конфликт правил с GDD
- **Локация:** `Assets/_Scripts/Meta/DailyBonusManager.cs:55, 91-106, 174-183`, `Assets/_Scripts/UI/DailyBonusScreenUI.cs:71-72`
- **Суть бага:**
  1. `ClaimReward()` сразу инкрементирует `dailyBonusDay++` (День 1 → День 2) и ставит `lastDailyClaimDate = today` (`IsRewardAvailable() == false`).
  2. Затем `DailyBonusScreenUI.RefreshUI()` вычисляет:
     ```csharp
     bool isClaimed = dayIndex < currentDay || (dayIndex == currentDay && !isRewardAvailable);
     ```
     При `currentDay == 2` и `!isRewardAvailable` **и День 1, и День 2** сразу помечаются как полученные! А после получения Дня 7 (`dailyBonusDay` сбрасывается в `1`) **День 1 нового цикла** сразу отображается как уже полученный.
  3. В `DailyBonusManager.ValidateStreak()` (`строки 97–106`) пропуск дня сбрасывает цикл на День 1, что прямо противоречит `CONTEXT.md:208` (*«Пропуск дня — не сбрасывает прогресс цикла, но день не засчитывается»*). Нужно согласовать с геймдизайном.
  4. `DateTime.TryParse` и `ToString("yyyy-MM-dd")` не используют `CultureInfo.InvariantCulture` (риск сбоя на устройствах с негригорианским календарём).
- **Решение:** Развести понятия «текущий доступный день» и «последний полученный день» (например, при `!isRewardAvailable` показывать полученными только `dayIndex < currentDay`, а при переходе с 7 на 1 — отдельно обрабатывать завершённый 7-й день, либо сдвигать `dailyBonusDay` только при наступлении нового дня).

### 2.5. Дрейф высоты (`Y`) пулимых врагов в `VoxelMeshAnimator`
- **Локация:** `Assets/_Scripts/Visuals/VoxelMeshAnimator.cs:120-125, 152-166`, `Assets/_Prefabs/Enemies/BatShooter.prefab` (`enableBobbing: 1`)
- **Суть бага:** В `OnEnable()` выполняется `baseLocalPosition = bobbingTransform.localPosition;`. Когда летучая мышь умирает и уходит в пул, её `Visuals.localPosition.y` смещена синусоидой bobbing. При следующем спавне из пула это смещённое значение перезаписывается как новая `baseLocalPosition`, из-за чего летучие мыши с каждым циклом пула постепенно «уплывают» вверх или вниз под землю.
- **Дополнительно:** В `Update()` (`строки 152–156`) `timer += Time.deltaTime` выполняется только при `frames.Length > 1` — если кадров ≤ 1, bobbing вообще не работает; в `Stop()` позиция не возвращается в `baseLocalPosition`.
- **Решение:** Кешировать `baseLocalPosition` один раз в `Awake()`, а в `OnEnable()` / `OnDisable()` / `Stop()` восстанавливать `bobbingTransform.localPosition = baseLocalPosition`.

### 2.6. Мульти-снаряды `ShooterAbility` и `FireballAbility` пропадают впустую
- **Локация:** `ShooterAbility.cs:41-54`, `FireballAbility.cs:52-65`, `AbilityProjectile.cs:39-43`, `FireballProjectile.cs:17-29`
- **Суть бага:** При `currentCount > 1` все снаряды выпускаются в **одного и того же** ближайшего врага. Как только первый снаряд убивает цель, остальные возвращаются в пул в `AbilityProjectile.Update()` из-за `!target.gameObject.activeInHierarchy` — не нанося урона (а `FireballProjectile` даже не взрывается в последней известной точке цели). Кроме того, `ProjectileAbilityBase` обрабатывает `ModifierType.PierceCount`, но ни `ShooterAbility`, ни `FireballAbility` его не используют.
- **Решение:** Распределять снаряды по `N` разным ближайшим целям (или пускать веером/с задержкой), а для `FireballProjectile` взрываться при достижении последней известной позиции цели, если та умерла в полёте.

### 2.7. Баги `BoomerangProjectile`
- **Локация:** `Assets/_Scripts/Abilities/Active/BoomerangProjectile.cs:21-69`
- **Суть бага:**
  1. На развороте (`строки 59–60`) очищается `damagedEnemies.Clear()`, но **не сбрасывается `currentPierces = 0`**. Если бумеранг исчерпал лимит пробитий по пути туда, обратно он летит с нулевым уроном.
  2. `InitializeBoomerang()` не вызывает `base.Initialize()` и не ставит `spawnTime`, а `Update()` не проверяет `MAX_LIFETIME`. Если скорость героя превысит `travelSpeed` бумеранга, снаряд будет вечно гнаться за героем и никогда не вернётся в пул.

### 2.8. Микро-джиттер героя/камеры и невращающийся `FirePoint`
- **Локация:** `Assets/_Prefabs/Hero.prefab` (`Rigidbody.m_Interpolate: 0`, `FirePoint`), `Assets/_Scripts/Core/CameraController.cs:91-100`
- **Суть бага:**
  1. Герой двигается через `Rigidbody.linearVelocity` в `FixedUpdate` (50 Hz) с `Interpolate = None`, а камера жёстко снапается к `target.position + offset` в `LateUpdate` (`smoothTime = 0`) на 60/90/120 Hz. Это даёт заметное подёргивание картинки на мобильных экранах.
  2. `FirePoint` расположен в `(0, 1, 1)` как дочерний объект корня `Hero` (который не вращается, вращается только `Visuals`), поэтому снаряды всегда вылетают со смещением строго на `+1` по мировому `+Z`.
- **Решение:** Включить на `Hero.prefab` `Rigidbody.interpolation = RigidbodyInterpolation.Interpolate` и перенести `FirePoint` внутрь `Visuals` (либо в центр героя `(0, 1, 0)`).

### 2.9. Гонка корутин в `WaveManager` и устаревшее направление в `EnemyBase`
- **Локация:** `Assets/_Scripts/Enemies/WaveManager.cs:138-180`, `Assets/_Scripts/Enemies/EnemyBase.cs:35-69, 127`
- **Суть бага:**
  1. Если `enemiesToSpawn * spawnDelay > maxWaveDuration`, `WaveDurationCoroutine` вызывает `AdvanceWave()` и запускает новую `SpawnWaveCoroutine`, пока старая ещё не завершилась. Две корутины делят один флаг `isSpawning` и конфликтуют.
  2. В `EnemyBase.Initialize()` не сбрасываются `nextCheckTime = 0f` и `currentMoveDir = Vector3.zero`. Переиспользованный из пула враг в первые доли секунды может бежать в старом направлении из прошлой жизни.
  3. В `WaveManager.Start()` нет защиты от `null` или дубликатов в списке `enemyTypes` (`enemyPools.Add` бросит `ArgumentException`).

### 2.10. Сломанная формула скейлинга урона у `ShootingEnemy`
- **Локация:** `Assets/_Scripts/Enemies/ShootingEnemy.cs:162-164`
- **Суть бага:**
  ```csharp
  int finalDamage = Mathf.RoundToInt(
      shootingData.weaponData.damage * (1f + (currentDamage - shootingData.contactDamage) * 0.1f));
  ```
  Множитель урона снаряда выводится через разность `contactDamage` (у `BatData` `contactDamage = 5`), умноженную на `0.1f`. При `contactDamage == 0` урон вообще перестанет расти с волнами и элитностью, а при `5` растёт вдвое медленнее, чем `difficultyMultiplier`.
- **Решение:** Сохранять `protected float currentDamageMultiplier` в `EnemyBase` (и домножать в `MakeElite`) и применять его напрямую к `weaponData.damage`.

### 2.11. Залипание `VirtualJoystick` и смещение ручки при `normalizeWithDpi`
- **Локация:** `Assets/_Scripts/UI/VirtualJoystick.cs:64-70, 154-171`, `Assets/_Scripts/LevelUp/UpgradeManager.cs:75`
- **Суть бага:**
  1. Если во время удержания джойстика открывается панель LevelUp (`Time.timeScale = 0f`), событие `OnPointerUp` перехватывается панелью карт. `Direction` остаётся ненулевым, и после выбора карты герой сам бежит в последнем направлении. Нужен публичный `ResetJoystick()`, вызываемый при паузе/открытии модальных окон и в `OnDisable()`.
  2. При включённом `normalizeWithDpi` (`строки 64–70`) масштабируется сам `limit`, который затем используется и для `handle.anchoredPosition` (`строка 154`), из-за чего ручка вылетает за пределы визуального круга `background`.

### 2.12. Панель выбора карточек может открыться поверх Game Over
- **Локация:** `Assets/_Scripts/LevelUp/UpgradeManager.cs:70-82`, `Assets/_Scripts/LevelUp/XpCrystal.cs:23-38`
- **Суть бага:** `UpgradeManager.ProcessNextLevelUp()` вызывает `gameManager.PauseForLevelUp()` (который молча делает `return`, если уже `GameState.GameOver`), но не проверяет результат и всё равно открывает `cardUI.ShowCards(...)`. При этом `XpCrystal.Update()` продолжает выполняться при `Time.timeScale == 0f` и может засчитать уже подлетевший кристалл (`sqrMagnitude < 0.25f`) прямо на экране Game Over.

### 2.13. Проблемы UI ачивок (`AchievementScreenUI` и `AchievementPopupUI`)
- **Локация:** `Assets/_Scripts/UI/AchievementScreenUI.cs:28-34, 70-76`, `Assets/_Scripts/UI/AchievementPopupUI.cs:57-62`, `Assets/_Scripts/UI/AchievementItemUI.cs:78`
- **Суть багов:**
  1. `AchievementScreenUI` вызывает `RefreshUI()` только один раз в `Construct()`, но не в `OnEnable()` — данные на вкладке устаревают.
  2. Клик по **невыполненной** ачивке вызывает `rewardPopup.ShowReward(data)`, открывая окно с заголовком `"Reward Unlocked!"` (в `IMPLEMENTATION_PLAN.md:250` — только для завершённых).
  3. В `AchievementItemUI.cs:78` нет защиты от `data.targetValue <= 0` (деление на ноль).
  4. `AchievementPopupUI` не добавлен ни в `Gameplay.unity`, ни в `MainMenu.unity`, не зарегистрирован в `GameLifetimeScope`/`MainMenuLifetimeScope`, использует `WaitForSeconds` вместо `WaitForSecondsRealtime` (зависнет при `timeScale == 0`) и при одновременном анлоке 2 ачивок перезатирает первую через `StopAllCoroutines()` вместо очереди.

### 2.14. `HeroEconomy` отсутствует на `Hero.prefab`
- **Локация:** `Assets/_Prefabs/Hero.prefab`, `Assets/_Scenes/Gameplay.unity:4777-4848`
- **Суть бага:** Компонент `HeroEconomy` добавлен только как `m_AddedComponents` (scene override) в `Gameplay.unity`. При использовании `Hero.prefab` на любой новой сцене/арене `GameLifetimeScope` упадет при инъекции `HeroEconomy`.

---

## 3. Архитектурные дефекты

### 3.1. Разорванный мета-луп: `unlockedAbilityIds` не влияет на пул карточек
- **Локация:** `Assets/_Scripts/Meta/SaveData.cs:24`, `Assets/_Scripts/Meta/SaveManager.cs:161-168`, `Assets/_Scripts/LevelUp/UpgradeManager.cs:103-128`
- **Проблема:** `AchievementManager` и `DailyBonusManager` записывают разблокированные способности в `SaveData.unlockedAbilityIds`, но `UpgradeManager` даже не получает `SaveManager` через DI и никак не фильтрует `cardPool` по разблокировкам. Все способности доступны с первого запуска.
- **Решение:** Добавить в `AbilityData` флаг `bool unlockedByDefault = true`, инжектировать `SaveManager` в `UpgradeManager` и отфильтровывать заблокированные `AbilityCardData` в `GetRandomCards()`.

### 3.2. Рассинхронизация системы урона и статов
- **Локация:** `HeroCombat.cs:54-62`, `HeroAbilities.cs:31-58`, `UpgradeApplier.cs:77-87`, `LaserTurretAbilityData.cs:12`
- **Проблемы:**
  1. Карта `Stat_GlobalDamage` усиливает только `HeroCombat` (базовое оружие), но не влияет на `HeroAbilities` (все активные способности).
  2. `HeroCombat` скейлит урон мультипликативно (`damage * multiplier`), а `HeroAbilities` добавляет мета-бонус аддитивно один раз при выдаче способности от `activeData.baseDamage`.
  3. Для `LaserTurret` (`LaserTurretData.asset`) `baseDamage = 10`, но сама турель использует отдельное поле `dps = 15` — мета-апгрейд `ability_damage` считается от неверного поля.
  4. `StatType.Armor` в `UpgradeApplier.cs:84-87` — заглушка, которая только пишет в `Debug.Log`.
  5. `OrbitalCompanionAbility.cs:55-57` при `ModifierType.Area` меняет радиус хитбокса `currentArea`, но не обновляет визуальный скейл орбитальных объектов (и новые объекты в `SpawnInstances()` берут `OrbitalData.baseArea` вместо `currentArea`).
- **Решение:** Ввести единую модель рантайм-статов `HeroRuntimeStats` (глобальный множитель урона, урон способностей, броня, скорость, радиус магнита), от которой читают и `HeroCombat`, и `HeroAbilities`.

### 3.3. Дуализм событий (`GameEventBus` vs C# `event`)
- **Локация:** `Assets/_Scripts/Core/Events/GameEvents.cs`, `HeroHealth.cs`, `HeroExperience.cs`, `GameManager.cs`
- **Проблема:** Из 7 структур событий в `GameEventBus` **5 имеют 0 подписчиков** (`WaveCompletedEvent`, `HeroLeveledUpEvent`, `AbilityUnlockedEvent`, `HeroDiedEvent`, `CoinCollectedEvent`), при этом параллельно используются прямые C# `event` (`HeroHealth.OnDied`, `HeroExperience.OnLevelUp`, `GameManager.OnGameOver`).
- **Решение:** Принять единое правило: глобальные игровые события — через `GameEventBus`, локальные UI-биндинги — через события модели/ViewModel; удалить неиспользуемое дублирование.

### 3.4. Фрагментация DI и незадействованные хуки ачивок
- **Локация:** `RootLifetimeScope.cs`, `MainMenuLifetimeScope.cs`, `GameLifetimeScope.cs`, `AchievementManager.cs:137-145`
- **Проблемы:**
  1. `DailyBonusManager` зарегистрирован как `Lifetime.Scoped` только в `MainMenuLifetimeScope`, хотя относится к глобальной мете (`RootLifetimeScope`).
  2. В `AchievementManager` методы `OnBossDefeated()` и `OnAbilityUpgraded()` никем не вызываются, а условия `CollectAbilitiesInRun` и `NoDamageWaves` вообще не имеют триггеров.
  3. В `MainMenuUI.cs:72` бейдж ачивок жёстко отключён (`achievementsBadge.SetActive(false)`), вкладка «Способности» (`OnAbilitiesClicked`) закрывает все панели и оставляет пустой экран, «Настройки» — заглушка.
  4. `HeroAbilities.AddAbility()` (`HeroAbilities.cs:74-75`) публикует `AbilityUnlockedEvent` даже если способность не была добавлена (слоты заполнены или уже имеется).
  5. `UpgradeManager.GetRandomCards()` не проверяет `abilityCard.abilityData != null` и не страхуется от попадания `AbilityModifierCardData` в базовый `cardPool` без наличия базовой способности.
  6. `SerializableDictionary.OnAfterDeserialize()` (`SerializableDictionary.cs:29`) при повреждённом JSON с `keys.Count > values.Count` приведёт к `ArgumentOutOfRangeException`.
  7. `XpManager.cs:57` и `CoinManager.cs:51` присваивают `go.layer = LayerMask.NameToLayer("Pickups")` без проверки на `-1`.

### 3.5. Отсутствие Assembly Definitions (`.asmdef`) и автотестов
- **Проблема:** Весь проект компилируется в единый `Assembly-CSharp.dll`. В проекте **0 Unit / PlayMode тестов**, хотя есть сложная чистая логика (формулы XP, мета-апгрейды, `DailyBonusManager`, `SaveManager`, `UpgradeManager`, `SerializableDictionary`), которую идеально покрыть EditMode-тестами.
- **Решение:** Разбить код на сборки (`ProjectB.Data`, `ProjectB.Core`, `ProjectB.Meta`, `ProjectB.Combat`, `ProjectB.Abilities`, `ProjectB.UI`, `ProjectB.Editor`, `ProjectB.Tests`).

---

## 4. Проблемы данных, баланса и контента

### 4.1. Все 17 карточек имеют редкость `Common` (`rarity: 0`)
- **Локация:** `Assets/_Scripts/LevelUp/Cards/*.asset`
- **Проблема:** У всех карточек, включая `BlackHoleCard` (должна быть Legendary), `LaserTurretCard` (Epic), `FireballCard` / `IceAuraCard` / `BoomerangCard` (Rare), сериализовано `rarity: 0`. Вся система весов (`50 / 30 / 15 / 5`) и цветового кодирования рамок в `CardUI` фактически не работает.

### 4.2. Дебажные и несогласованные значения в боевых ассетах
- `Assets/_Prefabs/Hero.prefab`: `HeroExperience.baseXP: 1` (левелап с каждого первого врага; в коде `HeroExperience.cs:108` формула `level ^ 1.05` вместо `level ^ 1.5` из GDD).
- `Assets/_ScriptableObjects/WaveConfig.asset`: `minWaveForElites: 1` (по плану — с 10-й волны), `difficultyPerWave: 0.01` (+1% за волну — на 50-й волне враги всего в 1.49x сильнее).
- `Assets/_Scripts/Abilities/Data/FireballData.asset`: `explosionRadius: 10` (диаметр взрыва 20 метров при радиусе поиска цели 12; в коде дефолт `3f`).
- `Assets/_Scripts/LevelUp/Cards/LaserTurretMod_Damage.asset`: `value: 1` (+1 DPS при базе 15, тогда как для мечей `+10`).
- `Assets/_Scripts/LevelUp/Cards/OrbitalSmallSwordCard.asset`: не сериализовано `isConsumable: 1` (в отличие от остальных карт способностей).
- `Assets/_ScriptableObjects/EnemyShooterWeapon.asset`: `projectilePrefab: {fileID: 0}` (заставляет `ShootingEnemy` в рантайме создавать примитивы и клонировать материалы).
- `Assets/Resources/Achievements/`: создано всего **2 ачивки** (`Kill100Enemies`, `ReachWave10`) из запланированных 10–15.

### 4.3. Смешение языков и Legacy UI Text
- В карточках и интерфейсе вперемешку русский и английский: `Orbital Sword`, `Damage Up`, `+20 Max HP`, `Wave Reached`, `Completed!` рядом с `Получить: Стрелок`, `Лазерная Турель`, `День 1`.
- В `Gameplay.unity` на кнопках экрана Game Over (`RestartButton`, `MenuButton`, `ReviveButton`) используется устаревший `UnityEngine.UI.Text` (`Text (Legacy)`) вместо `TextMeshProUGUI`.

---

## 5. Нарушения правил проекта (`AGENTS.md`), настроек Android и гигиены

### 5.1. Нарушение правила Portrait Only
- **Локация:** `ProjectSettings/ProjectSettings.asset:11, 61-65`
- **Фактическое состояние:**
  ```yaml
  defaultScreenOrientation: 4          # AutoRotation (для Portrait нужно 0!)
  allowedAutorotateToPortrait: 1
  allowedAutorotateToPortraitUpsideDown: 1
  allowedAutorotateToLandscapeRight: 1 # Разрешён Landscape!
  allowedAutorotateToLandscapeLeft: 1  # Разрешён Landscape!
  useOSAutorotation: 1
  ```
- **Решение:** В Player Settings выставить `Default Orientation = Portrait` (`defaultScreenOrientation: 0`) и отключить `Landscape Left` / `Landscape Right` (и при желании `Portrait Upside Down`).

### 5.2. Незавершённые настройки Android-билда
- **Локация:** `ProjectSettings/ProjectSettings.asset:15, 172-184`
- **Проблемы:**
  - `AndroidTargetSdkVersion: 0` (Automatic) — Google Play требует явный Target API 34+ (отмечено `[x]` в плане, но не выставлено).
  - `applicationIdentifier.Android: com.UnityTechnologies.com.unity.template.urpblank` и `companyName: DefaultCompany` — шаблонные идентификаторы.

### 5.3. Нет адаптации под вырезы экрана (`SafeArea`)
- **Локация:** `ProjectSettings/ProjectSettings.asset:75` (`androidRenderOutsideSafeArea: 1`), `Assets/_Scripts/UI/` (0 упоминаний `Screen.safeArea`)
- **Проблема:** При поддержке экранов `9:19.5` / `9:20` (требование `AGENTS.md`) верхний HUD (`HpBar`, `XpBar`) и `TopBar` главного меню окажутся под камерой-вырезом / статус-баром.
- **Решение:** Добавить компонент `SafeAreaFitter` на корневой контейнер HUD и меню.

### 5.4. Нарушения структуры папок и лишние файлы/пакеты
- **ScriptableObjects внутри `_Scripts/`:** 7 `.asset` файлов в `Assets/_Scripts/Abilities/Data/` и 17 `.asset` файлов в `Assets/_Scripts/LevelUp/Cards/` — перенести в `Assets/_ScriptableObjects/Abilities/` и `Assets/_ScriptableObjects/Cards/` (обновив пути в `AbilitiesGenerator.cs`).
- **Две папки Editor:** `Assets/Editor/` (5 скриптов) и `Assets/_Scripts/Editor/` (8 скриптов) — объединить в `Assets/_Scripts/Editor/`.
- **Сломанные сцены в Build Settings:** в `ProjectSettings/EditorBuildSettings.asset` числятся отключённые несуществующие пути `Assets/Scenes/MetaScreen.unity` и `Assets/Scenes/SampleScene.unity`.
- **Лишние пакеты и шаблоны:** `Assets/TutorialInfo/`, `Assets/TextMesh Pro/Examples & Extras/`, а в `Packages/manifest.json` — неиспользуемые `com.unity.visualscripting`, `com.unity.multiplayer.center`, `com.unity.ai.navigation`, `com.unity.timeline`.

---

## 6. Сверка с MVP Scope (`CONTEXT.md`)

| Элемент MVP | Требование GDD (`CONTEXT.md`) | Текущее состояние | Статус |
|---|---|---|:---:|
| Герой (Рыцарь) | 1 герой, воксельная модель, движение, автоатака | Есть префаб с воксельной моделью и автоатакой, нет ульты (щит 5с) и пассивки (+20% HP) | 🟡 Частично |
| Способности | 8 способностей (4 активные + 4 пассивные) | 7 активных (часть с багами/плейсхолдерами), 0 пассивных | 🟡 Частично |
| Карточки 2+1 | 2 бесплатные + 1 случайная за рекламу | Выдаются 3 бесплатные, слот рекламы — неактивная заглушка `AdPlaceholder` | 🔴 Не соответствует |
| Волны и боссы | Бесконечные волны, мини-босс каждые 5, босс (Голем/Некромант) каждые 25 | Обычные + элитные волны; кода боссов и HP-бара босса нет | 🔴 Отсутствует |
| Синергии и эволюции | Парные синергии и эволюции способностей | Не реализовано | 🔴 Отсутствует |
| Секретные предметы | Сундук, древний свиток, загадочный кристалл на арене | Не реализовано | 🔴 Отсутствует |
| Мета-прокачка | 7 перманентных апгрейдов за монеты | Реализовано 7 апгрейдов (баги в `MagnetRadius` и `StartLevel`) | 🟡 Требует фиксов |
| Ачивки и разблокировки | 10–15 ачивок, разблокировка способностей, попап, экран коллекции | Каркас есть, 2 ачивки, анлок не влияет на пул, попап не подключён, нет экрана способностей | 🟡 Частично |
| Ежедневный бонус | 7-дневный цикл + удвоение за рекламу | Реализовано (off-by-one баг в UI, конфликт правила пропуска дня, реклама-заглушка) | 🟡 Требует фиксов |
| Боевой HUD | HP, XP, уровень, волна, монеты, иконки скиллов, ульта, джойстик | Есть только HP, XP/уровень и джойстик; нет волны, монет, иконок скиллов, ульты | 🟡 Неполный |
| Game Feel (VFX/SFX) | Числа урона, партиклы, базовые звуки и музыка | 0 `AudioSource`, нет чисел урона, нет партиклов/хит-флеша | 🔴 Отсутствует |
| Реклама (AdMob) | Rewarded (3-я карта, x2 монеты, воскрешение, дейли) + Interstitial | Папка `Assets/_Scripts/Ads` пуста (0 файлов) | 🔴 Не начато |

---

## 7. Пошаговый план исправлений (Actionable Roadmap)

### Этап P0 — Стабилизация и мобильная производительность (сделать до новых фич)
1. **Убрать 50 000 `Instantiate` травы** в `ArenaConfig.asset` / `ArenaGenerator.cs` (временно снизить количество, затем перевести на `RenderMeshInstanced` или чанки).
2. **Убрать `PlayerPrefs.Save()` из боя:** накапливать прогресс киллов/статистики в памяти, сохранять при окончании забега, выходе в меню и `OnApplicationPause(true)`; убрать двойные/тройные вызовы `Save()`.
3. **Починить префабы:**
   - `Hero.prefab`: `pickupLayer = Pickups`, `baseXP = 10`, `Rigidbody.Interpolate = Interpolate`, добавить `HeroEconomy` в сам префаб, перенести `FirePoint`.
   - `Slime.prefab` / `BatShooter.prefab`: добавить kinematic `Rigidbody`.
4. **Устранить утечки материалов и шейдер-блоат:** `MaterialPropertyBlock` в `EnemyBase`, префабы/shared-материалы для снарядов и аур, удалить `Shader.Find` из рантайма и `AddLitShaderFixer.cs` (вычистить `URP/Lit` из `Always Included Shaders`).
5. **Оптимизировать горячие циклы:** таймеры сканирования в `OrbitalCompanionAbility`, `BlackHoleAbility`, `LaserTurret`, `HeroExperience`, `WeaponController`; общий `EnemyProjectilePool` вместо пула в каждом `ShootingEnemy`; кеш `LayerMask` и `IDamageable`; отключить `Update()` у неактивных пикапов; убрать боевые `Debug.Log`.
6. **Починить критические геймплейные баги:**
   - `BlackHoleAbility` (спавнить в точке врага или не затягивать в контактный радиус героя).
   - `MagnetRadius` (`*= 1 + bonus/100f`) и `StartLevel` (выдавать стартовые карты).
   - `DailyBonusScreenUI` (исправить off-by-one + `InvariantCulture` + согласовать правило стрика с GDD).
   - `VoxelMeshAnimator` (кешировать `baseLocalPosition` в `Awake`, восстанавливать в `OnEnable`/`Stop`).
   - `BoomerangProjectile` (сброс `currentPierces` на возврате + `MAX_LIFETIME`), `ShooterAbility`/`FireballAbility` (разные цели / взрыв в последней точке).
   - `VirtualJoystick.ResetJoystick()` на паузе, защита `UpgradeManager` и `XpCrystal` от срабатывания при `GameState.GameOver`, исправление гонки корутин в `WaveManager` и скейлинга урона в `ShootingEnemy`.
7. **Настройки Android и гигиена:** `defaultScreenOrientation = Portrait (0)` без Landscape, `AndroidTargetSdkVersion = 34+`, свой `applicationIdentifier`, `SafeAreaFitter` на UI, удалить `Assets/TextMesh Pro/Examples & Extras` и `Assets/TutorialInfo`.
8. **Контрольная точка:** собрать APK и замерить FPS/загрузку на реальном Android-устройстве при 100+ врагах.

### Этап P1 — Архитектурная целостность и баланс данных
1. **Замкнуть мета-луп:** фильтровать `UpgradeManager` по `SaveData.unlockedAbilityIds` + флаг `unlockedByDefault` на `AbilityData`.
2. **Единые статы героя (`HeroRuntimeStats`):** объединить модификаторы урона `HeroCombat` и `HeroAbilities`, исправить `LaserTurret` (`dps` vs `baseDamage`), реализовать `StatType.Armor`.
3. **Починить ачивки:** подключить `AchievementPopupUI` в сцены/DI с `WaitForSecondsRealtime` и очередью, обновлять `AchievementScreenUI` в `OnEnable()`, запретить показ «Reward Unlocked!» для закрытых ачивок, подключить хуки `MaxAbilityLevel` / `CollectAbilitiesInRun` / `NoDamageWaves`.
4. **Привести в порядок данные:** проставить корректные `rarity` на всех 17 карточках, вычистить дебажные значения в `WaveConfig.asset` и `FireballData.asset`, выровнять формулу XP (`^ 1.5`), унифицировать язык текстов и заменить Legacy `Text` на TMP.
5. **Структура кода:** вынести `.asset` из `_Scripts/` в `_ScriptableObjects/`, объединить папки `Editor`, ввести `.asmdef` и базовые EditMode-тесты для `SaveManager`, `MetaUpgradeManager`, `DailyBonusManager`, `UpgradeManager`.

### Этап P2 — Доведение до MVP Scope (`CONTEXT.md`)
1. Переделать LevelUp UI под формат **2 бесплатные + 1 карта за рекламу**.
2. Дополнить боевой HUD (волна, монеты, иконки активных скиллов, кнопка ульты Рыцаря) и экран коллекции способностей.
3. Реализовать мини-боссов (каждые 5 волн) и 2 боссов (каждые 25 волн) с отдельным HP-баром.
4. Добавить 4 пассивные способности, синергии и 3 секретных предмета на арене.
5. Заполнить 10–15 ачивок с реальными разблокировками.
6. Добавить базовый Game Feel: пул чисел урона, партиклы смерти/попадания/levelup, `AudioManager` со звуками и музыкой.
7. Интегрировать `AdManager` (AdMob: 3-я карта, x2 монеты, воскрешение, дейли-бонус, интерстишелы).
