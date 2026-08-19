# SmartBike Missions and Quests

This reusable system supports:

- reaching a number of checkpoints;
- collecting items;
- maintaining SmartBike movement for a period;
- exploring a named area;
- completing ordered checkpoints without missing one;
- point, badge and content-unlock rewards saved with `PlayerPrefs`.

## Unity setup

### Playable demonstration

Open `CityScene` and select **Tools > SmartBike Quests > Create Demo Quest System**.
The command creates a three-level campaign, gameplay spawner, movement adapter and
in-game mission HUD. When Play mode begins, visible checkpoint gates, floating energy
orbs and an exploration zone are placed ahead of the bicycle.

Ride through checkpoints 1, 2 and 3 in order, collect the five orbs, enter the
exploration zone and remain moving for 20 seconds. Completing everything awards
500 points and the City Explorer badge. The reward page previews Level 2 and provides
a **Start Next Level** button. Levels 2 and 3 increase the checkpoint, collection,
movement and reward targets. Completing Level 3 displays the campaign-complete state.

### Manual setup

1. Create a quest asset with **Assets > Create > SmartBike > Quest Definition**.
2. Add objectives to the asset and give every quest/objective a stable ID.
3. Add an empty `Quest System` GameObject to the gameplay scene.
4. Add `QuestManager`, assign the quest asset, and enable **Start Automatically**.
5. Add `SmartBikeQuestMovementSource` to the same object for movement objectives.
6. Add `QuestWorldTrigger` to checkpoint, collectible, or exploration objects. Their
   colliders must be triggers, and the player must use the `Player` tag.
7. For an ordered route, use one route ID on every checkpoint and indices `0, 1, 2...`.
8. Add `QuestHud` to display progress and rewards during gameplay.

## Suggested demonstration quest

- Reach 3 checkpoints (`targetId = demo-route`, target count 3).
- Collect 5 items (`targetId = demo-item`, target count 5).
- Maintain movement for 20 seconds (minimum speed 0.5 m/s).
- Explore the park (`targetId = demo-area`, target count 1).
- Complete route without missing a checkpoint (`targetId = demo-route`, target count 3).
- Reward: 500 points, badge `city-explorer`, unlock `night-route`.

`SmartBikeQuestMovementSource` uses MQTT speed when fresh telemetry is available and
otherwise measures the bicycle's real movement through the Unity scene.
