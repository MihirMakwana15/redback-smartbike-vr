using System;
using UnityEngine;

/// <summary>
/// Lightweight development-only test panel for validating workout MQTT
/// messages without building production UI. Disable or remove it after testing.
/// </summary>
public sealed class WorkoutMqttTestPanel : MonoBehaviour
{
    [SerializeField] private WorkoutCommandPublisher publisher;
    [SerializeField] private Mqtt mqtt;
    [SerializeField] private SmartBikeTelemetryService telemetry;

    private string brokerHost = "localhost";
    private string username = string.Empty;
    private string password = string.Empty;
    private string status = "Ready to test";
    private GUIStyle statusStyle;

    private void Awake()
    {
        if (mqtt == null)
            mqtt = GetComponent<Mqtt>();
        if (publisher == null)
            publisher = GetComponent<WorkoutCommandPublisher>();
        if (telemetry == null)
            telemetry = GetComponent<SmartBikeTelemetryService>();

        if (mqtt != null)
        {
            brokerHost = mqtt.MqttHostname;
            username = mqtt.MqttUsername;
            password = mqtt.MqttPassword;
        }
    }

    private void OnEnable()
    {
        if (mqtt == null)
            mqtt = GetComponent<Mqtt>();

        if (mqtt != null)
        {
            mqtt.ConnectionStateChanged += OnConnectionStateChanged;
            mqtt.ConnectionError += OnConnectionError;
        }
    }

    private void OnDisable()
    {
        if (mqtt == null)
            return;

        mqtt.ConnectionStateChanged -= OnConnectionStateChanged;
        mqtt.ConnectionError -= OnConnectionError;
    }

    private void OnGUI()
    {
        const float width = 430f;
        GUILayout.BeginArea(new Rect(20f, 20f, width, 445f), GUI.skin.box);
        GUILayout.Label("SmartBike MQTT Workout Test", MakeTitleStyle());

        GUILayout.BeginHorizontal();
        GUILayout.Label("Broker:", GUILayout.Width(60f));
        brokerHost = GUILayout.TextField(brokerHost);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Username:", GUILayout.Width(70f));
        username = GUILayout.TextField(username);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Password:", GUILayout.Width(70f));
        password = GUILayout.PasswordField(password, '*');
        GUILayout.EndHorizontal();

        GUILayout.Label($"Topic: bike/{Mqtt.DeviceId}/workout");
        GUILayout.Label($"Connection: {(mqtt != null && mqtt.IsConnected ? "CONNECTED" : "DISCONNECTED")}");

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Connect", GUILayout.Height(32f)))
            Connect();
        if (GUILayout.Button("Disconnect", GUILayout.Height(32f)))
            Disconnect();
        GUILayout.EndHorizontal();

        GUI.enabled = mqtt != null && mqtt.IsConnected && publisher != null;
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Send Ramped", GUILayout.Height(42f)))
            Publish(WorkoutCommand.Ramped);
        if (GUILayout.Button("Send FTP", GUILayout.Height(42f)))
            Publish(WorkoutCommand.FTP);
        GUILayout.EndHorizontal();
        GUI.enabled = true;

        GUILayout.Label($"Status: {status}", MakeStatusStyle());
        GUILayout.Space(5f);
        GUILayout.Label("Week 5 Telemetry", MakeTitleStyle());
        if (telemetry != null)
        {
            GUILayout.Label(
                $"Speed: {telemetry.SpeedMetresPerSecond:0.00} m/s  " +
                $"({telemetry.SpeedKilometresPerHour:0.0} km/h)");
            GUILayout.Label(
                $"Cadence: {telemetry.CadenceRpm:0.0} rpm  |  " +
                $"Distance: {telemetry.DistanceMetres:0.0} m");
            GUILayout.Label(
                $"Messages: {telemetry.ValidMessageCount} valid / " +
                $"{telemetry.InvalidMessageCount} invalid");
        }
        if (GUILayout.Button("Send Sample Sensor Data", GUILayout.Height(32f)))
            PublishSampleTelemetry();
        GUILayout.Label("Remove this component after capturing test evidence.");
        GUILayout.EndArea();
    }

    private void Connect()
    {
        if (mqtt == null)
        {
            status = "ERROR: Mqtt component is missing.";
            return;
        }

        mqtt.MqttHostname = brokerHost.Trim();
        mqtt.MqttUsername = username.Trim();
        mqtt.MqttPassword = password;
        status = mqtt.Connect()
            ? $"Connected to {mqtt.MqttHostname}:{mqtt.MqttPort}"
            : "Connection failed. Check the Unity Console.";
    }

    private void Disconnect()
    {
        if (mqtt == null)
            return;

        mqtt.Disconnect();
        status = "Disconnected";
    }

    private void Publish(WorkoutCommand command)
    {
        bool sent = publisher.PublishWorkout(command);
        status = sent
            ? $"Sent {WorkoutCommandPublisher.ToPayload(command)} on {Mqtt.WorkoutTopic}"
            : $"ERROR: {publisher.LastError}";
    }

    private void PublishSampleTelemetry()
    {
        if (mqtt == null || !mqtt.IsConnected)
        {
            status = "ERROR: Connect MQTT before sending sample telemetry.";
            return;
        }

        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        mqtt.Publish(
            Mqtt.SpeedTopic,
            $"{{\"value\":6.5,\"unitName\":\"m/s\",\"timestamp\":{timestamp}}}");
        mqtt.Publish(
            Mqtt.CadenceTopic,
            $"{{\"value\":82,\"unitName\":\"rpm\",\"timestamp\":{timestamp}}}");
        mqtt.Publish(
            Mqtt.DistanceTopic,
            $"{{\"value\":125.4,\"unitName\":\"m\",\"timestamp\":{timestamp}}}");
        status = "Published sample speed, cadence and distance.";
    }

    private void OnConnectionStateChanged(bool connected)
    {
        status = connected ? "MQTT connected" : "MQTT disconnected";
    }

    private void OnConnectionError(string message)
    {
        status = $"ERROR: {message}";
    }

    private GUIStyle MakeTitleStyle()
    {
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 18;
        style.fontStyle = FontStyle.Bold;
        return style;
    }

    private GUIStyle MakeStatusStyle()
    {
        if (statusStyle == null)
        {
            statusStyle = new GUIStyle(GUI.skin.label)
            {
                wordWrap = true,
                fontStyle = FontStyle.Bold
            };
        }

        statusStyle.normal.textColor = status.StartsWith("ERROR")
            ? new Color(1f, 0.35f, 0.35f)
            : Color.white;
        return statusStyle;
    }
}
