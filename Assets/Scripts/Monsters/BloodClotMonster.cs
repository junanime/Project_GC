using System.Collections;
using UnityEngine;

namespace Vampire
{
    public enum BloodClotMiniStageRole
    {
        EnterMiniStageOnDeath,
        CompleteMiniStageOnDeath
    }

    public class BloodClotMonster : Monster
    {
        [Header("Blood Clot Mini Stage")]
        [Tooltip("이 혈전이 죽었을 때 어떤 역할을 할지 정합니다. 필드 혈전은 미니 스테이지 입장, 미니 스테이지 내부 핵은 미니 스테이지 완료로 설정합니다.")]
        [SerializeField] private BloodClotMiniStageRole defaultRole = BloodClotMiniStageRole.EnterMiniStageOnDeath;

        [Tooltip("혈전이 죽었을 때 호출할 미니 스테이지 관리자입니다. 비어 있으면 씬에서 자동으로 찾습니다.")]
        [SerializeField] private MiniStageDirector miniStageDirector;

        [Tooltip("혈전이 피격될 때 뒤로 밀리지 않게 할지 여부입니다. 혈전은 고정 오브젝트처럼 쓰는 것이 좋으므로 기본값은 true입니다.")]
        [SerializeField] private bool ignoreKnockback = true;

        [Tooltip("혈전은 움직이지 않는 몬스터이므로 FixedUpdate에서 속도를 0으로 고정합니다.")]
        [SerializeField] private bool lockVelocity = true;

        [Tooltip("사망 시 기존 몬스터처럼 경험치/코인을 드랍할지 여부입니다. 미니 스테이지 입장 혈전은 보통 false가 좋습니다.")]
        [SerializeField] private bool dropLootOnDeath = false;

        [Tooltip("로그를 출력해서 혈전 사망과 미니 스테이지 연결 상태를 확인합니다.")]
        [SerializeField] private bool debugLog = true;

        private BloodClotMiniStageRole currentRole;
        private bool configuredBySpawner = false;

        public void ConfigureMiniStage(MiniStageDirector director, BloodClotMiniStageRole role)
        {
            miniStageDirector = director;
            currentRole = role;
            configuredBySpawner = true;

            if (debugLog)
            {
                Debug.Log($"[BloodClotMonster] ConfigureMiniStage 완료: role={currentRole}");
            }
        }

        public override void Setup(int monsterIndex, Vector2 position, MonsterBlueprint monsterBlueprint, float hpBuff = 0)
        {
            base.Setup(monsterIndex, position, monsterBlueprint, hpBuff);

            if (!configuredBySpawner)
            {
                currentRole = defaultRole;
            }

            if (miniStageDirector == null)
            {
                miniStageDirector = FindObjectOfType<MiniStageDirector>();
            }

            if (debugLog)
            {
                Debug.Log($"[BloodClotMonster] Setup 완료: role={currentRole}, position={position}");
            }
        }

        protected override void Update()
        {
            // 혈전은 플레이어 방향으로 좌우 반전될 필요가 거의 없어서 기본 Monster.Update()를 사용하지 않습니다.
        }

        protected override void FixedUpdate()
        {
            if (!lockVelocity || rb == null)
            {
                return;
            }

            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        public override void Knockback(Vector2 knockback)
        {
            if (ignoreKnockback)
            {
                return;
            }

            base.Knockback(knockback);
        }

        public override IEnumerator Killed(bool killedByPlayer = true)
        {
            alive = false;

            if (monsterHitbox != null)
            {
                monsterHitbox.enabled = false;
            }

            if (entityManager != null && entityManager.LivingMonsters != null)
            {
                entityManager.LivingMonsters.Remove(this);
            }

            if (dropLootOnDeath && killedByPlayer)
            {
                DropLoot();
            }

            if (deathParticles != null)
            {
                deathParticles.Play();
            }

            yield return HitAnimation();

            if (deathParticles != null)
            {
                if (monsterSpriteRenderer != null)
                {
                    monsterSpriteRenderer.enabled = false;
                }

                if (shadow != null)
                {
                    shadow.SetActive(false);
                }

                yield return new WaitForSeconds(deathParticles.main.duration - 0.15f);

                if (monsterSpriteRenderer != null)
                {
                    monsterSpriteRenderer.enabled = true;
                }

                if (shadow != null)
                {
                    shadow.SetActive(true);
                }
            }

            if (debugLog)
            {
                Debug.Log($"[BloodClotMonster] 사망 처리: role={currentRole}");
            }

            TriggerMiniStageAction();

            OnKilled.Invoke(this);
            OnKilled.RemoveAllListeners();

            configuredBySpawner = false;

            if (entityManager != null)
            {
                entityManager.DespawnMonster(monsterIndex, this, killedByPlayer);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        private void TriggerMiniStageAction()
        {
            if (miniStageDirector == null)
            {
                Debug.LogWarning("[BloodClotMonster] MiniStageDirector가 없어 미니 스테이지 동작을 실행하지 못했습니다.");
                return;
            }

            switch (currentRole)
            {
                case BloodClotMiniStageRole.EnterMiniStageOnDeath:
                    miniStageDirector.EnterMiniStage(this);
                    break;

                case BloodClotMiniStageRole.CompleteMiniStageOnDeath:
                    miniStageDirector.CompleteMiniStage(this);
                    break;
            }
        }
    }
}