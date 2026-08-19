using System.Collections;
using UnityEngine;

namespace SmartBike.Quests
{
    public sealed class QuestGameplayInstaller : MonoBehaviour
    {
        [SerializeField] private string routeId = "demo-route";
        [SerializeField] private string collectibleId = "demo-item";
        [SerializeField] private string explorationAreaId = "demo-area";
        [SerializeField] private float routeSpacing = 12f;

        private readonly Color checkpointColour = new Color(0.1f, 0.85f, 1f);
        private readonly Color collectibleColour = new Color(1f, 0.75f, 0.05f);
        private readonly Color areaColour = new Color(0.2f, 1f, 0.35f, 0.45f);
        private Transform worldRoot;
        private Coroutine buildRoutine;

        private IEnumerator Start()
        {
            // CampaignController normally owns level creation. The one-frame fallback
            // keeps this component usable by itself without generating duplicate objects.
            yield return null;
            if (worldRoot == null)
                BuildLevel(1);
        }

        public void BuildLevel(int levelNumber)
        {
            if (buildRoutine != null)
                StopCoroutine(buildRoutine);
            if (worldRoot != null)
                Destroy(worldRoot.gameObject);
            buildRoutine = StartCoroutine(BuildWorld(levelNumber));
        }

        private IEnumerator BuildWorld(int levelNumber)
        {
            PlayerController player = null;
            while (player == null)
            {
                player = FindObjectOfType<PlayerController>();
                yield return null;
            }

            Vector3 origin = player.transform.position;
            Vector3 forward = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.1f)
                forward = Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

            GameObject root = new GameObject($"Level {levelNumber} Mission Objects");
            worldRoot = root.transform;

            QuestDefinition quest = QuestManager.Instance != null ? QuestManager.Instance.ActiveQuest : null;
            int checkpointCount = GetTargetCount(quest, QuestObjectiveType.ReachCheckpoints, 3);
            int collectibleCount = GetTargetCount(quest, QuestObjectiveType.CollectItems, 5);

            CreateSafeRoute(origin, forward, checkpointCount);

            for (int i = 0; i < checkpointCount; i++)
                CreateCheckpoint(origin + forward * routeSpacing * (i + 1), forward, i);

            for (int i = 0; i < collectibleCount; i++)
            {
                float sideOffset = i % 2 == 0 ? -2f : 2f;
                float distance = 7f + i * ((routeSpacing * checkpointCount - 10f) /
                                           Mathf.Max(1, collectibleCount - 1));
                Vector3 position = origin + forward * distance + right * sideOffset;
                CreateCollectible(position, i + 1);
            }

            // The green gateway transports the rider to a separate generated garden.
            Vector3 explorationPosition = GroundPosition(
                origin + forward * routeSpacing * (checkpointCount - 0.55f));
            Vector3 gardenCentre = GroundPosition(origin) + right * 24f + forward * routeSpacing * 1.5f;
            Vector3 gardenArrival = gardenCentre - forward * 5f + Vector3.up * 0.8f;
            Vector3 routeReturn = explorationPosition + forward * 4f + Vector3.up * 0.8f;
            CreateDiscoveryGarden(gardenCentre, forward, routeReturn);
            CreateExplorationArea(explorationPosition, gardenArrival);
            buildRoutine = null;
        }

        private void CreateCheckpoint(Vector3 position, Vector3 forward, int index)
        {
            GameObject root = new GameObject($"Quest Checkpoint {index + 1}");
            root.transform.SetParent(worldRoot);
            root.transform.position = GroundPosition(position);
            root.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);

            CreateVisualCube(root.transform, new Vector3(-2.5f, 1.7f, 0f), new Vector3(0.22f, 3.4f, 0.22f), checkpointColour);
            CreateVisualCube(root.transform, new Vector3(2.5f, 1.7f, 0f), new Vector3(0.22f, 3.4f, 0.22f), checkpointColour);
            CreateVisualCube(root.transform, new Vector3(0f, 3.4f, 0f), new Vector3(5.2f, 0.22f, 0.22f), checkpointColour);

