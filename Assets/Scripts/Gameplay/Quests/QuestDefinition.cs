using System;
using System.Collections.Generic;
using UnityEngine;

namespace SmartBike.Quests
{
    public enum QuestObjectiveType
    {
        ReachCheckpoints,
        CollectItems,
        MaintainMovement,
        ExploreArea,
        CompleteRouteWithoutMissingCheckpoint
    }

    [Serializable]
    public sealed class QuestObjectiveDefinition
    {
        [SerializeField] private string objectiveId = "objective";
        [SerializeField] private string description = "Complete the objective";
        [SerializeField] private QuestObjectiveType type;
        [SerializeField, Min(1)] private int targetCount = 1;
        [SerializeField, Min(0.1f)] private float targetSeconds = 10f;
        [SerializeField] private string targetId = string.Empty;
        [SerializeField, Min(0f)] private float minimumMovementSpeed = 0.5f;

        public string ObjectiveId => objectiveId;
        public string Description => description;
        public QuestObjectiveType Type => type;
        public int TargetCount => Mathf.Max(1, targetCount);
        public float TargetSeconds => Mathf.Max(0.1f, targetSeconds);
        public string TargetId => targetId;
        public float MinimumMovementSpeed => Mathf.Max(0f, minimumMovementSpeed);
    }

    [CreateAssetMenu(fileName = "Quest", menuName = "SmartBike/Quest Definition")]
    public sealed class QuestDefinition : ScriptableObject
    {
        [SerializeField] private string questId = "quest-01";
        [SerializeField] private string title = "New Quest";
        [SerializeField, TextArea(2, 5)] private string description;
        [SerializeField, Min(0)] private int pointReward = 100;
        [SerializeField] private string badgeId = string.Empty;
        [SerializeField] private string unlockId = string.Empty;
        [SerializeField] private List<QuestObjectiveDefinition> objectives =
            new List<QuestObjectiveDefinition>();

        public string QuestId => questId;
        public string Title => title;
        public string Description => description;
        public int PointReward => Mathf.Max(0, pointReward);
        public string BadgeId => badgeId;
        public string UnlockId => unlockId;
        public IReadOnlyList<QuestObjectiveDefinition> Objectives => objectives;

        private void OnValidate()
        {
            questId = string.IsNullOrWhiteSpace(questId) ? name : questId.Trim();
            title = string.IsNullOrWhiteSpace(title) ? name : title.Trim();
        }
    }
}
