using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Underground.Garage;
using Underground.Progression;
using Underground.UI;
using Underground.Vehicle;

namespace Underground.EditorTools
{
    public static partial class UndergroundPrototypeBuilder
    {
        private static void ComposeGarageShowroomScene(GameObject playerCarPrefab, UpgradeDefinition engineUpgrade)
        {
            GarageBackdropBuild backdrop = ComposeGarageBackdrop(playerCarPrefab, "GarageShowroom", "DisplayTurntable", 1.05f, 0f, true);
            ConfigureShowroomController(backdrop.showroomController, true, 34f, -0.22f, 0f);

            GameObject systemsRoot = new GameObject("GarageSystems");
            GarageManager garageManager = systemsRoot.AddComponent<GarageManager>();
            RepairSystem repairSystem = systemsRoot.AddComponent<RepairSystem>();
            UpgradeSystem upgradeSystem = systemsRoot.AddComponent<UpgradeSystem>();
            SetObjectReference(repairSystem, "playerDamageSystem", backdrop.car.GetComponent<VehicleDamageSystem>());
            SetObjectReference(repairSystem, "currentCarStats", backdrop.vehicle != null ? backdrop.vehicle.BaseStats : null);
            SetObjectReference(upgradeSystem, "playerVehicle", backdrop.vehicle);

            Canvas canvas = CreateCanvas("GarageCanvas");
            GarageUiBuild uiBuild = CreateGarageShowroomUi(canvas.transform);
            GarageUIController garageUi = canvas.gameObject.AddComponent<GarageUIController>();

            UpgradePurchaseAction upgradeAction = uiBuild.upgradeButton.gameObject.AddComponent<UpgradePurchaseAction>();
            SetObjectReference(upgradeAction, "upgradeSystem", upgradeSystem);
            SetObjectReference(upgradeAction, "upgradeDefinition", engineUpgrade);

            SerializedObject uiSo = new SerializedObject(garageUi);
            uiSo.FindProperty("garageManager").objectReferenceValue = garageManager;
            uiSo.FindProperty("repairSystem").objectReferenceValue = repairSystem;
            uiSo.FindProperty("engineUpgradeAction").objectReferenceValue = upgradeAction;
            uiSo.FindProperty("showroomController").objectReferenceValue = backdrop.showroomController;
            uiSo.FindProperty("displayedVehicle").objectReferenceValue = backdrop.vehicle;
            uiSo.FindProperty("moneyText").objectReferenceValue = uiBuild.moneyText;
            uiSo.FindProperty("reputationText").objectReferenceValue = uiBuild.reputationText;
            uiSo.FindProperty("currentCarText").objectReferenceValue = uiBuild.currentCarText;
            uiSo.FindProperty("displayNameText").objectReferenceValue = uiBuild.displayNameText;
            uiSo.FindProperty("brandText").objectReferenceValue = uiBuild.brandText;
            uiSo.FindProperty("ratingText").objectReferenceValue = uiBuild.ratingText;
            uiSo.FindProperty("statusText").objectReferenceValue = uiBuild.statusText;
            uiSo.FindProperty("accelerationFill").objectReferenceValue = uiBuild.accelerationFill;
            uiSo.FindProperty("topSpeedFill").objectReferenceValue = uiBuild.topSpeedFill;
            uiSo.FindProperty("handlingFill").objectReferenceValue = uiBuild.handlingFill;
            uiSo.FindProperty("bankButton").objectReferenceValue = uiBuild.bankButton;
            uiSo.FindProperty("repairButton").objectReferenceValue = uiBuild.repairButton;
            uiSo.FindProperty("upgradeButton").objectReferenceValue = uiBuild.upgradeButton;
            uiSo.FindProperty("continueButton").objectReferenceValue = uiBuild.continueButton;
            uiSo.FindProperty("rotateLeftButton").objectReferenceValue = uiBuild.rotateLeftButton;
            uiSo.FindProperty("rotateRightButton").objectReferenceValue = uiBuild.rotateRightButton;
            uiSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ComposeGarageBackdropForMenu(GameObject playerCarPrefab)
        {
            RemoveExistingGarageBackdrop();
            ConfigureGarageBackdropCamera();
            GarageBackdropBuild backdrop = ComposeGarageBackdrop(playerCarPrefab, "MainMenuGarageBackdrop", "MenuDisplayTurntable", 1.05f, 0f, false);
            ConfigureShowroomController(backdrop.showroomController, false, 34f, -0.22f, 0f);
        }

        private static GarageBackdropBuild ComposeGarageBackdrop(GameObject playerCarPrefab, string rootName, string displayRootName, float displayZ, float autoRotateSpeed, bool configureCamera)
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.05f, 0.055f, 0.07f, 1f);
            RenderSettings.fog = false;

