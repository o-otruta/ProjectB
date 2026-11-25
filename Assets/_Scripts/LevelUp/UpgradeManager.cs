using System;
using System.Collections.Generic;
using UnityEngine;
using ProjectB.Player;
using ProjectB.UI;
using ProjectB.Core;
using ProjectB.Data;
using ProjectB.Meta;
using VContainer;
using ProjectB.Abilities;

namespace ProjectB.LevelUp
{
    public class UpgradeManager : MonoBehaviour
    {
        [Tooltip("Количество карточек на выбор при повышении уровня")]
        public int maxCardsToOffer = 3;

        [System.NonSerialized] public List<CardData> cardPool; // Test override pool; production reads injected CardCatalog
        public List<SynergyData> synergies = new List<SynergyData>(); // Синергии способностей
        private List<CardData> dynamicPool = new List<CardData>(); // Пул добавленных модификаторов

        private CardCatalog cardCatalog;
        private RunRulesConfig runRulesConfig;
        private CardSelectionUI cardUI;
        private HeroExperience heroExp;
        private GameManager gameManager;
        private HeroAbilities heroAbilities;
        private UpgradeApplier upgradeApplier;
        private SaveManager saveManager;
        private HeroRuntimeStats runtimeStats;
        private ProjectB.Combat.HeroCombat heroCombat;
        
        private int pendingLevelUps = 0;
        private int freeRerollsRemaining = 0;

        public int FreeRerollsRemaining => freeRerollsRemaining;

        public IReadOnlyList<CardData> ResolvedCardPool
        {
            get
            {
                if (cardPool != null && cardPool.Count > 0) return cardPool;
                return cardCatalog != null ? cardCatalog.cards : null;
            }
        }

        public void SetCardCatalog(CardCatalog catalog)
        {
            cardCatalog = catalog;
        }

        public void SetRunRulesConfig(RunRulesConfig rules)
        {
            runRulesConfig = rules;
        }

        [Inject]
        public void Construct(
            GameManager gameManager,
            CardSelectionUI cardUI,
            HeroExperience heroExp,
            HeroAbilities heroAbilities,
            UpgradeApplier upgradeApplier,
            SaveManager saveManager,
            HeroRuntimeStats runtimeStats = null,
            ProjectB.Combat.HeroCombat heroCombat = null,
            CardCatalog cardCatalog = null,
            RunRulesConfig runRulesConfig = null)
        {
            this.runtimeStats = runtimeStats;
            this.heroCombat = heroCombat;
            this.gameManager = gameManager;
            this.cardUI = cardUI;
            this.heroExp = heroExp;
            this.heroAbilities = heroAbilities;
            this.upgradeApplier = upgradeApplier;
            this.saveManager = saveManager;
            if (cardCatalog != null) this.cardCatalog = cardCatalog;
            if (runRulesConfig != null) this.runRulesConfig = runRulesConfig;
        }

        private void Start()
        {
            if (heroExp != null)
            {
                heroExp.OnLevelUp += HandleLevelUp;
            }
            if (gameManager != null)
            {
                gameManager.OnGameOver += HandleGameOver;
            }
            
            // Формируем пул на текущий забег: фиксируем снапшот разблокированных способностей на старте забега,
            // чтобы способности, открытые ачивками посреди забега, появлялись начиная со следующего забега.
            InitializeRunPool();

            // Выдаём стартовые выборы карточек за купленный мета-апгрейд StartLevel
            if (heroExp != null)
            {
                int startingLevelUps = heroExp.ConsumeStartingLevelUps();
                if (startingLevelUps > 0)
                {
                    StartCoroutine(GrantStartingLevelUpsNextFrame(startingLevelUps));
                }
            }
        }

        public void InitializeRunPool()
        {
            dynamicPool.Clear();
            int baseFreeRerolls = runRulesConfig != null ? runRulesConfig.freeRerollsPerRun : RunRulesConfig.DEFAULT_FREE_REROLLS_PER_RUN;
            freeRerollsRemaining = Mathf.Max(0, baseFreeRerolls) + (runtimeStats != null ? runtimeStats.FreeRerollBonus : 0);

            IReadOnlyList<CardData> sourcePool = ResolvedCardPool;
            if (sourcePool == null) return;

            for (int i = 0; i < sourcePool.Count; i++)
            {
                var card = sourcePool[i];
                if (card == null) continue;

                if (card is AbilityCardData abilityCard)
                {
                    if (abilityCard.abilityData == null)
                    {
                        Debug.LogWarning($"[UpgradeManager] AbilityCardData '{abilityCard.name}' has null abilityData. Skipping.");
                        continue;
                    }

                    if (!IsAbilityUnlockedForRun(abilityCard.abilityData))
                    {
                        continue;
                    }
                }

                if (!dynamicPool.Contains(card))
                {
                    dynamicPool.Add(card);
                }
            }
        }

        private bool IsAbilityUnlockedForRun(AbilityData abilityData)
        {
            if (abilityData == null) return false;
            if (abilityData.unlockedByDefault) return true;
            return saveManager != null && saveManager.IsAbilityUnlocked(abilityData.id);
        }

