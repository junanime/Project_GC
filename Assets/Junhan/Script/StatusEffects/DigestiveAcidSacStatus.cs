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

        public void Apply(
            float puddleLifetime,
            float puddleRadius,
            float puddleDamagePerSecond,
            float puddleTickInterval,
            Color puddleColor)
        {
            this.puddleLifetime = Mathf.Max(0.1f, puddleLifetime);
            this.puddleRadius = Mathf.Max(0.1f, puddleRadius);
            this.puddleDamagePerSecond = Mathf.Max(0f, puddleDamagePerSecond);
            this.puddleTickInterval = Mathf.Max(0.05f, puddleTickInterval);
            this.puddleColor = puddleColor;

            if (!initialized)
            {
                Initialize();
            }
        }

        private void Initialize()
        {
            ownerMonster = GetComponent<Monster>() ?? GetComponentInParent<Monster>();

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

            Vector2 spawnPosition = killedMonster != null
                ? (Vector2)killedMonster.transform.position
                : (Vector2)transform.position;

            GameObject puddleObject = new GameObject("Digestive Acid Puddle");
            puddleObject.transform.position = spawnPosition;

            DigestiveAcidPuddle puddle = puddleObject.AddComponent<DigestiveAcidPuddle>();
            puddle.Init(
                puddleLifetime,
                puddleRadius,
                puddleDamagePerSecond,
                puddleTickInterval,
                puddleColor
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