using UnityEngine;

namespace SmartBike.Quests
{
    public sealed class QuestDebugPanel : MonoBehaviour
    {
        [SerializeField] private bool visible = true;
        [SerializeField] private string routeId = "demo-route";
        [SerializeField] private string collectibleId = "demo-item";
        [SerializeField] private string areaId = "demo-area";
        private int checkpointIndex;

        private void OnGUI()
        {
            if (!visible)
                return;

            QuestManager manager = QuestManager.Instance;
            GUILayout.BeginArea(new Rect(20, 20, 430, 330), GUI.skin.box);
            GUILayout.Label("SmartBike Quest Test Panel");

            if (manager == null)
            {
                GUILayout.Label("QuestManager not found.");
                GUILayout.EndArea();
                return;
            }

            GUILayout.Label(manager.GetStatusText());
            GUILayout.Label($"Saved points: {manager.Points}");

            if (GUILayout.Button($"Reach checkpoint {checkpointIndex}"))
                manager.ReportCheckpoint(routeId, checkpointIndex++);
            if (GUILayout.Button("Collect item"))
                manager.ReportCollectible(collectibleId);
            if (GUILayout.Button("Explore area"))
                manager.ReportAreaExplored(areaId);

            GUILayout.Label("Movement timer uses live SmartBike speed.");
            GUILayout.EndArea();
        }
    }
}
