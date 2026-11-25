namespace ProjectB.Data
{
    /// <summary>
    /// What an achievement measures. Values are serialized in assets — never renumber existing entries.
    /// "Lifetime" counters accumulate across runs; "best" conditions keep the best single-run value;
    /// "set" conditions count distinct members (heroes, synergies, events) stored in <c>SaveData.achievementSetEntries</c>.
    /// </summary>
    public enum AchievementConditionType
    {
        /// <summary>Best wave reached (respects <see cref="AchievementData.runConstraint"/>).</summary>
        ReachWave = 0,
        /// <summary>Lifetime kills.</summary>
        TotalKills = 1,
        /// <summary>Lifetime big-boss kills; <c>targetId</c> = boss id, empty = any boss.</summary>
        DefeatBoss = 2,
        /// <summary>Best level of a single ability; <c>targetId</c> = ability id, empty = any.</summary>
        MaxAbilityLevel = 3,
        /// <summary>Best number of abilities owned in one run.</summary>
        CollectAbilitiesInRun = 4,
        /// <summary>Best streak of consecutive waves without taking damage.</summary>
        NoDamageWaves = 5,
        /// <summary>Lifetime secret items found; <c>targetId</c> = item id.</summary>
        FindSecretItem = 6,
        /// <summary>Lifetime Challenge Shrines cleared.</summary>
        ClearShrines = 7,
        /// <summary>Best kill combo.</summary>
        ReachCombo = 8,

        /// <summary>Lifetime mini-boss kills.</summary>
        DefeatMiniBosses = 9,
        /// <summary>Best kills in a single run.</summary>
        KillsInRun = 10,
        /// <summary>Lifetime elite kills.</summary>
        EliteKills = 11,
        /// <summary>Best number of enemies hit by a single barrel explosion.</summary>
        BarrelMultiKill = 12,
        /// <summary>Lifetime Siege Golemite shells broken.</summary>
        SiegeShellsBroken = 13,
        /// <summary>Best XP pulled in by one Vacuum Magnet.</summary>
        VacuumMagnetXp = 14,
        /// <summary>Distinct synergies ever activated (set).</summary>
        UniqueSynergies = 15,
        /// <summary>Best number of distinct synergies activated in one run.</summary>
        SynergiesInRun = 16,
        /// <summary>Lifetime ultimates used.</summary>
        UltimatesUsed = 17,
        /// <summary>Lifetime big bosses killed before their anti-kite enrage kicked in.</summary>
        BossBeforeEnrage = 18,
        /// <summary>Lifetime big bosses killed while hero HP is at or below <c>conditionParam</c> percent.</summary>
        BossAtLowHp = 19,
        /// <summary>Best run length in whole minutes.</summary>
        RunDurationMinutes = 20,
        /// <summary>Lifetime completed wave events; <c>targetId</c> = <see cref="ProjectB.Data.Enemies.WaveEventType"/> name, empty = any.</summary>
        WaveEventsCompleted = 21,
        /// <summary>Distinct wave event types completed (set).</summary>
        UniqueWaveEvents = 22,
        /// <summary>Distinct wave mutators survived (set).</summary>
        UniqueMutators = 23,
        /// <summary>Lifetime Second Wind triggers.</summary>
        SecondWindTriggers = 24,
        /// <summary>Distinct heroes that reached wave <c>conditionParam</c> (set).</summary>
        HeroesReachedWave = 25,

        // --- Meta (synced from SaveData by MetaAchievementSync) ---
        TotalRuns = 26,
        TalentsUnlocked = 27,
        KeystonesUnlocked = 28,
        MetaUpgradeLevels = 29,
        DailyBonusClaims = 30,
        AbilitiesUnlocked = 31
    }
}
