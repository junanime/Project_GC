using UnityEngine;

namespace Vampire
{
    public class ZPositioner : MonoBehaviour
    {
        private const float PlayerResolveRetryInterval = 0.5f;

        private Transform playerTransform;

        private float scale = 0.01f;
        private bool manuallySetZ = false;
        private float manualY;

        private float nextPlayerResolveTime;

        public void Init(Transform playerTransform)
        {
            this.playerTransform = playerTransform;

            if (playerTransform != null)
            {
                nextPlayerResolveTime = 0f;
            }
        }

        private void LateUpdate()
        {
            // Spawned actors and loot receive their player in Init before their
            // first frame. Only stand-alone scene objects need a player lookup.
            if (playerTransform == null)
            {
                if (Time.unscaledTime < nextPlayerResolveTime) return;
                nextPlayerResolveTime = Time.unscaledTime + PlayerResolveRetryInterval;
                Character player = FindObjectOfType<Character>();
                if (player == null) return;
                playerTransform = player.transform;
            }

            Vector3 position = transform.position;
            float depth = scale * ((manuallySetZ ? manualY : position.y) - playerTransform.position.y);
            if (position.z == depth) return;
            position.z = depth;
            transform.position = position;
        }

        public void ManuallySetZByY(float y)
        {
            manuallySetZ = true;
            manualY = y;
        }

        public void AutomaticallySetZ()
        {
            manuallySetZ = false;
        }

    }
}
