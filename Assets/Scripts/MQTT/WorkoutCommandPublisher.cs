using System;
using UnityEngine;
using UnityEngine.Events;

public enum WorkoutCommand
{
    Ramped,
    FTP
}

/// <summary>
/// Unity-facing API for starting SmartBike workouts over MQTT.
/// Attach this component to a persistent scene object and connect the public
/// methods to XR or UI Button OnClick events.
/// </summary>
public sealed class WorkoutCommandPublisher : MonoBehaviour
{
    [Serializable] public sealed class StringEvent : UnityEvent<string> { }

    [SerializeField] private bool connectOnPublish = true;
    [SerializeField] private StringEvent onCommandPublished = new StringEvent();
    [SerializeField] private StringEvent onPublishFailed = new StringEvent();

    public string LastCommand { get; private set; }
    public string LastError { get; private set; }

    public void PublishRampedWorkout()
    {
        PublishWorkout(WorkoutCommand.Ramped);
    }

    public void PublishFtpWorkout()
    {
        PublishWorkout(WorkoutCommand.FTP);
    }

    public bool PublishWorkout(WorkoutCommand command)
    {
        LastError = string.Empty;

        if (!Enum.IsDefined(typeof(WorkoutCommand), command))
            return Fail($"Unsupported workout command: {command}");

        Mqtt mqtt = Mqtt.Instance;
        if (mqtt == null)
            return Fail("No active Mqtt component was found in the scene.");

        if (!mqtt.IsConnected && (!connectOnPublish || !mqtt.Connect()))
            return Fail("Workout command was not sent because MQTT is disconnected.");

        try
        {
            string payload = ToPayload(command);
            mqtt.Publish(Mqtt.WorkoutTopic, payload);
            LastCommand = payload;
            Debug.Log($"Workout command published | Topic: {Mqtt.WorkoutTopic} | Payload: {payload}");
            onCommandPublished.Invoke(payload);
            return true;
        }
        catch (Exception exception)
        {
            return Fail($"Failed to publish workout command: {exception.Message}");
        }
    }

    public static string ToPayload(WorkoutCommand command)
    {
        switch (command)
        {
            case WorkoutCommand.Ramped:
                return "Ramped";
            case WorkoutCommand.FTP:
                return "FTP";
            default:
                throw new ArgumentOutOfRangeException(nameof(command), command, "Unsupported workout command.");
        }
    }

    private bool Fail(string message)
    {
        LastError = message;
        Debug.LogError(message);
        onPublishFailed.Invoke(message);
        return false;
    }
}
