using UnityEngine;
using uPLibrary.Networking.M2Mqtt.Messages;
using uPLibrary.Networking.M2Mqtt;
using System;
using System.Collections;

public class Mqtt : MonoBehaviour
{
    // ensure the credentials are NEVER CHECKED INTO THE REPOSITORY
    public string MqttHostname = "localhost";
    public int MqttPort = 1883;
    public string MqttUsername = "";
    public string MqttPassword = "";
    public bool AutoConnect = false;
    [Min(1f)] public float ReconnectDelaySeconds = 5f;

    // Device ID of the Bike being connected to
    [SerializeField] private string deviceId = "000001";
    public static string DeviceId { get; private set; } = "000001";

    // Send commands to these topics to change the experience on the bike
    public static string ControlTopic => $"bike/{DeviceId}/control";
    public static string ResistanceTopic => $"bike/{DeviceId}/resistance";
    public static string InclineTopic => $"bike/{DeviceId}/incline/control";
    public static string FanTopic => $"bike/{DeviceId}/fan";
    // Subscribe to these topics to receive information from the bike/cyclist
    public static string HeartRateTopic => $"bike/{DeviceId}/heartrate";
    public static string CadenceTopic => $"bike/{DeviceId}/cadence";
    public static string SpeedTopic => $"bike/{DeviceId}/speed";
    public static string DistanceTopic => $"bike/{DeviceId}/distance";
    public static string PowerTopic => $"bike/{DeviceId}/power";
    public static string WorkoutTopic => $"bike/{DeviceId}/workout";

    public string WildcardTopic => $"bike/{DeviceId}/#";

    public static string LeftTurnTopic => $"Turn/Left";
    public static string RightTurnTopic => $"Turn/Right";

    private string _connectionId;
    public string ConnectionID => _connectionId;

    private static Mqtt _instance;
    public static Mqtt Instance => _instance;

    private MqttClient _client;
    private string _clientHostname;
    private int _clientPort;
    private Coroutine _reconnectCoroutine;
    private bool _isShuttingDown;
    private volatile bool _reconnectRequested;

    private bool _connected;
    public bool IsConnected => _client != null && _client.IsConnected && _connected;

    public event Action<bool> ConnectionStateChanged;
    public event Action<string> ConnectionError;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        DeviceId = SanitiseDeviceId(deviceId);
        _connectionId = $"smartbike-vr-{DeviceId}-{Guid.NewGuid():N}";

        if (!string.IsNullOrWhiteSpace(PlayerPrefs.GetString("MQTTHost")))
            MqttHostname = PlayerPrefs.GetString("MQTTHost");
        if (!string.IsNullOrWhiteSpace(PlayerPrefs.GetString("MQTTUsername")))
            MqttUsername = PlayerPrefs.GetString("MQTTUsername");
        if (!string.IsNullOrWhiteSpace(PlayerPrefs.GetString("MQTTPassword")))
            MqttPassword = PlayerPrefs.GetString("MQTTPassword");

