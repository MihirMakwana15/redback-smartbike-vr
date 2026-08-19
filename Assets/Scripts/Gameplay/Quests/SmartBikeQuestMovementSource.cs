using UnityEngine;

namespace SmartBike.Quests
{
    public sealed class SmartBikeQuestMovementSource : MonoBehaviour
    {
        [SerializeField] private SmartBikeTelemetryService telemetry;
        [SerializeField] private PlayerController player;

        private Vector3 previousPlayerPosition;
        private bool hasPreviousPosition;

        private void Awake()
        {
            if (telemetry == null)
                telemetry = FindObjectOfType<SmartBikeTelemetryService>();
        }

        private void Update()
        {
            if (QuestManager.Instance == null)
                return;

            if (player == null)
                player = FindObjectOfType<PlayerController>();

            float physicalSpeed = 0f;
            if (player != null)
            {
                if (hasPreviousPosition && Time.unscaledDeltaTime > 0f)
                    physicalSpeed = Vector3.Distance(player.transform.position, previousPlayerPosition) /
                                    Time.unscaledDeltaTime;

                previousPlayerPosition = player.transform.position;
                hasPreviousPosition = true;
            }

            float speed = telemetry != null && telemetry.HasFreshSpeed
                ? telemetry.SpeedMetresPerSecond
                : physicalSpeed;

            QuestManager.Instance.SetMovementSpeed(speed);
        }
    }
}
