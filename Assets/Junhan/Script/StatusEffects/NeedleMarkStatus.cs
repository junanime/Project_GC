using UnityEngine;

namespace Vampire
{
    // 표식침 상태이상
    // 첫 피격 시 표식을 남기고, 다음 침 피격 시 표식을 소모한다.
    public class NeedleMarkStatus : MonoBehaviour
    {
        private bool marked = false;
        private float expireTime = 0f;

        public bool IsMarked()
        {
            if (!marked)
            {
                return false;
            }

            if (Time.time > expireTime)
            {
                marked = false;
                return false;
            }

            return true;
        }

        public void Apply(float duration)
        {
            marked = true;
            expireTime = Time.time + Mathf.Max(0.1f, duration);
        }

        public bool TryConsume()
        {
            if (!IsMarked())
            {
                return false;
            }

            marked = false;
            return true;
        }

        private void Update()
        {
            if (marked && Time.time > expireTime)
            {
                Destroy(this);
            }
        }
    }
}