            BoxCollider trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 1.7f, 0f);
            trigger.size = new Vector3(5f, 3.4f, 1.5f);
            QuestWorldTrigger questTrigger = root.AddComponent<QuestWorldTrigger>();
            ConfigureTrigger(questTrigger, QuestTriggerType.Checkpoint, routeId, index, false);
            AddWorldLabel(root.transform, $"CHECKPOINT {index + 1}", new Vector3(0f, 4.2f, 0f), checkpointColour);
        }

        private void CreateCollectible(Vector3 position, int index)
        {
            GameObject collectible = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            collectible.name = $"Energy Orb {index}";
            collectible.transform.SetParent(worldRoot);
            collectible.transform.position = GroundPosition(position) + Vector3.up * 1.4f;
            collectible.transform.localScale = Vector3.one * 0.75f;
            SetColour(collectible, collectibleColour);

            Collider collider = collectible.GetComponent<Collider>();
            collider.isTrigger = true;
            QuestWorldTrigger trigger = collectible.AddComponent<QuestWorldTrigger>();
            ConfigureTrigger(trigger, QuestTriggerType.Collectible, collectibleId, 0, true);
            collectible.AddComponent<QuestVisualMotion>();
        }

        private void CreateExplorationArea(Vector3 position, Vector3 destination)
        {
            GameObject area = new GameObject("Park Exploration Zone");
            area.name = "Park Exploration Zone";
            area.transform.SetParent(worldRoot);
            area.transform.position = GroundPosition(position);

            CreateVisualCube(area.transform, new Vector3(-3f, 1.5f, 0f),
                new Vector3(0.3f, 3f, 0.3f), areaColour);
            CreateVisualCube(area.transform, new Vector3(3f, 1.5f, 0f),
                new Vector3(0.3f, 3f, 0.3f), areaColour);
            CreateVisualCube(area.transform, new Vector3(0f, 3f, 0f),
                new Vector3(6.3f, 0.3f, 0.3f), areaColour);
            CreateVisualCube(area.transform, new Vector3(0f, 0.04f, 0f),
                new Vector3(6f, 0.08f, 4f), new Color(0.1f, 0.8f, 0.25f, 0.65f));

            BoxCollider collider = area.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.center = new Vector3(0f, 1.5f, 0f);
            collider.size = new Vector3(6f, 3f, 4f);
            QuestWorldTrigger trigger = area.AddComponent<QuestWorldTrigger>();
            ConfigureTrigger(trigger, QuestTriggerType.ExploreArea, explorationAreaId, 0, false);
            QuestTeleportTrigger portal = area.AddComponent<QuestTeleportTrigger>();
            portal.Configure(destination, "Discovery Garden entered — find the return portal");
            AddWorldLabel(area.transform, "EXPLORATION ZONE", new Vector3(0f, 4f, 0f), areaColour);
        }

        private void CreateDiscoveryGarden(Vector3 centre, Vector3 forward, Vector3 returnDestination)
        {
            GameObject garden = new GameObject("Discovery Garden");
            garden.transform.SetParent(worldRoot);
            garden.transform.position = centre;
            garden.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Discovery Garden Platform";
            floor.transform.SetParent(garden.transform, false);
            floor.transform.localPosition = new Vector3(0f, -0.15f, 0f);
            floor.transform.localScale = new Vector3(18f, 0.3f, 18f);
            SetColour(floor, new Color(0.08f, 0.28f, 0.22f));

            Color border = new Color(0.1f, 0.9f, 0.65f);
            CreateVisualCube(garden.transform, new Vector3(-9f, 0.25f, 0f),
                new Vector3(0.25f, 0.5f, 18f), border);
            CreateVisualCube(garden.transform, new Vector3(9f, 0.25f, 0f),
                new Vector3(0.25f, 0.5f, 18f), border);
            CreateVisualCube(garden.transform, new Vector3(0f, 0.25f, 9f),
                new Vector3(18f, 0.5f, 0.25f), border);
            CreateVisualCube(garden.transform, new Vector3(0f, 0.25f, -9f),
                new Vector3(18f, 0.5f, 0.25f), border);

            AddWorldLabel(garden.transform, "DISCOVERY GARDEN",
                new Vector3(0f, 4.8f, 0f), new Color(0.25f, 1f, 0.7f));

            Vector3[] treePositions =
            {
                new Vector3(-6f, 0f, -4f), new Vector3(6f, 0f, -4f),
                new Vector3(-6f, 0f, 3f), new Vector3(6f, 0f, 3f)
            };
            foreach (Vector3 treePosition in treePositions)
                CreateGardenTree(garden.transform, treePosition);

            GameObject returnPortal = new GameObject("Return To Route Portal");
            returnPortal.transform.SetParent(garden.transform, false);
            returnPortal.transform.localPosition = new Vector3(0f, 0f, 6f);
            CreateVisualCube(returnPortal.transform, new Vector3(-2.2f, 1.5f, 0f),
                new Vector3(0.25f, 3f, 0.25f), new Color(0.75f, 0.3f, 1f));
            CreateVisualCube(returnPortal.transform, new Vector3(2.2f, 1.5f, 0f),
                new Vector3(0.25f, 3f, 0.25f), new Color(0.75f, 0.3f, 1f));
            CreateVisualCube(returnPortal.transform, new Vector3(0f, 3f, 0f),
                new Vector3(4.6f, 0.25f, 0.25f), new Color(0.75f, 0.3f, 1f));
            BoxCollider returnCollider = returnPortal.AddComponent<BoxCollider>();
            returnCollider.isTrigger = true;
            returnCollider.center = new Vector3(0f, 1.5f, 0f);
            returnCollider.size = new Vector3(4.4f, 3f, 2f);
            QuestTeleportTrigger returnTrigger = returnPortal.AddComponent<QuestTeleportTrigger>();
            returnTrigger.Configure(returnDestination, "Returned to the mission route");
            AddWorldLabel(returnPortal.transform, "RETURN TO ROUTE",
                new Vector3(0f, 3.8f, 0f), new Color(0.8f, 0.45f, 1f));
        }

        private static void CreateGardenTree(Transform parent, Vector3 localPosition)
        {
            GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Garden Tree";
            trunk.transform.SetParent(parent, false);
            trunk.transform.localPosition = localPosition + Vector3.up * 1.25f;
            trunk.transform.localScale = new Vector3(0.45f, 1.25f, 0.45f);
            Destroy(trunk.GetComponent<Collider>());
            SetColour(trunk, new Color(0.35f, 0.18f, 0.08f));

            GameObject crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            crown.name = "Garden Tree Crown";
            crown.transform.SetParent(parent, false);
            crown.transform.localPosition = localPosition + Vector3.up * 3.1f;
            crown.transform.localScale = new Vector3(2.2f, 2.5f, 2.2f);
            Destroy(crown.GetComponent<Collider>());
            SetColour(crown, new Color(0.12f, 0.75f, 0.28f));
        }

        private void CreateSafeRoute(Vector3 origin, Vector3 forward, int checkpointCount)
        {
            float routeLength = routeSpacing * checkpointCount + 8f;
            const float segmentLength = 4f;
            int segmentCount = Mathf.CeilToInt(routeLength / segmentLength);
            Color roadColour = new Color(0.12f, 0.15f, 0.18f);

            for (int i = 0; i < segmentCount; i++)
            {
                float distance = 2f + i * segmentLength;
                Vector3 centre = GroundPosition(origin + forward * distance);
                GameObject road = GameObject.CreatePrimitive(PrimitiveType.Cube);
                road.name = "Quest Safe Road";
                road.transform.SetParent(worldRoot);
                road.transform.position = centre - Vector3.up * 0.08f;
                road.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
                road.transform.localScale = new Vector3(8f, 0.16f, segmentLength + 0.2f);
                SetColour(road, roadColour);
            }
        }

        private static void ConfigureTrigger(QuestWorldTrigger trigger, QuestTriggerType type,
            string id, int checkpointIndex, bool hide)
        {
            trigger.Configure(type, id, checkpointIndex, hide);
        }

        private static void CreateVisualCube(Transform parent, Vector3 localPosition, Vector3 scale, Color colour)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = "Checkpoint Visual";
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = scale;
            Destroy(part.GetComponent<Collider>());
            SetColour(part, colour);
        }

        private static void AddWorldLabel(Transform parent, string text, Vector3 localPosition, Color colour)
        {
            GameObject labelObject = new GameObject(text);
            labelObject.transform.SetParent(parent, false);
            labelObject.transform.localPosition = localPosition;
            TextMesh label = labelObject.AddComponent<TextMesh>();
            label.text = text;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 42;
            label.characterSize = 0.12f;
            label.color = colour;
        }

        private static Vector3 GroundPosition(Vector3 position)
        {
            if (Physics.Raycast(position + Vector3.up * 100f, Vector3.down, out RaycastHit hit, 250f))
                position.y = hit.point.y;
            else if (Terrain.activeTerrain != null)
                position.y = Terrain.activeTerrain.SampleHeight(position) + Terrain.activeTerrain.transform.position.y;
            return position;
        }

        private static void SetColour(GameObject target, Color colour)
        {
            Renderer renderer = target.GetComponent<Renderer>();
            if (renderer == null)
                return;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");
            if (shader == null)
                return;
            Material material = new Material(shader);
            material.color = colour;
            if (colour.a < 1f)
            {
                material.SetFloat("_Surface", 1f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            renderer.material = material;
        }

        private static int GetTargetCount(QuestDefinition quest, QuestObjectiveType type, int fallback)
        {
            if (quest == null || quest.Objectives == null)
                return fallback;
            foreach (QuestObjectiveDefinition objective in quest.Objectives)
            {
                if (objective.Type == type)
                    return objective.TargetCount;
            }
            return fallback;
        }
    }
}