            Material wallMaterial = CreateOrUpdateGarageMaterial("Assets/Materials/Generated/GarageWall.mat", new Color(0.08f, 0.085f, 0.1f), 0.05f, 0.72f);
            Material floorMaterial = CreateOrUpdateGarageMaterial("Assets/Materials/Generated/GarageFloor.mat", new Color(0.12f, 0.105f, 0.095f), 0.18f, 0.92f);
            Material platformMaterial = CreateOrUpdateGarageMaterial("Assets/Materials/Generated/GaragePlatform.mat", new Color(0.16f, 0.16f, 0.165f), 0.22f, 0.88f);
            Material accentMaterial = CreateOrUpdateGarageMaterial("Assets/Materials/Generated/GarageAccent.mat", new Color(0.52f, 0.9f, 0.36f), 0f, 0.42f, new Color(0.08f, 0.22f, 0.06f) * 0.65f);
            Material trimMaterial = CreateOrUpdateGarageMaterial("Assets/Materials/Generated/GarageTrim.mat", new Color(0.22f, 0.22f, 0.2f), 0.1f, 0.76f);

            GameObject showroomRoot = new GameObject(rootName);
            CreateGarageEnvironment(showroomRoot.transform, wallMaterial, floorMaterial, platformMaterial, accentMaterial, trimMaterial);
            CreateGarageLighting(showroomRoot.transform);
            CreateGarageReflectionProbe(showroomRoot.transform);
            AttachGlobalVolume(showroomRoot.transform, "GarageGlobalVolume");

            if (configureCamera)
            {
                ConfigureGarageBackdropCamera();
            }

            GameObject displayRoot = new GameObject(displayRootName);
            displayRoot.transform.position = new Vector3(0f, 0f, displayZ);
            GameObject car = (GameObject)PrefabUtility.InstantiatePrefab(playerCarPrefab);
            car.transform.SetParent(displayRoot.transform, false);
            car.transform.localPosition = new Vector3(0f, 0.65f, 0f);
            car.transform.localRotation = Quaternion.identity;

            VehicleDynamicsController vehicle = car.GetComponent<VehicleDynamicsController>();
            GarageShowroomController showroomController = displayRoot.AddComponent<GarageShowroomController>();
            SetObjectReference(showroomController, "displayRoot", displayRoot.transform);
            SetObjectReference(showroomController, "vehicle", vehicle);
            SetObjectReference(showroomController, "vehicleBody", car.GetComponent<Rigidbody>());
            SetObjectReference(showroomController, "vehicleInput", car.GetComponent<VehicleInput>());
            SetObjectReference(showroomController, "respawn", car.GetComponent<CarRespawn>());
            SetFloatValue(showroomController, "autoRotateSpeed", autoRotateSpeed);

            return new GarageBackdropBuild(showroomRoot, displayRoot, showroomController, car, vehicle);
        }

        private static void ConfigureShowroomController(GarageShowroomController showroomController, bool allowMouseRotation, float initialYaw, float showroomBodyDrop, float autoRotateSpeed)
        {
            if (showroomController == null)
            {
                return;
            }

            SetBoolValue(showroomController, "allowMouseRotation", allowMouseRotation);
            SetFloatValue(showroomController, "initialYaw", initialYaw);
            SetFloatValue(showroomController, "showroomBodyDrop", showroomBodyDrop);
            SetFloatValue(showroomController, "autoRotateSpeed", autoRotateSpeed);
        }

