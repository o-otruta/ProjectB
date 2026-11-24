using UnityEngine;

namespace ProjectB.Data
{
    public enum SfxType
    {
        Hit = 0,
        EnemyDeath = 1,
        XpPickup = 2,
        CoinPickup = 3,
        LevelUp = 4,
        HeroHit = 5,
        ShieldUlt = 6,
        BossSpawn = 7,
        SecretPickup = 8,
        UiClick = 9,
        Explosion = 10,
        LightningZap = 11,
        CritHit = 12,
        SynergyFanfare = 13,
        PoisonSplash = 14,
        ArrowWhoosh = 15,
        IceShatter = 16,
        BossDeath = 17,
        ShrineStart = 18,
        ComboMilestone = 19,
        UiConfirm = 20,
        UiError = 21,
        UltReady = 22
    }

    public enum MusicType
    {
        None = 0,
        Menu = 1,
        Gameplay = 2,
        Boss = 3
    }

    [CreateAssetMenu(fileName = "AudioCatalog", menuName = "ProjectB/Data/Audio Catalog")]
    public class AudioCatalog : ScriptableObject
    {
        [Header("Sound Effects")]
        public AudioClip hitClip;
        public AudioClip enemyDeathClip;
        public AudioClip xpPickupClip;
        public AudioClip coinPickupClip;
        public AudioClip levelUpClip;
        public AudioClip heroHitClip;
        public AudioClip shieldUltClip;
        public AudioClip bossSpawnClip;
        public AudioClip secretPickupClip;
        public AudioClip uiClickClip;
        public AudioClip explosionClip;
        public AudioClip lightningZapClip;
        public AudioClip critHitClip;
        public AudioClip synergyFanfareClip;
        public AudioClip poisonSplashClip;
        public AudioClip arrowWhooshClip;
        public AudioClip iceShatterClip;
        public AudioClip bossDeathClip;
        public AudioClip shrineStartClip;
        public AudioClip comboMilestoneClip;

        [Header("UI (Sprint 13)")]
        public AudioClip uiConfirmClip;
        public AudioClip uiErrorClip;
        public AudioClip ultReadyClip;

        [Header("Music")]
        public AudioClip menuMusicClip;
        public AudioClip gameplayMusicClip;
        public AudioClip bossMusicClip;

        [Header("Base Mix Levels")]
        [Range(0f, 1f)] public float musicBaseVolume = 0.45f;
        [Range(0f, 1f)] public float sfxBaseVolume = 0.85f;

        public AudioClip GetSfxClip(SfxType type)
        {
            return type switch
            {
                SfxType.Hit => hitClip,
                SfxType.EnemyDeath => enemyDeathClip,
                SfxType.XpPickup => xpPickupClip,
                SfxType.CoinPickup => coinPickupClip,
                SfxType.LevelUp => levelUpClip,
                SfxType.HeroHit => heroHitClip,
                SfxType.ShieldUlt => shieldUltClip,
                SfxType.BossSpawn => bossSpawnClip,
                SfxType.SecretPickup => secretPickupClip,
                SfxType.UiClick => uiClickClip,
                SfxType.Explosion => explosionClip,
                SfxType.LightningZap => lightningZapClip,
                SfxType.CritHit => critHitClip,
                SfxType.SynergyFanfare => synergyFanfareClip,
                SfxType.PoisonSplash => poisonSplashClip,
                SfxType.ArrowWhoosh => arrowWhooshClip,
                SfxType.IceShatter => iceShatterClip,
                SfxType.BossDeath => bossDeathClip,
                SfxType.ShrineStart => shrineStartClip,
                SfxType.ComboMilestone => comboMilestoneClip,
                SfxType.UiConfirm => uiConfirmClip != null ? uiConfirmClip : uiClickClip,
                SfxType.UiError => uiErrorClip,
                SfxType.UltReady => ultReadyClip,
                _ => null
            };
        }

        public AudioClip GetMusicClip(MusicType type)
        {
            return type switch
            {
                MusicType.Menu => menuMusicClip,
                MusicType.Gameplay => gameplayMusicClip,
                MusicType.Boss => bossMusicClip,
                _ => null
            };
        }
    }
}