        private System.Collections.IEnumerator GrantStartingLevelUpsNextFrame(int count)
        {
            yield return null;
            if (gameManager != null && gameManager.IsGameOver) yield break;

            pendingLevelUps += count;
            if (cardUI != null && !cardUI.IsActive)
            {
                ProcessNextLevelUp();
            }
        }

        private void OnDestroy()
        {
            if (heroExp != null)
            {
                heroExp.OnLevelUp -= HandleLevelUp;
            }
            if (gameManager != null)
            {
                gameManager.OnGameOver -= HandleGameOver;
            }
        }

        private void HandleGameOver(int wave, int coins)
        {
            pendingLevelUps = 0;
            if (cardUI != null && cardUI.IsActive)
            {
                cardUI.Hide();
            }
        }

        private void HandleLevelUp(int newLevel)
        {
            if (gameManager != null && gameManager.IsGameOver) return;

            pendingLevelUps++;
            
            if (cardUI != null && !cardUI.IsActive)
            {
                ProcessNextLevelUp();
            }
        }

        private void ProcessNextLevelUp()
        {
            if (gameManager != null && gameManager.IsGameOver)
            {
                pendingLevelUps = 0;
                if (cardUI != null) cardUI.Hide();
                return;
            }

            if (pendingLevelUps > 0)
            {
                pendingLevelUps--;
                if (gameManager != null && !gameManager.PauseForLevelUp())
                {
                    pendingLevelUps = 0;
                    if (cardUI != null) cardUI.Hide();
                    return;
                }
                
                // Determine how many slots the UI has. By default 3 in our project.
                List<CardData> cardsToOffer = GetRandomCards(maxCardsToOffer);
                if (cardsToOffer.Count > 0)
                {
                    cardUI.ShowCards(cardsToOffer, OnCardSelected, HandleRerollRequested, freeRerollsRemaining);
                }
                else
                {
                    // No cards left
                    ResumeGame();
                }
            }
            else
            {
                if (cardUI != null) cardUI.Hide();
                ResumeGame();
            }
        }

        public void AddCardsToPool(List<CardData> newCards)
        {
            if (newCards == null) return;
            for (int i = 0; i < newCards.Count; i++)
            {
                var card = newCards[i];
                if (card != null && !dynamicPool.Contains(card))
                {
                    dynamicPool.Add(card);
                }
            }
        }

        private List<CardData> BuildValidPool()
        {
            List<CardData> validPool = new List<CardData>();
            bool canAddActive = heroAbilities == null || heroAbilities.CanAddActive();
            bool canAddPassive = heroAbilities == null || heroAbilities.CanAddPassive();

            // Filter out invalid, locked, already acquired, or slot-capped abilities and orphan modifiers.
            // Modifiers of abilities at the run level cap (RunRulesConfig base 5, raised by the Constellation
            // Path of Mastery to 10 / 20 / 50) leave the pool; stat and weapon cards keep it from running dry.
            // Reactive synergies auto-activate in HeroAbilities and never enter LevelUp cards.
            if (dynamicPool != null)
            {
                foreach (var card in dynamicPool)
                {
                    if (card == null) continue;

                    if (card is AbilityCardData abilityCard)
                    {
                        if (abilityCard.abilityData == null)
                            continue;

                        // Skip if we already have this ability
                        if (heroAbilities != null && heroAbilities.HasAbility(abilityCard.abilityData.id))
                            continue;
                            
                        if (abilityCard.abilityData.type == AbilityType.Active && !canAddActive)
                            continue;
                        if (abilityCard.abilityData.type == AbilityType.Passive && !canAddPassive)
                            continue;
                    }
                    else if (card is AbilityModifierCardData modifierCard)
                    {
                        if (string.IsNullOrEmpty(modifierCard.targetAbilityId))
                            continue;

                        if (heroAbilities != null && !heroAbilities.HasAbility(modifierCard.targetAbilityId))
                            continue;

                        if (heroAbilities != null && heroAbilities.IsAbilityAtMaxLevel(modifierCard.targetAbilityId))
                            continue;
                    }
                    else if (card is WeaponUpgradeCardData weaponCard)
                    {
                        if (weaponCard.maxStacks > 0 && heroCombat != null &&
                            heroCombat.GetWeaponUpgradeStacks(weaponCard.upgradeType) >= weaponCard.maxStacks)
                            continue;
                        // Melee weapons (Knight) take Radius instead of Multishot; ranged weapons never see Radius.
                        if (heroCombat != null
                                ? !heroCombat.SupportsWeaponUpgrade(weaponCard.upgradeType)
                                : weaponCard.upgradeType == WeaponUpgradeType.Radius)
                            continue;
                    }
                    
                    validPool.Add(card);
                }
            }

            return validPool;
        }

        public List<CardData> GetRandomCards(int count)
        {
            List<CardData> validPool = BuildValidPool();
            if (validPool.Count == 0 || count <= 0) return new List<CardData>();

            int countToSelect = Mathf.Min(count, validPool.Count);
            List<CardData> selectedCards = new List<CardData>(countToSelect);

            for (int i = 0; i < countToSelect; i++)
            {
                CardData selectedCard = GetWeightedRandomCard(validPool);
                if (selectedCard != null)
                {
                    selectedCards.Add(selectedCard);
                    validPool.Remove(selectedCard);
                }
            }
            
            return selectedCards;
        }