        private static void RemoveExistingGarageBackdrop()
        {
            string[] rootNames = { "MainMenuGarageBackdrop", "GarageShowroom" };
            for (int i = 0; i < rootNames.Length; i++)
            {
                GameObject existing = GameObject.Find(rootNames[i]);
                if (existing != null)
                {
                    Object.DestroyImmediate(existing);
                }
            }

            string[] displayNames = { "MenuDisplayTurntable", "DisplayTurntable" };
            for (int i = 0; i < displayNames.Length; i++)
            {
                GameObject existing = GameObject.Find(displayNames[i]);
                if (existing != null)
                {
                    Object.DestroyImmediate(existing);
                }
            }
        }

        private static void CreateGarageEnvironment(Transform parent, Material wallMaterial, Material floorMaterial, Material platformMaterial, Material accentMaterial, Material trimMaterial)
        {
            CreateGaragePrimitive(parent, "Floor", PrimitiveType.Cube, new Vector3(0f, -0.2f, 0f), new Vector3(28f, 0.4f, 24f), floorMaterial);
            CreateGaragePrimitive(parent, "BackWall", PrimitiveType.Cube, new Vector3(0f, 3.8f, 10.8f), new Vector3(28f, 7.6f, 0.45f), wallMaterial);
            CreateGaragePrimitive(parent, "LeftWall", PrimitiveType.Cube, new Vector3(-13.7f, 3.8f, 0f), new Vector3(0.45f, 7.6f, 24f), wallMaterial);
            CreateGaragePrimitive(parent, "RightWall", PrimitiveType.Cube, new Vector3(13.7f, 3.8f, 0f), new Vector3(0.45f, 7.6f, 24f), wallMaterial);
            CreateGaragePrimitive(parent, "Ceiling", PrimitiveType.Cube, new Vector3(0f, 7.65f, 0f), new Vector3(28f, 0.35f, 24f), wallMaterial);
            CreateGaragePrimitive(parent, "RearTrim", PrimitiveType.Cube, new Vector3(0f, 1.35f, 10.45f), new Vector3(18f, 2.2f, 0.35f), trimMaterial);

            GameObject platform = CreateGaragePrimitive(parent, "Turntable", PrimitiveType.Cylinder, new Vector3(0f, 0.2f, 1.2f), new Vector3(4.8f, 0.18f, 4.8f), platformMaterial);
            platform.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            CreateGaragePrimitive(parent, "TurntableTrim", PrimitiveType.Cylinder, new Vector3(0f, 0.28f, 1.2f), new Vector3(5.15f, 0.03f, 5.15f), accentMaterial);

            CreateGaragePrimitive(parent, "AccentBarLeft", PrimitiveType.Cube, new Vector3(-9.4f, 0.12f, 5f), new Vector3(0.18f, 0.06f, 9f), accentMaterial);
            CreateGaragePrimitive(parent, "AccentBarRight", PrimitiveType.Cube, new Vector3(9.4f, 0.12f, 5f), new Vector3(0.18f, 0.06f, 9f), accentMaterial);
            CreateGaragePrimitive(parent, "AccentRear", PrimitiveType.Cube, new Vector3(0f, 0.12f, 8.85f), new Vector3(10f, 0.06f, 0.18f), accentMaterial);
        }

        private static void CreateGarageLighting(Transform parent)
        {
            CreateGarageLight(parent, "KeyLightLeft", LightType.Spot, new Vector3(-4.6f, 5.9f, -5.8f), Quaternion.Euler(36f, 28f, 0f), Color.white, 165f, 62f, 20f);
            CreateGarageLight(parent, "KeyLightRight", LightType.Spot, new Vector3(4.6f, 5.9f, -5.8f), Quaternion.Euler(36f, -28f, 0f), Color.white, 165f, 62f, 20f);
            CreateGarageLight(parent, "RearFill", LightType.Spot, new Vector3(0f, 4.8f, 8.8f), Quaternion.Euler(46f, 180f, 0f), new Color(0.72f, 0.8f, 1f), 62f, 72f, 16f);
            CreateGarageLight(parent, "NeonLeft", LightType.Point, new Vector3(-3.8f, 0.35f, 1.2f), Quaternion.identity, new Color(0.45f, 0.85f, 1f), 3.2f, 0f, 5.8f);
            CreateGarageLight(parent, "NeonRight", LightType.Point, new Vector3(3.8f, 0.35f, 1.2f), Quaternion.identity, new Color(0.35f, 1f, 0.45f), 3.2f, 0f, 5.8f);
        }

