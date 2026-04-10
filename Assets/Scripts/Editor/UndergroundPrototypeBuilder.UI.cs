using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif
using Underground.Core;
using Underground.UI;

namespace Underground.EditorTools
{
    public static partial class UndergroundPrototypeBuilder
    {
        private static Canvas CreateCanvas(string name)
        {
            GameObject canvasObject = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<ResponsiveCanvasController>();
            return canvas;
        }

        private static TMP_Text CreateInfoText(Transform parent, string name, Vector2 anchoredPosition, string text)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(480f, 40f);
            rect.anchoredPosition = anchoredPosition;
            TextMeshProUGUI tmp = textObject.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 28f;
            tmp.color = Color.white;
            return tmp;
        }

        private static void CreateTitle(Transform parent, string text, Vector2 anchoredPosition, float fontSize)
        {
            TMP_Text title = CreateInfoText(parent, "Title", anchoredPosition, text);
            title.fontSize = fontSize;
            title.alignment = TextAlignmentOptions.Center;
        }

        private static Button CreateButton(Transform parent, string label, Vector2 anchoredPosition, UnityEngine.Events.UnityAction action)
        {
            GameObject buttonObject = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(260f, 56f);
            rect.anchoredPosition = anchoredPosition;
            buttonObject.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f, 0.9f);
            Button button = buttonObject.GetComponent<Button>();
            if (action != null)
            {
                button.onClick.AddListener(action);
            }

            TMP_Text labelText = CreateInfoText(buttonObject.transform, "Label", Vector2.zero, label);
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.fontSize = 24f;
            return button;
        }

        private static void ConfigureAnchoredRect(RectTransform rectTransform, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.pivot = pivot;
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = sizeDelta;
        }

        private static TMP_Text CreateAnchoredInfoText(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 anchoredPosition, string text, TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft, float fontSize = 28f)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            ConfigureAnchoredRect(rect, anchor, anchor, pivot, anchoredPosition, new Vector2(420f, 38f));

            TextMeshProUGUI tmp = textObject.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = Color.white;
            tmp.alignment = alignment;
            return tmp;
        }

        private static Button CreateAnchoredButton(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta, UnityEngine.Events.UnityAction action)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);

            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            ConfigureAnchoredRect(rect, anchor, anchor, pivot, anchoredPosition, sizeDelta);

            buttonObject.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f, 0.9f);
            Button button = buttonObject.GetComponent<Button>();
            if (action != null)
            {
                button.onClick.AddListener(action);
            }

            TMP_Text label = CreateAnchoredInfoText(buttonObject.transform, "Label", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, name, TextAlignmentOptions.Center, 24f);
            label.rectTransform.sizeDelta = new Vector2(sizeDelta.x - 24f, sizeDelta.y - 16f);
            return button;
        }

        private static void EnsureEventSystem()
        {
            EventSystem eventSystem = Object.FindFirstObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
#if ENABLE_INPUT_SYSTEM
                InputSystemUIInputModule createdInputSystemModule = eventSystemObject.AddComponent<InputSystemUIInputModule>();
                createdInputSystemModule.enabled = false;
#endif
                return;
            }

            if (eventSystem.GetComponent<StandaloneInputModule>() == null)
            {
                eventSystem.gameObject.AddComponent<StandaloneInputModule>();
            }

#if ENABLE_INPUT_SYSTEM
            InputSystemUIInputModule inputSystemModule = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (inputSystemModule == null)
            {
                inputSystemModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            }

            inputSystemModule.enabled = false;
#endif
        }

        private static Transform FindDescendantByName(Transform root, string objectName)
        {
            if (root == null || string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            if (root.name == objectName)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform match = FindDescendantByName(root.GetChild(i), objectName);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static T FindComponentByName<T>(Transform root, string objectName) where T : Component
        {
            Transform match = FindDescendantByName(root, objectName);
            return match != null ? match.GetComponent<T>() : null;
        }

        private static void AssignObjectReferenceIfMissing<T>(SerializedObject serializedObject, string propertyName, T value) where T : Object
        {
            if (serializedObject == null || string.IsNullOrWhiteSpace(propertyName))
            {
                return;
            }

            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null || property.objectReferenceValue != null || value == null)
            {
                return;
            }

            property.objectReferenceValue = value;
        }

        private static void ConfigureBootstrapSceneLoader(BootstrapSceneLoader loader)
        {
            if (loader == null)
            {
                return;
            }

            SerializedObject serializedObject = new SerializedObject(loader);
            SerializedProperty firstSceneName = serializedObject.FindProperty("firstSceneName");
            if (firstSceneName == null)
            {
                return;
            }

            firstSceneName.stringValue = GetPreferredMainMenuSceneName();
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObjectReference(Component component, string propertyName, Object value)
        {
            SerializedObject serializedObject = new SerializedObject(component);
            serializedObject.FindProperty(propertyName).objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloatValue(Component component, string propertyName, float value)
        {
            SerializedObject serializedObject = new SerializedObject(component);
            serializedObject.FindProperty(propertyName).floatValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetVector3Value(Component component, string propertyName, Vector3 value)
        {
            SerializedObject serializedObject = new SerializedObject(component);
            serializedObject.FindProperty(propertyName).vector3Value = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBoolValue(Component component, string propertyName, bool value)
        {
            SerializedObject serializedObject = new SerializedObject(component);
            serializedObject.FindProperty(propertyName).boolValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetStringValue(Component component, string propertyName, string value)
        {
            SerializedObject serializedObject = new SerializedObject(component);
            serializedObject.FindProperty(propertyName).stringValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetLayerRecursively(GameObject gameObject, int layer)
        {
            if (layer < 0)
            {
                return;
            }

            gameObject.layer = layer;
            for (int i = 0; i < gameObject.transform.childCount; i++)
            {
                SetLayerRecursively(gameObject.transform.GetChild(i).gameObject, layer);
            }
        }
    }
}
