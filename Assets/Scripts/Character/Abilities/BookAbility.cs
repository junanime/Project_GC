using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    public class BookAbility : Ability
    {
        [Header("Book Stats")]
        [SerializeField] protected GameObject bookPrefab;
        [SerializeField] protected LayerMask monsterLayer;
        [SerializeField] protected UpgradeableProjectileCount projectileCount;
        [SerializeField] protected UpgradeableAOE radius;
        [SerializeField] protected UpgradeableDamage damage;
        [SerializeField] protected UpgradeableKnockback knockback;
        [SerializeField] protected UpgradeableRotationSpeed speed;

        private List<Book> books;


        // =========================================================
        // Use
        // =========================================================

        protected override void Use()
        {
            base.Use();

            gameObject.SetActive(true);

            projectileCount.OnChanged.AddListener(
                RefreshBooks
            );

            books = new List<Book>();


            for (int i = 0; i < projectileCount.Value; i++)
            {
                AddBook();
            }
        }


        // =========================================================
        // Upgrade
        // =========================================================

        protected override void Upgrade()
        {
            base.Upgrade();

            RefreshBooks();
        }


        // =========================================================
        // Update
        // =========================================================

        private void Update()
        {
            if (books == null || books.Count == 0)
            {
                return;
            }


            for (int i = 0; i < books.Count; i++)
            {
                if (books[i] == null)
                {
                    continue;
                }


                float theta =
                    (2f * Mathf.PI * i) /
                    books.Count;


                books[i].transform.localPosition =
                    new Vector3(
                        Mathf.Sin(
                            theta +
                            Time.time * speed.Value
                        ),
                        Mathf.Cos(
                            theta +
                            Time.time * speed.Value
                        ),
                        0f
                    );
            }
        }


        // =========================================================
        // Damage
        // =========================================================

        public void Damage(
            IDamageable damageable
        )
        {
            if (damageable == null)
            {
                return;
            }


            Vector2 knockbackDirection =
                (
                    damageable.transform.position -
                    playerCharacter.transform.position
                ).normalized;


            float dealtDamage =
                damage.Value;


            // 실제 몬스터 피해
            damageable.TakeDamage(
                dealtDamage,
                knockback.Value *
                knockbackDirection
            );


            // =====================================================
            // 피해량 기록
            // =====================================================
            //
            // 기존:
            //
            // playerCharacter.OnDealDamage.Invoke(
            //     damage.Value
            // );
            //
            // 변경:
            //
            // ReportDamage()
            //
            // 1. StatsManager 전체 피해량
            // 2. AugmentDamageTracker의 이 Ability 피해량
            //
            // 을 동시에 기록
            // =====================================================

            ReportDamage(
                dealtDamage
            );
        }


        // =========================================================
        // Refresh Books
        // =========================================================

        private void RefreshBooks()
        {
            if (books == null)
            {
                books = new List<Book>();
            }


            for (
                int i = books.Count;
                i < projectileCount.Value;
                i++
            )
            {
                AddBook();
            }
        }


        // =========================================================
        // Add Book
        // =========================================================

        private void AddBook()
        {
            if (bookPrefab == null ||
                playerCharacter == null)
            {
                return;
            }


            GameObject bookObject =
                Instantiate(
                    bookPrefab,
                    playerCharacter.transform
                );


            if (bookObject == null)
            {
                return;
            }


            Book book =
                bookObject.GetComponent<Book>();


            if (book == null)
            {
                Debug.LogWarning(
                    "[BookAbility] 생성된 Book Prefab에 Book 컴포넌트가 없습니다."
                );

                Destroy(bookObject);
                return;
            }


            book.Init(
                this,
                monsterLayer
            );


            books.Add(
                book
            );
        }
    }
}