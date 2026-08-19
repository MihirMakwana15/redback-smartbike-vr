using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace SmartBike.Quests
{
    public sealed class QuestManager : MonoBehaviour
    {
        [Serializable] public sealed class StringEvent : UnityEvent<string> { }
        [Serializable] public sealed class IntEvent : UnityEvent<int> { }

        private sealed class ObjectiveProgress
        {
            public QuestObjectiveDefinition Definition;
            public int Count;
            public float Seconds;
            public int NextCheckpointIndex;
            public bool RouteInvalid;
            public bool Completed;
        }

        public static QuestManager Instance { get; private set; }

        [Header("Quest")]
        [SerializeField] private QuestDefinition startingQuest;
        [SerializeField] private bool startAutomatically = true;
        [SerializeField] private bool saveRewards = true;

        [Header("Events")]
        [SerializeField] private StringEvent onQuestStarted = new StringEvent();
        [SerializeField] private StringEvent onProgressChanged = new StringEvent();
        [SerializeField] private StringEvent onQuestCompleted = new StringEvent();
        [SerializeField] private IntEvent onPointsChanged = new IntEvent();

        private readonly List<ObjectiveProgress> progress = new List<ObjectiveProgress>();
        private QuestDefinition activeQuest;
        private float currentMovementSpeed;
        private int points;

        public QuestDefinition ActiveQuest => activeQuest;
        public int Points => points;
        public bool HasActiveQuest => activeQuest != null;
        public string LastCompletedQuest { get; private set; }
        public int LastRewardPoints { get; private set; }
        public float CurrentMovementSpeed => currentMovementSpeed;
        public bool HasCompletedQuest => !string.IsNullOrEmpty(LastCompletedQuest);
        public event Action StateChanged;
        public event Action<string> FeedbackRaised;
        public event Action<QuestDefinition> QuestCompleted;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            points = PlayerPrefs.GetInt("SmartBike.Quests.Points", 0);
        }

        private void Start()
        {
            if (startAutomatically && startingQuest != null)
                StartQuest(startingQuest);
        }

        private void Update()
        {
            if (activeQuest == null || currentMovementSpeed <= 0f)
                return;

            bool changed = false;
            foreach (ObjectiveProgress item in progress)
            {
                if (item.Completed || item.Definition.Type != QuestObjectiveType.MaintainMovement)
                    continue;
                if (currentMovementSpeed < item.Definition.MinimumMovementSpeed)
                    continue;

                item.Seconds += Time.unscaledDeltaTime;
                bool nowCompleted = item.Seconds >= item.Definition.TargetSeconds;
                if (nowCompleted != item.Completed)
                {
                    item.Completed = nowCompleted;
                    changed = true;
                }
            }

            if (changed)
                PublishState();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void StartQuest(QuestDefinition definition)
        {
            if (definition == null)
            {
                Debug.LogError("Cannot start a null quest definition.");
                return;
            }

            if (definition.Objectives == null || definition.Objectives.Count == 0)
            {
                Debug.LogError($"Quest '{definition.name}' has no objectives.");
                return;
            }

            activeQuest = definition;
            LastCompletedQuest = string.Empty;
            LastRewardPoints = 0;
            progress.Clear();

            foreach (QuestObjectiveDefinition objective in definition.Objectives)
            {
                progress.Add(new ObjectiveProgress { Definition = objective });
            }

            onQuestStarted.Invoke(definition.Title);
            PublishState();
        }

        public void CancelQuest()
        {
            activeQuest = null;
            progress.Clear();
            PublishState();
        }

        public void SetMovementSpeed(float metresPerSecond)
        {
            currentMovementSpeed = Mathf.Max(0f, metresPerSecond);
        }

        public void RaiseFeedback(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
                FeedbackRaised?.Invoke(message);
        }

        public void ReportCollectible(string collectibleId = "")
        {
            IncrementMatching(QuestObjectiveType.CollectItems, collectibleId);
        }

        public void ReportAreaExplored(string areaId)
        {
            IncrementMatching(QuestObjectiveType.ExploreArea, areaId);
        }

        public void ReportCheckpoint(string routeId, int checkpointIndex)
        {
            if (activeQuest == null)
                return;

            bool changed = false;
            bool routeRejected = false;
            foreach (ObjectiveProgress item in progress)
            {
                if (item.Completed || !Matches(item.Definition.TargetId, routeId))
                    continue;

                if (item.Definition.Type == QuestObjectiveType.ReachCheckpoints)
                {
                    item.Count++;
                    item.Completed = item.Count >= item.Definition.TargetCount;
                    changed = true;
                    continue;
                }

                if (item.Definition.Type !=
                    QuestObjectiveType.CompleteRouteWithoutMissingCheckpoint)
                    continue;

                if (checkpointIndex != item.NextCheckpointIndex)
                {
                    item.RouteInvalid = true;
                    routeRejected = true;
                    FeedbackRaised?.Invoke("Checkpoint order missed — restart the ride to retry the perfect route");
                    changed = true;
                    continue;
                }

                item.NextCheckpointIndex++;
                item.Count = item.NextCheckpointIndex;
                item.Completed = !item.RouteInvalid &&
                                 item.Count >= item.Definition.TargetCount;
                changed = true;
            }

            if (changed)
            {
                if (!routeRejected)
                    FeedbackRaised?.Invoke($"Checkpoint {checkpointIndex + 1} reached");
                PublishState();
            }
        }

        public string GetStatusText()
        {
            if (activeQuest == null)
                return "No active quest";

            var lines = new List<string> { activeQuest.Title };
            foreach (ObjectiveProgress item in progress)
            {
                string marker = item.Completed ? "[Complete]" : "[ ]";
                string value;

                if (item.Definition.Type == QuestObjectiveType.MaintainMovement)
                    value = $"{Mathf.Min(item.Seconds, item.Definition.TargetSeconds):0.0}/" +
                            $"{item.Definition.TargetSeconds:0.0}s";
                else if (item.Definition.Type ==
                         QuestObjectiveType.CompleteRouteWithoutMissingCheckpoint &&
                         item.RouteInvalid)
                    value = "checkpoint missed";
                else
                    value = $"{Mathf.Min(item.Count, item.Definition.TargetCount)}/" +
                            item.Definition.TargetCount;

                lines.Add($"{marker} {item.Definition.Description} ({value})");
            }

            return string.Join("\n", lines);
        }

        public bool IsBadgeUnlocked(string badgeId)
        {
            return !string.IsNullOrWhiteSpace(badgeId) &&
                   PlayerPrefs.GetInt($"SmartBike.Quests.Badge.{badgeId}", 0) == 1;
        }

        public bool IsContentUnlocked(string unlockId)
        {
            return !string.IsNullOrWhiteSpace(unlockId) &&
                   PlayerPrefs.GetInt($"SmartBike.Quests.Unlock.{unlockId}", 0) == 1;
        }

        public bool IsQuestCompleted(string questId)
        {
            return !string.IsNullOrWhiteSpace(questId) &&
                   PlayerPrefs.GetInt($"SmartBike.Quests.Completed.{questId}", 0) == 1;
        }

        private void IncrementMatching(QuestObjectiveType type, string targetId)
        {
            if (activeQuest == null)
                return;

            bool changed = false;
            foreach (ObjectiveProgress item in progress)
            {
                if (item.Completed || item.Definition.Type != type ||
                    !Matches(item.Definition.TargetId, targetId))
                    continue;

                item.Count++;
                item.Completed = item.Count >= item.Definition.TargetCount;
                changed = true;
            }

            if (changed)
            {
                string message = type == QuestObjectiveType.CollectItems
                    ? "Energy item collected"
                    : "Exploration area discovered";
                FeedbackRaised?.Invoke(message);
                PublishState();
            }
        }

        private static bool Matches(string expected, string actual)
        {
            return string.IsNullOrWhiteSpace(expected) ||
                   string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);
        }

        private void PublishState()
        {
            string status = GetStatusText();
            onProgressChanged.Invoke(status);
            StateChanged?.Invoke();

            if (activeQuest == null || progress.Count == 0)
                return;

            foreach (ObjectiveProgress item in progress)
            {
                if (!item.Completed)
                    return;
            }

            CompleteQuest();
        }

        private void CompleteQuest()
        {
            QuestDefinition completed = activeQuest;
            activeQuest = null;

            LastCompletedQuest = completed.Title;
            LastRewardPoints = completed.PointReward;

            points += completed.PointReward;
            if (saveRewards)
            {
                PlayerPrefs.SetInt("SmartBike.Quests.Points", points);
                PlayerPrefs.SetInt($"SmartBike.Quests.Completed.{completed.QuestId}", 1);

                if (!string.IsNullOrWhiteSpace(completed.BadgeId))
                    PlayerPrefs.SetInt($"SmartBike.Quests.Badge.{completed.BadgeId}", 1);
                if (!string.IsNullOrWhiteSpace(completed.UnlockId))
                    PlayerPrefs.SetInt($"SmartBike.Quests.Unlock.{completed.UnlockId}", 1);

                PlayerPrefs.Save();
            }

            onPointsChanged.Invoke(points);
            onQuestCompleted.Invoke(completed.Title);
            StateChanged?.Invoke();
            QuestCompleted?.Invoke(completed);
            Debug.Log($"Quest completed: {completed.Title} | Reward: {completed.PointReward} points");
            FeedbackRaised?.Invoke($"Mission complete — {completed.PointReward} points earned");
        }
    }
}
