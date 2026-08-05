using System;
using System.Collections.Concurrent;
using System.Globalization;
using UnityEngine;
using UnityEngine.Events;
using uPLibrary.Networking.M2Mqtt.Messages;

/// <summary>
/// Receives SmartBike speed, cadence and distance telemetry over MQTT.
/// MQTT callbacks are queued and processed in Update because M2Mqtt invokes
/// callbacks on a network thread and Unity objects are main-thread only.
/// </summary>
public sealed class SmartBikeTelemetryService : MonoBehaviour
{
    [Serializable] public sealed class FloatEvent : UnityEvent<float> { }

    [Header("Validation")]
    [SerializeField, Min(0f)] private float maximumSpeedMetresPerSecond = 40f;
    [SerializeField, Min(0f)] private float maximumCadenceRpm = 250f;
    [SerializeField, Min(0.25f)] private float staleDataSeconds = 3f;
    [SerializeField] private bool calculateDistanceFromSpeed = true;
    [SerializeField] private bool logValidMessages;

    [Header("Unity Events")]
    [SerializeField] private FloatEvent onSpeedChanged = new FloatEvent();
    [SerializeField] private FloatEvent onCadenceChanged = new FloatEvent();
    [SerializeField] private FloatEvent onDistanceChanged = new FloatEvent();

    private readonly ConcurrentQueue<TelemetryMessage> pendingMessages =
        new ConcurrentQueue<TelemetryMessage>();

    private bool subscribed;
    private bool connectionEventRegistered;
    private float lastSpeedUpdateTime = float.NegativeInfinity;

    public float SpeedMetresPerSecond { get; private set; }
    public float SpeedKilometresPerHour => SpeedMetresPerSecond * 3.6f;
    public float CadenceRpm { get; private set; }
    public float DistanceMetres { get; private set; }
    public bool IsSubscribed => subscribed;
    public bool HasFreshSpeed => Time.unscaledTime - lastSpeedUpdateTime <= staleDataSeconds;
    public int ValidMessageCount { get; private set; }
    public int InvalidMessageCount { get; private set; }
    public string LastError { get; private set; } = string.Empty;

    private struct TelemetryMessage
    {
        public string Topic;
        public string Payload;
    }

    private void OnEnable()
    {
        RegisterConnectionEvent();
    }

    private void Start()
    {
        RegisterConnectionEvent();
        TrySubscribe();
    }

    private void Update()
    {
        if (!subscribed)
            TrySubscribe();

        while (pendingMessages.TryDequeue(out TelemetryMessage message))
            ProcessMessage(message);

        if (calculateDistanceFromSpeed && HasFreshSpeed && SpeedMetresPerSecond > 0f)
        {
            DistanceMetres += SpeedMetresPerSecond * Time.unscaledDeltaTime;
            onDistanceChanged.Invoke(DistanceMetres);
        }
    }

    private void OnDisable()
    {
        if (Mqtt.Instance == null)
            return;

        if (connectionEventRegistered)
            Mqtt.Instance.ConnectionStateChanged -= OnConnectionStateChanged;
        Mqtt.Instance.Unsubscribe(OnMqttMessage);
        connectionEventRegistered = false;
        subscribed = false;
    }

    public void ResetDistance()
    {
        DistanceMetres = 0f;
        onDistanceChanged.Invoke(DistanceMetres);
    }

    private void RegisterConnectionEvent()
    {
        if (connectionEventRegistered || Mqtt.Instance == null)
            return;

        Mqtt.Instance.ConnectionStateChanged += OnConnectionStateChanged;
        connectionEventRegistered = true;
    }

    private void TrySubscribe()
    {
        Mqtt mqtt = Mqtt.Instance;
        if (mqtt == null || !mqtt.IsConnected)
            return;

        try
        {
            mqtt.Subscribe(
                OnMqttMessage,
                Mqtt.SpeedTopic,
                Mqtt.CadenceTopic,
                Mqtt.DistanceTopic);
            subscribed = true;
            LastError = string.Empty;
            Debug.Log(
                $"SmartBike telemetry subscribed | {Mqtt.SpeedTopic}, " +
                $"{Mqtt.CadenceTopic}, {Mqtt.DistanceTopic}");
        }
        catch (Exception exception)
        {
            subscribed = false;
            LastError = exception.Message;
            Debug.LogError($"Telemetry subscription failed: {exception.Message}");
        }
    }

