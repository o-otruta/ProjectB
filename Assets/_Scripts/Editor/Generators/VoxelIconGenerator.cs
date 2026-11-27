using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using ProjectB.Abilities;
using ProjectB.Data;
using ProjectB.Data.Enemies;
using ProjectB.Data.Meta;
using ProjectB.LevelUp;

namespace ProjectB.EditorScripts
{
    /// <summary>
    /// Generates crisp 64x64 pixel/voxel UI icons and the tiled voxel floor grid texture,
    /// and automatically assigns them across all ScriptableObjects and FloorMat.mat.
    /// </summary>
    public static class VoxelIconGenerator
    {
        private const int GRID_SIZE = 16;
        private const int UPSCALE = 4;
        private const int ICON_SIZE = GRID_SIZE * UPSCALE; // 64x64
        private const string ICON_OUTPUT_DIR = "Assets/_UI/Icons/Generated";
        private const string FLOOR_TEX_PATH = "Assets/_Materials/FloorGrid.png";
        private const string FLOOR_MAT_PATH = "Assets/_Materials/FloorMat.mat";

        private static readonly Color32 Transparent = new Color32(0, 0, 0, 0);
        private static readonly Color32 Outline = new Color32(18, 20, 28, 255);
        private static readonly Color32 FrameDark = new Color32(34, 38, 52, 255);
        private static readonly Color32 FrameBg = new Color32(26, 30, 42, 235);
        private static readonly Color32 WhiteHi = new Color32(248, 250, 255, 255);
        private static readonly Color32 Silver = new Color32(198, 208, 224, 255);
        private static readonly Color32 SteelDark = new Color32(110, 122, 146, 255);
        private static readonly Color32 Gold = new Color32(255, 206, 56, 255);
        private static readonly Color32 GoldHi = new Color32(255, 244, 150, 255);
        private static readonly Color32 GoldDark = new Color32(188, 120, 22, 255);
        private static readonly Color32 Brown = new Color32(142, 86, 44, 255);
        private static readonly Color32 BrownDark = new Color32(92, 52, 24, 255);
        private static readonly Color32 FireRed = new Color32(236, 72, 46, 255);
        private static readonly Color32 FireOrange = new Color32(255, 148, 36, 255);
        private static readonly Color32 IceBlue = new Color32(94, 214, 255, 255);
        private static readonly Color32 IceDark = new Color32(42, 134, 212, 255);
        private static readonly Color32 Purple = new Color32(168, 78, 242, 255);
        private static readonly Color32 PurpleDark = new Color32(92, 34, 156, 255);
        private static readonly Color32 Green = new Color32(82, 218, 108, 255);
        private static readonly Color32 GreenDark = new Color32(38, 142, 62, 255);
        private static readonly Color32 Crimson = new Color32(224, 52, 76, 255);
        private static readonly Color32 CrimsonDark = new Color32(148, 24, 46, 255);
        private static readonly Color32 Cyan = new Color32(64, 232, 220, 255);

        private static readonly Dictionary<string, string[]> Templates = BuildTemplates();

