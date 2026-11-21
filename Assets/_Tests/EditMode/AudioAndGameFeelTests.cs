using NUnit.Framework;
using UnityEngine;
using ProjectB.Core;
using ProjectB.Data;
using ProjectB.Meta;
using ProjectB.Visuals;

namespace ProjectB.Tests.EditMode
{
    public class AudioAndGameFeelTests
    {
        private bool hadOriginalSave;
        private string originalSaveJson;
        private SaveManager saveManager;

        [SetUp]
        public void SetUp()
        {
            hadOriginalSave = PlayerPrefs.HasKey(SaveManager.SAVE_KEY);
            originalSaveJson = hadOriginalSave ? PlayerPrefs.GetString(SaveManager.SAVE_KEY) : null;
            PlayerPrefs.DeleteKey(SaveManager.SAVE_KEY);
            saveManager = new SaveManager();
        }

        [TearDown]
        public void TearDown()
        {
            saveManager?.Dispose();
            if (hadOriginalSave)
            {
                PlayerPrefs.SetString(SaveManager.SAVE_KEY, originalSaveJson);
            }
            else
            {
                PlayerPrefs.DeleteKey(SaveManager.SAVE_KEY);
            }
            PlayerPrefs.Save();
        }

        [Test]
        public void AudioCatalog_LoadsAll10SfxAnd2MusicClips()
        {
            var catalog = Resources.Load<AudioCatalog>(AudioManager.CATALOG_RESOURCE_PATH);
            Assert.IsNotNull(catalog, "AudioCatalog.asset missing in Resources!");

            foreach (SfxType sfx in System.Enum.GetValues(typeof(SfxType)))
            {
                Assert.IsNotNull(catalog.GetSfxClip(sfx), $"Missing SFX clip for {sfx}");
            }

            Assert.IsNotNull(catalog.GetMusicClip(MusicType.Menu), "Missing Menu music clip");
            Assert.IsNotNull(catalog.GetMusicClip(MusicType.Gameplay), "Missing Gameplay music clip");
        }

        [Test]
        public void AudioManager_PersistsVolumeSettingsAndClampsValues()
        {
            var audio = new AudioManager(saveManager);
            audio.SetMusicVolume(0.35f, saveImmediately: false);
            audio.SetSfxVolume(1.5f, saveImmediately: false);
            audio.SaveSettings();

            Assert.AreEqual(0.35f, audio.MusicVolume, 0.001f);
            Assert.AreEqual(1.0f, audio.SfxVolume, 0.001f);

            var reloadedSave = new SaveManager();
            try
            {
                var reloadedAudio = new AudioManager(reloadedSave);
                Assert.AreEqual(0.35f, reloadedAudio.MusicVolume, 0.001f);
                Assert.AreEqual(1.0f, reloadedAudio.SfxVolume, 0.001f);
            }
            finally
            {
                reloadedSave.Dispose();
            }
        }

        [Test]
        public void DamageNumberItem_ReturnsCachedStringsWithoutAllocationForNormalRange()
        {
            Assert.AreSame(DamageNumberItem.GetCachedNumberText(42), DamageNumberItem.GetCachedNumberText(42));
            Assert.AreEqual("0", DamageNumberItem.GetCachedNumberText(0));
            Assert.AreEqual("999", DamageNumberItem.GetCachedNumberText(999));
            Assert.AreEqual("1500", DamageNumberItem.GetCachedNumberText(1500));
        }
    }
}
