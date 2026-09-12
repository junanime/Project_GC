using System.Collections;
using UnityEngine;

namespace Vampire
{
    public class BoomerangMonster : Monster
    {
        [SerializeField] protected Transform boomerangSpawnPosition;
        protected new BoomerangMonsterBlueprint monsterBlueprint;
        protected float timeSinceLastBoomerangAttack;
        protected float timeSinceLastMeleeAttack;
        protected float outOfRangeTime;
        protected int boomerangIndex;

        public override void Setup(
            int monsterIndex,
            Vector2 position,
            MonsterBlueprint monsterBlueprint,
            float hpBuff = 0)
        {
            base.Setup(monsterIndex, position, monsterBlueprint, hpBuff);

            this.monsterBlueprint =
                (BoomerangMonsterBlueprint)monsterBlueprint;

            boomerangIndex =
                entityManager.AddPoolForBoomerang(
                    this.monsterBlueprint.boomerangPrefab
                );

            outOfRangeTime = 0;
        }

        protected override void FixedUpdate()
        {
            base.FixedUpdate();

            Vector2 toPlayer =
                playerCharacter.transform.position -
                transform.position;

            float distance = toPlayer.magnitude;

            Vector2 dirToPlayer =
                distance > 0.0001f
                    ? toPlayer / distance
                    : Vector2.zero;

            entityManager.Grid.UpdateClient(this);

            timeSinceLastBoomerangAttack +=
                Time.fixedDeltaTime;

            timeSinceLastMeleeAttack +=
                Time.fixedDeltaTime;

            if (distance <= monsterBlueprint.range)
            {
                rb.velocity +=
                    dirToPlayer *
                    monsterBlueprint.acceleration *
                    Time.fixedDeltaTime / 2;

                if (timeSinceLastBoomerangAttack >=
                    1.0f / monsterBlueprint.boomerangAttackSpeed)
                {
                    ThrowBoomerang(
                        playerCharacter.transform.position
                    );

                    timeSinceLastBoomerangAttack = 0;
                }
            }
            else
            {
                rb.velocity +=
                    dirToPlayer *
                    monsterBlueprint.acceleration *
                    Time.fixedDeltaTime;
            }
        }

        protected void ThrowBoomerang(Vector2 targetPosition)
        {
            Boomerang boomerang =
                entityManager.SpawnBoomerang(
                    boomerangIndex,
                    boomerangSpawnPosition.position,
                    monsterBlueprint.boomerangDamage,
                    0,
                    monsterBlueprint.throwRange,
                    monsterBlueprint.throwTime,
                    monsterBlueprint.targetLayer
                );

            if (boomerang == null)
            {
                Debug.LogWarning(
                    $"[BoomerangMonster] SpawnBoomerang 실패 | Monster: {monsterBlueprint.name}",
                    this
                );

                return;
            }

            // 이 부메랑을 던진 몬스터 정보 전달
            boomerang.SetSourceMonster(
                monsterBlueprint
            );

            boomerang.Throw(
                boomerangSpawnPosition,
                targetPosition
            );
        }

        private void OnCollisionStay2D(Collision2D col)
        {
            if (col == null ||
                col.collider == null ||
                monsterBlueprint == null ||
                playerCharacter == null)
            {
                return;
            }

            if (((monsterBlueprint.targetLayer &
                 (1 << col.collider.gameObject.layer)) != 0) &&
                timeSinceLastMeleeAttack >=
                1.0f / monsterBlueprint.atkspeed)
            {
                playerCharacter.TakeDamageFromMonster(
                    monsterBlueprint.atk,
                    Vector2.zero,
                    monsterBlueprint
                );

                timeSinceLastMeleeAttack = 0;
            }
        }
    }
}