        [MenuItem("ProjectB/Generators/Generate All Voxel Icons")]
        public static void GenerateAllIcons()
        {
            EnsureFolder(ICON_OUTPUT_DIR);

            var spriteCache = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

            // Compass UI arrow & HUD slot lock icon
            GetOrCreateSprite("ui_compass_arrow", framed: false, spriteCache);
            GetOrCreateSprite("ui_slot_lock", framed: false, spriteCache);

            // 1. Abilities
            var abilityIconsById = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
            foreach (string guid in AssetDatabase.FindAssets("t:AbilityData", new[] { "Assets/_ScriptableObjects/Abilities" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var ability = AssetDatabase.LoadAssetAtPath<AbilityData>(path);
                if (ability == null) continue;

                string templateKey = ResolveAbilityTemplateKey(ability.id);
                Sprite sprite = GetOrCreateSprite($"ability_{ability.id}", framed: true, spriteCache, templateKey, GetRarityFrameColor(ability.rarity));
                ability.icon = sprite;
                EditorUtility.SetDirty(ability);
                if (!string.IsNullOrEmpty(ability.id))
                {
                    abilityIconsById[ability.id] = sprite;
                }
            }

            // 2. Cards
            foreach (string guid in AssetDatabase.FindAssets("t:CardData", new[] { "Assets/_ScriptableObjects/Cards" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var card = AssetDatabase.LoadAssetAtPath<CardData>(path);
                if (card == null) continue;

                Sprite sprite = null;
                if (card is AbilityCardData abilityCard && abilityCard.abilityData != null)
                {
                    sprite = abilityCard.abilityData.icon;
                    if (sprite == null)
                    {
                        string key = ResolveAbilityTemplateKey(abilityCard.abilityData.id);
                        sprite = GetOrCreateSprite($"ability_{abilityCard.abilityData.id}", framed: true, spriteCache, key, GetRarityFrameColor(abilityCard.rarity));
                    }
                }
                else if (card is AbilityModifierCardData modCard)
                {
                    string baseKey = ResolveAbilityTemplateKey(modCard.targetAbilityId);
                    string spriteName = $"card_mod_{modCard.targetAbilityId}_{modCard.modifierType}";
                    sprite = GetOrCreateModifierSprite(spriteName, baseKey, modCard.modifierType, GetRarityFrameColor(modCard.rarity), spriteCache);
                }
                else if (card is StatUpgradeCardData statCard)
                {
                    string key = ResolveStatTemplateKey(statCard.statType);
                    sprite = GetOrCreateSprite($"card_stat_{statCard.statType}", framed: true, spriteCache, key, GetRarityFrameColor(statCard.rarity));
                }
                else if (card is WeaponUpgradeCardData weaponCard)
                {
                    string key = weaponCard.upgradeType switch
                    {
                        WeaponUpgradeType.Multishot => "shooter",
                        WeaponUpgradeType.FireRate => "swift_boots",
                        WeaponUpgradeType.Radius => "area_tome",
                        _ => "battle_fury"
                    };
                    sprite = GetOrCreateSprite($"card_weapon_{weaponCard.upgradeType}", framed: true, spriteCache, key, GetRarityFrameColor(weaponCard.rarity));
                }

                if (sprite != null)
                {
                    card.icon = sprite;
                    EditorUtility.SetDirty(card);
                }
            }

            // 3. Heroes
            foreach (string guid in AssetDatabase.FindAssets("t:HeroData", new[] { "Assets/_ScriptableObjects" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var hero = AssetDatabase.LoadAssetAtPath<HeroData>(path);
                if (hero == null) continue;

                string heroKey = hero.id switch
                {
                    "mage" => "hero_mage",
                    "ranger" => "hero_ranger",
                    "necromancer" => "hero_necromancer",
                    _ => "hero_knight"
                };
                string ultKey = hero.ultimateType switch
                {
                    HeroUltimateType.MeteorStrike => "ult_meteor",
                    HeroUltimateType.ArrowRain => "ult_arrow_rain",
                    HeroUltimateType.SoulNova => "ult_soul_nova",
                    _ => "ult_shield"
                };
                Color32 frameCol = hero.id switch
                {
                    "mage" => Purple,
                    "ranger" => Green,
                    "necromancer" => Cyan,
                    _ => IceBlue
                };
                Color32 ultFrameCol = hero.ultimateType switch
                {
                    HeroUltimateType.MeteorStrike => FireOrange,
                    HeroUltimateType.ArrowRain => Green,
                    HeroUltimateType.SoulNova => Cyan,
                    _ => IceBlue
                };

                hero.icon = GetOrCreateSprite($"hero_{hero.id}", framed: true, spriteCache, heroKey, frameCol);
                hero.ultimateIcon = GetOrCreateSprite($"ult_{hero.id}", framed: true, spriteCache, ultKey, ultFrameCol);
                EditorUtility.SetDirty(hero);
            }

            // 4. Achievements
            foreach (string guid in AssetDatabase.FindAssets("t:AchievementData", new[] { "Assets/Resources/Achievements" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var ach = AssetDatabase.LoadAssetAtPath<AchievementData>(path);
                if (ach == null) continue;

                string key = ResolveAchievementTemplateKey(ach);
                Color32 frameCol = ach.rewardType switch
                {
                    AchievementRewardType.UnlockHero => Gold,
                    AchievementRewardType.UnlockAbility => Purple,
                    AchievementRewardType.UnlockActiveSlot => FireOrange,
                    AchievementRewardType.UnlockPassiveSlot => Cyan,
                    AchievementRewardType.GiveShards => Purple,
                    AchievementRewardType.GiveCoins => Gold,
                    _ => Cyan
                };

                ach.icon = GetOrCreateSprite($"ach_{ach.id}", framed: true, spriteCache, key, frameCol);
                EditorUtility.SetDirty(ach);
            }

            // 5. Meta Upgrades
            foreach (string guid in AssetDatabase.FindAssets("t:MetaUpgradeData", new[] { "Assets/Resources/MetaUpgrades" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var meta = AssetDatabase.LoadAssetAtPath<MetaUpgradeData>(path);
                if (meta == null) continue;

                string key = ResolveMetaUpgradeTemplateKey(meta.effectType);
                meta.icon = GetOrCreateSprite($"meta_{meta.id}", framed: true, spriteCache, key, Gold);
                EditorUtility.SetDirty(meta);
            }

            // 6. Daily Bonus Rewards
            var dailyBonus = AssetDatabase.LoadAssetAtPath<DailyBonusRewardData>("Assets/Resources/DailyBonusRewardData.asset");
            if (dailyBonus != null && dailyBonus.rewards != null)
            {
                for (int i = 0; i < dailyBonus.rewards.Length; i++)
                {
                    var reward = dailyBonus.rewards[i];
                    string key;
                    Color32 frameCol;
                    if (reward.rewardType == DailyBonusRewardType.Ability && !string.IsNullOrEmpty(reward.abilityId))
                    {
                        key = ResolveAbilityTemplateKey(reward.abilityId);
                        frameCol = Purple;
                    }
                    else if (i >= 6)
                    {
                        key = "secret_chest";
                        frameCol = Gold;
                    }
                    else if (i >= 3)
                    {
                        key = "coin_stack";
                        frameCol = Gold;
                    }
                    else
                    {
                        key = "coin_single";
                        frameCol = Gold;
                    }

                    reward.icon = GetOrCreateSprite($"daily_day_{i + 1}", framed: true, spriteCache, key, frameCol);
                    dailyBonus.rewards[i] = reward;
                }
                EditorUtility.SetDirty(dailyBonus);
            }

            // 7. Constellation Talent Nodes (Sprint 11)
            foreach (string guid in AssetDatabase.FindAssets("t:TalentNodeData", new[] { "Assets/_ScriptableObjects/Talents" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var node = AssetDatabase.LoadAssetAtPath<TalentNodeData>(path);
                if (node == null) continue;

                string key = ResolveTalentTemplateKey(node);
                Color32 frameCol = node.nodeType switch
                {
                    TalentNodeType.Synergy => Cyan,
                    TalentNodeType.Keystone => FireOrange,
                    TalentNodeType.Origin => Purple,
                    TalentNodeType.Mastery => FireOrange,
                    _ => node.constellation switch
                    {
                        TalentConstellation.Fury => Crimson,
                        TalentConstellation.Bastion => IceBlue,
                        TalentConstellation.Fortune => Gold,
                        _ => Purple
                    }
                };
                node.icon = GetOrCreateSprite($"talent_{node.id}", framed: true, spriteCache, key, frameCol);
                EditorUtility.SetDirty(node);
            }

            // 8. Enemies (Sprint 16.2)
            foreach (string guid in AssetDatabase.FindAssets("t:EnemyData", new[] { "Assets/_ScriptableObjects/Enemies" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var enemy = AssetDatabase.LoadAssetAtPath<EnemyData>(path);
                if (enemy == null) continue;

                string key = ResolveEnemyTemplateKey(enemy.name);
                Color32 frameCol = enemy switch
                {
                    SiegeGolemiteEnemyData => FireOrange,
                    FrostWispEnemyData => IceBlue,
                    SplitterEnemyData => Green,
                    SummonerEnemyData => Purple,
                    ShieldedEnemyData => Silver,
                    ExploderEnemyData => FireRed,
                    FleeingLootEnemyData => Gold,
                    _ => Crimson
                };
                enemy.icon = GetOrCreateSprite($"enemy_{enemy.name}", framed: true, spriteCache, key, frameCol);
                EditorUtility.SetDirty(enemy);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[VoxelIconGenerator] Generated {spriteCache.Count} voxel sprites and assigned icons to all ScriptableObjects.");
        }

        [MenuItem("ProjectB/Generators/Generate Floor Grid Texture")]
        public static void GenerateFloorGridTexture()
        {
            const int size = 64;
            const int half = 32;
            var pixels = new Color32[size * size];

            Color32 grassA = new Color32(72, 138, 62, 255);
            Color32 grassALight = new Color32(82, 150, 70, 255);
            Color32 grassADark = new Color32(64, 126, 55, 255);

            Color32 grassB = new Color32(63, 124, 54, 255);
            Color32 grassBLight = new Color32(72, 136, 62, 255);
            Color32 grassBDark = new Color32(55, 112, 47, 255);

            var rng = new System.Random(4207);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool tileA = ((x / half) + (y / half)) % 2 == 0;
                    int lx = x % half;
                    int ly = y % half;
                    bool isSeam = lx == 0 || ly == 0;

                    Color32 baseCol = tileA ? grassA : grassB;
                    if (isSeam)
                    {
                        baseCol = tileA ? grassADark : grassBDark;
                    }
                    else if (((x / 4) + (y / 4)) % 7 == 0 && rng.NextDouble() < 0.35)
                    {
                        baseCol = tileA ? grassALight : grassBLight;
                    }
                    else if (rng.NextDouble() < 0.08)
                    {
                        baseCol = tileA ? grassADark : grassBDark;
                    }

                    pixels[y * size + x] = baseCol;
                }
            }

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: true);
            tex.SetPixels32(pixels);
            tex.Apply();

            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), FLOOR_TEX_PATH);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllBytes(fullPath, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(FLOOR_TEX_PATH, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(FLOOR_TEX_PATH) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.filterMode = FilterMode.Point;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.mipmapEnabled = true;
                importer.anisoLevel = 4;
                importer.sRGBTexture = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            Texture2D floorTex = AssetDatabase.LoadAssetAtPath<Texture2D>(FLOOR_TEX_PATH);
            Material floorMat = AssetDatabase.LoadAssetAtPath<Material>(FLOOR_MAT_PATH);
            if (floorMat != null && floorTex != null)
            {
                floorMat.SetTexture("_BaseMap", floorTex);
                floorMat.SetTextureScale("_BaseMap", new Vector2(125f, 125f));
                if (floorMat.HasProperty("_MainTex"))
                {
                    floorMat.SetTexture("_MainTex", floorTex);
                    floorMat.SetTextureScale("_MainTex", new Vector2(125f, 125f));
                }
                floorMat.SetColor("_BaseColor", Color.white);
                if (floorMat.HasProperty("_Color"))
                {
                    floorMat.SetColor("_Color", Color.white);
                }
                floorMat.enableInstancing = true;
                EditorUtility.SetDirty(floorMat);
                AssetDatabase.SaveAssets();
            }

            Debug.Log("[VoxelIconGenerator] Generated FloorGrid.png and updated FloorMat.mat.");
        }

        private static Sprite GetOrCreateSprite(
            string fileName,
            bool framed,
            Dictionary<string, Sprite> cache,
            string templateKey = null,
            Color32 frameColor = default)
        {
            if (cache.TryGetValue(fileName, out var cached))
            {
                return cached;
            }

            string key = string.IsNullOrEmpty(templateKey) ? fileName : templateKey;
            if (!Templates.TryGetValue(key, out string[] grid))
            {
                grid = Templates["fallback_star"];
            }

            Color32[,] canvas = RenderTemplate(grid, framed, frameColor);
            Sprite sprite = WriteAndImportSprite(fileName, canvas);
            cache[fileName] = sprite;
            return sprite;
        }

        private static Sprite GetOrCreateModifierSprite(
            string fileName,
            string baseTemplateKey,
            ModifierType modifierType,
            Color32 frameColor,
            Dictionary<string, Sprite> cache)
        {
            if (cache.TryGetValue(fileName, out var cached))
            {
                return cached;
            }

            if (!Templates.TryGetValue(baseTemplateKey, out string[] grid))
            {
                grid = Templates["fallback_star"];
            }

            Color32[,] canvas = RenderTemplate(grid, framed: true, frameColor);
            ApplyModifierBadge(canvas, modifierType);
            Sprite sprite = WriteAndImportSprite(fileName, canvas);
            cache[fileName] = sprite;
            return sprite;
        }

        private static Color32[,] RenderTemplate(string[] rows, bool framed, Color32 frameColor)
        {
            var buf = new Color32[GRID_SIZE, GRID_SIZE];
            for (int y = 0; y < GRID_SIZE; y++)
            {
                for (int x = 0; x < GRID_SIZE; x++)
                {
                    if (!framed)
                    {
                        buf[x, y] = Transparent;
                    }
                    else if (x == 0 || y == 0 || x == GRID_SIZE - 1 || y == GRID_SIZE - 1)
                    {
                        buf[x, y] = Outline;
                    }
                    else if (x == 1 || y == 1 || x == GRID_SIZE - 2 || y == GRID_SIZE - 2)
                    {
                        buf[x, y] = frameColor.a > 0 ? frameColor : FrameDark;
                    }
                    else
                    {
                        buf[x, y] = FrameBg;
                    }
                }
            }

            for (int row = 0; row < GRID_SIZE && row < rows.Length; row++)
            {
                string line = rows[row];
                int y = GRID_SIZE - 1 - row;
                for (int col = 0; col < GRID_SIZE && col < line.Length; col++)
                {
                    char ch = line[col];
                    if (ch == '.' || ch == ' ') continue;
                    Color32 c = MapPaletteChar(ch);
                    if (c.a > 0)
                    {
                        buf[col, y] = c;
                    }
                }
            }

            return buf;
        }

        private static void ApplyModifierBadge(Color32[,] buf, ModifierType type)
        {
            Color32 badgeColor = type switch
            {
                ModifierType.Damage => Crimson,
                ModifierType.Count => Cyan,
                ModifierType.Cooldown => Green,
                ModifierType.Radius => Gold,
                ModifierType.Area => Gold,
                ModifierType.Duration => Purple,
                ModifierType.Speed => IceBlue,
                ModifierType.PierceCount => FireOrange,
                ModifierType.SlowAmount => IceBlue,
                ModifierType.CritMultiplier => FireOrange,
                _ => Gold
            };

            // 6x6 badge anchored at bottom-right inside the frame (x: 9..14, y: 1..6)
            for (int y = 1; y <= 6; y++)
            {
                for (int x = 9; x <= 14; x++)
                {
                    bool border = x == 9 || x == 14 || y == 1 || y == 6;
                    buf[x, y] = border ? Outline : FrameDark;
                }
            }

            // Crisp micro glyph inside (x: 10..13, y: 2..5)
            switch (type)
            {
                case ModifierType.Cooldown:
                case ModifierType.Speed:
                    // Lightning / chevron
                    buf[12, 5] = badgeColor;
                    buf[11, 4] = badgeColor;
                    buf[12, 4] = badgeColor;
                    buf[11, 3] = badgeColor;
                    buf[12, 3] = badgeColor;
                    buf[11, 2] = badgeColor;
                    break;

                case ModifierType.Radius:
                case ModifierType.Area:
                    // Outward corner brackets
                    buf[10, 5] = badgeColor; buf[13, 5] = badgeColor;
                    buf[11, 4] = badgeColor; buf[12, 4] = badgeColor;
                    buf[11, 3] = badgeColor; buf[12, 3] = badgeColor;
                    buf[10, 2] = badgeColor; buf[13, 2] = badgeColor;
                    break;

                case ModifierType.Duration:
                    // Hourglass
                    buf[10, 5] = badgeColor; buf[11, 5] = badgeColor; buf[12, 5] = badgeColor; buf[13, 5] = badgeColor;
                    buf[11, 4] = badgeColor; buf[12, 4] = badgeColor;
                    buf[11, 3] = badgeColor; buf[12, 3] = badgeColor;
                    buf[10, 2] = badgeColor; buf[11, 2] = badgeColor; buf[12, 2] = badgeColor; buf[13, 2] = badgeColor;
                    break;

                default:
                    // Plus symbol (+)
                    buf[11, 5] = badgeColor; buf[12, 5] = badgeColor;
                    buf[10, 4] = badgeColor; buf[11, 4] = badgeColor; buf[12, 4] = badgeColor; buf[13, 4] = badgeColor;
                    buf[10, 3] = badgeColor; buf[11, 3] = badgeColor; buf[12, 3] = badgeColor; buf[13, 3] = badgeColor;
                    buf[11, 2] = badgeColor; buf[12, 2] = badgeColor;
                    break;
            }
        }

        private static Sprite WriteAndImportSprite(string fileName, Color32[,] grid)
        {
            var pixels = new Color32[ICON_SIZE * ICON_SIZE];
            for (int gy = 0; gy < GRID_SIZE; gy++)
            {
                for (int gx = 0; gx < GRID_SIZE; gx++)
                {
                    Color32 c = grid[gx, gy];
                    int baseX = gx * UPSCALE;
                    int baseY = gy * UPSCALE;
                    for (int dy = 0; dy < UPSCALE; dy++)
                    {
                        int rowOffset = (baseY + dy) * ICON_SIZE + baseX;
                        for (int dx = 0; dx < UPSCALE; dx++)
                        {
                            pixels[rowOffset + dx] = c;
                        }
                    }
                }
            }

            var tex = new Texture2D(ICON_SIZE, ICON_SIZE, TextureFormat.RGBA32, mipChain: false);
            tex.SetPixels32(pixels);
            tex.Apply();

            string assetPath = $"{ICON_OUTPUT_DIR}/{fileName}.png";
            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
            File.WriteAllBytes(fullPath, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = ICON_SIZE;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        private static Color32 MapPaletteChar(char ch)
        {
            return ch switch
            {
                'K' => Outline,
                'W' => WhiteHi,
                'S' => Silver,
                'D' => SteelDark,
                'G' => Gold,
                'H' => GoldHi,
                'A' => GoldDark,
                'B' => Brown,
                'N' => BrownDark,
                'R' => FireRed,
                'O' => FireOrange,
                'I' => IceBlue,
                'J' => IceDark,
                'P' => Purple,
                'Q' => PurpleDark,
                'E' => Green,
                'F' => GreenDark,
                'C' => Crimson,
                'M' => CrimsonDark,
                'Y' => Cyan,
                _ => Transparent
            };
        }

        private static Color32 GetRarityFrameColor(CardRarity rarity)
        {
            return rarity switch
            {
                CardRarity.Common => Silver,
                CardRarity.Rare => IceBlue,
                CardRarity.Epic => Purple,
                CardRarity.Legendary => Gold,
                _ => Silver
            };
        }

        private static string ResolveAbilityTemplateKey(string id)
        {
            return id switch
            {
                "orbital_small_sword" => "sword",
                "Shooter" => "shooter",
                "Fireball" => "fireball",
                "IceAura" => "ice_aura",
                "Boomerang" => "boomerang",
                "LaserTurret" => "laser_turret",
                "BlackHole" => "black_hole",
                "BattleFury" => "battle_fury",
                "IronSkin" => "iron_skin",
                "SwiftBoots" => "swift_boots",
                "ChainLightning" => "chain_lightning",
                "PoisonAlchemist" => "poison_flask",
                "GuardianShield" => "guardian_shield",
                "MiniDragon" => "mini_dragon",
                "VoxelTornado" => "voxel_tornado",
                "EarthquakeSlam" => "seismic_hammer",
                "ChronoTome" => "chrono_tome",
                "CriticalLens" => "critical_lens",
                "TitanHeart" => "titan_heart",
                "GoldenIdol" => "golden_idol",
                "BuzzsawChakram" => "buzzsaw_chakram",
                "SkyfallSpire" => "skyfall_spire",
                "GlacialShatter" => "glacial_shatter",
                "ShadowDecoy" => "shadow_decoy",
                "VampiricFang" => "vampiric_fang",
                "AreaTome" => "area_tome",
                _ => "fallback_star"
            };
        }

        private static string ResolveTalentTemplateKey(TalentNodeData node)
        {
            if (node.nodeType == TalentNodeType.Origin) return "boss_shard";
            if (node.nodeType == TalentNodeType.Synergy) return ResolveSynergyTemplateKey(node.synergyId);
            if (node.nodeType == TalentNodeType.Mastery)
            {
                // Path of Mastery: Lv 10 Overdrive → Lv 20 Awakened → Lv 50 Mythic.
                float cap = node.GetEffectValue(TalentEffectType.AbilityMaxLevel);
                return cap >= 30f ? "skyfall_spire" : (cap >= 20f ? "synergy_blades" : "sword");
            }
            if (node.nodeType == TalentNodeType.Keystone)
            {
                if (node.GetEffectValue(TalentEffectType.SecondWind) > 0f) return "ult_shield";
                if (node.GetEffectValue(TalentEffectType.BonusPassiveSlots) > 0f) return "golden_idol";
                return "slot_active";
            }

            TalentEffectType primary = node.effects != null && node.effects.Count > 0 ? node.effects[0].type : TalentEffectType.None;
            return primary switch
            {
                TalentEffectType.CritChancePercent => "critical_lens",
                TalentEffectType.GlobalDamagePercent => "battle_fury",
                TalentEffectType.GlobalAreaPercent => "area_tome",
                TalentEffectType.BossDamagePercent => "boss_golem",
                TalentEffectType.CooldownReductionPercent => "chrono_tome",
                TalentEffectType.ArmorFlat => "iron_skin",
                TalentEffectType.HealingPercent => "vitality_regen",
                TalentEffectType.IFrameSeconds => "guardian_shield",
                TalentEffectType.UltimateCooldownPercent => "ult_meteor",
                TalentEffectType.CoinGainPercent => "coin_stack",
                TalentEffectType.FreeRerolls => "tome_arsenal",
                TalentEffectType.BarrelRespawnBonus => "ult_meteor",
                TalentEffectType.SecretAndShrineRewardPercent => "secret_chest",
                TalentEffectType.MaxHpPercent => "titan_heart",
                TalentEffectType.MoveSpeedPercent => "swift_boots",
                TalentEffectType.XpGainPercent => "secret_crystal",
                TalentEffectType.BonusActiveSlots => "slot_active",
                TalentEffectType.BonusPassiveSlots => "slot_passive",
                _ => "boss_shard"
            };
        }

        private static string ResolveStatTemplateKey(StatType statType)
        {
            return statType switch
            {
                StatType.MaxHP => "vitality_regen",
                StatType.MoveSpeed => "swift_boots",
                StatType.GlobalDamage => "battle_fury",
                StatType.Armor => "iron_skin",
                StatType.MagnetRadius => "magnet",
                _ => "fallback_star"
            };
        }

        private static string ResolveSynergyTemplateKey(string synergyId)
        {
            return synergyId switch
            {
                "steam_blast" => "synergy_steam",
                "blade_storm" => "synergy_blades",
                "overcharged_grid" => "synergy_grid",
                "thunder_storm" => "synergy_thunderstorm",
                "toxic_inferno" => "synergy_toxic_inferno",
                "aegis_phalanx" => "synergy_aegis_phalanx",
                "dragon_lord" => "synergy_dragon_lord",
                "cataclysm_vortex" => "synergy_cataclysm_vortex",
                "shatter_nova" => "glacial_shatter",
                "ricochet_barrage" => "buzzsaw_chakram",
                "solar_eclipse" => "skyfall_spire",
                "phantom_legion" => "shadow_decoy",
                _ => "fallback_star"
            };
        }

        private static string ResolveAchievementTemplateKey(AchievementData ach)
        {
            // Slot rewards and unlocks get the icon of what they grant.
            if (ach.rewardType == AchievementRewardType.UnlockActiveSlot) return "slot_active";
            if (ach.rewardType == AchievementRewardType.UnlockPassiveSlot) return "slot_passive";
            if (ach.rewardType == AchievementRewardType.UnlockHero)
            {
                return ach.rewardId switch
                {
                    "ranger" => "hero_ranger",
                    "necromancer" => "hero_necromancer",
                    _ => "hero_mage"
                };
            }
            if (ach.rewardType == AchievementRewardType.UnlockAbility && !string.IsNullOrEmpty(ach.rewardId))
            {
                return ResolveAbilityTemplateKey(ach.rewardId);
            }

            // Hero-restricted runs show the hero.
            string heroId = ach.runConstraint != null ? ach.runConstraint.heroId : null;
            if (!string.IsNullOrEmpty(heroId))
            {
                return heroId switch
                {
                    "mage" => "hero_mage",
                    "ranger" => "hero_ranger",
                    "necromancer" => "hero_necromancer",
                    _ => "hero_knight"
                };
            }

            switch (ach.conditionType)
            {
                case AchievementConditionType.DefeatBoss:
                    return ach.targetId switch
                    {
                        "golem" => "boss_golem",
                        "necromancer" => "boss_necro",
                        "frost_lich" => "ice_aura",
                        "demon_lord" => "ult_meteor",
                        _ => "boss_golem"
                    };
                case AchievementConditionType.FindSecretItem:
                    return ach.targetId switch
                    {
                        "secret_chest" => "secret_chest",
                        "secret_scroll" => "secret_scroll",
                        _ => "secret_crystal"
                    };
                case AchievementConditionType.ReachWave:
                    if (ach.runConstraint != null && ach.runConstraint.maxRunSeconds > 0) return "swift_boots";
                    if (ach.runConstraint != null && ach.runConstraint.maxActiveAbilities >= 0) return "sword";
                    if (ach.runConstraint != null && ach.runConstraint.maxPassiveAbilities >= 0) return "battle_fury";
                    if (ach.runConstraint != null && ach.runConstraint.noRerolls) return "chrono_tome";
                    return "wave_banner";
                case AchievementConditionType.TotalKills:
                case AchievementConditionType.KillsInRun: return "kills_skull";
                case AchievementConditionType.CollectAbilitiesInRun: return "tome_arsenal";
                case AchievementConditionType.NoDamageWaves: return "iron_skin";
                case AchievementConditionType.MaxAbilityLevel: return "chrono_tome";
                case AchievementConditionType.ClearShrines: return "skyfall_spire";
                case AchievementConditionType.ReachCombo: return "chain_lightning";
                case AchievementConditionType.DefeatMiniBosses: return "titan_heart";
                case AchievementConditionType.EliteKills: return "battle_fury";
                case AchievementConditionType.BarrelMultiKill: return "fireball";
                case AchievementConditionType.SiegeShellsBroken: return "seismic_hammer";
                case AchievementConditionType.VacuumMagnetXp: return "magnet";
                case AchievementConditionType.UniqueSynergies:
                case AchievementConditionType.SynergiesInRun: return "synergy_thunderstorm";
                case AchievementConditionType.UltimatesUsed: return "ult_meteor";
                case AchievementConditionType.BossBeforeEnrage: return "boss_golem";
                case AchievementConditionType.BossAtLowHp: return "vampiric_fang";
                case AchievementConditionType.RunDurationMinutes: return "chrono_tome";
                case AchievementConditionType.WaveEventsCompleted:
                    return ach.targetId == "GoldGoblin" ? "golden_idol" : "wave_banner";
                case AchievementConditionType.UniqueWaveEvents: return "wave_banner";
                case AchievementConditionType.UniqueMutators: return "black_hole";
                case AchievementConditionType.SecondWindTriggers: return "guardian_shield";
                case AchievementConditionType.HeroesReachedWave: return "hero_knight";
                case AchievementConditionType.TotalRuns: return "swift_boots";
                case AchievementConditionType.TalentsUnlocked:
                case AchievementConditionType.KeystonesUnlocked: return "fallback_star";
                case AchievementConditionType.MetaUpgradeLevels: return "coin_stack";
                case AchievementConditionType.DailyBonusClaims: return "coin_single";
                case AchievementConditionType.AbilitiesUnlocked: return "tome_arsenal";
                default: return "fallback_star";
            }
        }

        private static string ResolveMetaUpgradeTemplateKey(MetaUpgradeEffectType type)
        {
            return type switch
            {
                MetaUpgradeEffectType.HeroHP => "vitality_regen",
                MetaUpgradeEffectType.HeroDamage => "sword",
                MetaUpgradeEffectType.HeroSpeed => "swift_boots",
                MetaUpgradeEffectType.MagnetRadius => "magnet",
                MetaUpgradeEffectType.StartLevel => "wave_banner",
                MetaUpgradeEffectType.XPBonus => "secret_crystal",
                MetaUpgradeEffectType.AbilityDamage => "fireball",
                MetaUpgradeEffectType.CritMultiplier => "critical_lens",
                _ => "fallback_star"
            };
        }

        private static void EnsureFolder(string assetPath)
        {
            string[] parts = assetPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }

        private static Dictionary<string, string[]> BuildTemplates()
        {
            return new Dictionary<string, string[]>
            {
                ["sword"] = new[]
                {
                    "................",
                    "................",
                    "...........KK...",
                    "..........KWHK..",
                    ".........KWSK...",
                    "........KWSD....",
                    ".......KWSD.....",
                    "......KWSD......",
                    "....KKWSD.......",
                    "...KGGDK........",
                    "....KGBK........",
                    "...KBGK.K.......",
                    "..KBNK..........",
                    "..KKK...........",
                    "................",
                    "................"
                },
                ["shooter"] = new[]
                {
                    "................",
                    "................",
                    "........KKKK....",
                    "......KKYYYYK...",
                    ".....KYWWYYYYK..",
                    "....KYWSSDDDDK..",
                    "...KYYSD.KKKK...",
                    "...KYYK.........",
                    "...KGGK.KK......",
                    "...KGBKKWWK.....",
                    "....KBBKYYK.....",
                    "....KBNKKK......",
                    ".....KK.........",
                    "................",
                    "................",
                    "................"
                },
                ["fireball"] = new[]
                {
                    "................",
                    "................",
                    ".......KK.......",
                    "......KORK......",
                    ".....KOOHK......",
                    "....KROOHOK.....",
                    "...KRROHHROK....",
                    "...KRROHHROK....",
                    "...KRROOORRK....",
                    "....KRRRRRK.....",
                    ".....KRRRK......",
                    "......KKK.......",
                    ".....K...K......",
                    "................",
                    "................",
                    "................"
                },
                ["ice_aura"] = new[]
                {
                    "................",
                    "................",
                    ".......KK.......",
                    "......KIWK......",
                    "...K..KIWK..K...",
                    "..KIWKJJJKIWK...",
                    "...KKJIWWJKK....",
                    "..KIIWWJJWWIIK..",
                    "...KKJIWWJKK....",
                    "..KIWKJJJKIWK...",
                    "...K..KIWK..K...",
                    "......KIWK......",
                    ".......KK.......",
                    "................",
                    "................",
                    "................"
                },
                ["boomerang"] = new[]
                {
                    "................",
                    "................",
                    "....KKKKK.......",
                    "...KGGGGHK......",
                    "..KGGKKKKGK.....",
                    "..KGK....KGK....",
                    "..KAK.....KGK...",
                    "..........KGK...",
                    "..........KAK...",
                    ".........KGGK...",
                    "........KGGK....",
                    ".......KGGK.....",
                    "........KK......",
                    "................",
                    "................",
                    "................"
                },
                ["laser_turret"] = new[]
                {
                    "................",
                    "................",
                    "................",
                    "....KKKKKK......",
                    "...KSDWWDSK.....",
                    "...KSDCCDSKKKK..",
                    "....KSDDDKRRRRK.",
                    ".....KSSK.KKKK..",
                    "....KSSDSK......",
                    "...KDDDDDDK.....",
                    "..KDDGGGGDDK....",
                    "..KKKKKKKKKK....",
                    "................",
                    "................",
                    "................",
                    "................"
                },
                ["black_hole"] = new[]
                {
                    "................",
                    "................",
                    ".....KKKKK......",
                    "...KKPPPPPQQK...",
                    "..KPPKKKKKPPPK..",
                    "..KPQKKKKKKQPK..",
                    ".KPQKKWKKWKKQPK.",
                    ".KPQKKKKKKKKQPK.",
                    ".KPQKKKKKKKKQPK.",
                    "..KPQKKKKKKQPK..",
                    "..KPPKKKKKPPPK..",
                    "...KKQQPPPPKK...",
                    ".....KKKKKK.....",
                    "................",
                    "................",
                    "................"
                },
                ["battle_fury"] = new[]
                {
                    "................",
                    "................",
                    "...KKKK..KKKK...",
                    "..KCCCK..KCCCK..",
                    "..KCRRK..KCRRK..",
                    "..KCRCKKKKCRCK..",
                    "...KCCCSSCCCK...",
                    "....KKKGAKKK....",
                    "......KGBK......",
                    "......KGBK......",
                    "......KBNK......",
                    "......KBNK......",
                    "......KKKK......",
                    "................",
                    "................",
                    "................"
                },
                ["iron_skin"] = new[]
                {
                    "................",
                    "................",
                    "....KKKKKKKK....",
                    "...KWWWWSSSSK...",
                    "...KWSSDDDDDK...",
                    "...KWSGGGGGDK...",
                    "...KWSDGGGDDK...",
                    "...KSDDDDDDDK...",
                    "....KSDDDDDK....",
                    ".....KSDDDK.....",
                    "......KSDK......",
                    ".......KK.......",
                    "................",
                    "................",
                    "................",
                    "................"
                },
                ["swift_boots"] = new[]
                {
                    "................",
                    "................",
                    ".....KKKKK......",
                    "....KWWYYK......",
                    "....KYYWWYK.....",
                    "....KYYKKKK.....",
                    "....KYYK........",
                    "...KYYYYKK......",
                    "..KYYYYYYYK.....",
                    "..KDDDDDDDK.....",
                    "..KKKKKKKKK.....",
                    "................",
                    "................",
                    "................",
                    "................",
                    "................"
                },
                ["vitality_regen"] = new[]
                {
                    "................",
                    "................",
                    "...KKKK..KKKK...",
                    "..KCCCCKKCCCCK..",
                    "..KCWWCCCCCCCk..".Replace('k', 'K'),
                    "..KCWCCCCCCCCK..",
                    "..KCCCCCCCCCCK..",
                    "...KCCCMMMCCK...",
                    "....KCCMMCCK....",
                    ".....KCMMCK.....",
                    "......KCCK......",
                    ".......KK.......",
                    "................",
                    "................",
                    "................",
                    "................"
                },
                ["magnet"] = new[]
                {
                    "................",
                    "................",
                    "...KKK....KKK...",
                    "..KSSK....KSSK..",
                    "..KCCK....KIIK..",
                    "..KCCK....KIIK..",
                    "..KCCK....KIIK..",
                    "..KCCKKKKKKIIK..",
                    "...KCCCCCCCCK...",
                    "....KCCCCCCK....",
                    ".....KKKKKK.....",
                    "................",
                    "................",
                    "................",
                    "................",
                    "................"
                },
                ["synergy_steam"] = new[]
                {
                    "................",
                    "................",
                    ".....KK..KK.....",
                    "....KORKKIWK....",
                    "...KROOKKIWWK...",
                    "...KROHWWJIWK...",
                    "...KRRWWHWIIK...",
                    "....KROHWWJJK...",
                    "....KROOKKIJK...",
                    ".....KRRKKIK....",
                    "......KKKKK.....",
                    ".......KHK......",
                    "........K.......",
                    "................",
                    "................",
                    "................"
                },
                ["synergy_blades"] = new[]
                {
                    "................",
                    "................",
                    "...KK......KK...",
                    "..KWHK....KWHK..",
                    "...KWK....KWK...",
                    "....KWSKKWSK....",
                    "....KGGWWGGK....",
                    "....KWHSSHWK....",
                    "...KWSK..KWSK...",
                    "..KGGK....KGGK..",
                    "..KKK......KKK..",
                    "................",
                    "................",
                    "................",
                    "................",
                    "................"
                },
                ["synergy_grid"] = new[]
                {
                    "................",
                    "................",
                    "......KKKK......",
                    ".....KYYYYK.....",
                    "....KYWWWWYK....",
                    "...KYYGGGGYYK...",
                    "..KYWGHHHHGWYK..",
                    "..KYWGHHHHGWYK..",
                    "...KYYGGGGYYK...",
                    "....KYWWWWYK....",
                    ".....KYYYYK.....",
                    "......KKKK......",
                    "................",
                    "................",
                    "................",
                    "................"
                },
                ["hero_knight"] = new[]
                {
                    "................",
                    "................",
                    ".....KKKKKK.....",
                    "....KWWWWSSK....",
                    "...KWSSSSSDDK...",
                    "...KWSKKKKSDK...",
                    "...KWSGGGGSDK...",
                    "...KWSDDDDDSK...",
                    "....KSDDDDSK....",
                    ".....KSSSSK.....",
                    "....KCCCCCCK....",
                    "...KCCGGGGCCK...",
                    "...KKKKKKKKKK...",
                    "................",
                    "................",
                    "................"
                },
                ["hero_mage"] = new[]
                {
                    "................",
                    "................",
                    ".......KK.......",
                    "......KPPK......",
                    ".....KPPPPK.....",
                    "....KPPPPPPK....",
                    "....KGGGGGGK....",
                    "..KKPPPPPPPPKK..",
                    "...KKKKKKKKKK...",
                    "....KPPWWPPK....",
                    "....KPPYYPPK....",
                    ".....KPPPPK.....",
                    "......KKKK......",
                    "................",
                    "................",
                    "................"
                },
                ["ult_shield"] = new[]
                {
                    "................",
                    "................",
                    "....KKKKKKKK....",
                    "...KIIWWWWIIK...",
                    "..KIWYYYYYYWIK..",
                    "..KIWYWWWWYWIK..",
                    "..KIWYWIWWYWIK..",
                    "..KIWYYYYYYWIK..",
                    "...KIJJJJJJIK...",
                    "....KIJJJJIK....",
                    ".....KIJJIK.....",
                    "......KIIK......",
                    ".......KK.......",
                    "................",
                    "................",
                    "................"
                },
                ["ult_meteor"] = new[]
                {
                    "................",
                    "................",
                    ".........KK.....",
                    "........KHHK....",
                    ".......KHOHK....",
                    "....KKKHORRK....",
                    "...KHHORRRK.....",
                    "..KHOORRRK.KK...",
                    "..KRRORRK.KORK..",
                    "...KRRRK..KRRK..",
                    "....KKK....KK...",
                    "................",
                    "................",
                    "................",
                    "................",
                    "................"
                },
                ["hero_ranger"] = new[]
                {
                    "................",
                    "................",
                    ".....KKKKKK.....",
                    "....KEEEEEFK....",
                    "...KEEEWWEEEK...",
                    "...KEKKKKKKEK...",
                    "...KEKYYKKYEK...",
                    "...KEEEEEEEFK...",
                    "....KFFFFBFK....",
                    "...KBEEEEBBK....",
                    "..KBNEEEEKBHK...",
                    "..KKKFFFFKKK....",
                    "................",
                    "................",
                    "................",
                    "................"
                },
                ["hero_necromancer"] = new[]
                {
                    "................",
                    "................",
                    ".....KKKKKK.....",
                    "....KQPPPPQK....",
                    "...KQQKKKKQQK...",
                    "...KQKYYKKYQK...",
                    "...KQQKKKKQQK...",
                    "....KQQWWQQK....",
                    "...KWWQPPQWWK...",
                    "..KWWQQPPQQWWK..",
                    "...KKQQPPQQKK...",
                    ".....KKKKKK.....",
                    "................",
                    "................",
                    "................",
                    "................"
                },
                ["ult_arrow_rain"] = new[]
                {
                    "................",
                    "..KK...KK...KK..",
                    ".KEEK.KEEK.KEEK.",
                    "..KBK..KBK..KBK.",
                    "..KBK..KBK..KBK.",
                    "..KBK..KBK..KBK.",
                    "..KHK..KHK..KHK.",
                    ".KHHHK.KHHHK.KHK",
                    "..KHK..KHK..KHHK",
                    "...K....K....KH.",
                    "....KK...KK...K.",
                    "...KHHK.KHHK....",
                    "....KK...KK.....",
                    "................",
                    "................",
                    "................"
                },
                ["ult_soul_nova"] = new[]
                {
                    "................",
                    "..KK........KK..",
                    ".KYYK..KK..KYYK.",
                    "..KK.KWWWWK.KK..",
                    "....KWWWWWWK....",
                    ".KK.KWKKWWKK.KK.",
                    "KYYKKWKYYKWKKYYK",
                    ".KK.KWWWWWWK.KK.",
                    "....KWWKKWWK....",
                    "..KK.KWWWWK.KK..",
                    ".KYYK..KK..KYYK.",
                    "..KK........KK..",
                    "................",
                    "................",
                    "................",
                    "................"
                },
                ["secret_chest"] = new[]
                {
                    "................",
                    "................",
                    "...KKKKKKKKKK...",
                    "..KBBBBBBBBBBK..",
                    "..KBGGGGGGGGBK..",
                    "..KGGGGGGGGGGK..",
                    "..KBGGWWWWGGBK..",
                    "..KBBBBHHBBBBK..",
                    "..KBNBBHHBBBNK..",
                    "..KBNNBBBBNNBK..",
                    "..KNNNNNNNNNNK..",
                    "...KKKKKKKKKK...",
                    "................",
                    "................",
                    "................",
                    "................"
                },
                ["secret_scroll"] = new[]
                {
                    "................",
                    "................",
                    "....KKKKKKKK....",
                    "...KGGGGGGGGK...",
                    "...KWWWWWWWWK...",
                    "....KWWPPWWK....",
                    "....KWPWWPWK....",
                    "....KWWWWWWK....",
                    "....KWWDDWWK....",
                    "...KGGGGGGGGK...",
                    "...KHHHHHHHHK...",
                    "....KKKKKKKK....",
                    "................",
                    "................",
                    "................",
                    "................"
                },
                ["secret_crystal"] = new[]
                {
                    "................",
                    "................",
                    ".......KK.......",
                    "......KWWK......",
                    ".....KPWWPK.....",
                    "....KPPWWPPK....",
                    "....KPPYYPPK....",
                    "....KPPYYPPK....",
                    ".....KPQQPK.....",
                    ".....KPQQPK.....",
                    "......KQQK......",
                    ".......KK.......",
                    "................",
                    "................",
                    "................",
                    "................"
                },
                ["boss_golem"] = new[]
                {
                    "................",
                    "................",
                    "...KKKKKKKKKK...",
                    "..KDDDDDDDDDDK..",
                    "..KDDSSDDSSDDK..",
                    "..KDOOKDDKOODK..",
                    "..KDDDDDDDDDDK..",
                    "..KDDKKDDKKDDK..",
                    "...KDDDDDDDDK...",
                    "...KDDGGGGDDK...",
                    "....KKKKKKKK....",
                    "................",
                    "................",
                    "................",
                    "................",
                    "................"
                },
                ["boss_necro"] = new[]
                {
                    "................",
                    "................",
                    ".....KKKKKK.....",
                    "....KQQPPQQK....",
                    "...KQQWWWWQQK...",
                    "...KQWKKKKWQK...",
                    "...KQKYYKKYQK...",
                    "...KQQWWWWQQK...",
                    "....KQQWWQQK....",
                    "....KPPKKPPK....",
                    ".....KKKKKK.....",
                    "................",
                    "................",
                    "................",
                    "................",
                    "................"
                },
                ["kills_skull"] = new[]
                {
                    "................",
                    "................",
                    "....KKKKKKKK....",
                    "...KWWWWWWWWK...",
                    "..KWWWWWWWWWWK..",
                    "..KWWKKKKWWKKK..",
                    "..KWWKRRKWWKRK..",
                    "..KWWWWSSWWWWK..",
                    "...KWWKWWKWWK...",
                    "....KWWWWWWK....",
                    "....KWSKWSWK....",
                    ".....KKKKKK.....",
                    "................",
                    "................",
                    "................",
                    "................"
                },
                ["wave_banner"] = new[]
                {
                    "................",
                    "................",
                    "...KKKKKKKKKK...",
                    "...KCCCCCCCCK...",
                    "...KCCGGGGCCK...",
                    "...KCGHHHHGCK...",
                    "...KCCGGGGCCK...",
                    "...KCCCCCCCCK...",
                    "....KCCCCCCK....",
                    ".....KCCCCK.....",
                    "......KCCK......",
                    ".......KK.......",
                    "................",
                    "................",
                    "................",
                    "................"
                },
                ["tome_arsenal"] = new[]
                {
                    "................",
                    "................",
                    "...KKKKKKKKKK...",
                    "..KPPGGGGGGPPK..",
                    "..KPPWWWWWWPPK..",
                    "..KPPWGGGGWPPK..",
                    "..KPPWGGGGWPPK..",
                    "..KPPWWWWWWPPK..",
                    "..KQQDDDDDDQQK..",
                    "..KQQWWWWWWQQK..",
                    "...KKKKKKKKKK...",
                    "................",
                    "................",
                    "................",
                    "................",
                    "................"
                },
                ["coin_single"] = new[]
                {
                    "................",
                    "................",
                    ".....KKKKKK.....",
                    "....KHHHHHHK....",
                    "...KHHGGGGGGK...",
                    "...KHGHHGGAAK...",
                    "...KHGGHGGAAK...",
                    "...KHGGGGAAAK...",
                    "....KGGAAAAK....",
                    ".....KKKKKK.....",
                    "................",
                    "................",
                    "................",
                    "................",
                    "................",
                    "................"
                },
                ["coin_stack"] = new[]
                {
                    "................",
                    "................",
                    ".....KKKKKK.....",
                    "....KHHHHHHK....",
                    "....KGGAAAAK....",
                    "...KKKKKKKKKK...",
                    "...KHHHHHHHHK...",
                    "...KGGGAAAAAK...",
                    "...KKKKKKKKKK...",
                    "...KGGGGAAAAK...",
                    "...KAAAAAAAAK...",
                    "....KKKKKKKK....",
                    "................",
                    "................",
                    "................",
                    "................"
                },
                ["ui_compass_arrow"] = new[]
                {
                    "................",
                    ".......KK.......",
                    "......KHHK......",
                    ".....KHGGHK.....",
                    "....KHGGGGHK....",
                    "...KHGWWWWGHK...",
                    "..KHGWWKKWWGHK..",
                    "..KGGWK..KWGGK..",
                    "..KAAK....KAAK..",
                    "...KK..KK..KK...",
                    "......KGGK......",
                    "......KAAK......",
                    ".......KK.......",
                    "................",
                    "................",
                    "................"
                },
                ["ui_slot_lock"] = new[]
                {
                    "................",
                    ".....KKKKKK.....",
                    "....KWWWWWWK....",
                    "...KWSSSSSSWK...",
                    "...KWSKKKKWSK...",
                    "...KWSK..KWSK...",
                    "..KKKKKKKKKKKK..",
                    "..KSSSSSSSSSSK..",
                    "..KSDDDDDDDDDK..",
                    "..KSDDKKKKDDDK..",
                    "..KSDDKWWKDDDK..",
                    "..KSDDKWWKDDDK..",
                    "..KSDDKKKKDDDK..",
                    "..KDDDDDDDDDDK..",
                    "...KKKKKKKKKK...",
                    "................"
                },
                ["slot_active"] = new[]
                {
                    "................",
                    "................",
                    "...KKKKKKKKKK...",
                    "..KOOOOOOOOOOK..",
                    "..KOOOHHHHOOOK..",
                    "..KOOHWSSWHOOK..",
                    "..KOOHWGGWHOOK..",
                    "..KOOHWGGWHOOK..",
                    "..KOOHWSSWHOOK..",
                    "..KOOOHHHHOOOK..",
                    "..KOOOOOOOOOOK..",
                    "...KKKKKKKKKK...",
                    "................",
                    "................",
                    "................",
                    "................"
                },
                ["slot_passive"] = new[]
                {
                    "................",
                    "................",
                    "...KKKKKKKKKK...",
                    "..KYYYYYYYYYYK..",
                    "..KYYYWWWWYYYK..",
                    "..KYYWIIIIWYYK..",
                    "..KYYWIWWWIYYK..",
                    "..KYYWIWWWIYYK..",
                    "..KYYWIIIIWYYK..",
                    "..KYYYWWWWYYYK..",
                    "..KYYYYYYYYYYK..",
                    "...KKKKKKKKKK...",
                    "................",
                    "................",
                    "................",
                    "................"
                },
                ["chain_lightning"] = new[]
                {
                    "................",
                    "................",
                    "........KKKK....",
                    ".......KWWWWK...",
                    "......KWYYYYK...",
                    ".....KYYWWKK....",
                    "....KYYWWK......",
                    "...KYYYYWWWK....",
                    "....KKKYYWWK....",
                    "......KYYWK.....",
                    ".....KYYWK......",
                    "....KWWWK.......",
                    "....KKKK........",
                    "................",
                    "................",
                    "................"
                },
                ["poison_flask"] = new[]
                {
                    "................",
                    "................",
                    ".....KKKKKK.....",
                    ".....KBBBBK.....",
                    "......KSSK......",
                    "......KWWK......",
                    ".....KKEEK......",
                    "....KEEEEEKK....",
                    "...KEEWEEEEFK...",
                    "...KEEWWEEFFK...",
                    "...KEEEEEEFFK...",
                    "....KFFFFFFK....",
                    ".....KKKKKK.....",
                    "................",
                    "................",
                    "................"
                },
                ["guardian_shield"] = new[]
                {
                    "................",
                    "................",
                    "...KKKKKKKKKK...",
                    "..KHHHHHHHHHHK..",
                    "..KHWGGGGGGWHK..",
                    "..KHGIIIIIIIGHK.",
                    "..KHGIIWWIIIGHK.",
                    "..KHGIIIIIIIGHK.",
                    "...KHGGGGGGGHK..",
                    "....KGGGGGGGK...",
                    ".....KAAAAAK....",
                    "......KAAAK.....",
                    ".......KKK......",
                    "................",
                    "................",
                    "................"
                },
                ["mini_dragon"] = new[]
                {
                    "................",
                    "................",
                    "....KK....KK....",
                    "...KORK..KRRK...",
                    "...KOORKKRRRK...",
                    "....KORRRRKK....",
                    "..KKRRRRRHHK....",
                    ".KRRRRRROKKKK...",
                    "..KKORRRROOK....",
                    "....KORRRRK.....",
                    "....KRRKKRK.....",
                    "...KRRK..KHK....",
                    "...KKK....KK....",
                    "................",
                    "................",
                    "................"
                },
                ["voxel_tornado"] = new[]
                {
                    "................",
                    "................",
                    "..KKKKKKKKKKKK..",
                    "..KYYWWWWWWYYK..",
                    "...KYYIIIIYYK...",
                    "....KYYYYYYK....",
                    "...KYYWWWWYYK...",
                    "....KIIYYIIK....",
                    ".....KYYYYK.....",
                    "......KWWK......",
                    ".....KYYYK......",
                    "......KYK.......",
                    ".......K........",
                    "................",
                    "................",
                    "................"
                },
                ["seismic_hammer"] = new[]
                {
                    "................",
                    "................",
                    "..KKKKKKKKKKKK..",
                    "..KSSDDDDDDDDSK.",
                    "..KSDGGGGGGGGSK.",
                    "..KSDDDDDDDDDSK.",
                    "..KKKKKKKKKKKK..",
                    "......KBBK......",
                    "......KBBK......",
                    ".....KHHHHK.....",
                    "....KOOOOOOK....",
                    "...KHHKKKKHHK...",
                    "................",
                    "................",
                    "................",
                    "................"
                },
                ["chrono_tome"] = new[]
                {
                    "................",
                    "................",
                    "....KKKKKKKK....",
                    "...KYYYYYYYYK...",
                    "...KYWWWWWWYK...",
                    "...KYWKKKKWYK...",
                    "...KYWKIWWWYK...",
                    "...KYWKIWWWYK...",
                    "...KYWKKKKWYK...",
                    "...KYWWWWWWYK...",
                    "...KJJJJJJJJK...",
                    "....KKKKKKKK....",
                    "................",
                    "................",
                    "................",
                    "................"
                },
                ["critical_lens"] = new[]
                {
                    "................",
                    "................",
                    ".....KKKKKK.....",
                    "...KKHHHHHHKK...",
                    "..KHHOOOOOOHHK..",
                    "..KHORWWWWROHK..",
                    "..KHOWRRRRWOHK..",
                    "..KHOWRRRRWOHK..",
                    "..KHORWWWWROHK..",
                    "..KHHOOOOOOHHK..",
                    "...KKHHHHHHKK...",
                    ".....KKKKKK.....",
                    "................",
                    "................",
                    "................",
                    "................"
                },
                ["titan_heart"] = new[]
                {
                    "................",
                    "................",
                    "...KKKK..KKKK...",
                    "..KCCCCKKCCCCK..",
                    "..KCWWCHHCCCCK..",
                    "..KCWCHHHHCCCK..",
                    "..KCCCHHHHCCCK..",
                    "...KCCHHHCCCK...",
                    "....KCCCCCCK....",
                    ".....KCCCCK.....",
                    "......KCCK......",
                    ".......KK.......",
                    "................",
                    "................",
                    "................",
                    "................"
                },
                ["golden_idol"] = new[]
                {
                    "................",
                    "................",
                    ".....KKKKKK.....",
                    "....KHHHHHHK....",
                    "....KHHGGHHK....",
                    "...KHGKKKKGHK...",
                    "...KHGGWWGGHK...",
                    "....KGGGGGGK....",
                    "...KGGHHHHGGK...",
                    "...KGGGGGGGGK...",
                    "..KAAAAAAAAAAK..",
                    "..KKKKKKKKKKKK..",
                    "................",
                    "................",
                    "................",
                    "................"
                },
                ["synergy_thunderstorm"] = new[]
                {
                    "................",
                    "................",
                    ".....KKKKKK.....",
                    "...KKIIWWIIKK...",
                    "..KIIWWYYYYIIK..",
                    "..KIIWYYYYWWIK..",
                    "...KKKYYWWKKK...",
                    ".....KYYWWK.....",
                    "....KYYYYWWK....",
                    ".....KKYYWK.....",
                    "......KWWK......",
                    ".......KK.......",
                    "................",
                    "................",
                    "................",
                    "................"
                },
                ["synergy_toxic_inferno"] = new[]
                {
                    "................",
                    "................",
                    "......KKKK......",
                    "....KKOEEOKK....",
                    "...KOREEEEROK...",
                    "..KROEEWWEEORK..",
                    "..KROEEWWEEORK..",
                    "..KRRREEEERRRK..",
                    "...KRRFFFFRRK...",
                    "....KKKKKKKK....",
                    "................",
                    "................",
                    "................",
                    "................",
                    "................",
                    "................"
                },
                ["synergy_aegis_phalanx"] = new[]
                {
                    "................",
                    "................",
                    "..KK........KK..",
                    ".KWHK......KWHK.",
                    "..KWSKKKKKKWSK..",
                    "...KHHHHHHHHK...",
                    "...KHGIIIIIGHK..",
                    "...KHGIIWWIGHK..",
                    "...KWSGGGGWSK...",
                    "..KGGK....KGGK..",
                    "..KKK......KKK..",
                    "................",
                    "................",
                    "................",
                    "................",
                    "................"
                },
                ["synergy_dragon_lord"] = new[]
                {
                    "................",
                    "................",
                    "...KK..KK..KK...",
                    "..KHHKKHHKKHHK..",
                    "..KHHHHHHHHHHK..",
                    "...KRRRRRRRRK...",
                    "..KRROOOOOORRK..",
                    "..KRRHHKKHHRRK..",
                    "...KRRRRRRRRK...",
                    "....KGGGGGGK....",
                    ".....KKKKKK.....",
                    "................",
                    "................",
                    "................",
                    "................",
                    "................"
                },
                ["synergy_cataclysm_vortex"] = new[]
                {
                    "................",
                    "................",
                    "....KKKKKKKK....",
                    "..KKPPYYYYPPKK..",
                    ".KPPYYWWWWYYPPK.",
                    ".KPQYKKKKKKYQPK.",
                    ".KPQYKWWWWKYQPK.",
                    ".KPQYKKKKKKYQPK.",
                    ".KPPYYWWWWYYPPK.",
                    "..KKPPYYYYPPKK..",
                    "....KKKKKKKK....",
                    "................",
                    "................",
                    "................",
                    "................",
                    "................"
                },
                ["buzzsaw_chakram"] = new[]
                {
                    "................",
                    "......K..K......",
                    ".....KSKKSK.....",
                    "...KKSSSSSSKK...",
                    "..KSSSDDDDSSSK..",
                    "..KSSDYYYYDSSK..",
                    ".KSSDYKKKKYDSSK.",
                    "..KSDYKOOKYDSK..",
                    "..KSDYKOOKYDSK..",
                    ".KSSDYKKKKYDSSK.",
                    "..KSSDYYYYDSSK..",
                    "..KSSSDDDDSSSK..",
                    "...KKSSSSSSKK...",
                    ".....KSKKSK.....",
                    "......K..K......",
                    "................"
                },
                ["skyfall_spire"] = new[]
                {
                    "......KKKK......",
                    ".....KOHHOK.....",
                    ".....KOHHOK.....",
                    "....KROHHORK....",
                    "....KROHHORK....",
                    ".....KOHHOK.....",
                    ".....KOHHOK.....",
                    "......KOOK......",
                    "......KOOK......",
                    ".......KK.......",
                    "................",
                    "...KK......KK...",
                    "..KRRK.KK.KRRK..",
                    ".KROORKOOKROORK.",
                    "..KKKKKKKKKKKK..",
                    "................"
                },
                ["glacial_shatter"] = new[]
                {
                    "................",
                    ".......KK.......",
                    "......KWIK......",
                    ".KK..KWIIJK..KK.",
                    ".KIK.KIIIJK.KIK.",
                    "..KIKKIIIJKKIK..",
                    "...KIIWIIJJIK...",
                    "..KKIWWIIJJJKK..",
                    ".KIIIWIIIIJJJIK.",
                    "..KKIIIJJJJJKK..",
                    "...KIJJIIJJIK...",
                    "..KIKKJJJJKKIK..",
                    ".KIK.KJJJJK.KIK.",
                    ".KK..KJJJJK..KK.",
                    "......KJJK......",
                    ".......KK......."
                },
                ["shadow_decoy"] = new[]
                {
                    "................",
                    "......KKKK......",
                    ".....KQPPQK.....",
                    "....KQPPPPQK....",
                    "....KPKKKKPK....",
                    "....KPKWKWKK....",
                    "....KPKKKKPK....",
                    "...KQPPPPPPQK...",
                    "..KQPPQPPQPPQK..",
                    "..KQPPQPPQPPQK..",
                    "..KQPPPQQPPPQK..",
                    ".KQPPPPQQPPPPQK.",
                    ".KQPPPPPPPPPPQK.",
                    ".KQQPPQQQQPPQQK.",
                    ".KKKKKKKKKKKKKK.",
                    "................"
                },
                ["vampiric_fang"] = new[]
                {
                    "................",
                    "..KKKKKKKKKKKK..",
                    ".KWWWWWWWWWWWWK.",
                    ".KWSSWWWWWWSSWK.",
                    "..KWWKKKKKKWWK..",
                    "..KWWK....KWWK..",
                    "...KWWK..KWWK...",
                    "...KWWK..KWWK...",
                    "....KWK..KWK....",
                    "....KCK..KCK....",
                    ".....K....K.....",
                    "......KK........",
                    ".....KCCK.......",
                    "....KCCMCK......",
                    ".....KMMK.......",
                    "......KK........"
                },
                ["area_tome"] = new[]
                {
                    "................",
                    "...KKKKKKKKKK...",
                    "..KYYYYYYYYYYK..",
                    "..KYKKKKKKKKYK..",
                    "..KYKIIIIIIKYK..",
                    "..KYKIKKKKIKYK..",
                    "..KYKIKWWKIKYK..",
                    "..KYKIKWWKIKYK..",
                    "..KYKIKKKKIKYK..",
                    "..KYKIIIIIIKYK..",
                    "..KYKKKKKKKKYK..",
                    "..KYYYYYYYYYYK..",
                    "..KJJJJJJJJJJK..",
                    "..KWWWWWWWWWWK..",
                    "...KKKKKKKKKK...",
                    "................"
                },
                ["boss_shard"] = new[]
                {
                    "................",
                    ".......KK.......",
                    "......KWPK......",
                    ".....KWPPQK.....",
                    "....KWPPPQQK....",
                    "....KWPPPQQK....",
                    "...KWPPPPQQQK...",
                    "...KWPPPPQQQK...",
                    "...KPPPPQQQQK...",
                    "....KPPPQQQK....",
                    "....KPPPQQQK....",
                    ".....KPPQQK.....",
                    ".....KPPQQK.....",
                    "......KPQK......",
                    ".......KK.......",
                    "................"
                },
                ["fallback_star"] = new[]
                {
                    "................",
                    "................",
                    ".......KK.......",
                    "......KHHK......",
                    "......KGGK......",
                    "..KKKKKGGKKKKK..",
                    ".KHHHGGGGGGAAAK.",
                    "..KHHGGGGGGAAK..",
                    "...KGGGGGGGGK...",
                    "....KGGGGGGK....",
                    "...KGGKKKKGGK...",
                    "..KGAK....KAGK..",
                    "..KKK......KKK..",
                    "................",
                    "................",
                    "................"
                },
                ["enemy_slime"] = new[]
                {
                    "................",
                    "................",
                    "......KKKK......",
                    "....KKEEEEKK....",
                    "...KEEEEEEEEK...",
                    "..KEEEWWWWEEEK..",
                    "..KEEKWEEWKEEK..",
                    ".KEEEKKEEKKEEEK.",
                    ".KEEEEEEEEEEEEK.",
                    ".KEEEEEEEEEEEEK.",
                    ".KFFFFFFFFFFFFK.",
                    "..KKKKKKKKKKKK..",
                    "................",
                    "................",
                    "................",
                    "................"
                },
                ["enemy_splitter_slime"] = new[]
                {
                    "................",
                    ".....KKKKKK.....",
                    "...KKEEEEEEKK...",
                    "..KEEWWEEWWEEK..",
                    ".KEEYYEEEEYYEEK.",
                    ".KEEYYEEEEYYEEK.",
                    ".KEEEWWEEWWEEEK.",
                    ".KEEEKKEEKKEEEK.",
                    ".KEEEEEEEEEEEEK.",
                    ".KFFYYYYYYYYFFK.",
                    ".KFFFFFFFFFFFFK.",
                    "..KKKKKKKKKKKK..",
                    "................",
                    "................",
                    "................",
                    "................"
                },
                ["enemy_split_slimelet"] = new[]
                {
                    "................",
                    "................",
                    "................",
                    ".....KKKKKK.....",
                    "....KEEEEEEK....",
                    "...KEEYYYYEEK...",
                    "...KEKWEEKWEK...",
                    "...KEEEEEEEEK...",
                    "...KFFFFFFFFK...",
                    "....KKKKKKKK....",
                    "................",
                    "................",
                    "................",
                    "................",
                    "................",
                    "................"
                },
                ["enemy_frost_wisp"] = new[]
                {
                    ".......KK.......",
                    "......KIWK......",
                    ".....KIWWIK.....",
                    "..K..KIYYIK..K..",
                    ".KYK.KIWWIK.KYK.",
                    "..K.KIIYYIIK.K..",
                    "...KIIWKKWIIK...",
                    "...KIJJJJJJIK...",
                    "....KIJYYJIK....",
                    ".....KIJJIK.....",
                    "....K..KK..K....",
                    "...KYK....KYK...",
                    "....K......K....",
                    "................",
                    "................",
                    "................"
                },
                ["enemy_siege_golemite"] = new[]
                {
                    "..KK........KK..",
                    ".KSSK.KKKK.KSSK.",
                    ".KDDKKSDDSKKDDK.",
                    ".KDDKSGOOGSKDDK.",
                    ".KDDKDDDDDDKDDK.",
                    "..KKKDSOOSDKDDK.",
                    "..KDDDSOOSDKDDK.",
                    "..KDDDSOOSDKDDK.",
                    "..KDDDOOOODKDDK.",
                    "..KDDDDDDDDKDDK.",
                    "..KDDK....KDDK..",
                    "..KDDK....KDDK..",
                    "..KKKK....KKKK..",
                    "................",
                    "................",
                    "................"
                },
                ["enemy_goblin"] = new[]
                {
                    "................",
                    ".KK..........KK.",
                    ".KEK.KKKKKK.KEK.",
                    "..KEKEEEEEEKEK..",
                    "...KEEEEEEEEEK..",
                    "...KERKEEKREEK..",
                    "...KEEEEEEEEEK..",
                    "....KEKKKKKEK...",
                    "....KBBBBBBBK...",
                    "...KBBBBBBBBBK..",
                    "....KBBKKBBK....",
                    "....KDDKKDDK....",
                    "................",
                    "................",
                    "................",
                    "................"
                },
                ["enemy_skeleton"] = new[]
                {
                    "................",
                    ".....KKKKKK.....",
                    "....KWWWWWWK....",
                    "...KWWWWWWWWK...",
                    "...KWWKWWKWWK...",
                    "...KWWWWWWWWK...",
                    "....KWWKKWWK....",
                    "....KWKWKWKK....",
                    ".....KKKKKK.....",
                    "....KSWWWWWSK...",
                    ".....KSWWSK.....",
                    "....KWWKKWWK....",
                    "....KWWKKWWK....",
                    "................",
                    "................",
                    "................"
                },
                ["enemy_bat"] = new[]
                {
                    "................",
                    "................",
                    ".KK..........KK.",
                    "KPPK..KKKK..KPPK",
                    "KPQPKKPPPPKKPQPK",
                    "KPPPKPPRRPPKPPPK",
                    ".KPPKPPPPPPKPPK.",
                    "..KK.KPPPPK.KK..",
                    ".....KPPPPK.....",
                    "......KKKK......",
                    "................",
                    "................",
                    "................",
                    "................",
                    "................",
                    "................"
                },
                ["enemy_bomb_slime"] = new[]
                {
                    ".......KK.......",
                    "......KHHK......",
                    ".....KORK.......",
                    "....KKKKKKKK....",
                    "...KCRRRRRRCK...",
                    "..KCRROOOORRCK..",
                    "..KCRKRRRRKRCK..",
                    "..KCRRRRRRRRCK..",
                    "..KCMMMMMMMMCK..",
                    "...KKKKKKKKKK...",
                    "................",
                    "................",
                    "................",
                    "................",
                    "................",
                    "................"
                },
                ["enemy_orc_charger"] = new[]
                {
                    "................",
                    ".KWK.KKKKKK.KWK.",
                    ".KWWKCCCCCCKWWK.",
                    "..KKCCCCCCCCKK..",
                    "..KCCKWWWWKCCK..",
                    "..KCCCRRRRCCCK..",
                    "..KCCCCCCCCCCK..",
                    "...KBBBBBBBBK...",
                    "..KBBBBBBBBBBK..",
                    "...KBBKKKKBBK...",
                    "....KK....KK....",
                    "................",
                    "................",
                    "................",
                    "................",
                    "................"
                },
                ["enemy_shield_bearer"] = new[]
                {
                    "................",
                    "....KKKKKKKK....",
                    "...KSSSSSSSSK...",
                    "..KSSDDDDDDSSK..",
                    "..KSDRKDDKRDSS..",
                    "..KSDDDDDDDSSK..",
                    "..KSDDGGGGDDSK..",
                    "..KSDDGGGGDDSK..",
                    "..KSDDDDDDDSSK..",
                    "...KSSDDDDSSK...",
                    "....KKKKKKKK....",
                    "................",
                    "................",
                    "................",
                    "................",
                    "................"
                },
                ["enemy_void_summoner"] = new[]
                {
                    "......KKKK......",
                    ".....KPPPPK.....",
                    "....KPPQQPPK....",
                    "...KPPYYYYPPK...",
                    "...KPQKKKKQPK...",
                    "...KPPYYYYPPK...",
                    "....KPPPPPPK....",
                    "...KQQPPPPQQK...",
                    "..KQQQPPPPQQQK..",
                    "..KQQQPPPPQQQK..",
                    "...KKKPPPPKKK...",
                    "....KKKKKKKK....",
                    "................",
                    "................",
                    "................",
                    "................"
                },
                ["enemy_gold_goblin"] = new[]
                {
                    "................",
                    ".KK..........KK.",
                    ".KHK.KKKKKK.KHK.",
                    "..KHKHHHHHHKHK..",
                    "...KHHHHHHHHHK..",
                    "...KHGKHHKGHK...",
                    "...KGGGGGGGGK...",
                    "....KGKKKKGK....",
                    "....KAAAAAAK....",
                    "...KGGGGGGGGK...",
                    "....KGGKKGGK....",
                    "....KAAKKAAK....",
                    "................",
                    "................",
                    "................",
                    "................"
                }
            };
        }

        private static string ResolveEnemyTemplateKey(string enemyName)
        {
            if (string.IsNullOrEmpty(enemyName)) return "enemy_slime";
            if (enemyName.Contains("Splitter")) return "enemy_splitter_slime";
            if (enemyName.Contains("SplitSlimelet")) return "enemy_split_slimelet";
            if (enemyName.Contains("FrostWisp")) return "enemy_frost_wisp";
            if (enemyName.Contains("SiegeGolemite")) return "enemy_siege_golemite";
            if (enemyName.Contains("Bomb")) return "enemy_bomb_slime";
            if (enemyName.Contains("GoldGoblin")) return "enemy_gold_goblin";
            if (enemyName.Contains("Goblin")) return "enemy_goblin";
            if (enemyName.Contains("Skeleton")) return "enemy_skeleton";
            if (enemyName.Contains("Bat")) return "enemy_bat";
            if (enemyName.Contains("Charger")) return "enemy_orc_charger";
            if (enemyName.Contains("Shield")) return "enemy_shield_bearer";
            if (enemyName.Contains("Summoner")) return "enemy_void_summoner";
            return "enemy_slime";
        }
    }
}
