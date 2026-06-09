using System.Collections;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 낮은 체력 + 빠른 이동속도 + 접촉 자폭형 몬스터.
    /// 스타크래프트 스커지처럼 플레이어에게 빠르게 접근하고,
    /// 플레이어와 접촉하면 큰 피해를 준 뒤 사망한다.
    /// </summary>
    public class ExplodingMonster : Monster
    {
        private ExplodingMonsterBlueprint explodingBlueprint;

        private float timeSinceSpawn = 0f;

        private bool explosionStarted = false;
        private bool explosionDamageApplied = false;
        private bool warningActive = false;

        private Color originalColor = Color.white;
        private Vector3 originalScale = Vector3.one;

        private Coroutine warningCoroutine;

        public override void Setup(
            int monsterIndex,
            Vector2 position,
            MonsterBlueprint monsterBlueprint,
            float hpBuff = 0)
        {
            explodingBlueprint = monsterBlueprint as ExplodingMonsterBlueprint;

            if (explodingBlueprint == null)
            {
                Debug.LogError(
                    "[ExplodingMonster] ExplodingMonsterBlueprint가 연결되지 않았습니다. " +
                    "자폭 몬스터 프리팹에는 Exploding Monster Blueprint를 사용해야 합니다.",
                    this
                );

                return;
            }

            base.Setup(monsterIndex, position, monsterBlueprint, hpBuff);

            timeSinceSpawn = 0f;

            explosionStarted = false;
            explosionDamageApplied = false;
            warningActive = false;

            originalScale = transform.localScale;

            if (monsterSpriteRenderer != null)
            {
                monsterSpriteRenderer.enabled = true;
                originalColor = monsterSpriteRenderer.color;
                monsterSpriteRenderer.color = originalColor;
            }

            if (shadow != null)
            {
                shadow.SetActive(true);
            }

            if (monsterHitbox != null)
            {
                monsterHitbox.enabled = true;
            }

            if (monsterLegsCollider != null)
            {
                monsterLegsCollider.enabled = true;
            }

            if (rb != null)
            {
                rb.simulated = true;
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }

            if (warningCoroutine != null)
            {
                StopCoroutine(warningCoroutine);
                warningCoroutine = null;
            }

            if (explodingBlueprint.debugLog)
            {
                Debug.Log(
                    $"[ExplodingMonster] 스폰 완료 | HP={currentHealth:0.##} | " +
                    $"Speed={explodingBlueprint.movespeed:0.##} | Damage={explodingBlueprint.explosionDamage:0.##}",
                    this
                );
            }
        }

        protected override void Update()
        {
            base.Update();

            if (!alive || explosionStarted || playerCharacter == null)
            {
                return;
            }

            timeSinceSpawn += Time.deltaTime;

            UpdateWarningState();
        }

        protected override void FixedUpdate()
        {
            base.FixedUpdate();

            if (!alive || explosionStarted || playerCharacter == null || rb == null)
            {
                return;
            }

            Vector2 toPlayer =
                (Vector2)playerCharacter.transform.position -
                (Vector2)transform.position;

            if (toPlayer.sqrMagnitude > 0.0001f)
            {
                Vector2 moveDirection = toPlayer.normalized;

                rb.velocity +=
                    moveDirection *
                    explodingBlueprint.acceleration *
                    Time.fixedDeltaTime;

                // 자폭 몬스터는 빠르지만 너무 비정상적으로 가속되지 않도록 상한을 둔다.
                float maxSpeed = Mathf.Max(0.1f, explodingBlueprint.movespeed);

                if (rb.velocity.magnitude > maxSpeed)
                {
                    rb.velocity = rb.velocity.normalized * maxSpeed;
                }
            }

            if (entityManager != null && entityManager.Grid != null)
            {
                entityManager.Grid.UpdateClient(this);
            }

            // 충돌 이벤트가 누락되는 경우를 대비한 안전장치.
            // 플레이어 중심이 폭발 반경 안에 들어오면 폭발한다.
            TryExplodeByDistance();
        }

        private void UpdateWarningState()
        {
            if (warningActive)
            {
                return;
            }

            if (explodingBlueprint == null || playerCharacter == null)
            {
                return;
            }

            float warningDistance = Mathf.Max(0.1f, explodingBlueprint.warningDistance);

            float distanceToPlayer =
                Vector2.Distance(transform.position, playerCharacter.transform.position);

            if (distanceToPlayer <= warningDistance)
            {
                warningActive = true;
                warningCoroutine = StartCoroutine(WarningBlinkRoutine());

                if (explodingBlueprint.debugLog)
                {
                    Debug.Log("[ExplodingMonster] 폭발 경고 상태 진입", this);
                }
            }
        }

        private IEnumerator WarningBlinkRoutine()
        {
            if (monsterSpriteRenderer == null)
            {
                yield break;
            }

            float blinkSpeed = Mathf.Max(1f, explodingBlueprint.warningBlinkSpeed);

            while (alive && !explosionStarted)
            {
                float t = Mathf.PingPong(Time.time * blinkSpeed, 1f);

                // 원래 색과 붉은색 사이를 빠르게 깜빡인다.
                monsterSpriteRenderer.color = Color.Lerp(
                    originalColor,
                    Color.red,
                    t
                );

                yield return null;
            }

            if (monsterSpriteRenderer != null)
            {
                monsterSpriteRenderer.color = originalColor;
            }
        }

        private void TryExplodeByDistance()
        {
            if (!alive || explosionStarted)
            {
                return;
            }

            if (explodingBlueprint == null)
            {
                return;
            }

            if (timeSinceSpawn < Mathf.Max(0f, explodingBlueprint.armDelay))
            {
                return;
            }

            if (playerCharacter == null)
            {
                return;
            }

            if (playerCharacter.IsDashing)
            {
                // 현재 게임의 대쉬는 몬스터를 관통하는 회피 수단이므로,
                // 대쉬 중에는 자폭 판정을 발생시키지 않는다.
                return;
            }

            Vector2 playerCenter = playerCharacter.CenterTransform != null
                ? (Vector2)playerCharacter.CenterTransform.position
                : (Vector2)playerCharacter.transform.position;

            float radius = Mathf.Max(0.05f, explodingBlueprint.explosionRadius);
            float distance = Vector2.Distance(transform.position, playerCenter);

            if (distance <= radius)
            {
                StartExplosion(playerCharacter);
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            TryExplodeFromCollider(collision.collider);
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            TryExplodeFromCollider(collision.collider);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryExplodeFromCollider(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            TryExplodeFromCollider(other);
        }

        private void TryExplodeFromCollider(Collider2D other)
        {
            if (!alive || explosionStarted)
            {
                return;
            }

            if (explodingBlueprint == null)
            {
                return;
            }

            if (timeSinceSpawn < Mathf.Max(0f, explodingBlueprint.armDelay))
            {
                return;
            }

            if (other == null)
            {
                return;
            }

            bool isPlayerLayer =
                (explodingBlueprint.playerLayer & (1 << other.gameObject.layer)) != 0;

            if (!isPlayerLayer)
            {
                return;
            }

            Character hitCharacter = other.GetComponentInParent<Character>();

            if (hitCharacter == null)
            {
                return;
            }

            if (hitCharacter != playerCharacter)
            {
                return;
            }

            if (hitCharacter.IsDashing)
            {
                // 대쉬 중에는 피해도, 자폭도 발생하지 않는다.
                // 플레이어가 자폭 몬스터를 관통해서 피할 수 있게 하기 위한 처리.
                return;
            }

            StartExplosion(hitCharacter);
        }

        private void StartExplosion(Character targetCharacter)
        {
            if (!alive || explosionStarted)
            {
                return;
            }

            explosionStarted = true;
            alive = false;

            if (warningCoroutine != null)
            {
                StopCoroutine(warningCoroutine);
                warningCoroutine = null;
            }

            // 핵심 수정:
            // 자폭 판정이 시작된 즉시 물리 충돌을 끊는다.
            // 그래야 플레이어에게 붙어서 OnCollisionStay2D / OnTriggerStay2D가 반복 실행되지 않는다.
            DisableCollisionAndMovement();

            // 데미지는 자폭 시작 시점에 딱 1번만 적용한다.
            ApplyExplosionDamageOnce(targetCharacter);

            if (entityManager != null)
            {
                entityManager.LivingMonsters.Remove(this);
            }

            StartCoroutine(ExplosionRoutine());
        }

        private void DisableCollisionAndMovement()
        {
            if (rb != null)
            {
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;

                // 콜라이더만 끄는 것보다 확실하게 물리 시뮬레이션까지 잠시 끊는다.
                // 풀링으로 다시 재사용될 때 Setup에서 true로 복구한다.
                rb.simulated = false;
            }

            if (monsterHitbox != null)
            {
                monsterHitbox.enabled = false;
            }

            if (monsterLegsCollider != null)
            {
                monsterLegsCollider.enabled = false;
            }
        }

        private IEnumerator ExplosionRoutine()
        {
            if (deathParticles != null)
            {
                deathParticles.Play();
            }

            if (monsterSpriteRenderer != null)
            {
                // 폭발 직전 한 프레임 크게 보이게 해서 터지는 느낌을 준다.
                monsterSpriteRenderer.color = Color.red;
                transform.localScale = originalScale * 1.15f;
            }

            yield return new WaitForSeconds(0.08f);

            if (monsterSpriteRenderer != null)
            {
                monsterSpriteRenderer.enabled = false;
            }

            if (shadow != null)
            {
                shadow.SetActive(false);
            }

            if (deathParticles != null)
            {
                float waitTime = Mathf.Max(0.05f, deathParticles.main.duration - 0.08f);
                yield return new WaitForSeconds(waitTime);
            }

            // 풀링으로 재사용될 수 있으므로, 비활성화 전에 원래 상태로 복구한다.
            transform.localScale = originalScale;

            if (monsterSpriteRenderer != null)
            {
                monsterSpriteRenderer.enabled = true;
                monsterSpriteRenderer.color = originalColor;
            }

            if (shadow != null)
            {
                shadow.SetActive(true);
            }

            if (rb != null)
            {
                rb.simulated = true;
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }

            OnKilled.Invoke(this);
            OnKilled.RemoveAllListeners();

            bool countAsPlayerKill =
                explodingBlueprint != null &&
                explodingBlueprint.rewardOnSelfExplosion;

            if (entityManager != null)
            {
                entityManager.DespawnMonster(monsterIndex, this, countAsPlayerKill);
            }

            if (explodingBlueprint != null && explodingBlueprint.debugLog)
            {
                Debug.Log(
                    $"[ExplodingMonster] 자폭 완료 | Reward={countAsPlayerKill}",
                    this
                );
            }
        }

        private void ApplyExplosionDamageOnce(Character targetCharacter)
        {
            if (explosionDamageApplied)
            {
                return;
            }

            explosionDamageApplied = true;

            if (targetCharacter == null)
            {
                return;
            }

            if (explodingBlueprint == null)
            {
                return;
            }

            if (targetCharacter.IsDashing)
            {
                return;
            }

            Vector2 playerCenter = targetCharacter.CenterTransform != null
                ? (Vector2)targetCharacter.CenterTransform.position
                : (Vector2)targetCharacter.transform.position;

            float radius = Mathf.Max(0.05f, explodingBlueprint.explosionRadius);
            float distance = Vector2.Distance(transform.position, playerCenter);

            if (distance > radius)
            {
                if (explodingBlueprint.debugLog)
                {
                    Debug.Log(
                        $"[ExplodingMonster] 자폭은 시작됐지만 플레이어가 폭발 반경 밖입니다. Distance={distance:0.##}, Radius={radius:0.##}",
                        this
                    );
                }

                return;
            }

            Vector2 knockbackDirection =
                (playerCenter - (Vector2)transform.position).normalized;

            if (knockbackDirection == Vector2.zero)
            {
                knockbackDirection = Vector2.right;
            }

            Vector2 knockback =
                knockbackDirection *
                Mathf.Max(0f, explodingBlueprint.explosionKnockback);

            targetCharacter.TakeDamage(
                Mathf.Max(0f, explodingBlueprint.explosionDamage),
                knockback
            );

            if (explodingBlueprint.debugLog)
            {
                Debug.Log(
                    $"[ExplodingMonster] 플레이어에게 자폭 피해 1회 적용 | Damage={explodingBlueprint.explosionDamage}",
                    this
                );
            }
        }

        protected override void DropLoot()
        {
            // 플레이어가 침으로 처치했을 때는 부모 Monster의 DropLoot를 그대로 사용한다.
            // 자폭으로 죽었을 때는 ExplosionRoutine에서 DropLoot를 호출하지 않으므로 보상이 없다.
            base.DropLoot();
        }
    }
}