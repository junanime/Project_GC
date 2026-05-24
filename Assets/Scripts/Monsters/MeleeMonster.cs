using UnityEngine;

namespace Vampire
{
    public class MeleeMonster : Monster
    {
        protected new MeleeMonsterBlueprint monsterBlueprint;
        protected float timeSinceLastAttack;

        public override void Setup(
            int monsterIndex,
            Vector2 position,
            MonsterBlueprint monsterBlueprint,
            float hpBuff = 0)
        {
            if (monsterBlueprint is not MeleeMonsterBlueprint meleeMonsterBlueprint)
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

            this.monsterBlueprint = meleeMonsterBlueprint;
            timeSinceLastAttack = 0f;
        }

        protected override void Update()
        {
            base.Update();

            if (!alive || monsterBlueprint == null)
            {
                return;
            }

            timeSinceLastAttack += Time.deltaTime;
        }

        protected override void FixedUpdate()
        {
            base.FixedUpdate();

            if (!alive || monsterBlueprint == null)
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
                monsterBlueprint.acceleration *
                Time.fixedDeltaTime;

            if (entityManager != null && entityManager.Grid != null)
            {
                entityManager.Grid.UpdateClient(this);
            }
        }

        private void OnCollisionStay2D(Collision2D col)
        {
            if (!alive || monsterBlueprint == null)
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
                (monsterBlueprint.meleeLayer & (1 << col.collider.gameObject.layer)) != 0;

            if (!isTargetLayer)
            {
                return;
            }

            float attackDelay = monsterBlueprint.atkspeed > 0f
                ? 1.0f / monsterBlueprint.atkspeed
                : 1.0f;

            if (timeSinceLastAttack < attackDelay)
            {
                return;
            }

            playerCharacter.TakeDamage(monsterBlueprint.atk);
            timeSinceLastAttack = Mathf.Repeat(timeSinceLastAttack, attackDelay);
        }
    }
}