using UnityEngine;

namespace SmartBike.Quests
{
    public sealed class QuestVisualMotion : MonoBehaviour
    {
        private Vector3 startPosition;

        private void Start()
        {
            startPosition = transform.position;
        }

        private void Update()
        {
            transform.Rotate(Vector3.up, 90f * Time.deltaTime, Space.World);
            transform.position = startPosition + Vector3.up * (Mathf.Sin(Time.time * 2f) * 0.25f);
        }
    }
}
