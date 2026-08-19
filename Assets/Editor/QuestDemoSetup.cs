using SmartBike.Quests;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class QuestDemoSetup
{
    private const string QuestFolder = "Assets/Quests";
    private const string Level1Path = QuestFolder + "/Level1CityExplorer.asset";
    private const string Level2Path = QuestFolder + "/Level2EnduranceRide.asset";
    private const string Level3Path = QuestFolder + "/Level3PerfectRoute.asset";

    [MenuItem("Tools/SmartBike Quests/Reset Quest Progress")]
    public static void ResetQuestProgress()
    {
        string[] keys =
        {
            "SmartBike.Quests.Points",
            "SmartBike.Quests.HighestLevel",
            "SmartBike.Quests.Completed.smartbike-level-1",
            "SmartBike.Quests.Completed.smartbike-level-2",
            "SmartBike.Quests.Completed.smartbike-level-3",
            "SmartBike.Quests.Badge.city-explorer",
            "SmartBike.Quests.Badge.endurance-rider",
            "SmartBike.Quests.Badge.route-master",
            "SmartBike.Quests.Unlock.level-2",
            "SmartBike.Quests.Unlock.level-3",
            "SmartBike.Quests.Unlock.night-route"
        };
        foreach (string key in keys)
            PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save();
        Debug.Log("SmartBike quest progress reset to Level 1 with 0 points.");
    }

    [MenuItem("Tools/SmartBike Quests/Create Demo Quest System")]
    public static void CreateDemoQuestSystem()
    {
        EnsureFolder();
        QuestDefinition level1 = GetOrCreateQuest(Level1Path);
        QuestDefinition level2 = GetOrCreateQuest(Level2Path);
        QuestDefinition level3 = GetOrCreateQuest(Level3Path);
        ConfigureQuest(level1, 1);
        ConfigureQuest(level2, 2);
        ConfigureQuest(level3, 3);

        QuestManager manager = Object.FindObjectOfType<QuestManager>();
        if (manager == null)
        {
            GameObject questObject = new GameObject("SmartBike Quest System");
            Undo.RegisterCreatedObjectUndo(questObject, "Create SmartBike Quest System");
            manager = questObject.AddComponent<QuestManager>();
            questObject.AddComponent<SmartBikeQuestMovementSource>();
        }

        if (manager.GetComponent<SmartBikeQuestMovementSource>() == null)
            manager.gameObject.AddComponent<SmartBikeQuestMovementSource>();
        if (manager.GetComponent<QuestGameplayInstaller>() == null)
            manager.gameObject.AddComponent<QuestGameplayInstaller>();
        if (manager.GetComponent<QuestHud>() == null)
            manager.gameObject.AddComponent<QuestHud>();
        QuestCampaignController campaign = manager.GetComponent<QuestCampaignController>();
        if (campaign == null)
            campaign = manager.gameObject.AddComponent<QuestCampaignController>();

        QuestDebugPanel oldTestPanel = manager.GetComponent<QuestDebugPanel>();
        if (oldTestPanel != null)
            Undo.DestroyObjectImmediate(oldTestPanel);

        SerializedObject managerObject = new SerializedObject(manager);
        managerObject.FindProperty("startingQuest").objectReferenceValue = level1;
        managerObject.FindProperty("startAutomatically").boolValue = true;
        managerObject.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject campaignObject = new SerializedObject(campaign);
        SerializedProperty levels = campaignObject.FindProperty("levels");
        levels.arraySize = 3;
        levels.GetArrayElementAtIndex(0).objectReferenceValue = level1;
        levels.GetArrayElementAtIndex(1).objectReferenceValue = level2;
        levels.GetArrayElementAtIndex(2).objectReferenceValue = level3;
        campaignObject.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(manager);
        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        Selection.activeGameObject = manager.gameObject;
        AssetDatabase.SaveAssets();

        if (!Application.isBatchMode)
        {
            EditorUtility.DisplayDialog(
                "SmartBike Quest Demo Ready",
                "A playable City Explorer quest was added to the current scene. Enter Play mode and " +
                "ride through the visible checkpoints, collect five energy orbs, and enter the exploration zone.",
                "OK");
        }
    }

    public static void InstallIntoCityScene()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/CityScene.unity", OpenSceneMode.Single);
        CreateDemoQuestSystem();
        EditorSceneManager.SaveScene(scene);
    }

    private static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder(QuestFolder))
            AssetDatabase.CreateFolder("Assets", "Quests");
    }

    private static QuestDefinition GetOrCreateQuest(string path)
    {
        QuestDefinition quest = AssetDatabase.LoadAssetAtPath<QuestDefinition>(path);
        if (quest != null)
            return quest;
        quest = ScriptableObject.CreateInstance<QuestDefinition>();
        AssetDatabase.CreateAsset(quest, path);
        return quest;
    }

    private static void ConfigureQuest(QuestDefinition quest, int level)
    {
        SerializedObject serializedQuest = new SerializedObject(quest);
        string[] titles = { "City Explorer", "Endurance Ride", "Perfect Route Challenge" };
        string[] descriptions =
        {
            "Learn the route, collect energy and discover the park.",
            "Ride farther, collect more energy and maintain your pace.",
            "Complete the longest route in perfect order without missing a checkpoint."
        };
        int[] checkpointTargets = { 3, 4, 5 };
        int[] collectibleTargets = { 5, 7, 10 };
        float[] movementTargets = { 20f, 30f, 45f };
        int[] rewards = { 500, 750, 1200 };
        string[] badges = { "city-explorer", "endurance-rider", "route-master" };
        string[] unlocks = { "level-2", "level-3", "night-route" };
        int index = Mathf.Clamp(level - 1, 0, 2);

        serializedQuest.FindProperty("questId").stringValue = $"smartbike-level-{level}";
        serializedQuest.FindProperty("title").stringValue = titles[index];
        serializedQuest.FindProperty("description").stringValue = descriptions[index];
        serializedQuest.FindProperty("pointReward").intValue = rewards[index];
        serializedQuest.FindProperty("badgeId").stringValue = badges[index];
        serializedQuest.FindProperty("unlockId").stringValue = unlocks[index];

        SerializedProperty objectives = serializedQuest.FindProperty("objectives");
        objectives.arraySize = 5;
        ConfigureObjective(objectives.GetArrayElementAtIndex(0), "checkpoints",
            $"Reach {checkpointTargets[index]} checkpoints",
            QuestObjectiveType.ReachCheckpoints, checkpointTargets[index], 10f, "demo-route", 0.5f);
        ConfigureObjective(objectives.GetArrayElementAtIndex(1), "collectibles",
            $"Collect {collectibleTargets[index]} energy orbs",
            QuestObjectiveType.CollectItems, collectibleTargets[index], 10f, "demo-item", 0.5f);
        ConfigureObjective(objectives.GetArrayElementAtIndex(2), "movement",
            $"Keep moving for {movementTargets[index]:0} seconds",
            QuestObjectiveType.MaintainMovement, 1, movementTargets[index], "", 0.5f + index * 0.25f);
        ConfigureObjective(objectives.GetArrayElementAtIndex(3), "explore", "Explore the park area",
            QuestObjectiveType.ExploreArea, 1, 10f, "demo-area", 0.5f);
        ConfigureObjective(objectives.GetArrayElementAtIndex(4), "ordered-route",
            "Complete the route without missing a checkpoint",
            QuestObjectiveType.CompleteRouteWithoutMissingCheckpoint, checkpointTargets[index], 10f,
            "demo-route", 0.5f);

        serializedQuest.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(quest);
    }

    private static void ConfigureObjective(
        SerializedProperty objective,
        string id,
        string description,
        QuestObjectiveType type,
        int targetCount,
        float targetSeconds,
        string targetId,
        float minimumSpeed)
    {
        objective.FindPropertyRelative("objectiveId").stringValue = id;
        objective.FindPropertyRelative("description").stringValue = description;
        objective.FindPropertyRelative("type").enumValueIndex = (int)type;
        objective.FindPropertyRelative("targetCount").intValue = targetCount;
        objective.FindPropertyRelative("targetSeconds").floatValue = targetSeconds;
        objective.FindPropertyRelative("targetId").stringValue = targetId;
        objective.FindPropertyRelative("minimumMovementSpeed").floatValue = minimumSpeed;
    }
}
