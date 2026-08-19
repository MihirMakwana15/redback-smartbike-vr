using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SmartBike.Quests
{
    public sealed class QuestHud : MonoBehaviour
    {
        [SerializeField] private bool visible = true;

        private Text objectiveText;
        private Text pointsText;
        private Text speedText;
        private Text notificationText;
        private Text headingText;
        private Text completionTitleText;
        private Text rewardText;
        private Text nextMissionText;
        private Text primaryButtonText;
        private GameObject completionPanel;
        private GameObject notificationPanel;
        private QuestCampaignController campaign;
        private float notificationUntil;
        private string previousStatus;

        private readonly Color darkPanel = new Color(0.025f, 0.055f, 0.09f, 0.9f);
        private readonly Color cyan = new Color(0.18f, 0.9f, 1f);
        private readonly Color gold = new Color(1f, 0.78f, 0.12f);

        private void Start()
        {
            if (!visible)
                return;

            BuildHud();
            campaign = FindObjectOfType<QuestCampaignController>();
            if (campaign != null)
                campaign.LevelStarted += HandleLevelStarted;
            if (QuestManager.Instance != null)
            {
                QuestManager.Instance.StateChanged += Refresh;
                QuestManager.Instance.FeedbackRaised += HandleFeedback;
            }
            if (campaign != null)
                campaign.LevelStarted -= HandleLevelStarted;
            Refresh();
        }

        private void OnDestroy()
        {
            if (QuestManager.Instance != null)
            {
                QuestManager.Instance.StateChanged -= Refresh;
                QuestManager.Instance.FeedbackRaised -= HandleFeedback;
            }
        }

        private void Update()
        {
            QuestManager manager = QuestManager.Instance;
            if (manager == null || speedText == null)
                return;

            speedText.text = $"SPEED  {manager.CurrentMovementSpeed * 3.6f:0.0} km/h";
            if (notificationText != null && Time.unscaledTime > notificationUntil)
                notificationPanel.SetActive(false);

            string status = manager.GetStatusText();
            if (manager.HasActiveQuest && status != previousStatus)
            {
                previousStatus = status;
                Refresh();
            }
        }

        private void Refresh()
        {
            QuestManager manager = QuestManager.Instance;
            if (manager == null || objectiveText == null)
                return;

            pointsText.text = $"POINTS  {manager.Points}";
            objectiveText.text = FormatStatus(manager.GetStatusText());
            if (campaign != null)
                headingText.text = $"LEVEL {campaign.CurrentLevelNumber}  •  {manager.ActiveQuest?.Title ?? manager.LastCompletedQuest}";

            // Starting another level must always remove the previous reward overlay,
            // even if event ordering differs between editor, offline and Fusion play.
            if (manager.HasActiveQuest)
                completionPanel.SetActive(false);

            if (manager.HasCompletedQuest)
            {
                completionPanel.SetActive(true);
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
                int levelNumber = campaign != null ? campaign.CurrentLevelNumber : 1;
                completionTitleText.text = $"LEVEL {levelNumber} COMPLETE!";
                rewardText.text = $"{manager.LastCompletedQuest}\n\n+{manager.LastRewardPoints} POINTS\nBADGE AND NEW CONTENT UNLOCKED";

                if (campaign != null && campaign.HasNextLevel)
                {
                    nextMissionText.text = $"NEXT: LEVEL {levelNumber + 1}\n{campaign.NextLevel.Title}\n{campaign.NextLevel.Description}";
                    primaryButtonText.text = "START NEXT LEVEL";
                }
                else
                {
                    nextMissionText.text = "CAMPAIGN COMPLETE\nYou completed every SmartBike mission!";
                    primaryButtonText.text = "RIDE AGAIN";
                }
                ShowNotification("MISSION COMPLETE — REWARDS UNLOCKED", 10f);
            }
        }

        private static string FormatStatus(string status)
        {
            int firstLine = status.IndexOf('\n');
            if (firstLine >= 0)
                status = status.Substring(firstLine + 1);
            return status.Replace("[Complete]", "✓").Replace("[ ]", "○");
        }

        private void BuildHud()
        {
            if (FindObjectOfType<EventSystem>() == null)
            {
                GameObject eventSystem = new GameObject("Quest UI EventSystem");
                eventSystem.AddComponent<EventSystem>();
                eventSystem.AddComponent<StandaloneInputModule>();
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            GameObject canvasObject = new GameObject("Quest HUD Canvas");
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasObject.AddComponent<GraphicRaycaster>();

            GameObject panel = CreatePanel(canvasObject.transform, "Mission Panel", darkPanel);
            SetRect(panel.GetComponent<RectTransform>(), new Vector2(30f, -30f), new Vector2(680f, 430f),
                new Vector2(0f, 1f));

            headingText = CreateText(panel.transform, "Heading", "CITY EXPLORER", font, 34, cyan,
                FontStyle.Bold, TextAnchor.UpperLeft);
            SetRect(headingText.rectTransform, new Vector2(28f, -24f), new Vector2(620f, 52f), new Vector2(0f, 1f));

            objectiveText = CreateText(panel.transform, "Objectives", "Preparing mission...", font, 25,
                Color.white, FontStyle.Normal, TextAnchor.UpperLeft);
            SetRect(objectiveText.rectTransform, new Vector2(28f, -88f), new Vector2(620f, 285f),
                new Vector2(0f, 1f));

            pointsText = CreateText(panel.transform, "Points", "POINTS  0", font, 26, gold,
                FontStyle.Bold, TextAnchor.MiddleLeft);
            SetRect(pointsText.rectTransform, new Vector2(28f, 12f), new Vector2(290f, 48f), Vector2.zero);

            speedText = CreateText(panel.transform, "Speed", "SPEED  0.0 km/h", font, 26, cyan,
                FontStyle.Bold, TextAnchor.MiddleRight);
            SetRect(speedText.rectTransform, new Vector2(350f, 12f), new Vector2(295f, 48f), Vector2.zero);

            notificationPanel = CreatePanel(canvasObject.transform, "Mission Notification",
                new Color(0.02f, 0.35f, 0.48f, 0.92f));
            SetRect(notificationPanel.GetComponent<RectTransform>(), new Vector2(0f, -70f),
                new Vector2(850f, 66f), new Vector2(0.5f, 1f));
            notificationText = CreateText(notificationPanel.transform, "Text", string.Empty, font,
                28, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            notificationText.rectTransform.anchorMin = Vector2.zero;
            notificationText.rectTransform.anchorMax = Vector2.one;
            notificationText.rectTransform.offsetMin = new Vector2(15f, 0f);
            notificationText.rectTransform.offsetMax = new Vector2(-15f, 0f);
            notificationPanel.SetActive(false);

            BuildCompletionPanel(canvasObject.transform, font);
        }

        private void BuildCompletionPanel(Transform canvas, Font font)
        {
            completionPanel = CreatePanel(canvas, "Mission Complete Panel", new Color(0.02f, 0.04f, 0.075f, 0.97f));
            SetRect(completionPanel.GetComponent<RectTransform>(), Vector2.zero, new Vector2(720f, 480f),
                new Vector2(0.5f, 0.5f));

            completionTitleText = CreateText(completionPanel.transform, "Title", "MISSION COMPLETE", font, 42, gold,
                FontStyle.Bold, TextAnchor.MiddleCenter);
            SetRect(completionTitleText.rectTransform, new Vector2(0f, -35f), new Vector2(650f, 70f), new Vector2(0.5f, 1f));

            rewardText = CreateText(completionPanel.transform, "RewardText", string.Empty, font, 24,
                Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            SetRect(rewardText.rectTransform, new Vector2(0f, 55f), new Vector2(620f, 170f), new Vector2(0.5f, 0.5f));

            nextMissionText = CreateText(completionPanel.transform, "NextMission", string.Empty, font, 20,
                cyan, FontStyle.Normal, TextAnchor.MiddleCenter);
            SetRect(nextMissionText.rectTransform, new Vector2(0f, -85f), new Vector2(620f, 115f), new Vector2(0.5f, 0.5f));

            Button replay = CreateButton(completionPanel.transform, "START NEXT LEVEL", font);
            primaryButtonText = replay.GetComponentInChildren<Text>();
            SetRect(replay.GetComponent<RectTransform>(), new Vector2(0f, 32f), new Vector2(300f, 64f),
                new Vector2(0.5f, 0f));
            replay.onClick.AddListener(HandlePrimaryAction);
            completionPanel.SetActive(false);
        }

        private void ShowNotification(string message, float seconds)
        {
            notificationText.text = message;
            notificationPanel.SetActive(true);
            notificationUntil = Time.unscaledTime + seconds;
        }

        private void HandleFeedback(string message)
        {
            ShowNotification(message.ToUpperInvariant(), 2.5f);
        }

        private void HandleLevelStarted(int levelNumber, QuestDefinition definition)
        {
            completionPanel.SetActive(false);
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            previousStatus = string.Empty;
            headingText.text = $"LEVEL {levelNumber}  •  {definition.Title}";
            ShowNotification($"LEVEL {levelNumber} STARTED — {definition.Title}".ToUpperInvariant(), 4f);
            Refresh();
        }

        private void HandlePrimaryAction()
        {
            if (campaign != null && campaign.HasNextLevel)
            {
                completionPanel.SetActive(false);
                campaign.StartNextLevel();
            }
            else
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private static GameObject CreatePanel(Transform parent, string name, Color colour)
        {
            GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            panel.GetComponent<Image>().color = colour;
            return panel;
        }

        private static Text CreateText(Transform parent, string name, string value, Font font, int size,
            Color colour, FontStyle style, TextAnchor alignment)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            Text text = textObject.GetComponent<Text>();
            text.text = value;
            text.font = font;
            text.fontSize = size;
            text.color = colour;
            text.fontStyle = style;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private Button CreateButton(Transform parent, string label, Font font)
        {
            GameObject buttonObject = CreatePanel(parent, "Replay Button", new Color(0.05f, 0.58f, 0.72f, 1f));
            Button button = buttonObject.AddComponent<Button>();
            Text text = CreateText(buttonObject.transform, "Text", label, font, 23, Color.white,
                FontStyle.Bold, TextAnchor.MiddleCenter);
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            return button;
        }

        private static void SetRect(RectTransform rect, Vector2 position, Vector2 size, Vector2 anchor)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