    private void OnConnectionStateChanged(bool connected)
    {
        subscribed = false;
        if (!connected)
        {
            SpeedMetresPerSecond = 0f;
            CadenceRpm = 0f;
        }
    }

    private void OnMqttMessage(object sender, MqttMsgPublishEventArgs args)
    {
        pendingMessages.Enqueue(new TelemetryMessage
        {
            Topic = args.Topic,
            Payload = System.Text.Encoding.UTF8.GetString(args.Message)
        });
    }

    private void ProcessMessage(TelemetryMessage message)
    {
        if (message.Topic == Mqtt.SpeedTopic)
        {
            if (!TryReadNumber(message.Payload, "speed", out float speed) ||
                speed < 0f || speed > maximumSpeedMetresPerSecond)
            {
                Reject("speed", message.Payload);
                return;
            }

            SpeedMetresPerSecond = speed;
            lastSpeedUpdateTime = Time.unscaledTime;
            ValidMessageCount++;
            onSpeedChanged.Invoke(speed);
            LogAccepted("speed", speed, "m/s");
            return;
        }

        if (message.Topic == Mqtt.CadenceTopic)
        {
            if (!TryReadNumber(message.Payload, "cadence", out float cadence) ||
                cadence < 0f || cadence > maximumCadenceRpm)
            {
                Reject("cadence", message.Payload);
                return;
            }

            CadenceRpm = cadence;
            ValidMessageCount++;
            onCadenceChanged.Invoke(cadence);
            LogAccepted("cadence", cadence, "rpm");
            return;
        }

        if (message.Topic == Mqtt.DistanceTopic)
        {
            if (!TryReadNumber(message.Payload, "distance", out float distance) ||
                distance < 0f)
            {
                Reject("distance", message.Payload);
                return;
            }

            DistanceMetres = distance;
            ValidMessageCount++;
            onDistanceChanged.Invoke(distance);
            LogAccepted("distance", distance, "m");
        }
    }

    private static bool TryReadNumber(string payload, string field, out float value)
    {
        value = 0f;
        if (string.IsNullOrWhiteSpace(payload))
            return false;

        string trimmed = payload.Trim();
        if (float.TryParse(
            trimmed,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out value))
            return true;

        return TryExtractJsonNumber(trimmed, field, out value) ||
               TryExtractJsonNumber(trimmed, "value", out value);
    }

    private static bool TryExtractJsonNumber(string json, string key, out float value)
    {
        value = 0f;
        int keyIndex = json.IndexOf($"\"{key}\"", StringComparison.OrdinalIgnoreCase);
        if (keyIndex < 0)
            keyIndex = json.IndexOf($"'{key}'", StringComparison.OrdinalIgnoreCase);
        if (keyIndex < 0)
            keyIndex = json.IndexOf(key, StringComparison.OrdinalIgnoreCase);
        if (keyIndex < 0)
            return false;

        int colonIndex = json.IndexOf(':', keyIndex);
        if (colonIndex < 0)
            return false;

        int start = colonIndex + 1;
        while (start < json.Length &&
               (char.IsWhiteSpace(json[start]) || json[start] == '"' || json[start] == '\''))
            start++;

        int end = start;
        while (end < json.Length &&
               (char.IsDigit(json[end]) || json[end] == '-' || json[end] == '+' ||
                json[end] == '.' || json[end] == 'e' || json[end] == 'E'))
            end++;

        return end > start &&
               float.TryParse(
                   json.Substring(start, end - start),
                   NumberStyles.Float,
                   CultureInfo.InvariantCulture,
                   out value);
    }

    private void Reject(string field, string payload)
    {
        InvalidMessageCount++;
        LastError = $"Invalid {field} payload: {payload}";
        Debug.LogWarning(LastError);
    }

    private void LogAccepted(string field, float value, string unit)
    {
        if (logValidMessages)
            Debug.Log($"Telemetry accepted | {field}: {value:0.##} {unit}");
    }
}
