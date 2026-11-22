using System;
using UnityEngine;
using VContainer;
using ProjectB.Data;
using ProjectB.Meta;

namespace ProjectB.Core
{
    /// <summary>
    /// Global persistent audio manager (Singleton in RootLifetimeScope).
    /// Uses a fixed pool of AudioSources, per-SFX rate limiting, subtle pitch jitter,
    /// and persists music/SFX volumes in SaveManager.
    /// </summary>
    public class AudioManager : IDisposable
    {
        public const string CATALOG_RESOURCE_PATH = "AudioCatalog";
        private const int SFX_POOL_SIZE = 12;
        private const float DEFAULT_MIN_SFX_INTERVAL = 0.045f;

        private readonly SaveManager saveManager;
        private readonly AudioCatalog catalog;
        private readonly float[] lastSfxPlayTimes;

        private GameObject audioHost;
        private AudioSource musicSource;
        private AudioSource[] sfxSources;
        private int nextSfxIndex;
        private MusicType currentMusic = MusicType.None;
        private float musicDuckMultiplier = 1f;

        public float MusicVolume => saveManager?.Data != null ? Mathf.Clamp01(saveManager.Data.musicVolume) : 1f;
        public float SfxVolume => saveManager?.Data != null ? Mathf.Clamp01(saveManager.Data.sfxVolume) : 1f;
        public bool IsMusicDucked => musicDuckMultiplier < 0.99f;
        public MusicType CurrentMusic => currentMusic;
        public float EffectiveMusicVolume => MusicVolume * (catalog != null ? catalog.musicBaseVolume : 0.5f) * musicDuckMultiplier;

        [Inject]
        public AudioManager(SaveManager saveManager)
            : this(saveManager, Resources.Load<AudioCatalog>(CATALOG_RESOURCE_PATH))
        {
        }

        public AudioManager(SaveManager saveManager, AudioCatalog catalog)
        {
            this.saveManager = saveManager;
            this.catalog = catalog;
            lastSfxPlayTimes = new float[Enum.GetValues(typeof(SfxType)).Length];

            if (Application.isPlaying)
            {
                InitializeAudioHost();
            }
        }

        private void InitializeAudioHost()
        {
            if (audioHost != null) return;

            audioHost = new GameObject("[AudioManager]");
            UnityEngine.Object.DontDestroyOnLoad(audioHost);

            musicSource = audioHost.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.playOnAwake = false;
            musicSource.spatialBlend = 0f;

            sfxSources = new AudioSource[SFX_POOL_SIZE];
            for (int i = 0; i < SFX_POOL_SIZE; i++)
            {
                var src = audioHost.AddComponent<AudioSource>();
                src.loop = false;
                src.playOnAwake = false;
                src.spatialBlend = 0f;
                sfxSources[i] = src;
            }

            ApplyVolumes();
        }

        public void PlaySfx(SfxType type, float volumeScale = 1f, bool randomizePitch = true)
        {
            if (catalog == null || sfxSources == null) return;
            float sfxVol = SfxVolume * catalog.sfxBaseVolume * volumeScale;
            if (sfxVol <= 0.001f) return;

            int idx = (int)type;
            float now = Time.unscaledTime;
            if (idx >= 0 && idx < lastSfxPlayTimes.Length)
            {
                if (now - lastSfxPlayTimes[idx] < DEFAULT_MIN_SFX_INTERVAL) return;
                lastSfxPlayTimes[idx] = now;
            }

            AudioClip clip = catalog.GetSfxClip(type);
            if (clip == null) return;

            AudioSource src = sfxSources[nextSfxIndex];
            nextSfxIndex = (nextSfxIndex + 1) % sfxSources.Length;
            if (src == null) return;

            src.pitch = randomizePitch ? UnityEngine.Random.Range(0.93f, 1.07f) : 1f;
            src.volume = Mathf.Clamp01(sfxVol);
            src.PlayOneShot(clip);
        }

        public void PlayMusic(MusicType type)
        {
            if (type == currentMusic && musicSource != null && musicSource.isPlaying) return;
            currentMusic = type;

            if (musicSource == null) return;

            if (type == MusicType.None || catalog == null)
            {
                musicSource.Stop();
                musicSource.clip = null;
                return;
            }

            AudioClip clip = catalog.GetMusicClip(type);
            if (clip == null)
            {
                musicSource.Stop();
                return;
            }

            if (musicSource.clip != clip)
            {
                musicSource.clip = clip;
            }
            ApplyVolumes();
            if (!musicSource.isPlaying)
            {
                musicSource.Play();
            }
        }

        public void SetMusicVolume(float value, bool saveImmediately = false)
        {
            float clamped = Mathf.Clamp01(value);
            if (saveManager?.Data != null)
            {
                saveManager.Data.musicVolume = clamped;
                if (saveImmediately) saveManager.Save();
            }
            ApplyVolumes();
        }

        public void SetSfxVolume(float value, bool saveImmediately = false)
        {
            float clamped = Mathf.Clamp01(value);
            if (saveManager?.Data != null)
            {
                saveManager.Data.sfxVolume = clamped;
                if (saveImmediately) saveManager.Save();
            }
            ApplyVolumes();
        }

        public void SetMusicDucked(bool ducked)
        {
            musicDuckMultiplier = ducked ? 0.5f : 1f;
            ApplyVolumes();
        }

        public void SaveSettings()
        {
            saveManager?.Save();
        }

        private void ApplyVolumes()
        {
            if (musicSource != null)
            {
                musicSource.volume = EffectiveMusicVolume;
            }
        }

        public void Dispose()
        {
            if (audioHost != null)
            {
                UnityEngine.Object.Destroy(audioHost);
                audioHost = null;
            }
        }
    }
}
