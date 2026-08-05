# Week 5 - SmartBike MQTT Telemetry Subscription

## Outcome

Unity now subscribes to SmartBike speed, cadence and distance data. Incoming
MQTT messages are moved from the MQTT network thread to Unity's main thread,
validated, and exposed through public properties and Unity events.

## Topics and units

| Data | MQTT topic | Unit | Unity property |
|---|---|---|---|
| Speed | `bike/{deviceId}/speed` | metres per second | `SpeedMetresPerSecond` |
| Cadence | `bike/{deviceId}/cadence` | revolutions per minute | `CadenceRpm` |
| Distance | `bike/{deviceId}/distance` | metres | `DistanceMetres` |

The current IoT driver publishes speed and cadence. If no separate distance
message is available, Unity calculates distance using:

`distance = distance + (speed x elapsed time)`

## Supported payloads

The service accepts the standard IoT payload:

```json
{
  "value": 6.5,
  "unitName": "m/s",
  "timestamp": 1785295198.86,
  "metadata": {
    "deviceName": "smartbike"
  }
}
```

It also accepts payloads containing a named field such as `"speed": 6.5`,
`"cadence": 82`, or `"distance": 125.4`, and simple numeric payloads.

## Reliability behaviour

- MQTT callbacks only enqueue data; Unity state is updated in `Update()`.
- Speed is accepted from 0 to 40 m/s.
- Cadence is accepted from 0 to 250 rpm.
- Distance must be non-negative.
- Invalid payloads are rejected and counted.
- Speed becomes stale after three seconds and stops increasing distance.
- Subscriptions are restored after a broker reconnect.
- Duplicate callback registration is prevented.
- Speed and cadence reset to zero after disconnection.

## Unity test

1. Open `CityScene`.
2. Select **MQTT > Set Up Workout Test Panel**.
3. Enter Play mode.
4. Connect to `test.mosquitto.org` with port `1883` and blank credentials.
5. Confirm `SmartBike telemetry subscribed` in the Console.
6. Click **Send Sample Sensor Data**.
7. Confirm the panel displays approximately:
   - speed: `6.50 m/s` / `23.4 km/h`
   - cadence: `82 rpm`
   - distance: `125.4 m`
   - three valid messages and zero invalid messages

## Independent publisher test

From the IoT testing directory:

```powershell
.\.venv\Scripts\python.exe .\sensor_publisher.py `
  --host test.mosquitto.org `
  --device-id 000001 `
  --speed 6.5 `
  --cadence 82 `
  --distance 125.4
```

The script publishes the same payload structure produced by the SmartBike
driver. Change the values to repeat the test with boundary and workout cases.

## HD-level evidence checklist

- Screenshot of the connected Unity telemetry panel.
- Screenshot showing speed, cadence and distance values.
- Unity Console showing all three topic subscriptions and accepted messages.
- PowerShell output showing the test publisher sent all three messages.
- Test invalid values such as speed `-1` and cadence `300`; record that Unity
  rejects them without crashing.
- Stop and restart the broker connection; confirm the service re-subscribes.
- Explain that distance is derived from speed because the current IoT driver
  does not publish a dedicated distance topic.
