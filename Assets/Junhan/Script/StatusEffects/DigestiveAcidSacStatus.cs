using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 소화액낭침 상태.
    /// 침에 맞은 몬스터에게 붙고, 해당 몬스터가 죽으면 사망 위치에 소화액 웅덩이를 생성합니다.
    /// </summary>
    public class DigestiveAcidSacStatus : MonoBehaviour
    {
        private Monster ownerMonster;

        private float puddleLifetime = 3f;
        private float puddleRadius = 1.2f;
        private float puddleDamagePerSecond = 2f;
        private float puddleTickInterval = 0.5f;
        private Color puddleColor = new Color(0.6f, 1f, 0.15f, 0.75f);

        private bool initialized = false;
        private bool puddleCreated = false;

        // 피해 출처
        private Character sourceCharacter;
        private string damageSourceName = "소화액낭침";

        /// <summary>
        /// 기존 호출부 호환용 Apply.
        /// </summary>
        public void Apply(
            float puddleLifetime,
            float puddleRadius,
            float puddleDamagePerSecond,
            float puddleTickInterval,
            Color puddleColor)
        {
            Apply(
                puddleLifetime,
                puddleRadius,
                puddleDamagePerSecond,
                puddleTickInterval,
                puddleColor,
                null,
                "소화액낭침"
            );
        }

        /// <summary>
        /// 소화액낭침 상태 적용.
        /// sourceCharacter와 damageSourceName을 저장해
        /// 이후 생성되는 웅덩이까지 피해 출처를 전달합니다.
        /// </summary>
        public void Apply(
            float puddleLifetime,
            float puddleRadius,
            float puddleDamagePerSecond,
            float puddleTickInterval,
            Color puddleColor,
            Character sourceCharacter,
            string damageSourceName)
        {
            this.puddleLifetime = Mathf.Max(0.1f, puddleLifetime);
            this.puddleRadius = Mathf.Max(0.1f, puddleRadius);
            this.puddleDamagePerSecond = Mathf.Max(0f, puddleDamagePerSecond);
            this.puddleTickInterval = Mathf.Max(0.05f, puddleTickInterval);
            this.puddleColor = puddleColor;

            this.sourceCharacter = sourceCharacter;
            this.damageSourceName =
                string.IsNullOrWhiteSpace(damageSourceName)
                    ? "소화액낭침"
                    : damageSourceName;

            if (!initialized)
            {
                Initialize();
            }
        }

        private void Initialize()
        {
            ownerMonster =
                GetComponent<Monster>() ??
                GetComponentInParent<Monster>();

            if (ownerMonster == null)
            {
                Destroy(this);
                return;
            }

            ownerMonster.OnKilled.AddListener(OnOwnerKilled);
            initialized = true;
        }

        private void OnOwnerKilled(Monster killedMonster)
        {
            if (puddleCreated)
            {
                return;
            }

            puddleCreated = true;

            Vector2 spawnPosition =
                killedMonster != null
                    ? (Vector2)killedMonster.transform.position
                    : (Vector2)transform.position;

            GameObject puddleObject =
                new GameObject("Digestive Acid Puddle");

            puddleObject.transform.position =
                spawnPosition;

            DigestiveAcidPuddle puddle =
                puddleObject.AddComponent<DigestiveAcidPuddle>();

            puddle.Init(
                puddleLifetime,
                puddleRadius,
                puddleDamagePerSecond,
                puddleTickInterval,
                puddleColor,
                sourceCharacter,
                damageSourceName
            );
        }

        private void OnDestroy()
        {
            if (ownerMonster != null)
            {
                ownerMonster.OnKilled.RemoveListener(OnOwnerKilled);
            }
        }
    }
}