        public List<CardData> GetRerolledCards(List<CardData> previousCards, int count)
        {
            List<CardData> validPool = BuildValidPool();
            if (validPool.Count == 0 || count <= 0) return new List<CardData>();

            int countToSelect = Mathf.Min(count, validPool.Count);
            List<CardData> freshPool = new List<CardData>(validPool.Count);
            for (int i = 0; i < validPool.Count; i++)
            {
                var card = validPool[i];
                if (previousCards == null || !previousCards.Contains(card))
                {
                    freshPool.Add(card);
                }
            }

            List<CardData> selectedCards = new List<CardData>(countToSelect);
            while (selectedCards.Count < countToSelect && freshPool.Count > 0)
            {
                CardData selectedCard = GetWeightedRandomCard(freshPool);
                if (selectedCard == null) break;
                selectedCards.Add(selectedCard);
                freshPool.Remove(selectedCard);
                validPool.Remove(selectedCard);
            }

            while (selectedCards.Count < countToSelect && validPool.Count > 0)
            {
                CardData selectedCard = GetWeightedRandomCard(validPool);
                if (selectedCard == null) break;
                selectedCards.Add(selectedCard);
                validPool.Remove(selectedCard);
            }

            return selectedCards;
        }

        private List<CardData> HandleRerollRequested(List<CardData> currentOffered, bool usedFreeReroll)
        {
            if (usedFreeReroll && freeRerollsRemaining > 0)
            {
                freeRerollsRemaining--;
            }
            return GetRerolledCards(currentOffered, maxCardsToOffer);
        }

        private CardData GetWeightedRandomCard(List<CardData> validPool)
        {
            float totalWeight = 0f;
            foreach (var card in validPool)
            {
                totalWeight += GetRarityWeight(card.rarity);
            }

            float randomValue = UnityEngine.Random.Range(0f, totalWeight);
            float currentWeight = 0f;

            foreach (var card in validPool)
            {
                currentWeight += GetRarityWeight(card.rarity);
                if (randomValue <= currentWeight)
                {
                    return card;
                }
            }

            return validPool[0]; // Fallback
        }

        private float GetRarityWeight(CardRarity rarity)
        {
            switch (rarity)
            {
                case CardRarity.Common: return 50f;
                case CardRarity.Rare: return 30f;
                case CardRarity.Epic: return 15f;
                case CardRarity.Legendary: return 5f;
                default: return 50f;
            }
        }

        /// <summary>
        /// Grants a random unlocked Rare+ ability directly (used by Mystery Crystal secret item).
        /// Falls back to any unowned unlocked ability if no Rare+ abilities remain.
        /// </summary>
        public AbilityData GrantRandomRareAbility()
        {
            if (heroAbilities == null || dynamicPool == null) return null;

            List<AbilityCardData> rareCandidates = new List<AbilityCardData>();
            List<AbilityCardData> fallbackCandidates = new List<AbilityCardData>();

            bool canAddActive = heroAbilities.CanAddActive();
            bool canAddPassive = heroAbilities.CanAddPassive();

            for (int i = 0; i < dynamicPool.Count; i++)
            {
                if (dynamicPool[i] is AbilityCardData abilityCard && abilityCard.abilityData != null)
                {
                    var ab = abilityCard.abilityData;
                    if (heroAbilities.HasAbility(ab.id)) continue;
                    if (ab.type == AbilityType.Active && !canAddActive) continue;
                    if (ab.type == AbilityType.Passive && !canAddPassive) continue;

                    if (ab.rarity >= CardRarity.Rare || abilityCard.rarity >= CardRarity.Rare)
                    {
                        rareCandidates.Add(abilityCard);
                    }
                    else
                    {
                        fallbackCandidates.Add(abilityCard);
                    }
                }
            }

            var source = rareCandidates.Count > 0 ? rareCandidates : fallbackCandidates;
            if (source.Count == 0) return null;

            AbilityCardData chosen = source[UnityEngine.Random.Range(0, source.Count)];
            if (chosen.isConsumable)
            {
                dynamicPool.Remove(chosen);
            }

            ApplyCardEffect(chosen);
            return chosen.abilityData;
        }

        private void OnCardSelected(CardData selectedCard)
        {
            if (selectedCard.isConsumable)
            {
                dynamicPool.Remove(selectedCard);
                DevLog.Log($"[UpgradeManager] Removed consumable card {selectedCard.cardName} from pool.");
            }
            
            ApplyCardEffect(selectedCard);
            ProcessNextLevelUp();
        }

        private void ApplyCardEffect(CardData card)
        {
            DevLog.Log($"[UpgradeManager] Applying card: {card.cardName}");
            if (upgradeApplier != null)
            {
                upgradeApplier.ApplyCard(card, this);
            }
        }

        private void ResumeGame()
        {
            if (gameManager != null) gameManager.ResumeFromLevelUp();
        }
    }
}
