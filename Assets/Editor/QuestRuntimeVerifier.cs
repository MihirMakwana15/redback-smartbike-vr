using SmartBike.Quests;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class QuestRuntimeVerifier
{
    private const string RunningKey = "SmartBike.QuestRuntimeVerifier.Running";
    private static double startedAt;
    private static int phase;
    private static readonly string[] ProgressKeys =
    {
        "SmartBike.Quests.Points",
        "SmartBike.Quests.HighestLevel",
        "SmartBike.Quests.Completed.smartbike-level-1",
        "SmartBike.Quests.Badge.city-explorer",
        "SmartBike.Quests.Unlock.level-2"
    };

    [InitializeOnLoadMethod]
    private static void RestoreCallbacks()
    {
        if (SessionState.GetBool(RunningKey, false))
            RegisterCallbacks();
    }

    public static void Run()
    {
        SessionState.SetBool(RunningKey, true);
        BackupProgress();
        phase = 0;
        EditorSceneManager.OpenScene("Assets/Scenes/CityScene.unity", OpenSceneMode.Single);
        RegisterCallbacks();
        EditorApplication.EnterPlaymode();
    }

    private static void RegisterCallbacks()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.update -= CheckRuntime;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode)
            return;
        startedAt = EditorApplication.timeSinceStartup;
        EditorApplication.update -= CheckRuntime;
        EditorApplication.update += CheckRuntime;
    }

    private static void CheckRuntime()
    {
        QuestManager manager = Object.FindObjectOfType<QuestManager>();
        QuestCampaignController campaign = Object.FindObjectOfType<QuestCampaignController>();
        QuestHud hud = Object.FindObjectOfType<QuestHud>();
        PlayerController player = Object.FindObjectOfType<PlayerController>();
        GameObject missionObjects = GameObject.Find("Level 1 Mission Objects");
        GameObject hudCanvas = GameObject.Find("Quest HUD Canvas");
        GameObject safeRoad = GameObject.Find("Quest Safe Road");
        GameObject explorationZone = GameObject.Find("Park Exploration Zone");
        GameObject discoveryGarden = GameObject.Find("Discovery Garden");
        GameObject returnPortal = GameObject.Find("Return To Route Portal");

        bool ready = manager != null && manager.HasActiveQuest && campaign != null &&
                     hud != null && player != null && missionObjects != null && hudCanvas != null &&
                     safeRoad != null && explorationZone != null && discoveryGarden != null &&
                     returnPortal != null;
        bool timedOut = EditorApplication.timeSinceStartup - startedAt > 55d;

        if (phase == 0 && ready)
        {
            Vector3 beforePortal = player.transform.position;
            QuestTeleportTrigger explorationPortal = explorationZone.GetComponent<QuestTeleportTrigger>();
            explorationPortal?.TeleportPlayer(player);
            bool enteredGarden = Vector3.Distance(beforePortal, player.transform.position) > 10f;
            Vector3 gardenPosition = player.transform.position;
            QuestTeleportTrigger routePortal = returnPortal.GetComponent<QuestTeleportTrigger>();
            routePortal?.TeleportPlayer(player);
            bool returnedToRoute = Vector3.Distance(gardenPosition, player.transform.position) > 10f;
            if (!enteredGarden || !returnedToRoute)
            {
                Debug.LogError($"QUEST_RUNTIME_VERIFY_PORTAL entered={enteredGarden} returned={returnedToRoute}");
                Finish(false);
                return;
            }

            Debug.Log("QUEST_RUNTIME_VERIFY_INITIAL ready=True safeRoad=True explorationZone=True discoveryGarden=True portalTravel=True");
            manager.ReportCheckpoint("demo-route", 0);
            manager.ReportCheckpoint("demo-route", 1);
            manager.ReportCheckpoint("demo-route", 2);
            for (int i = 0; i < 5; i++)
                manager.ReportCollectible("demo-item");
            manager.ReportAreaExplored("demo-area");
            SmartBikeQuestMovementSource movementSource = Object.FindObjectOfType<SmartBikeQuestMovementSource>();
            if (movementSource != null)
                movementSource.enabled = false;
            manager.SetMovementSpeed(1f);
            phase = 1;
            return;
        }

        if (phase == 1 && manager != null && manager.HasCompletedQuest)
        {
            GameObject rewardPanel = GameObject.Find("Mission Complete Panel");
            GameObject nextButtonObject = GameObject.Find("Replay Button");
            Button nextButton = nextButtonObject != null ? nextButtonObject.GetComponent<Button>() : null;
            bool rewardReady = rewardPanel != null && rewardPanel.activeInHierarchy && nextButton != null;
            Debug.Log($"QUEST_RUNTIME_VERIFY_REWARD ready={rewardReady}");
            if (!rewardReady)
            {
                Finish(false);
                return;
            }

            nextButton.onClick.Invoke();
            phase = 2;
            startedAt = EditorApplication.timeSinceStartup;
            return;
        }

        if (phase == 1 && manager != null)
            manager.SetMovementSpeed(1f);

        if (phase == 2)
        {
            bool level2Ready = manager != null && manager.HasActiveQuest &&
                               manager.ActiveQuest.Title == "Endurance Ride" &&
                               campaign != null && campaign.CurrentLevelNumber == 2 &&
                               GameObject.Find("Level 2 Mission Objects") != null &&
                               GameObject.Find("Mission Complete Panel") == null;
            if (level2Ready)
            {
                Debug.Log("QUEST_RUNTIME_VERIFY_LEVEL2 ready=True rewardOverlayClosed=True");
                Debug.Log("QUEST_RUNTIME_VERIFY ready=True manager=True activeQuest=True campaign=True hud=True player=True world=True canvas=True reward=True nextLevel=True rewardOverlayClosed=True discoveryGarden=True portalTravel=True");
                Finish(true);
                return;
            }
        }

        if (timedOut)
        {
            Debug.LogError($"QUEST_RUNTIME_VERIFY ready=False phase={phase} initial={ready}");
            Finish(false);
        }
    }

    private static void Finish(bool success)
    {
        RestoreProgress();
        SessionState.SetBool(RunningKey, false);
        EditorApplication.update -= CheckRuntime;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.ExitPlaymode();
        EditorApplication.delayCall += () => EditorApplication.Exit(success ? 0 : 2);
    }

    private static void BackupProgress()
    {
        for (int i = 0; i < ProgressKeys.Length; i++)
        {
            SessionState.SetBool($"SmartBike.QuestVerifier.Exists.{i}", PlayerPrefs.HasKey(ProgressKeys[i]));
            SessionState.SetInt($"SmartBike.QuestVerifier.Value.{i}", PlayerPrefs.GetInt(ProgressKeys[i], 0));
        }
    }

    private static void RestoreProgress()
    {
        for (int i = 0; i < ProgressKeys.Length; i++)
        {
            if (SessionState.GetBool($"SmartBike.QuestVerifier.Exists.{i}", false))
                PlayerPrefs.SetInt(ProgressKeys[i], SessionState.GetInt($"SmartBike.QuestVerifier.Value.{i}", 0));
            else
                PlayerPrefs.DeleteKey(ProgressKeys[i]);
        }
        PlayerPrefs.Save();
    }
}
