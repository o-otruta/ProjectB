using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using ProjectB.Data;

namespace ProjectB.Meta
{
    public class SelectedHeroService
    {
        public const string CATALOG_RESOURCE_PATH = "HeroCatalog";

        private readonly SaveManager saveManager;
        private readonly List<HeroData> allHeroes;

        public event Action<HeroData> OnSelectedHeroChanged;

        [Inject]
        public SelectedHeroService(SaveManager saveManager)
            : this(saveManager, Resources.Load<HeroCatalog>(CATALOG_RESOURCE_PATH))
        {
        }

        public SelectedHeroService(SaveManager saveManager, HeroCatalog catalog)
        {
            this.saveManager = saveManager;
            allHeroes = new List<HeroData>();

            if (catalog != null && catalog.heroes != null)
            {
                for (int i = 0; i < catalog.heroes.Count; i++)
                {
                    if (catalog.heroes[i] != null)
                    {
                        allHeroes.Add(catalog.heroes[i]);
                    }
                }
            }
        }

        public IReadOnlyList<HeroData> GetAllHeroes() => allHeroes;

        public bool IsHeroUnlocked(HeroData hero)
        {
            if (hero == null) return false;
            if (hero.unlockedByDefault) return true;
            return saveManager != null && saveManager.IsHeroUnlocked(hero.id);
        }

        public HeroData GetHeroById(string heroId)
        {
            if (string.IsNullOrEmpty(heroId)) return null;
            for (int i = 0; i < allHeroes.Count; i++)
            {
                if (allHeroes[i] != null && allHeroes[i].id == heroId)
                {
                    return allHeroes[i];
                }
            }
            return null;
        }

        public HeroData GetSelectedHero()
        {
            string selectedId = saveManager != null ? saveManager.GetSelectedHeroId() : null;
            HeroData candidate = GetHeroById(selectedId);
            if (candidate != null && IsHeroUnlocked(candidate))
            {
                return candidate;
            }

            // Fallback to first unlocked hero
            for (int i = 0; i < allHeroes.Count; i++)
            {
                if (IsHeroUnlocked(allHeroes[i]))
                {
                    return allHeroes[i];
                }
            }

            return allHeroes.Count > 0 ? allHeroes[0] : null;
        }

        public bool TrySelectHero(string heroId, bool saveImmediately = true)
        {
            HeroData hero = GetHeroById(heroId);
            if (hero == null || !IsHeroUnlocked(hero))
            {
                return false;
            }

            saveManager?.SetSelectedHeroId(hero.id, saveImmediately);
            OnSelectedHeroChanged?.Invoke(hero);
            return true;
        }
    }
}
