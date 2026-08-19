using UnityEngine;

namespace SmartBike.Quests
{
    public enum QuestTriggerType
    {
        Checkpoint,
        Collectible,
        ExploreArea
    }

    [RequireComponent(typeof(Collider))]
    public sealed class QuestWorldTrigger : MonoBehaviour
    {
        [SerializeField] private QuestTriggerType triggerType;
        [SerializeField] private string targetId = string.Empty;
        [SerializeField, Min(0)] private int checkpointIndex;
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private bool triggerOnce = true;
        [SerializeField] private bool hideAfterTrigger;

        private bool triggered;

        public void Configure(QuestTriggerType type, string id, int index, bool hide)
        {
            triggerType = type;
            targetId = id;
            checkpointIndex = Mathf.Max(0, index);
            hideAfterTrigger = hide;
        }

        private void Reset()
        {
            Collider trigger = GetComponent<Collider>();
            trigger.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            bool isPlayer = other.CompareTag(playerTag) ||
                            other.GetComponentInParent<PlayerController>() != null;
            if ((triggerOnce && triggered) || !isPlayer)
                return;

            QuestManager manager = QuestManager.Instance;
            if (manager == null || !manager.HasActiveQuest)
                return;

            triggered = true;
            switch (triggerType)
            {
                case QuestTriggerType.Checkpoint:
                    manager.ReportCheckpoint(targetId, checkpointIndex);
                    break;
                case QuestTriggerType.Collectible:
                    manager.ReportCollectible(targetId);
                    break;
                case QuestTriggerType.ExploreArea:
                    manager.ReportAreaExplored(targetId);
                    break;
            }

            if (hideAfterTrigger)
                gameObject.SetActive(false);
            else
                MarkTriggered();
        }

        private void MarkTriggered()
        {
            foreach (Renderer itemRenderer in GetComponentsInChildren<Renderer>())
            {
                if (itemRenderer.material != null)
                    itemRenderer.material.color = new Color(0.2f, 1f, 0.35f);
            }
        }
    }

    [RequireComponent(typeof(Collider))]
    public sealed class QuestTeleportTrigger : MonoBehaviour
    {
        [SerializeField] private Vector3 destination;
        [SerializeField] private string arrivalMessage = "New area discovered";
        private float nextAllowedTeleportTime;

        public void Configure(Vector3 worldDestination, string message)
        {
            destination = worldDestination;
            arrivalMessage = message;
        }

        private void OnTriggerEnter(Collider other)
        {
            PlayerController player = other.GetComponentInParent<PlayerController>();
            if (player != null)
                TeleportPlayer(player);
        }

        public void TeleportPlayer(PlayerController player)
        {
            if (player == null || Time.unscaledTime < nextAllowedTeleportTime)
                return;

            Rigidbody body = player.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.velocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.position = destination;
            }
            player.transform.position = destination;
            nextAllowedTeleportTime = Time.unscaledTime + 1f;

            if (QuestManager.Instance != null)
                QuestManager.Instance.RaiseFeedback(arrivalMessage);
        }
    }
}
