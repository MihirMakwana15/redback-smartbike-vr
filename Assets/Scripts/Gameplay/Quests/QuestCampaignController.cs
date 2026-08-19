using System;
using System.Collections.Generic;
using UnityEngine;

namespace SmartBike.Quests
{
    public sealed class QuestCampaignController : MonoBehaviour
    {
        [SerializeField] private List<QuestDefinition> levels = new List<QuestDefinition>();
        [SerializeField] private bool rememberHighestLevel = true;

        private const string HighestLevelKey = "SmartBike.Quests.HighestLevel";
        private QuestGameplayInstaller installer;

        public int CurrentLevelIndex { get; private set; }
        public int CurrentLevelNumber => CurrentLevelIndex + 1;
        public int LevelCount => levels.Count;
        public bool HasNextLevel => CurrentLevelIndex + 1 < levels.Count;
        public QuestDefinition CurrentLevel => IsValid(CurrentLevelIndex) ? levels[CurrentLevelIndex] : null;
        public QuestDefinition NextLevel => IsValid(CurrentLevelIndex + 1) ? levels[CurrentLevelIndex + 1] : null;
        public event Action<int, QuestDefinition> LevelStarted;

        private void Awake()
        {
            installer = GetComponent<QuestGameplayInstaller>();
        }

        private void Start()
        {
            if (QuestManager.Instance == null || levels.Count == 0)
                return;

            QuestManager.Instance.QuestCompleted += HandleLevelCompleted;
            if (!QuestManager.Instance.HasActiveQuest)
                StartLevel(0);
            else
                installer?.BuildLevel(1);
        }

        private void OnDestroy()
        {
            if (QuestManager.Instance != null)
                QuestManager.Instance.QuestCompleted -= HandleLevelCompleted;
        }

        public void StartNextLevel()
        {
            if (HasNextLevel)
                StartLevel(CurrentLevelIndex + 1);
        }

        public void StartLevel(int index)
        {
            if (!IsValid(index) || QuestManager.Instance == null)
                return;

            CurrentLevelIndex = index;
            QuestDefinition definition = levels[index];
            QuestManager.Instance.StartQuest(definition);
            installer?.BuildLevel(CurrentLevelNumber);
            LevelStarted?.Invoke(CurrentLevelNumber, definition);
        }

        private void HandleLevelCompleted(QuestDefinition completed)
        {
            int completedIndex = levels.IndexOf(completed);
            if (completedIndex >= 0)
                CurrentLevelIndex = completedIndex;

            if (rememberHighestLevel)
            {
                int unlockedLevel = Mathf.Min(CurrentLevelNumber + 1, levels.Count);
                PlayerPrefs.SetInt(HighestLevelKey,
                    Mathf.Max(PlayerPrefs.GetInt(HighestLevelKey, 1), unlockedLevel));
                PlayerPrefs.Save();
            }
        }

        private bool IsValid(int index)
        {
            return index >= 0 && index < levels.Count && levels[index] != null;
        }
    }
}
