using System.Text;
using UnityEngine;
using ProjectB.Abilities;
using ProjectB.Data;

namespace ProjectB.UI
{
    public static class RewardDisplayFormatter
    {
        private static AbilityCatalog cachedAbilityCatalog;
        private static HeroCatalog cachedHeroCatalog;

        public static string ResolveAbilityName(string abilityId, AbilityCatalog abilityCatalog = null)
        {
            if (string.IsNullOrEmpty(abilityId)) return string.Empty;

            AbilityCatalog catalog = abilityCatalog != null ? abilityCatalog : GetAbilityCatalog();
            if (catalog != null && catalog.abilities != null)
            {
                for (int i = 0; i < catalog.abilities.Count; i++)
                {
                    AbilityData ab = catalog.abilities[i];
                    if (ab != null && ab.id == abilityId)
                    {
                        return !string.IsNullOrEmpty(ab.abilityName) ? ab.abilityName : HumanizeIdentifier(abilityId);
                    }
                }
            }

            return abilityId switch
            {
                "orbital_small_sword" => "Orbital Sword",
                "PoisonAlchemist" => "Poison Flask",
                "EarthquakeSlam" => "Seismic Hammer",
                "CriticalLens" => "Sniper Lens",
                "VoxelTornado" => "Arcane Tornado",
                "MiniDragon" => "Dragon Whelp",
                "GuardianShield" => "Guardian Shields",
                _ => HumanizeIdentifier(abilityId)
            };
        }

        public static string ResolveHeroName(string heroId, HeroCatalog heroCatalog = null)
        {
            if (string.IsNullOrEmpty(heroId)) return string.Empty;

            HeroCatalog catalog = heroCatalog != null ? heroCatalog : GetHeroCatalog();
            if (catalog != null)
            {
                HeroData hero = catalog.GetById(heroId);
                if (hero != null && !string.IsNullOrEmpty(hero.heroName))
                {
                    return hero.heroName;
                }
            }

            return HumanizeIdentifier(heroId);
        }

        public static string FormatReward(
            AchievementData ach,
            AbilityCatalog abilityCatalog = null,
            HeroCatalog heroCatalog = null)
        {
            if (ach == null) return string.Empty;

            return ach.rewardType switch
            {
                AchievementRewardType.GiveCoins => $"+{ach.rewardValue} Coins",
                AchievementRewardType.UnlockAbility => !string.IsNullOrEmpty(ach.rewardId)
                    ? $"Unlocked: {ResolveAbilityName(ach.rewardId, abilityCatalog)}"
                    : "New Ability Unlocked!",
                AchievementRewardType.UnlockHero => !string.IsNullOrEmpty(ach.rewardId)
                    ? $"Hero: {ResolveHeroName(ach.rewardId, heroCatalog)}"
                    : "New Hero Unlocked!",
                AchievementRewardType.UnlockActiveSlot => $"+{Mathf.Max(1, ach.rewardValue)} Active Slot",
                AchievementRewardType.UnlockPassiveSlot => $"+{Mathf.Max(1, ach.rewardValue)} Passive Slot",
                _ => string.Empty
            };
        }

        public static string HumanizeIdentifier(string rawId)
        {
            if (string.IsNullOrWhiteSpace(rawId)) return string.Empty;

            string trimmed = rawId.Trim();
            var sb = new StringBuilder(trimmed.Length + 6);
            bool capitalizeNext = true;

            for (int i = 0; i < trimmed.Length; i++)
            {
                char c = trimmed[i];
                if (c == '_' || c == '-')
                {
                    if (sb.Length > 0 && sb[sb.Length - 1] != ' ')
                    {
                        sb.Append(' ');
                    }
                    capitalizeNext = true;
                    continue;
                }

                if (i > 0 && char.IsUpper(c) && !char.IsUpper(trimmed[i - 1]) && sb.Length > 0 && sb[sb.Length - 1] != ' ')
                {
                    sb.Append(' ');
                }

                sb.Append(capitalizeNext ? char.ToUpperInvariant(c) : c);
                capitalizeNext = false;
            }

            return sb.ToString();
        }

        public static string FormatCompactNumber(long value)
        {
            if (value >= 1_000_000)
            {
                return $"{value / 1_000_000f:0.0}M";
            }
            if (value >= 10_000)
            {
                return $"{value / 1_000f:0.0}K";
            }
            if (value >= 1_000)
            {
                return $"{value / 1_000f:0.0}K";
            }
            return value.ToString();
        }

        private static AbilityCatalog GetAbilityCatalog()
        {
            if (cachedAbilityCatalog == null)
            {
                cachedAbilityCatalog = Resources.Load<AbilityCatalog>("AbilityCatalog");
            }
            return cachedAbilityCatalog;
        }

        private static HeroCatalog GetHeroCatalog()
        {
            if (cachedHeroCatalog == null)
            {
                cachedHeroCatalog = Resources.Load<HeroCatalog>("HeroCatalog");
            }
            return cachedHeroCatalog;
        }
    }
}
