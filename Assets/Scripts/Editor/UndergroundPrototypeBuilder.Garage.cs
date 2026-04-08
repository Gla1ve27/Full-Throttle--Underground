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
        private const string GarageBackdropTexturePath = "Assets/Art/Textures/Garage/GarageBackdrop.png";
        private const string GarageBackdropMaterialPath = "Assets/Materials/Generated/GarageBackdropImage.mat";
        private const string GarageFloorTexturePath = "Assets/Art/Textures/Garage/Floor.png";
        private const string GarageCeilingTexturePath = "Assets/Art/Textures/Garage/Ceiling.png";
        private const string GarageLeftWallTexturePath = "Assets/Art/Textures/Garage/Left Wall.png";
        private const string GarageRightWallTexturePath = "Assets/Art/Textures/Garage/Right Wall.png";

        private static void ComposeGarageShowroomScene(GameObject playerCarPrefab, UpgradeDefinition engineUpgrade)
        {
            GarageBackdropBuild backdrop = ComposeGarageBackdrop(playerCarPrefab, "GarageShowroom", "DisplayTurntable", 1.05f, 0f, true);
            ConfigureShowroomController(backdrop.showroomController, true, 146f, -0.22f, 0f);

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
            ConfigureShowroomController(backdrop.showroomController, false, 146f, -0.22f, 0f);
        }

        private static GarageBackdropBuild ComposeGarageBackdrop(GameObject playerCarPrefab, string rootName, string displayRootName, float displayZ, float autoRotateSpeed, bool configureCamera)
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.05f, 0.055f, 0.07f, 1f);
            RenderSettings.fog = false;

            Texture2D floorTexture = LoadGarageSurfaceTexture(GarageFloorTexturePath, false);
            Texture2D ceilingTexture = LoadGarageSurfaceTexture(GarageCeilingTexturePath, false);
            Texture2D leftWallTexture = LoadGarageSurfaceTexture(GarageLeftWallTexturePath, false);
            Texture2D rightWallTexture = LoadGarageSurfaceTexture(GarageRightWallTexturePath, false);
            Texture2D backdropTexture = LoadGarageSurfaceTexture(GarageBackdropTexturePath, true);

            Material floorMaterial = CreateOrUpdateGarageMaterial(
                "Assets/Materials/Generated/GarageFloor.mat",
                Color.white,
                0.04f,
                0.28f,
                null,
                floorTexture,
                new Vector2(3.2f, 3.2f));
            Material backWallMaterial = CreateOrUpdateGarageMaterial("Assets/Materials/Generated/GarageBackWall.mat", new Color(0.13f, 0.135f, 0.14f), 0.03f, 0.18f, null, ceilingTexture, new Vector2(1.8f, 1.4f));
            Material leftWallMaterial = CreateOrUpdateGarageMaterial("Assets/Materials/Generated/GarageLeftWall.mat", new Color(0.12f, 0.12f, 0.13f), 0.02f, 0.16f, null, leftWallTexture, new Vector2(1.9f, 1.35f));
            Material rightWallMaterial = CreateOrUpdateGarageMaterial("Assets/Materials/Generated/GarageRightWall.mat", new Color(0.12f, 0.12f, 0.13f), 0.02f, 0.16f, null, rightWallTexture, new Vector2(1.9f, 1.35f));
            Material ceilingMaterial = CreateOrUpdateGarageMaterial("Assets/Materials/Generated/GarageCeiling.mat", new Color(0.11f, 0.11f, 0.12f), 0.01f, 0.12f, null, ceilingTexture, new Vector2(2.6f, 2.2f));
            Material platformMaterial = CreateOrUpdateGarageMaterial("Assets/Materials/Generated/GaragePlatform.mat", new Color(0.16f, 0.16f, 0.165f), 0.22f, 0.46f, null, floorTexture, new Vector2(1.15f, 1.15f));
            Material accentMaterial = CreateOrUpdateGarageMaterial("Assets/Materials/Generated/GarageAccent.mat", new Color(0.52f, 0.9f, 0.36f), 0f, 0.18f, new Color(0.08f, 0.22f, 0.06f) * 0.42f);
            Material trimMaterial = CreateOrUpdateGarageMaterial("Assets/Materials/Generated/GarageTrim.mat", new Color(0.22f, 0.22f, 0.2f), 0.1f, 0.44f);

            GameObject showroomRoot = new GameObject(rootName);
            CreateGarageEnvironment(showroomRoot.transform, floorMaterial, backWallMaterial, leftWallMaterial, rightWallMaterial, ceilingMaterial, platformMaterial, accentMaterial, trimMaterial, backdropTexture);
            CreateGarageLighting(showroomRoot.transform);
            CreateGarageReflectionProbe(showroomRoot.transform);
            AttachGlobalVolume(showroomRoot.transform, "GarageGlobalVolume", ProjectGarageVolumeProfilePath);

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

        private static void CreateGarageEnvironment(
            Transform parent,
            Material floorMaterial,
            Material backWallMaterial,
            Material leftWallMaterial,
            Material rightWallMaterial,
            Material ceilingMaterial,
            Material platformMaterial,
            Material accentMaterial,
            Material trimMaterial,
            Texture2D backdropTexture)
        {
            CreateGaragePrimitive(parent, "Floor", PrimitiveType.Cube, new Vector3(0f, -0.2f, 0f), new Vector3(28f, 0.4f, 24f), floorMaterial);
            CreateGaragePrimitive(parent, "BackWall", PrimitiveType.Cube, new Vector3(0f, 3.8f, 10.8f), new Vector3(28f, 7.6f, 0.45f), backWallMaterial);
            CreateGaragePrimitive(parent, "LeftWall", PrimitiveType.Cube, new Vector3(-13.7f, 3.8f, 0f), new Vector3(0.45f, 7.6f, 24f), leftWallMaterial);
            CreateGaragePrimitive(parent, "RightWall", PrimitiveType.Cube, new Vector3(13.7f, 3.8f, 0f), new Vector3(0.45f, 7.6f, 24f), rightWallMaterial);
            CreateGaragePrimitive(parent, "Ceiling", PrimitiveType.Cube, new Vector3(0f, 7.65f, 0f), new Vector3(28f, 0.35f, 24f), ceilingMaterial);
            CreateGaragePrimitive(parent, "RearTrim", PrimitiveType.Cube, new Vector3(0f, 1.35f, 10.45f), new Vector3(18f, 2.2f, 0.35f), trimMaterial);
            CreateGarageBackdropWall(parent, backWallMaterial, backdropTexture);

            GameObject platform = CreateGaragePrimitive(parent, "Turntable", PrimitiveType.Cylinder, new Vector3(0f, 0.2f, 1.2f), new Vector3(4.8f, 0.18f, 4.8f), platformMaterial);
            platform.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            CreateGaragePrimitive(parent, "TurntableTrim", PrimitiveType.Cylinder, new Vector3(0f, 0.28f, 1.2f), new Vector3(5.15f, 0.03f, 5.15f), accentMaterial);

            CreateGaragePrimitive(parent, "AccentBarLeft", PrimitiveType.Cube, new Vector3(-9.4f, 0.12f, 5f), new Vector3(0.18f, 0.06f, 9f), accentMaterial);
            CreateGaragePrimitive(parent, "AccentBarRight", PrimitiveType.Cube, new Vector3(9.4f, 0.12f, 5f), new Vector3(0.18f, 0.06f, 9f), accentMaterial);
            CreateGaragePrimitive(parent, "AccentRear", PrimitiveType.Cube, new Vector3(0f, 0.12f, 8.85f), new Vector3(10f, 0.06f, 0.18f), accentMaterial);
        }

        private static void CreateGarageBackdropWall(Transform parent, Material fallbackWallMaterial, Texture2D backdropTexture)
        {
            Material backdropMaterial = CreateOrUpdateGarageBackdropMaterial(GarageBackdropMaterialPath, backdropTexture);

            GameObject backdropPlane = GameObject.CreatePrimitive(PrimitiveType.Quad);
            backdropPlane.name = "GarageBackdropImage";
            backdropPlane.transform.SetParent(parent, false);
            backdropPlane.transform.localPosition = new Vector3(0f, 3.8f, 10.55f);
            backdropPlane.transform.localRotation = Quaternion.identity;
            backdropPlane.transform.localScale = new Vector3(27.2f, 8.1f, 1f);

            Renderer renderer = backdropPlane.GetComponent<Renderer>();
            renderer.sharedMaterial = backdropMaterial != null ? backdropMaterial : fallbackWallMaterial;

            Collider collider = backdropPlane.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }
        }

        private static void CreateGarageLighting(Transform parent)
        {
            CreateGarageLight(parent, "KeyLightLeft", LightType.Spot, new Vector3(-4.6f, 5.9f, -5.8f), Quaternion.Euler(36f, 28f, 0f), new Color(1f, 0.96f, 0.92f), 7.5f, 62f, 20f);
            CreateGarageLight(parent, "KeyLightRight", LightType.Spot, new Vector3(4.6f, 5.9f, -5.8f), Quaternion.Euler(36f, -28f, 0f), new Color(1f, 0.96f, 0.92f), 7.5f, 62f, 20f);
            CreateGarageLight(parent, "RearFill", LightType.Spot, new Vector3(0f, 4.8f, 8.8f), Quaternion.Euler(46f, 180f, 0f), new Color(0.58f, 0.7f, 0.95f), 2.6f, 72f, 16f);
            CreateGarageLight(parent, "NeonLeft", LightType.Point, new Vector3(-3.8f, 0.35f, 1.2f), Quaternion.identity, new Color(0.28f, 0.6f, 0.9f), 0.22f, 0f, 5.8f);
            CreateGarageLight(parent, "NeonRight", LightType.Point, new Vector3(3.8f, 0.35f, 1.2f), Quaternion.identity, new Color(0.24f, 0.78f, 0.38f), 0.22f, 0f, 5.8f);
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

            if (camera == null)
            {
                camera = cameraObject.GetComponent<Camera>();
            }

            if (camera == null)
            {
                camera = cameraObject.AddComponent<Camera>();
            }

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
            probe.intensity = 0.32f;
            probe.importance = 1000;
        }

        private static GarageUiBuild CreateGarageShowroomUi(Transform parent)
        {
            GarageUiBuild build = new GarageUiBuild();

            // ── Accent colors ──
            Color accentGreen = new Color(0.45f, 0.92f, 0.32f, 1f);
            Color accentCyan = new Color(0.28f, 0.82f, 0.88f, 1f);
            Color panelDark = new Color(0.04f, 0.045f, 0.06f, 0.92f);
            Color panelMid = new Color(0.07f, 0.075f, 0.09f, 0.88f);
            Color buttonPrimary = new Color(0.14f, 0.38f, 0.18f, 0.96f);
            Color buttonSecondary = new Color(0.12f, 0.14f, 0.18f, 0.94f);
            Color buttonAccent = new Color(0.22f, 0.52f, 0.26f, 0.98f);
            Color dimWhite = new Color(0.65f, 0.68f, 0.72f, 1f);

            // ═══════════════════════════════════════════════════════════
            // TOP HEADER — Car name, brand, navigation arrows
            // ═══════════════════════════════════════════════════════════
            GameObject topBar = CreateGaragePanel(parent, "TopBar", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(1100f, 110f), panelDark);
            // Accent stripe at bottom of header
            CreateGaragePanel(topBar.transform, "TopBarAccent", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(1100f, 3f), accentGreen);

            build.brandText = CreateAnchoredInfoText(topBar.transform, "BrandText", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), "UNDERGROUND GARAGE", TextAlignmentOptions.Center, 14f);
            SetTextColor(build.brandText, dimWhite);
            build.displayNameText = CreateAnchoredInfoText(topBar.transform, "DisplayNameText", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -4f), "STARTER COUPE", TextAlignmentOptions.Center, 42f);

            // Large navigation arrows
            build.rotateLeftButton = CreateGarageButton(topBar.transform, "<", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, -4f), new Vector2(64f, 64f), buttonSecondary);
            build.rotateRightButton = CreateGarageButton(topBar.transform, ">", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-16f, -4f), new Vector2(64f, 64f), buttonSecondary);

            // ═══════════════════════════════════════════════════════════
            // LEFT SIDEBAR — Money, Reputation, Current Car
            // ═══════════════════════════════════════════════════════════
            GameObject leftInfo = CreateGaragePanel(parent, "LeftInfo", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -120f), new Vector2(280f, 146f), panelDark);
            // Accent stripe on left edge
            CreateGaragePanel(leftInfo.transform, "LeftAccent", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(3f, 146f), accentCyan);

            build.moneyText = CreateAnchoredInfoText(leftInfo.transform, "MoneyText", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(22f, -18f), "Money: 5,000", TextAlignmentOptions.TopLeft, 20f);
            build.reputationText = CreateAnchoredInfoText(leftInfo.transform, "RepText", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(22f, -52f), "Reputation: 0", TextAlignmentOptions.TopLeft, 20f);
            build.currentCarText = CreateAnchoredInfoText(leftInfo.transform, "CarText", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(22f, -86f), "Current Car: RMCar26", TextAlignmentOptions.TopLeft, 20f);
            SetTextColor(build.moneyText, accentGreen);
            SetTextColor(build.reputationText, accentCyan);

            // ═══════════════════════════════════════════════════════════
            // RIGHT PANEL — Overall Rating
            // ═══════════════════════════════════════════════════════════
            GameObject ratingPanel = CreateGaragePanel(parent, "RatingPanel", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, -120f), new Vector2(200f, 200f), panelDark);
            // Accent stripe on right edge
            CreateGaragePanel(ratingPanel.transform, "RightAccent", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0f), new Vector2(3f, 200f), accentGreen);

            TMP_Text ratingHeader = CreateAnchoredInfoText(ratingPanel.transform, "RatingHeader", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), "PERFORMANCE", TextAlignmentOptions.Center, 14f);
            SetTextColor(ratingHeader, dimWhite);
            build.ratingText = CreateAnchoredInfoText(ratingPanel.transform, "RatingText", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), "8.56", TextAlignmentOptions.Center, 52f);
            TMP_Text ratingLabel = CreateAnchoredInfoText(ratingPanel.transform, "RatingLabel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -36f), "RATING", TextAlignmentOptions.Center, 12f);
            SetTextColor(ratingLabel, dimWhite);

            CreateGarageVerticalMeter(ratingPanel.transform, new Vector2(0.9f, 0.15f), new Color(0.15f, 0.18f, 0.15f, 0.9f), accentGreen);

            // ═══════════════════════════════════════════════════════════
            // BOTTOM PANEL — Stats + Action Buttons
            // ═══════════════════════════════════════════════════════════
            GameObject bottomPanel = CreateGaragePanel(parent, "BottomPanel", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(1920f, 170f), panelDark);
            // Top accent stripe
            CreateGaragePanel(bottomPanel.transform, "BottomAccent", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(1920f, 2f), accentGreen);

            // Stat bars — wider, positioned in the left 60%
            build.accelerationFill = CreateGarageStatBlock(bottomPanel.transform, "ACCELERATION", new Vector2(0.12f, 0.65f), out _, accentGreen, panelMid);
            build.topSpeedFill = CreateGarageStatBlock(bottomPanel.transform, "TOP SPEED", new Vector2(0.32f, 0.65f), out _, accentCyan, panelMid);
            build.handlingFill = CreateGarageStatBlock(bottomPanel.transform, "HANDLING", new Vector2(0.52f, 0.65f), out _, accentGreen, panelMid);

            // Status text
            build.statusText = CreateAnchoredInfoText(bottomPanel.transform, "StatusText", new Vector2(0.02f, 0.15f), new Vector2(0f, 0.5f), Vector2.zero, "Use < > to browse cars. Right-click drag to rotate.", TextAlignmentOptions.Left, 16f);
            SetTextColor(build.statusText, dimWhite);

            // Action buttons — right side, larger, with clear labeling
            float buttonY = 0.38f;
            build.repairButton = CreateGarageButton(bottomPanel.transform, "Repair Car", new Vector2(0.62f, buttonY), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(170f, 48f), buttonSecondary);
            build.upgradeButton = CreateGarageButton(bottomPanel.transform, "Buy Engine", new Vector2(0.74f, buttonY), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(170f, 48f), buttonPrimary);
            build.bankButton = CreateGarageButton(bottomPanel.transform, "Bank Progress", new Vector2(0.86f, buttonY), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(180f, 48f), buttonSecondary);
            build.continueButton = CreateGarageButton(bottomPanel.transform, "Continue", new Vector2(0.96f, buttonY), new Vector2(1f, 0.5f), new Vector2(-14f, 0f), new Vector2(160f, 48f), buttonAccent);

            return build;
        }

        private static void SetTextColor(TMP_Text text, Color color)
        {
            if (text != null)
            {
                text.color = color;
            }
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

        private static Image CreateGarageStatBlock(Transform parent, string title, Vector2 anchor, out TMP_Text valueText, Color fillColor, Color blockColor)
        {
            GameObject block = CreateGaragePanel(parent, $"{title}_Block", anchor, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(280f, 72f), blockColor);

            TMP_Text titleText = CreateAnchoredInfoText(block.transform, $"{title}_Title", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), title, TextAlignmentOptions.Center, 14f);
            SetTextColor(titleText, new Color(0.55f, 0.58f, 0.62f, 1f));
            valueText = CreateAnchoredInfoText(block.transform, $"{title}_Value", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), string.Empty, TextAlignmentOptions.Center, 14f);

            GameObject track = CreateGaragePanel(block.transform, $"{title}_Track", new Vector2(0.5f, 0.38f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(230f, 12f), new Color(0.12f, 0.14f, 0.16f, 1f));
            GameObject fillObject = CreateGaragePanel(track.transform, $"{title}_Fill", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(230f, 12f), fillColor);
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

        private static Material CreateOrUpdateGarageMaterial(
            string path,
            Color baseColor,
            float metallic,
            float smoothness,
            Color? emissionColor = null,
            Texture texture = null,
            Vector2? textureScale = null)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = GetPreferredLitShader();
            if (material == null)
            {
                material = new Material(shader ?? Shader.Find("Standard"));
                AssetDatabase.CreateAsset(material, path);
            }
            else if (shader != null && material.shader != shader)
            {
                material.shader = shader;
            }

            material.color = baseColor;
            if (texture != null)
            {
                material.color = Color.white;
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", texture != null ? Color.white : baseColor);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", texture != null ? Color.white : baseColor);
            }

            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", metallic);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", smoothness);
            }

            if (texture != null)
            {
                SetGarageMaterialTexture(material, texture);

                if (textureScale.HasValue)
                {
                    SetGarageMaterialTextureScale(material, textureScale.Value);
                }
            }

            if (emissionColor.HasValue)
            {
                Color color = emissionColor.Value;
                material.EnableKeyword("_EMISSION");
                if (material.HasProperty("_EmissionColor"))
                {
                    material.SetColor("_EmissionColor", color);
                }
                if (material.HasProperty("_EmissiveColor"))
                {
                    material.SetColor("_EmissiveColor", color);
                }
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void SetGarageMaterialTexture(Material material, Texture texture)
        {
            if (material == null || texture == null)
            {
                return;
            }

            string[] textureProperties =
            {
                "_BaseColorMap",
                "_BaseMap",
                "_MainTex"
            };

            for (int i = 0; i < textureProperties.Length; i++)
            {
                if (material.HasProperty(textureProperties[i]))
                {
                    material.SetTexture(textureProperties[i], texture);
                }
            }
        }

        private static Texture2D LoadGarageSurfaceTexture(string assetPath, bool clamp)
        {
            ConfigureGarageTextureImport(assetPath, clamp);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        private static void ConfigureGarageTextureImport(string assetPath, bool clamp)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            bool changed = false;

            if (importer.textureType != TextureImporterType.Default)
            {
                importer.textureType = TextureImporterType.Default;
                changed = true;
            }

            if (importer.textureShape != TextureImporterShape.Texture2D)
            {
                importer.textureShape = TextureImporterShape.Texture2D;
                changed = true;
            }

            if (importer.spriteImportMode != SpriteImportMode.None)
            {
                importer.spriteImportMode = SpriteImportMode.None;
                changed = true;
            }

            if (!importer.mipmapEnabled)
            {
                importer.mipmapEnabled = true;
                changed = true;
            }

            if (importer.wrapMode != (clamp ? TextureWrapMode.Clamp : TextureWrapMode.Repeat))
            {
                importer.wrapMode = clamp ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
                changed = true;
            }

            if (importer.filterMode != FilterMode.Trilinear)
            {
                importer.filterMode = FilterMode.Trilinear;
                changed = true;
            }

            if (importer.anisoLevel != 4)
            {
                importer.anisoLevel = 4;
                changed = true;
            }

            if (importer.maxTextureSize < 4096)
            {
                importer.maxTextureSize = 4096;
                changed = true;
            }

            if (importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = false;
                changed = true;
            }

            if (changed)
            {
                importer.SaveAndReimport();
            }
        }

        private static void SetGarageMaterialTextureScale(Material material, Vector2 scale)
        {
            if (material == null)
            {
                return;
            }

            string[] textureProperties =
            {
                "_BaseColorMap",
                "_BaseMap",
                "_MainTex"
            };

            for (int i = 0; i < textureProperties.Length; i++)
            {
                if (material.HasProperty(textureProperties[i]))
                {
                    material.SetTextureScale(textureProperties[i], scale);
                }
            }
        }

        private static Material CreateOrUpdateGarageBackdropMaterial(string path, Texture2D backdropTexture)
        {
            Shader shader = Shader.Find("Unlit/Texture");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader ?? Shader.Find("Standard"));
                AssetDatabase.CreateAsset(material, path);
            }
            else if (shader != null && material.shader != shader)
            {
                material.shader = shader;
            }

            if (backdropTexture != null)
            {
                if (material.HasProperty("_MainTex"))
                {
                    material.SetTexture("_MainTex", backdropTexture);
                }

                material.color = Color.white;
            }
            else
            {
                material.color = new Color(0.18f, 0.09f, 0.2f, 1f);
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
