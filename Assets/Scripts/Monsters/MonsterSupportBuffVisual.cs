using TMPro;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 버프를 받은 몬스터 주변에 임시 문양을 띄우는 표시용 컴포넌트입니다.
    /// 지금은 이미지가 없으므로 TextMeshPro 문자로 표시하고,
    /// 나중에 전용 이미지가 생기면 SpriteRenderer 방식으로 교체해도 됩니다.
    /// </summary>
    public class MonsterSupportBuffVisual : MonoBehaviour
    {
        private TextMeshPro iconText;
        private float remainingTime;
        private float orbitRadius;
        private float yOffset;
        private float orbitSpeed;
        private float phase;

        public static MonsterSupportBuffVisual GetOrCreate(Transform target, string objectName)
        {
            Transform existing = target.Find(objectName);

            if (existing != null && existing.TryGetComponent(out MonsterSupportBuffVisual visual))
            {
                return visual;
            }

            GameObject visualObject = new GameObject(objectName);
            visualObject.transform.SetParent(target, false);

            MonsterSupportBuffVisual newVisual = visualObject.AddComponent<MonsterSupportBuffVisual>();
            return newVisual;
        }

        public void Play(
            string symbol,
            Color color,
            float duration,
            float orbitRadius,
            float yOffset,
            float orbitSpeed,
            int sortingOrder,
            float fontSize)
        {
            if (iconText == null)
            {
                iconText = gameObject.AddComponent<TextMeshPro>();
                iconText.alignment = TextAlignmentOptions.Center;
            }

            iconText.text = symbol;
            iconText.color = color;
            iconText.fontSize = Mathf.Max(0.1f, fontSize);

            MeshRenderer renderer = iconText.GetComponent<MeshRenderer>();

            if (renderer != null)
            {
                renderer.sortingOrder = sortingOrder;
            }

            remainingTime = Mathf.Max(0.1f, duration);
            this.orbitRadius = Mathf.Max(0f, orbitRadius);
            this.yOffset = yOffset;
            this.orbitSpeed = orbitSpeed;
            phase = Random.Range(0f, Mathf.PI * 2f);

            gameObject.SetActive(true);
        }

        private void Update()
        {
            remainingTime -= Time.deltaTime;

            float angle = phase + Time.time * orbitSpeed;
            float x = Mathf.Cos(angle) * orbitRadius;
            float y = yOffset + Mathf.Sin(angle) * orbitRadius * 0.35f;

            transform.localPosition = new Vector3(x, y, 0f);
            transform.localRotation = Quaternion.identity;

            if (remainingTime <= 0f)
            {
                Destroy(gameObject);
            }
        }
    }
}