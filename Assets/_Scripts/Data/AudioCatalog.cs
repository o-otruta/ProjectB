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
        UiClick = 9
    }

    public enum MusicType
    {
        None = 0,
        Menu = 1,
        Gameplay = 2
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

        [Header("Music")]
        public AudioClip menuMusicClip;
        public AudioClip gameplayMusicClip;

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
                _ => null
            };
        }

        public AudioClip GetMusicClip(MusicType type)
        {
            return type switch
            {
                MusicType.Menu => menuMusicClip,
                MusicType.Gameplay => gameplayMusicClip,
                _ => null
            };
        }
    }
}
