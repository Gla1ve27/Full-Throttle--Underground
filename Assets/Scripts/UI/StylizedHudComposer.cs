using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Underground.UI
{
    [RequireComponent(typeof(Canvas))]
    public class StylizedHudComposer : MonoBehaviour
    {
        [SerializeField] private Color accentColor = new Color(0.96f, 0.28f, 0.56f, 0.95f);
        [SerializeField] private Color secondaryAccentColor = new Color(0.25f, 0.62f, 1f, 0.95f);
        [SerializeField] private Color panelColor = new Color(0.06f, 0.08f, 0.12f, 0.54f);
        [SerializeField] private Color softTextColor = new Color(0.88f, 0.92f, 1f, 0.92f);
        [SerializeField] private GameObject speedometerPrefab;

        private Sprite cachedSprite;

        private void Awake()
        {
            Compose();
        }

        public void Compose()
        {
            HUDController hudController = GetComponent<HUDController>();
            if (hudController == null)
            {
                hudController = gameObject.AddComponent<HUDController>();
            }

            HudRadarController radarController = GetComponent<HudRadarController>();
            if (radarController == null)
            {
                radarController = gameObject.AddComponent<HudRadarController>();
            }

            RectTransform existingRoot = transform.Find("ChaseHudRoot") as RectTransform;
            if (existingRoot != null)
            {
                RepairExistingHud(existingRoot);
                hudController.RefreshViewBindings();
                return;
            }

            ClearLegacyChildren();

            RectTransform root = CreateRect("ChaseHudRoot", transform);
            Stretch(root);

            BuildTopStatus(root, out TMP_Text levelText, out TMP_Text bankText, out TMP_Text riskText, out TMP_Text nextLevelText, out TMP_Text challengeText, out TMP_Text dayNightText, out Image riskRingFill);
            BuildBottomCenter(root, out TMP_Text sessionMoneyText);
            BuildSpeedCluster(root, out global::Speedometer speedometer, out TMP_Text gearText, out TMP_Text damageText);
            BuildRadar(root, radarController);
            BuildRacePrompt(root);

            hudController.BindView(
                speedometer,
                null,
                gearText,
                bankText,
                sessionMoneyText,
                levelText,
                riskText,
                damageText,
                dayNightText,
                nextLevelText,
                challengeText,
                null,
                null,
                riskRingFill);

            hudController.RefreshViewBindings();
        }

        private void BuildTopStatus(RectTransform parent, out TMP_Text levelText, out TMP_Text bankText, out TMP_Text riskText, out TMP_Text nextLevelText, out TMP_Text challengeText, out TMP_Text dayNightText, out Image riskRingFill)
        {
            RectTransform topRoot = CreateRect("TopStatus", parent);
            SetAnchors(topRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(940f, 120f));

            RectTransform levelCluster = CreateRect("LevelCluster", topRoot);
            SetAnchors(levelCluster, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(230f, 90f));
            levelText = CreateText("LevelValue", levelCluster, "1", 46f, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
            SetAnchors(levelText.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 8f), new Vector2(62f, 62f));
            bankText = CreateText("LevelDetail", levelCluster, "BANK\n0", 17f, FontStyles.Bold, TextAlignmentOptions.Left, softTextColor);
            SetAnchors(bankText.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(76f, 4f), new Vector2(150f, 60f));

            RectTransform riskCluster = CreateRect("RiskCluster", topRoot);
            SetAnchors(riskCluster, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120f, 120f));
            Image riskBack = CreateImage("RiskBack", riskCluster, new Color(0.12f, 0.15f, 0.2f, 0.82f));
            ConfigureRadial(riskBack, 1f, secondaryAccentColor * 0.22f);
            riskBack.rectTransform.sizeDelta = new Vector2(92f, 92f);
            riskRingFill = CreateImage("RiskFill", riskCluster, accentColor);
            ConfigureRadial(riskRingFill, 0.1f, accentColor);
            riskRingFill.rectTransform.sizeDelta = new Vector2(92f, 92f);
            Image riskInner = CreateImage("RiskInner", riskCluster, new Color(0.05f, 0.07f, 0.1f, 0.94f));
            riskInner.rectTransform.sizeDelta = new Vector2(64f, 64f);
            riskText = CreateText("RiskValue", riskCluster, "0", 30f, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
            SetAnchors(riskText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), new Vector2(60f, 44f));

            RectTransform nextCluster = CreateRect("NextCluster", topRoot);
            SetAnchors(nextCluster, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0f), new Vector2(240f, 90f));
            nextLevelText = CreateText("NextLevel", nextCluster, "NEXT 2\n500 REP", 17f, FontStyles.Bold, TextAlignmentOptions.Right, softTextColor);
            SetAnchors(nextLevelText.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 4f), new Vector2(180f, 60f));

            RectTransform banner = CreateRect("ChallengeBanner", parent);
            SetAnchors(banner, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-36f, -42f), new Vector2(380f, 70f));
            Image bannerBack = banner.gameObject.AddComponent<Image>();
            bannerBack.sprite = GetSprite();
            bannerBack.type = Image.Type.Sliced;
            bannerBack.color = new Color(0.23f, 0.16f, 0.45f, 0.78f);

            challengeText = CreateText("ChallengeTitle", banner, "RACER CHALLENGES", 24f, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
            SetAnchors(challengeText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-8f, 0f), new Vector2(260f, 34f));
            TMP_Text clockText = CreateText("ClockValue", banner, "12:00 PM", 16f, FontStyles.Bold, TextAlignmentOptions.Center, new Color(0.92f, 0.96f, 1f, 0.88f));
            SetAnchors(clockText.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-116f, 0f), new Vector2(112f, 28f));
            dayNightText = CreateText("ChallengeState", banner, "DAY", 18f, FontStyles.Bold, TextAlignmentOptions.Center, new Color(0.92f, 0.96f, 1f, 0.88f));
            SetAnchors(dayNightText.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-28f, 0f), new Vector2(72f, 30f));
        }

        private void BuildBottomCenter(RectTransform parent, out TMP_Text sessionMoneyText)
        {
            RectTransform centerRoot = CreateRect("BottomCenter", parent);
            SetAnchors(centerRoot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 32f), new Vector2(360f, 40f));
            sessionMoneyText = CreateText("SessionMoney", centerRoot, "SESSION $0", 24f, FontStyles.Bold, TextAlignmentOptions.Center, accentColor);
            Stretch(sessionMoneyText.rectTransform);
        }

        private void BuildRacePrompt(RectTransform parent)
        {
            RectTransform promptRoot = CreateRect("RacePromptRoot", parent);
            SetAnchors(promptRoot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 86f), new Vector2(560f, 88f));
            Image background = promptRoot.gameObject.AddComponent<Image>();
            background.sprite = GetSprite();
            background.color = new Color(0.05f, 0.07f, 0.1f, 0.88f);

            TMP_Text promptText = CreateText("RacePromptValue", promptRoot, "Press F or Enter to start race", 24f, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
            SetAnchors(promptText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(500f, 32f));

            TMP_Text objectiveText = CreateText("RaceObjectiveValue", promptRoot, "Street race marker detected", 16f, FontStyles.Bold, TextAlignmentOptions.Center, new Color(1f, 0.72f, 0.34f, 0.94f));
            SetAnchors(objectiveText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(500f, 24f));

            promptRoot.gameObject.SetActive(false);
        }

        private void BuildSpeedCluster(RectTransform parent, out global::Speedometer speedometer, out TMP_Text gearText, out TMP_Text damageText)
        {
            RectTransform speedRoot = CreateRect("SpeedCluster", parent);
            SetAnchors(speedRoot, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-28f, 26f), new Vector2(320f, 190f));

            RectTransform speedometerRect = CreateSpeedometer(speedRoot, out speedometer);
            speedometerRect.anchorMin = new Vector2(1f, 0f);
            speedometerRect.anchorMax = new Vector2(1f, 0f);
            speedometerRect.pivot = new Vector2(1f, 0f);
            speedometerRect.anchoredPosition = new Vector2(-6f, 18f);

            RectTransform gearPill = CreateRect("GearPill", speedRoot);
            SetAnchors(gearPill, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(12f, 72f), new Vector2(72f, 40f));
            Image gearPillImage = gearPill.gameObject.AddComponent<Image>();
            gearPillImage.sprite = GetSprite();
            gearPillImage.color = new Color(0.18f, 0.24f, 0.34f, 0.9f);
            gearText = CreateText("GearValue", gearPill, "1", 18f, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
            Stretch(gearText.rectTransform);

            damageText = CreateText("DamageValue", speedRoot, "DMG 0%", 18f, FontStyles.Bold, TextAlignmentOptions.Center, new Color(1f, 0.7f, 0.7f, 0.94f));
            SetAnchors(damageText.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(12f, 28f), new Vector2(140f, 28f));
        }

        private void BuildRadar(RectTransform parent, HudRadarController radarController)
        {
            RectTransform radarRoot = CreateRect("RadarCluster", parent);
            SetAnchors(radarRoot, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(34f, 24f), new Vector2(230f, 250f));

            RectTransform radarFrame = CreateRect("RadarFrame", radarRoot);
            SetAnchors(radarFrame, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(192f, 192f));
            Image radarBack = radarFrame.gameObject.AddComponent<Image>();
            radarBack.sprite = GetSprite();
            radarBack.color = new Color(0.06f, 0.08f, 0.12f, 0.74f);

            Image radarRing = CreateImage("RadarRing", radarFrame, secondaryAccentColor * 0.55f);
            radarRing.rectTransform.sizeDelta = new Vector2(180f, 180f);

            RectTransform markerLayer = CreateRect("Markers", radarFrame);
            Stretch(markerLayer);

            RectTransform playerMarker = CreateRect("PlayerMarker", markerLayer);
            SetAnchors(playerMarker, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10f, 10f));
            Image playerMarkerImage = playerMarker.gameObject.AddComponent<Image>();
            playerMarkerImage.sprite = GetSprite();
            playerMarkerImage.color = accentColor;
            playerMarker.localRotation = Quaternion.Euler(0f, 0f, 45f);

            TMP_Text radarLabel = CreateText("RadarLabel", radarRoot, "CITY SCAN", 18f, FontStyles.Bold, TextAlignmentOptions.Center, softTextColor);
            SetAnchors(radarLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(140f, 22f));

            radarController.BindView(radarFrame, markerLayer, playerMarker);
        }

        private void ClearLegacyChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                if (Application.isPlaying)
                {
                    Destroy(transform.GetChild(i).gameObject);
                }
                else
                {
                    DestroyImmediate(transform.GetChild(i).gameObject);
                }
            }
        }

        private Image CreateImage(string name, Transform parent, Color color)
        {
            RectTransform rect = CreateRect(name, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = GetSprite();
            image.color = color;
            return image;
        }

        private RectTransform CreateSpeedometer(Transform parent, out global::Speedometer speedometer)
        {
            GameObject speedometerObject;
            if (speedometerPrefab != null)
            {
                speedometerObject = Instantiate(speedometerPrefab, parent, false);
                speedometerObject.name = speedometerPrefab.name;
            }
            else
            {
                speedometerObject = new GameObject("Speedometer", typeof(RectTransform), typeof(Image), typeof(global::Speedometer));
                speedometerObject.transform.SetParent(parent, false);
                Image image = speedometerObject.GetComponent<Image>();
                image.sprite = GetSprite();
                image.color = panelColor;
            }

            speedometer = speedometerObject.GetComponent<global::Speedometer>();
            return speedometerObject.GetComponent<RectTransform>();
        }

        private void RepairExistingHud(RectTransform root)
        {
            Transform speedCluster = FindDescendant(root, "SpeedCluster");
            EnsureSpeedometer(speedCluster);
            EnsureClock(root);
            EnsureRacePrompt(root);
        }

        private void EnsureSpeedometer(Transform speedCluster)
        {
            if (speedCluster == null)
            {
                return;
            }

            global::Speedometer existingSpeedometer = speedCluster.GetComponentInChildren<global::Speedometer>(true);
            bool hasValidSpeedometer = existingSpeedometer != null
                && existingSpeedometer.arrow != null
                && existingSpeedometer.speedLabel != null;

            if (hasValidSpeedometer || speedometerPrefab == null)
            {
                return;
            }

            Vector3 preservedScale = Vector3.one;
            if (existingSpeedometer != null)
            {
                preservedScale = existingSpeedometer.transform.localScale;

                if (Application.isPlaying)
                {
                    Destroy(existingSpeedometer.gameObject);
                }
                else
                {
                    DestroyImmediate(existingSpeedometer.gameObject);
                }
            }

            GameObject speedometerObject = Instantiate(speedometerPrefab, speedCluster, false);
            speedometerObject.name = speedometerPrefab.name;
            RectTransform speedometerRect = speedometerObject.GetComponent<RectTransform>();
            speedometerRect.anchorMin = new Vector2(1f, 0f);
            speedometerRect.anchorMax = new Vector2(1f, 0f);
            speedometerRect.pivot = new Vector2(1f, 0f);
            speedometerRect.anchoredPosition = new Vector2(-6f, 18f);
            speedometerRect.localScale = preservedScale;
        }

        private void EnsureClock(RectTransform root)
        {
            if (FindDescendant(root, "ClockValue") != null)
            {
                return;
            }

            Transform challengeBanner = FindDescendant(root, "ChallengeBanner");
            if (challengeBanner == null)
            {
                return;
            }

            TMP_Text clockText = CreateText("ClockValue", challengeBanner, "12:00 PM", 16f, FontStyles.Bold, TextAlignmentOptions.Center, new Color(0.92f, 0.96f, 1f, 0.88f));
            SetAnchors(clockText.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-116f, 0f), new Vector2(112f, 28f));
        }

        private void EnsureRacePrompt(RectTransform root)
        {
            if (FindDescendant(root, "RacePromptRoot") != null)
            {
                return;
            }

            BuildRacePrompt(root);
        }

        private TMP_Text CreateText(string name, Transform parent, string value, float fontSize, FontStyles style, TextAlignmentOptions alignment, Color color)
        {
            RectTransform rect = CreateRect(name, parent);
            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            return text;
        }

        private RectTransform CreateRect(string name, Transform parent)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            return gameObject.GetComponent<RectTransform>();
        }

        private void ConfigureRadial(Image image, float fillAmount, Color color)
        {
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Radial360;
            image.fillOrigin = (int)Image.Origin360.Top;
            image.fillClockwise = true;
            image.fillAmount = fillAmount;
            image.color = color;
        }

        private Sprite GetSprite()
        {
            if (cachedSprite == null)
            {
                Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                texture.SetPixel(0, 0, Color.white);
                texture.Apply();
                texture.hideFlags = HideFlags.HideAndDontSave;
                cachedSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
                cachedSprite.name = "GeneratedHudSprite";
            }

            return cachedSprite;
        }

        private static void Stretch(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
        }

        private static void SetAnchors(RectTransform rectTransform, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.pivot = pivot;
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = sizeDelta;
        }

        private static Transform FindDescendant(Transform root, string objectName)
        {
            if (root == null || string.IsNullOrEmpty(objectName))
            {
                return null;
            }

            if (root.name == objectName)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDescendant(root.GetChild(i), objectName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