        private static void CreateGarageCamera()
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 2.15f, -8.2f), Quaternion.Euler(8f, 0f, 0f));

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 30f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.012f, 0.014f, 0.02f, 1f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 250f;
            EnablePostProcessing(camera);

            cameraObject.AddComponent<AudioListener>();
        }

        private static void ConfigureGarageBackdropCamera()
        {
            Camera camera = Object.FindFirstObjectByType<Camera>(FindObjectsInactive.Include);
            GameObject cameraObject = camera != null
                ? camera.gameObject
                : new GameObject("Main Camera");

            cameraObject.name = "Main Camera";
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 2.15f, -8.2f), Quaternion.Euler(8f, 0f, 0f));

            camera ??= cameraObject.GetComponent<Camera>() ?? cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 30f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.012f, 0.014f, 0.02f, 1f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 250f;
            camera.cullingMask = ~0;
            EnablePostProcessing(camera);

            AudioListener listener = cameraObject.GetComponent<AudioListener>();
            if (listener == null)
            {
                cameraObject.AddComponent<AudioListener>();
            }
        }

        private static GameObject CreateGaragePrimitive(Transform parent, string name, PrimitiveType primitiveType, Vector3 position, Vector3 scale, Material material)
        {
            GameObject gameObject = GameObject.CreatePrimitive(primitiveType);
            gameObject.name = name;
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.localPosition = position;
            gameObject.transform.localScale = scale;
            Renderer renderer = gameObject.GetComponent<Renderer>();
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
            }

            SetLayerRecursively(gameObject, LayerMask.NameToLayer("WorldStatic"));
            return gameObject;
        }

        private static void CreateGarageLight(Transform parent, string name, LightType type, Vector3 position, Quaternion rotation, Color color, float intensity, float spotAngle, float range)
        {
            GameObject lightObject = new GameObject(name);
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.localPosition = position;
            lightObject.transform.localRotation = rotation;

            Light lightComponent = lightObject.AddComponent<Light>();
            lightComponent.type = type;
            lightComponent.color = color;
            lightComponent.intensity = intensity;
            lightComponent.range = range;
            lightComponent.shadows = LightShadows.Soft;
            if (type == LightType.Spot)
            {
                lightComponent.spotAngle = spotAngle;
                lightComponent.innerSpotAngle = spotAngle * 0.6f;
            }
        }

        private static void CreateGarageReflectionProbe(Transform parent)
        {
            GameObject probeObject = new GameObject("GarageReflectionProbe");
            probeObject.transform.SetParent(parent, false);
            probeObject.transform.localPosition = new Vector3(0f, 2.9f, 1.4f);

            ReflectionProbe probe = probeObject.AddComponent<ReflectionProbe>();
            probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
            probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.EveryFrame;
            probe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.IndividualFaces;
            probe.size = new Vector3(30f, 10f, 26f);
            probe.boxProjection = true;
            probe.intensity = 1.15f;
            probe.importance = 1000;
        }

        private static GarageUiBuild CreateGarageShowroomUi(Transform parent)
        {
            GarageUiBuild build = new GarageUiBuild();

            GameObject topBar = CreateGaragePanel(parent, "TopBar", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(920f, 98f), new Color(0.05f, 0.06f, 0.07f, 0.9f));
            build.brandText = CreateAnchoredInfoText(topBar.transform, "BrandText", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(34f, -14f), "UNDERGROUND GARAGE", TextAlignmentOptions.TopLeft, 18f);
            build.displayNameText = CreateAnchoredInfoText(topBar.transform, "DisplayNameText", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -2f), "STARTER COUPE", TextAlignmentOptions.Center, 38f);
            CreateAnchoredInfoText(topBar.transform, "SlotText", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(34f, 16f), "Slot 1", TextAlignmentOptions.BottomLeft, 18f);
            build.rotateLeftButton = CreateGarageButton(topBar.transform, "<", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(46f, 46f), new Color(0.18f, 0.19f, 0.21f, 0.95f));
            build.rotateRightButton = CreateGarageButton(topBar.transform, ">", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-12f, 0f), new Vector2(46f, 46f), new Color(0.18f, 0.19f, 0.21f, 0.95f));

            GameObject leftInfo = CreateGaragePanel(parent, "LeftInfo", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(250f, 126f), new Color(0.05f, 0.06f, 0.08f, 0.78f));
            build.moneyText = CreateAnchoredInfoText(leftInfo.transform, "MoneyText", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -16f), "Money: 0", TextAlignmentOptions.TopLeft, 18f);
            build.reputationText = CreateAnchoredInfoText(leftInfo.transform, "RepText", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -44f), "Reputation: 0", TextAlignmentOptions.TopLeft, 18f);
            build.currentCarText = CreateAnchoredInfoText(leftInfo.transform, "CarText", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -72f), "Current Car: starter_car", TextAlignmentOptions.TopLeft, 18f);

            GameObject ratingPanel = CreateGaragePanel(parent, "RatingPanel", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-26f, 2f), new Vector2(180f, 186f), new Color(0.05f, 0.06f, 0.08f, 0.84f));
            CreateAnchoredInfoText(ratingPanel.transform, "RatingHeader", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), "VISUAL RATING", TextAlignmentOptions.Center, 19f);
            build.ratingText = CreateAnchoredInfoText(ratingPanel.transform, "RatingText", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -4f), "8.56", TextAlignmentOptions.Center, 48f);
            CreateGarageVerticalMeter(ratingPanel.transform, new Vector2(0.87f, 0.15f), new Color(0.2f, 0.25f, 0.2f, 0.9f), new Color(0.58f, 0.92f, 0.34f, 1f));

            GameObject bottomPanel = CreateGaragePanel(parent, "BottomPanel", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(980f, 140f), new Color(0.05f, 0.06f, 0.08f, 0.9f));
            build.accelerationFill = CreateGarageStatBlock(bottomPanel.transform, "ACCELERATION", new Vector2(0.17f, 0.7f), out _);
            build.topSpeedFill = CreateGarageStatBlock(bottomPanel.transform, "TOP SPEED", new Vector2(0.5f, 0.7f), out _);
            build.handlingFill = CreateGarageStatBlock(bottomPanel.transform, "HANDLING", new Vector2(0.83f, 0.7f), out _);
            build.statusText = CreateAnchoredInfoText(bottomPanel.transform, "StatusText", new Vector2(0.03f, 0.18f), new Vector2(0f, 0.5f), Vector2.zero, "Right-click and drag to rotate.", TextAlignmentOptions.Left, 18f);
            build.bankButton = CreateGarageButton(bottomPanel.transform, "Bank Progress", new Vector2(0.56f, 0.16f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150f, 40f), new Color(0.18f, 0.24f, 0.2f, 0.94f));
            build.repairButton = CreateGarageButton(bottomPanel.transform, "Repair Car", new Vector2(0.72f, 0.16f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(140f, 40f), new Color(0.18f, 0.22f, 0.26f, 0.94f));
            build.upgradeButton = CreateGarageButton(bottomPanel.transform, "Buy Engine", new Vector2(0.86f, 0.16f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(140f, 40f), new Color(0.16f, 0.27f, 0.18f, 0.96f));
            build.continueButton = CreateGarageButton(bottomPanel.transform, "Continue", new Vector2(0.97f, 0.16f), new Vector2(1f, 0.5f), new Vector2(-8f, 0f), new Vector2(118f, 40f), new Color(0.24f, 0.24f, 0.28f, 0.96f));

            return build;
        }

        private static GameObject CreateGaragePanel(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta, Color color)
        {
            GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            RectTransform rect = panel.GetComponent<RectTransform>();
            ConfigureAnchoredRect(rect, anchor, anchor, pivot, anchoredPosition, sizeDelta);
            panel.GetComponent<Image>().color = color;
            return panel;
        }

        private static Button CreateGarageButton(Transform parent, string label, Vector2 anchor, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta, Color color)
        {
            Button button = CreateAnchoredButton(parent, label, anchor, pivot, anchoredPosition, sizeDelta, null);
            Image image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = color;
            }

            return button;
        }

        private static Image CreateGarageStatBlock(Transform parent, string title, Vector2 anchor, out TMP_Text valueText)
        {
            GameObject block = CreateGaragePanel(parent, $"{title}_Block", anchor, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(250f, 66f), new Color(0.07f, 0.075f, 0.085f, 0.82f));
            CreateAnchoredInfoText(block.transform, $"{title}_Title", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), title, TextAlignmentOptions.Center, 18f);
            valueText = CreateAnchoredInfoText(block.transform, $"{title}_Value", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 12f), string.Empty, TextAlignmentOptions.Center, 18f);

            GameObject track = CreateGaragePanel(block.transform, $"{title}_Track", new Vector2(0.5f, 0.36f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(190f, 10f), new Color(0.18f, 0.2f, 0.22f, 1f));
            GameObject fillObject = CreateGaragePanel(track.transform, $"{title}_Fill", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(190f, 10f), new Color(0.58f, 0.92f, 0.34f, 1f));
            Image fill = fillObject.GetComponent<Image>();
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.fillAmount = 0.5f;
            return fill;
        }

        private static void CreateGarageVerticalMeter(Transform parent, Vector2 anchor, Color trackColor, Color fillColor)
        {
            GameObject track = CreateGaragePanel(parent, "MeterTrack", anchor, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(14f, 126f), trackColor);
            GameObject fillObject = CreateGaragePanel(track.transform, "MeterFill", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(14f, 126f), fillColor);
            Image fill = fillObject.GetComponent<Image>();
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Vertical;
            fill.fillOrigin = 0;
            fill.fillAmount = 0.82f;
        }

        private static Material CreateOrUpdateGarageMaterial(string path, Color baseColor, float metallic, float smoothness, Color? emissionColor = null)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = baseColor;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", baseColor);
            }

            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", metallic);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", smoothness);
            }

            if (emissionColor.HasValue)
            {
                Color color = emissionColor.Value;
                material.EnableKeyword("_EMISSION");
                if (material.HasProperty("_EmissionColor"))
                {
                    material.SetColor("_EmissionColor", color);
                }
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private readonly struct GarageBackdropBuild
        {
            public GarageBackdropBuild(GameObject environmentRoot, GameObject displayRoot, GarageShowroomController showroomController, GameObject car, VehicleDynamicsController vehicle)
            {
                this.environmentRoot = environmentRoot;
                this.displayRoot = displayRoot;
                this.showroomController = showroomController;
                this.car = car;
                this.vehicle = vehicle;
            }

            public readonly GameObject environmentRoot;
            public readonly GameObject displayRoot;
            public readonly GarageShowroomController showroomController;
            public readonly GameObject car;
            public readonly VehicleDynamicsController vehicle;
        }

        private struct GarageUiBuild
        {
            public TMP_Text moneyText;
            public TMP_Text reputationText;
            public TMP_Text currentCarText;
            public TMP_Text displayNameText;
            public TMP_Text brandText;
            public TMP_Text ratingText;
            public TMP_Text statusText;
            public Image accelerationFill;
            public Image topSpeedFill;
            public Image handlingFill;
            public Button bankButton;
            public Button repairButton;
            public Button upgradeButton;
            public Button continueButton;
            public Button rotateLeftButton;
            public Button rotateRightButton;
        }
    }
}
