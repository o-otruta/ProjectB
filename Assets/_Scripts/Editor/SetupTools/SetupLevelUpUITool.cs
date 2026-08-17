#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ProjectB.LevelUp;

namespace ProjectB.Editor
{
    public class SetupLevelUpUITool
    {
        [MenuItem("ProjectB/Setup LevelUp UI")]
        public static void SetupUI()
        {
            var canvasGo = GameObject.Find("Canvas");
            if (canvasGo == null)
            {
                Debug.LogWarning("Canvas not found! Please run 'ProjectB/Setup XP System' first.");
                return;
            }

            // Создаем основную панель (которая затеняет фон)
            var selectionPanelGo = new GameObject("CardSelectionPanel");
            selectionPanelGo.transform.SetParent(canvasGo.transform, false);
            var panelRect = selectionPanelGo.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            
            var panelImage = selectionPanelGo.AddComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0.8f);

            // Создаем контейнер для карточек (Horizontal Layout Group)
            var cardsContainerGo = new GameObject("CardsContainer");
            cardsContainerGo.transform.SetParent(selectionPanelGo.transform, false);
            var containerRect = cardsContainerGo.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0.5f, 0.5f);
            containerRect.anchorMax = new Vector2(0.5f, 0.5f);
            containerRect.sizeDelta = new Vector2(800, 1000); // Увеличили высоту для двух рядов
            
            var glg = cardsContainerGo.AddComponent<GridLayoutGroup>();
            glg.childAlignment = TextAnchor.MiddleCenter;
            glg.spacing = new Vector2(50, 50);
            glg.cellSize = new Vector2(450, 600);
            glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = 2;

            // Создаем 3 карточки
            CardUI[] cardUIs = new CardUI[3];
            for (int i = 0; i < 3; i++)
            {
                cardUIs[i] = CreateCardUI(cardsContainerGo.transform, $"Card_{i+1}");
                if (i == 2)
                {
                    AddAdPlaceholder(cardUIs[i].gameObject);
                }
            }

            // Добавляем CardSelectionUI на Canvas (или на LevelUpManager)
            var selectionUI = canvasGo.GetComponent<CardSelectionUI>();
            if (selectionUI == null)
            {
                selectionUI = canvasGo.AddComponent<CardSelectionUI>();
            }

            // Привязываем ссылки через SerializedObject
            var so = new SerializedObject(selectionUI);
            so.FindProperty("selectionPanel").objectReferenceValue = selectionPanelGo;
            
            var arrayProp = so.FindProperty("cardSlots");
            arrayProp.arraySize = 3;
            for (int i = 0; i < 3; i++)
            {
                arrayProp.GetArrayElementAtIndex(i).objectReferenceValue = cardUIs[i];
            }
            so.ApplyModifiedProperties();

            Debug.Log("LevelUp UI successfully generated!");
        }

        private static CardUI CreateCardUI(Transform parent, string name)
        {
            var cardGo = new GameObject(name);
            cardGo.transform.SetParent(parent, false);
            var rect = cardGo.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(300, 450);

            var bgImage = cardGo.AddComponent<Image>();
            bgImage.color = new Color(0.8f, 0.8f, 0.8f, 1f);

            var button = cardGo.AddComponent<Button>(); // Чтобы было видно клик-эффект
            
            var cardUI = cardGo.AddComponent<CardUI>();

            // Заголовок
            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(cardGo.transform, false);
            var titleRect = titleGo.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 0.8f);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;
            var titleText = titleGo.AddComponent<TextMeshProUGUI>();
            titleText.text = "Card Title";
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = Color.black;
            titleText.fontSize = 32;

            // Описание
            var descGo = new GameObject("Description");
            descGo.transform.SetParent(cardGo.transform, false);
            var descRect = descGo.AddComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0.1f, 0.1f);
            descRect.anchorMax = new Vector2(0.9f, 0.4f);
            descRect.offsetMin = Vector2.zero;
            descRect.offsetMax = Vector2.zero;
            var descText = descGo.AddComponent<TextMeshProUGUI>();
            descText.text = "Card Description goes here...";
            descText.alignment = TextAlignmentOptions.Center;
            descText.color = Color.black;
            descText.fontSize = 24;
            descText.enableWordWrapping = true;

            // Привязка ссылок
            var so = new SerializedObject(cardUI);
            so.FindProperty("nameText").objectReferenceValue = titleText;
            so.FindProperty("descriptionText").objectReferenceValue = descText;
            so.FindProperty("bgImage").objectReferenceValue = bgImage;
            so.ApplyModifiedProperties();

            return cardUI;
        }

        private static void AddAdPlaceholder(GameObject cardGo)
        {
            var adLabelGo = new GameObject("AdPlaceholder");
            adLabelGo.transform.SetParent(cardGo.transform, false);
            var rect = adLabelGo.AddComponent<RectTransform>();
            // Размещаем внизу карточки
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(10, 10);
            rect.offsetMax = new Vector2(-10, 60);

            var bg = adLabelGo.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.6f, 0.1f, 1f); // Зеленый фон

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(adLabelGo.transform, false);
            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.text = "Watch AD";
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.fontSize = 28;
            text.fontStyle = FontStyles.Bold;
        }
    }
}
#endif
