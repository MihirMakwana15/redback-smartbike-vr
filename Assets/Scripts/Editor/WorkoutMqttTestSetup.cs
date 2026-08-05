using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class WorkoutMqttTestSetup
{
    [MenuItem("MQTT/Set Up Workout Test Panel")]
    private static void SetUp()
    {
        Mqtt mqtt = Object.FindObjectOfType<Mqtt>(true);
        if (mqtt == null)
        {
            GameObject service = new GameObject("MQTT Service");
            mqtt = Undo.AddComponent<Mqtt>(service);
            Undo.RegisterCreatedObjectUndo(service, "Create MQTT test service");
        }

        WorkoutCommandPublisher publisher =
            mqtt.GetComponent<WorkoutCommandPublisher>();
        if (publisher == null)
            publisher = Undo.AddComponent<WorkoutCommandPublisher>(mqtt.gameObject);

        WorkoutMqttTestPanel panel = mqtt.GetComponent<WorkoutMqttTestPanel>();
        if (panel == null)
            panel = Undo.AddComponent<WorkoutMqttTestPanel>(mqtt.gameObject);

        SmartBikeTelemetryService telemetry =
            mqtt.GetComponent<SmartBikeTelemetryService>();
        if (telemetry == null)
            telemetry = Undo.AddComponent<SmartBikeTelemetryService>(mqtt.gameObject);

        SerializedObject panelData = new SerializedObject(panel);
        panelData.FindProperty("publisher").objectReferenceValue = publisher;
        panelData.FindProperty("mqtt").objectReferenceValue = mqtt;
        panelData.FindProperty("telemetry").objectReferenceValue = telemetry;
        panelData.ApplyModifiedProperties();

        EditorUtility.SetDirty(mqtt.gameObject);
        EditorSceneManager.MarkSceneDirty(mqtt.gameObject.scene);
        Selection.activeGameObject = mqtt.gameObject;
        SceneView.FrameLastActiveSceneView();

        Debug.Log(
            "MQTT workout test panel is ready. Enter Play mode, set the broker " +
            "address in the on-screen panel, then click Connect.");
        EditorUtility.DisplayDialog(
            "MQTT Workout Test Ready",
            "The MQTT service, workout publisher, and on-screen test panel are " +
            "configured in the current scene.\n\nEnter Play mode to begin.",
            "OK");
    }

    [MenuItem("MQTT/Remove Workout Test Panel")]
    private static void Remove()
    {
        WorkoutMqttTestPanel panel =
            Object.FindObjectOfType<WorkoutMqttTestPanel>(true);
        if (panel == null)
        {
            EditorUtility.DisplayDialog(
                "MQTT Workout Test",
                "No workout test panel was found in the current scene.",
                "OK");
            return;
        }

        GameObject owner = panel.gameObject;
        Undo.DestroyObjectImmediate(panel);
        EditorUtility.SetDirty(owner);
        EditorSceneManager.MarkSceneDirty(owner.scene);
        Debug.Log("Removed the temporary MQTT workout test panel.");
    }
}
