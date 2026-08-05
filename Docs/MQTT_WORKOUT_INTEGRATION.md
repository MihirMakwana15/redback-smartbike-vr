# SmartBike MQTT Workout Integration

## Purpose

The Unity VR application publishes workout selections to the SmartBike IoT
listener. The implementation uses the MQTT contract already present in
`redback-smartbike-iot/Drivers/Mqtt_integration/mqtt_listener.py`.

| Item | Value |
|---|---|
| Protocol | MQTT over TCP |
| Default port | `1883` |
| Topic | `bike/{deviceId}/workout` |
| Quality of service | QoS 1 (at least once) |
| Ramped payload | `Ramped` |
| FTP payload | `FTP` |
| Retained | No |

The Python listener normalises payload case, but Unity deliberately sends the
canonical values above. This keeps logs and test evidence consistent.

## Unity setup

1. Open the project in Unity 2022.3.22f1.
2. Add an empty GameObject named `MQTT Service` to the startup scene.
3. Add the `Mqtt` component.
4. Set **Mqtt Hostname**, **Mqtt Port**, and **Device Id** to match the IoT
   listener. Never commit real credentials.
5. Enable **Auto Connect** if the application should connect on startup.
6. Add the `WorkoutCommandPublisher` component to the same GameObject.
7. In the XR/UI buttons' **On Click** events, connect:
   - Ramped button → `WorkoutCommandPublisher.PublishRampedWorkout`
   - FTP button → `WorkoutCommandPublisher.PublishFtpWorkout`
8. Optionally connect `On Command Published` and `On Publish Failed` to a
   status label or logging component.

The MQTT object survives scene changes. A duplicate instance destroys itself,
preventing multiple clients from publishing the same command.

## Validation procedure

Start the Python listener with the same broker and device identifier:

```bash
python mqtt_listener.py \
  --device_id 000001 \
  --mqtt_host <broker-host> \
  --mqtt_user <username> \
  --mqtt_password <password>
```

Then enter Play mode and select each workout once.

Expected evidence:

1. Unity reports a successful connection.
2. Selecting Ramped logs:
   `Workout command published | Topic: bike/000001/workout | Payload: Ramped`
3. The listener reports:
   `Message received: bike/000001/workout ramped`
4. Selecting FTP produces the equivalent `FTP`/`ftp` messages.
5. Stop the broker, select a workout, and confirm Unity reports a controlled
   failure instead of crashing.
6. Restart the broker and confirm automatic reconnection when **Auto Connect**
   is enabled.

Record the Unity Console and listener terminal together for assessment
evidence. Do not include broker passwords in screenshots.

## Reliability and design decisions

- Workout commands use QoS 1 so transient packet loss is less likely to lose a
  user's selection.
- Publishing is rejected while disconnected; an optional connect-on-publish
  attempt is made first.
- Unexpected connection loss starts a timed reconnect loop on Unity's main
  thread.
- Commands are represented by an enum, preventing arbitrary or misspelled
  workout names.
- Connection and publishing outcomes are exposed as events so VR UI feedback
  can be added without coupling the network layer to a particular canvas.
- The component retains the last command/error to support debugging and test
  evidence.

## Known constraints and next iteration

QoS 1 can deliver a duplicate after reconnect. The current IoT listener stops
an active workout before starting the received one, so a production protocol
should add a command ID and acknowledgement topic for full idempotency.
Credentials currently come from Unity's existing local settings mechanism;
production deployment should inject secrets through a platform-secure store.
The next iteration can subscribe to cadence, heart-rate, speed, and power
topics and marshal received values onto Unity's main thread for VR updates.
