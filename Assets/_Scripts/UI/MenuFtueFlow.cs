using ProjectB.Meta;

namespace ProjectB.UI
{
    /// <summary>Which element the main menu onboarding pointer should highlight right now.</summary>
    public enum MenuFtueStep
    {
        None = 0,
        OpenUpgrades = 1,
        BuyUpgrade = 2,
        OpenConstellations = 3,
        OpenPlayTab = 4,
        PressStart = 5
    }

    /// <summary>
    /// Main menu onboarding after the first run (pure logic, no Unity objects):
    /// Upgrades tab → buy the cheapest upgrade → Constellations tab (only if the player owns Boss Shards) →
    /// Play tab → START. The chain is finished by starting the next run. The Constellations pointer also
    /// appears later on its own once the very first Boss Shard is earned in any run.
    /// </summary>
    public static class MenuFtueFlow
    {
        public static bool IsFlowPending(SaveManager saveManager)
        {
            SaveData data = saveManager?.Data;
            return data != null && data.totalRuns >= 1 && !data.ftueMenuFlowCompleted;
        }

        public static bool IsConstellationsHintPending(SaveManager saveManager)
        {
            SaveData data = saveManager?.Data;
            return data != null && data.bossShards > 0 && !data.ftueConstellationsHintDismissed;
        }

        public static bool ShouldBuyUpgrade(SaveManager saveManager, bool canAffordAnyUpgrade)
        {
            return IsFlowPending(saveManager) && canAffordAnyUpgrade && !saveManager.HasAnyMetaUpgrade();
        }

        public static MenuFtueStep Evaluate(SaveManager saveManager, int selectedTab, bool canAffordAnyUpgrade)
        {
            if (saveManager?.Data == null || saveManager.Data.totalRuns < 1) return MenuFtueStep.None;

            bool constellationsPending = IsConstellationsHintPending(saveManager);

            if (!IsFlowPending(saveManager))
            {
                return constellationsPending && selectedTab != MainMenuUI.TAB_CONSTELLATIONS
                    ? MenuFtueStep.OpenConstellations
                    : MenuFtueStep.None;
            }

            if (ShouldBuyUpgrade(saveManager, canAffordAnyUpgrade))
            {
                return selectedTab == MainMenuUI.TAB_UPGRADES ? MenuFtueStep.BuyUpgrade : MenuFtueStep.OpenUpgrades;
            }

            if (constellationsPending && selectedTab != MainMenuUI.TAB_CONSTELLATIONS)
            {
                return MenuFtueStep.OpenConstellations;
            }

            return selectedTab == MainMenuUI.TAB_PLAY ? MenuFtueStep.PressStart : MenuFtueStep.OpenPlayTab;
        }
    }
}