        CreateClient();
    }

    private void Start()
    {
        if (AutoConnect)
            Connect();
    }

    private void Update()
    {
        // M2Mqtt raises ConnectionClosed on its network thread. Defer Unity API
        // calls to Update so reconnection remains safe on all build targets.
        if (!_reconnectRequested)
            return;

        _reconnectRequested = false;
        SetConnectionState(false);
        StartReconnectLoop();
    }

    private void CreateClient()
    {
        if (_client != null)
        {
            _client.ConnectionClosed -= OnConnectionClosed;
            if (_client.IsConnected)
                _client.Disconnect();
        }

        _client = new MqttClient(MqttHostname, MqttPort, false, null, null, MqttSslProtocols.None);
        _clientHostname = MqttHostname;
        _clientPort = MqttPort;
        _client.ConnectionClosed += OnConnectionClosed;
        SetConnectionState(false);
    }

    // connection system to connect to this instance
    public bool Connect()
    {
        if (IsConnected)
            return true;

        try
        {
            Debug.Log($"Trying to connect to {MqttHostname}:{MqttPort}");

            // The test panel and settings window can change the broker after
            // Awake. M2Mqtt binds its host in the constructor, so rebuild the
            // client whenever the endpoint changes.
            if (_client == null ||
                !string.Equals(_clientHostname, MqttHostname, StringComparison.OrdinalIgnoreCase) ||
                _clientPort != MqttPort)
                CreateClient();

            if (string.IsNullOrWhiteSpace(MqttUsername))
                _client.Connect(ConnectionID);
            else
                _client.Connect(ConnectionID, MqttUsername, MqttPassword);

            SetConnectionState(_client.IsConnected);
            Debug.Log("Connection successful: " + IsConnected);
        }
        catch (Exception e)
        {
            string message = $"Unable to connect to MQTT broker {MqttHostname}:{MqttPort}. {e.Message}";
            Debug.LogError($"{message}\n{e}");
            ConnectionError?.Invoke(message);
            SetConnectionState(false);
            StartReconnectLoop();
        }

        return IsConnected;
    }

    // subscribe to the following events with the handler callback, passing no subscriptions will subscribe to the wildcard topic
    public void Subscribe(MqttClient.MqttMsgPublishEventHandler handler, params string[] subscriptions)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Cannot subscribe while MQTT is disconnected.");

        if (subscriptions.Length == 0)
            subscriptions = new[] { WildcardTopic };

        // Avoid duplicate callbacks when a component re-subscribes after an
        // unexpected disconnect.
        _client.MqttMsgPublishReceived -= handler;
        _client.MqttMsgPublishReceived += handler;

        byte[] qosLevels = new byte[subscriptions.Length];
        for (int i = 0; i < subscriptions.Length; i++)
        {
            qosLevels[i] = MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE;
        }

        _client.Subscribe(subscriptions, qosLevels);
        Debug.Log($"Subscribed to messages: {string.Join(", ", subscriptions)}");
    }

    public void Unsubscribe(MqttClient.MqttMsgPublishEventHandler handler)
    {
        _client.MqttMsgPublishReceived -= handler;
        Debug.Log("Unsubscribed from messages");
    }

    // Send a message to the broker on a certain topic
    // Topics for the bike are provided as public member variables
    // The message is in JSON format and should include a timestamp (seconds since 1/1/70 UTC)
    //
    // Payload for resistance: {"ts": 176854940, "resistance": 24} 
    // The value for resistance should be an integer between 0 and 100, and is percentage of the maximum
    // Values around 24 seem good for cycling with a light resistance (otherwise the pedals feel too easy)
    // and 100 is the maximum resistance.
    //
    // Payload for incline: {"ts": 176854940, "incline": 0.0)
    // The value for incline should be a float between -10 and +19 (in steps of 0.5)
    // and represents the angle the front wheel should be raised. Use 0 to have the bike flat.
    //
    // Payload for fan: ("ts": 17685940, "fan": 100)
    // The value for fan should be an integer between 0 and 100 and is percentage of the maximum
    // 0 is no wind
    // 100 is winds that feel similar to riding at 54 km/hr
    //
    // Commands use QoS 1 (at least once). Consumers should therefore be
    // idempotent because MQTT may redeliver a message after reconnecting.
    public void Publish(string topic, string msg)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Cannot publish while MQTT is disconnected.");
        if (string.IsNullOrWhiteSpace(topic))
            throw new ArgumentException("An MQTT topic is required.", nameof(topic));

        _client.Publish(
            topic,
            System.Text.Encoding.UTF8.GetBytes(msg ?? string.Empty),
            MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE,
            false);
    }

    public void Disconnect()
    {
        StopReconnectLoop();
        if (_client != null && _client.IsConnected)
            _client.Disconnect();
        SetConnectionState(false);
    }

    private void OnConnectionClosed(object sender, EventArgs args)
    {
        if (!_isShuttingDown)
            _reconnectRequested = true;
    }

    private void StartReconnectLoop()
    {
        if (!_isShuttingDown && AutoConnect && _reconnectCoroutine == null)
            _reconnectCoroutine = StartCoroutine(ReconnectLoop());
    }

    private IEnumerator ReconnectLoop()
    {
        while (!_isShuttingDown && !IsConnected)
        {
            yield return new WaitForSecondsRealtime(ReconnectDelaySeconds);
            Connect();
        }
        _reconnectCoroutine = null;
    }

    private void StopReconnectLoop()
    {
        if (_reconnectCoroutine == null)
            return;
        StopCoroutine(_reconnectCoroutine);
        _reconnectCoroutine = null;
    }

    private void SetConnectionState(bool connected)
    {
        if (_connected == connected)
            return;
        _connected = connected;
        ConnectionStateChanged?.Invoke(connected);
    }

    private static string SanitiseDeviceId(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "000001" : value.Trim();
    }

    private void OnDestroy()
    {
        if (_instance != this)
            return;

        _isShuttingDown = true;
        Disconnect();
        if (_client != null)
            _client.ConnectionClosed -= OnConnectionClosed;
        _instance = null;
    }
}
