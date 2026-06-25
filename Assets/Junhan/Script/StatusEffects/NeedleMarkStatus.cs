using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 표식침 상태이상.
    /// 첫 피격 시 머리 위에 X 표식을 남기고,
    /// 다음 침 피격 시 표식을 소모하면서 사선 베기 연출 후 제거합니다.
    /// </summary>
    public class NeedleMarkStatus : MonoBehaviour
    {
        private bool marked = false;
        private float expireTime = 0f;

        private NeedleMarkStatusIcon iconInstance;

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

            EnsureIconExists();
        }

        public bool TryConsume()
        {
            if (!IsMarked())
            {
                return false;
            }

            marked = false;

            if (iconInstance != null)
            {
                iconInstance.PlayConsumeAndDestroy();
                iconInstance = null;
            }

            Destroy(this);
            return true;
        }

        private void Update()
        {
            if (marked && Time.time > expireTime)
            {
                marked = false;
                CleanupIconImmediately();
                Destroy(this);
            }
        }

        private void EnsureIconExists()
        {
            if (iconInstance != null)
            {
                return;
            }

            GameObject iconObject = new GameObject("Needle Mark Status Icon");
            iconObject.transform.SetParent(transform, false);
            iconObject.transform.localPosition = CalculateIconLocalOffset();
            iconObject.transform.localRotation = Quaternion.identity;
            iconObject.transform.localScale = Vector3.one;

            iconInstance = iconObject.AddComponent<NeedleMarkStatusIcon>();
        }

        private Vector3 CalculateIconLocalOffset()
        {
            SpriteRenderer spriteRenderer = GetComponentInChildren<SpriteRenderer>();

            float yOffset = 0.95f;

            if (spriteRenderer != null)
            {
                yOffset = spriteRenderer.bounds.extents.y + 0.42f;
            }

            return new Vector3(0f, yOffset, 0f);
        }

        private void CleanupIconImmediately()
        {
            if (iconInstance != null)
            {
                Destroy(iconInstance.gameObject);
                iconInstance = null;
            }
        }

        private void OnDestroy()
        {
            // 표식 소모 연출 중에는 iconInstance를 null로 바꾼 뒤 아이콘 오브젝트가 자체적으로 사라지게 둔다.
            // 만료/몬스터 사망/오브젝트 제거처럼 즉시 정리해야 하는 경우만 여기서 정리한다.
            if (iconInstance != null)
            {
                CleanupIconImmediately();
            }
        }
    }
}