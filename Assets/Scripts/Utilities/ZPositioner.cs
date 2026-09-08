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

        private void Awake()
        {
            TryResolvePlayerTransform(true);
        }

        private void LateUpdate()
        {
            if (playerTransform == null)
            {
                TryResolvePlayerTransform(false);

                if (playerTransform == null)
                {
                    return;
                }
            }

            Vector3 temp =
                transform.position;

            temp.z =
                scale *
                (
                    (
                        manuallySetZ
                            ? manualY
                            : temp.y
                    ) -
                    playerTransform.position.y
                );

            transform.position =
                temp;
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

        private void TryResolvePlayerTransform(
            bool force)
        {
            if (playerTransform != null)
            {
                return;
            }

            if (!force &&
                Time.unscaledTime < nextPlayerResolveTime)
            {
                return;
            }

            nextPlayerResolveTime =
                Time.unscaledTime +
                PlayerResolveRetryInterval;

            Character foundPlayer =
                FindObjectOfType<Character>();

            if (foundPlayer == null)
            {
                return;
            }

            playerTransform =
                foundPlayer.transform;
        }
    }
}