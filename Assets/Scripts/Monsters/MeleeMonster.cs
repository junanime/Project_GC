using UnityEngine;

namespace Vampire
{
    public class MeleeMonster : Monster
    {
        protected MeleeMonsterBlueprint meleeMonsterBlueprint;
        protected float timeSinceLastAttack;

        public override void Setup(
            int monsterIndex,
            Vector2 position,
            MonsterBlueprint monsterBlueprint,
            float hpBuff = 0)
        {
            if (monsterBlueprint is not MeleeMonsterBlueprint meleeBlueprint)
            {
                Debug.LogError(
                    $"[MeleeMonster] 잘못된 Blueprint가 들어왔습니다. " +
                    $"MeleeMonster 프리팹에는 MeleeMonsterBlueprint 계열만 넣어야 합니다. " +
                    $"현재 Blueprint: {(monsterBlueprint != null ? monsterBlueprint.name : "NULL")} / " +
                    $"Type: {(monsterBlueprint != null ? monsterBlueprint.GetType().Name : "NULL")}",
                    this
                );

                return;
            }

            base.Setup(monsterIndex, position, monsterBlueprint, hpBuff);

            meleeMonsterBlueprint = meleeBlueprint;
            timeSinceLastAttack = 0f;
        }

        protected override void Update()
        {
            base.Update();

            if (!alive || meleeMonsterBlueprint == null)
            {
                return;
            }

            timeSinceLastAttack += Time.deltaTime;
        }

        protected override void FixedUpdate()
        {
            base.FixedUpdate();

            if (!alive || meleeMonsterBlueprint == null)
            {
                return;
            }

            if (playerCharacter == null || rb == null)
            {
                return;
            }

            Vector2 moveDirection =
                ((Vector2)playerCharacter.transform.position - (Vector2)transform.position).normalized;

            rb.velocity +=
                moveDirection *
                meleeMonsterBlueprint.acceleration *
                Time.fixedDeltaTime;

            if (entityManager != null && entityManager.Grid != null)
            {
                entityManager.Grid.UpdateClient(this);
            }
        }

        protected virtual void OnCollisionStay2D(Collision2D col)
        {
            if (!alive || meleeMonsterBlueprint == null)
            {
                return;
            }

            if (col == null || col.collider == null)
            {
                return;
            }

            if (playerCharacter == null)
            {
                return;
            }

            bool isTargetLayer =
                (meleeMonsterBlueprint.meleeLayer & (1 << col.collider.gameObject.layer)) != 0;

            if (!isTargetLayer)
            {
                return;
            }

            float attackDelay = meleeMonsterBlueprint.atkspeed > 0f
                ? 1.0f / meleeMonsterBlueprint.atkspeed
                : 1.0f;

            if (timeSinceLastAttack < attackDelay)
            {
                return;
            }

            // 결과 화면용:
            // 이 근접 공격을 한 몬스터의 정보를 Character에게 전달합니다.
            playerCharacter.TakeDamageFromMonster(
                meleeMonsterBlueprint.atk,
                Vector2.zero,
                meleeMonsterBlueprint
            );

            timeSinceLastAttack =
                Mathf.Repeat(
                    timeSinceLastAttack,
                    attackDelay
                );
        }
    }
}