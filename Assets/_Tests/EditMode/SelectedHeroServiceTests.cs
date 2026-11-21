using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ProjectB.Data;
using ProjectB.Meta;
using ProjectB.UI;

namespace ProjectB.Tests.EditMode
{
    public class SelectedHeroServiceTests
    {
        private bool hadOriginalSave;
        private string originalSaveJson;
        private SaveManager saveManager;
        private HeroCatalog catalog;
        private HeroData knight;
        private HeroData mage;

        [SetUp]
        public void SetUp()
        {
            hadOriginalSave = PlayerPrefs.HasKey(SaveManager.SAVE_KEY);
            originalSaveJson = hadOriginalSave ? PlayerPrefs.GetString(SaveManager.SAVE_KEY) : null;
            PlayerPrefs.DeleteKey(SaveManager.SAVE_KEY);

            saveManager = new SaveManager();

            knight = ScriptableObject.CreateInstance<HeroData>();
            knight.id = "knight";
            knight.heroName = "Knight";
            knight.unlockedByDefault = true;

            mage = ScriptableObject.CreateInstance<HeroData>();
            mage.id = "mage";
            mage.heroName = "Mage";
            mage.unlockedByDefault = false;

            catalog = ScriptableObject.CreateInstance<HeroCatalog>();
            catalog.heroes = new List<HeroData> { knight, mage };
        }

        [TearDown]
        public void TearDown()
        {
            saveManager?.Dispose();
            if (catalog != null) Object.DestroyImmediate(catalog);
            if (knight != null) Object.DestroyImmediate(knight);
            if (mage != null) Object.DestroyImmediate(mage);

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
        public void DefaultSelection_ReturnsUnlockedKnight()
        {
            var service = new SelectedHeroService(saveManager, catalog);
            var selected = service.GetSelectedHero();

            Assert.IsNotNull(selected);
            Assert.AreEqual("knight", selected.id);
            Assert.IsTrue(service.IsHeroUnlocked(knight));
            Assert.IsFalse(service.IsHeroUnlocked(mage));
        }

        [Test]
        public void TrySelectHero_RejectsLockedHeroAndAllowsAfterUnlock()
        {
            var service = new SelectedHeroService(saveManager, catalog);

            Assert.IsFalse(service.TrySelectHero("mage", saveImmediately: false));
            Assert.AreEqual("knight", service.GetSelectedHero().id);

            saveManager.UnlockHero("mage", saveImmediately: false);
            Assert.IsTrue(service.IsHeroUnlocked(mage));

            HeroData notifiedHero = null;
            service.OnSelectedHeroChanged += h => notifiedHero = h;

            Assert.IsTrue(service.TrySelectHero("mage", saveImmediately: true));
            Assert.AreEqual("mage", service.GetSelectedHero().id);
            Assert.AreEqual(mage, notifiedHero);

            // Verify persistence across SaveManager reload
            var reloadedSave = new SaveManager();
            try
            {
                var reloadedService = new SelectedHeroService(reloadedSave, catalog);
                Assert.AreEqual("mage", reloadedService.GetSelectedHero().id);
            }
            finally
            {
                reloadedSave.Dispose();
            }
        }

        [Test]
        public void HeroSelectionUI_HorizontalSwipesChangeHero_AndVerticalSwipeIsIgnored()
        {
            saveManager.UnlockHero("mage", saveImmediately: false);
            var service = new SelectedHeroService(saveManager, catalog);

            var canvasGo = new GameObject("TestCanvas", typeof(RectTransform), typeof(Canvas));
            var panelGo = new GameObject("HeroSelectionPanel", typeof(RectTransform));
            panelGo.transform.SetParent(canvasGo.transform, false);

            var nameGo = new GameObject("HeroName", typeof(RectTransform), typeof(TextMeshProUGUI));
            nameGo.transform.SetParent(panelGo.transform, false);
            var nameText = nameGo.GetComponent<TextMeshProUGUI>();
            nameText.raycastTarget = true;

            var ui = panelGo.AddComponent<HeroSelectionUI>();
            var so = new UnityEditor.SerializedObject(ui);
            so.FindProperty("heroNameText").objectReferenceValue = nameText;
            so.ApplyModifiedPropertiesWithoutUndo();

            var eventSystemGo = new GameObject("TestEventSystem", typeof(EventSystem));
            try
            {
                ui.Construct(service);
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(HeroSelectionUI).GetMethod("Awake", flags).Invoke(ui, null);
                typeof(HeroSelectionUI).GetMethod("OnEnable", flags).Invoke(ui, null);

                Assert.IsNotNull(panelGo.GetComponent<InvisibleRaycastTarget>(), "HeroSelectionUI should add InvisibleRaycastTarget when no Graphic is present");
                Assert.IsFalse(nameText.raycastTarget, "Non-interactive HeroName label should not block swipe raycasts");
                Assert.AreEqual("KNIGHT", nameText.text);

                var eventSystem = eventSystemGo.GetComponent<EventSystem>();
                var pointer = new PointerEventData(eventSystem);

                // Swipe left -> next hero (Mage)
                pointer.position = new Vector2(400f, 500f);
                ui.OnPointerDown(pointer);
                pointer.position = new Vector2(240f, 510f);
                ui.OnDrag(pointer);
                ui.OnPointerUp(pointer);
                Assert.AreEqual("MAGE", nameText.text);
                Assert.AreEqual("mage", service.GetSelectedHero().id);

                // Vertical swipe -> ignored (stays Mage)
                pointer.position = new Vector2(300f, 400f);
                ui.OnPointerDown(pointer);
                pointer.position = new Vector2(315f, 620f);
                ui.OnDrag(pointer);
                ui.OnPointerUp(pointer);
                Assert.AreEqual("MAGE", nameText.text);

                // Swipe right -> previous hero (Knight)
                pointer.position = new Vector2(220f, 500f);
                ui.OnPointerDown(pointer);
                pointer.position = new Vector2(390f, 495f);
                ui.OnDrag(pointer);
                ui.OnPointerUp(pointer);
                Assert.AreEqual("KNIGHT", nameText.text);
                Assert.AreEqual("knight", service.GetSelectedHero().id);
            }
            finally
            {
                Object.DestroyImmediate(eventSystemGo);
                Object.DestroyImmediate(canvasGo);
            }
        }
    }
}
