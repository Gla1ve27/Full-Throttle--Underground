using System.IO;
using FullThrottle.Minimap;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace FullThrottle.EditorTools
{
    [InitializeOnLoad]
    public static class MinimapSetupUtility
    {
        private const string PrefabFolder = "Assets/Prefabs/Minimap";
        private const string SpriteFolder = PrefabFolder + "/Sprites";
        private const string RenderTexturePath = PrefabFolder + "/RT_Minimap.renderTexture";
        private const string HudPrefabPath = PrefabFolder + "/MinimapHUD.prefab";
        private const string CircleSpritePath = SpriteFolder + "/MinimapCircle.png";
        private const string RingSpritePath = SpriteFolder + "/MinimapRing.png";
        private const string ArrowSpritePath = SpriteFolder + "/MinimapArrow.png";
        private const int TextureSize = 128;

        static MinimapSetupUtility()
        {
            EditorApplication.delayCall += EnsureDefaultAssetsAfterReload;
        }

        [MenuItem("Full Throttle/Minimap/Create Minimap HUD Prefab")]
        public static void CreateMinimapHudPrefab()
        {
            EnsureMinimapAssets(forceRebuildPrefab: true);
        }

        [MenuItem("Full Throttle/Minimap/Create Scene Minimap Setup")]
        public static void CreateSceneMinimapSetup()
        {
            EnsureMinimapAssets(forceRebuildPrefab: false);

            RenderTexture renderTexture = AssetDatabase.LoadAssetAtPath<RenderTexture>(RenderTexturePath);
            GameObject player = FindPlayerObject();
            MinimapCameraController cameraController = EnsureSceneCamera(renderTexture, player != null ? player.transform : null);
            MinimapSystem minimapSystem = EnsureSceneHud(cameraController, player != null ? player.transform : null);

            EditorUtility.SetDirty(cameraController);
            if (minimapSystem != null)
            {
                EditorUtility.SetDirty(minimapSystem);
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            if (player == null)
            {
                Debug.LogWarning("[MinimapSetup] Scene setup created, but no object tagged Player was found. Assign Player Target manually on MinimapCameraController and MinimapSystem.");
            }
            else
            {
                Debug.Log("[MinimapSetup] Created minimap scene setup and assigned the Player target.");
            }
        }

        private static void EnsureDefaultAssetsAfterReload()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            EnsureMinimapAssets(forceRebuildPrefab: false);
        }

        private static void EnsureMinimapAssets(bool forceRebuildPrefab)
        {
            EnsureFolder(PrefabFolder);
            EnsureFolder(SpriteFolder);

            RenderTexture renderTexture = EnsureRenderTexture();
            Sprite circleSprite = EnsureSprite(CircleSpritePath, SpriteShape.Circle);
            Sprite ringSprite = EnsureSprite(RingSpritePath, SpriteShape.Ring);
            Sprite arrowSprite = EnsureSprite(ArrowSpritePath, SpriteShape.Arrow);

            if (forceRebuildPrefab || AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath) == null)
            {
                BuildHudPrefab(renderTexture, circleSprite, ringSprite, arrowSprite);
            }

            AssetDatabase.SaveAssets();
        }

        private static RenderTexture EnsureRenderTexture()
        {
            RenderTexture renderTexture = AssetDatabase.LoadAssetAtPath<RenderTexture>(RenderTexturePath);
            if (renderTexture == null)
            {
                renderTexture = new RenderTexture(512, 512, 16, RenderTextureFormat.ARGB32)
                {
                    name = "RT_Minimap",
                    useMipMap = false,
                    autoGenerateMips = false,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                AssetDatabase.CreateAsset(renderTexture, RenderTexturePath);
            }
            else
            {
                renderTexture.width = 512;
                renderTexture.height = 512;
                renderTexture.depth = 16;
                renderTexture.useMipMap = false;
                renderTexture.autoGenerateMips = false;
                renderTexture.filterMode = FilterMode.Bilinear;
                renderTexture.wrapMode = TextureWrapMode.Clamp;
                EditorUtility.SetDirty(renderTexture);
            }

            return renderTexture;
        }

        private static Sprite EnsureSprite(string path, SpriteShape shape)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null)
            {
                return sprite;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path));

            Texture2D texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
            Color32[] pixels = new Color32[TextureSize * TextureSize];
            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    pixels[y * TextureSize + x] = GetPixel(shape, x, y);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Color32 GetPixel(SpriteShape shape, int x, int y)
        {
            float center = (TextureSize - 1) * 0.5f;
            float dx = (x - center) / center;
            float dy = (y - center) / center;
            float radius = Mathf.Sqrt(dx * dx + dy * dy);

            switch (shape)
            {
                case SpriteShape.Circle:
                    return radius <= 1f ? Color.white : new Color32(255, 255, 255, 0);
                case SpriteShape.Ring:
                    return radius <= 1f && radius >= 0.78f ? Color.white : new Color32(255, 255, 255, 0);
                case SpriteShape.Arrow:
                    float normalizedX = (float)x / (TextureSize - 1);
                    float normalizedY = (float)y / (TextureSize - 1);
                    float halfWidth = Mathf.Lerp(0.08f, 0.42f, 1f - normalizedY);
                    bool inHead = normalizedY >= 0.2f && Mathf.Abs(normalizedX - 0.5f) <= halfWidth;
                    bool inTail = normalizedY < 0.35f && Mathf.Abs(normalizedX - 0.5f) <= 0.11f;
                    return inHead || inTail ? Color.white : new Color32(255, 255, 255, 0);
                default:
                    return Color.white;
            }
        }

        private static void BuildHudPrefab(RenderTexture renderTexture, Sprite circleSprite, Sprite ringSprite, Sprite arrowSprite)
        {
            GameObject root = new GameObject("MinimapRoot", typeof(RectTransform), typeof(MinimapSystem));
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(1f, 1f);
            rootRect.anchorMax = new Vector2(1f, 1f);
            rootRect.pivot = new Vector2(1f, 1f);
            rootRect.anchoredPosition = new Vector2(-28f, -28f);
            rootRect.sizeDelta = new Vector2(190f, 190f);

            RectTransform circleMask = CreateRectChild(rootRect, "CircleMask", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Image maskImage = circleMask.gameObject.AddComponent<Image>();
            maskImage.sprite = circleSprite;
            maskImage.color = Color.white;
            maskImage.raycastTarget = false;
            Mask mask = circleMask.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            RectTransform mapImageRect = CreateRectChild(circleMask, "MapImage", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            RawImage mapImage = mapImageRect.gameObject.AddComponent<RawImage>();
            mapImage.texture = renderTexture;
            mapImage.color = Color.white;
            mapImage.raycastTarget = false;

            RectTransform iconContainer = CreateRectChild(rootRect, "IconContainer", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            RectTransform playerArrow = CreateRectChild(rootRect, "PlayerArrow", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            playerArrow.sizeDelta = new Vector2(28f, 28f);
            Image arrowImage = playerArrow.gameObject.AddComponent<Image>();
            arrowImage.sprite = arrowSprite;
            arrowImage.color = new Color(0.42f, 0.78f, 1f, 1f);
            arrowImage.raycastTarget = false;

            RectTransform frameRing = CreateRectChild(rootRect, "FrameRing", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Image ringImage = frameRing.gameObject.AddComponent<Image>();
            ringImage.sprite = ringSprite;
            ringImage.color = new Color(0.64f, 0.31f, 1f, 0.95f);
            ringImage.raycastTarget = false;

            SerializedObject serializedSystem = new SerializedObject(root.GetComponent<MinimapSystem>());
            serializedSystem.FindProperty("mapViewport").objectReferenceValue = circleMask;
            serializedSystem.FindProperty("mapImage").objectReferenceValue = mapImage;
            serializedSystem.FindProperty("iconContainer").objectReferenceValue = iconContainer;
            serializedSystem.FindProperty("playerArrow").objectReferenceValue = playerArrow;
            serializedSystem.FindProperty("rotateMapWithPlayer").boolValue = true;
            serializedSystem.FindProperty("rotatePlayerArrowWhenNorthUp").boolValue = true;
            serializedSystem.FindProperty("worldUnitsVisibleRadius").floatValue = 70f;
            serializedSystem.FindProperty("iconEdgePadding").floatValue = 10f;
            serializedSystem.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, HudPrefabPath);
            Object.DestroyImmediate(root);
            Debug.Log("[MinimapSetup] Saved minimap HUD prefab at " + HudPrefabPath);
        }

        private static RectTransform CreateRectChild(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            RectTransform rectTransform = child.GetComponent<RectTransform>();
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.offsetMin = offsetMin;
            rectTransform.offsetMax = offsetMax;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.localScale = Vector3.one;
            return rectTransform;
        }

        private static MinimapCameraController EnsureSceneCamera(RenderTexture renderTexture, Transform target)
        {
            GameObject root = GameObject.Find("MinimapCameraRoot");
            if (root == null)
            {
                root = new GameObject("MinimapCameraRoot");
                Undo.RegisterCreatedObjectUndo(root, "Create Minimap Camera Root");
            }

            Transform cameraTransform = root.transform.Find("MinimapCamera");
            GameObject cameraObject;
            if (cameraTransform == null)
            {
                cameraObject = new GameObject("MinimapCamera", typeof(Camera), typeof(MinimapCameraController));
                Undo.RegisterCreatedObjectUndo(cameraObject, "Create Minimap Camera");
                cameraObject.transform.SetParent(root.transform, false);
            }
            else
            {
                cameraObject = cameraTransform.gameObject;
                if (cameraObject.GetComponent<Camera>() == null)
                {
                    cameraObject.AddComponent<Camera>();
                }

                if (cameraObject.GetComponent<MinimapCameraController>() == null)
                {
                    cameraObject.AddComponent<MinimapCameraController>();
                }
            }

            Camera camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 50f;
            camera.targetTexture = renderTexture;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.02f, 0.025f, 0.03f, 1f);
            camera.allowHDR = false;
            camera.allowMSAA = false;

            MinimapCameraController controller = cameraObject.GetComponent<MinimapCameraController>();
            SerializedObject serializedController = new SerializedObject(controller);
            serializedController.FindProperty("target").objectReferenceValue = target;
            serializedController.FindProperty("height").floatValue = 80f;
            serializedController.FindProperty("rotateWithTarget").boolValue = true;
            serializedController.FindProperty("smoothFollow").boolValue = true;
            serializedController.FindProperty("orthographicSize").floatValue = 50f;
            serializedController.ApplyModifiedPropertiesWithoutUndo();

            return controller;
        }

        private static MinimapSystem EnsureSceneHud(MinimapCameraController cameraController, Transform target)
        {
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                Undo.RegisterCreatedObjectUndo(canvasObject, "Create Canvas");
                canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
            }

            Transform existing = canvas.transform.Find("MinimapRoot");
            GameObject hudObject;
            if (existing != null)
            {
                hudObject = existing.gameObject;
            }
            else
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
                hudObject = PrefabUtility.InstantiatePrefab(prefab, canvas.transform) as GameObject;
                if (hudObject == null)
                {
                    return null;
                }

                Undo.RegisterCreatedObjectUndo(hudObject, "Create Minimap HUD");
                hudObject.name = "MinimapRoot";
            }

            MinimapSystem minimapSystem = hudObject.GetComponent<MinimapSystem>();
            if (minimapSystem == null)
            {
                minimapSystem = hudObject.AddComponent<MinimapSystem>();
            }

            SerializedObject serializedSystem = new SerializedObject(minimapSystem);
            serializedSystem.FindProperty("minimapCamera").objectReferenceValue = cameraController;
            serializedSystem.FindProperty("playerTarget").objectReferenceValue = target;
            serializedSystem.ApplyModifiedPropertiesWithoutUndo();

            return minimapSystem;
        }

        private static GameObject FindPlayerObject()
        {
            GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
            if (taggedPlayer != null)
            {
                return taggedPlayer;
            }

            return Selection.activeGameObject;
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folderPath).Replace('\\', '/');
            string name = Path.GetFileName(folderPath);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private enum SpriteShape
        {
            Circle,
            Ring,
            Arrow
        }
    }
}
