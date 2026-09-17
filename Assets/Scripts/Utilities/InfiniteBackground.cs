using System.Collections;
using UnityEngine;

namespace Vampire
{
    public class InfiniteBackground : MonoBehaviour
    {
        private Transform playerTransform;

        [SerializeField] private Material backgroundMaterial;

        private MeshRenderer meshRenderer;
        private Material runtimeMaterial;

        private Vector2 previousResetPosition = Vector2.zero;
        private Vector2 resetOffset = Vector2.zero;

        private float resetDistance = 15f;
        private float resetDuration = 5f;


        private void Awake()
        {
            meshRenderer = GetComponent<MeshRenderer>();
            GroundVisualSorting.ApplyBackground(meshRenderer, -1000);

            // 화면 크기에 맞춰 배경 크기 설정
            Vector2 bottomLeft =
                Camera.main.ViewportToWorldPoint(
                    new Vector3(
                        0,
                        0,
                        Camera.main.nearClipPlane
                    )
                );

            Vector2 topRight =
                Camera.main.ViewportToWorldPoint(
                    new Vector3(
                        1,
                        1,
                        Camera.main.nearClipPlane
                    )
                );

            Vector3 screenSizeWorldSpace =
                new Vector3(
                    topRight.x - bottomLeft.x,
                    topRight.y - bottomLeft.y,
                    1
                );

            transform.localScale = screenSizeWorldSpace;


            // Inspector에 넣어둔 Material을 그대로 사용
            if (meshRenderer != null &&
                backgroundMaterial != null)
            {
                runtimeMaterial = new Material(backgroundMaterial) { name = backgroundMaterial.name + " (Runtime)", hideFlags = HideFlags.DontSave };
                backgroundMaterial = runtimeMaterial;
                meshRenderer.sharedMaterial = runtimeMaterial;
            }
        }


        private void OnDestroy()
        {
            if (runtimeMaterial != null) Destroy(runtimeMaterial);
        }

        public void Init(
            Texture2D backgroundTexture,
            Transform playerTransform)
        {
            this.playerTransform = playerTransform;

            /*
             * 중요:
             *
             * 기존에는 아래 코드 때문에
             * Inspector에서 Material을 변경해도
             * 외부에서 전달된 예전 Texture가 다시 들어갔음.
             *
             * backgroundMaterial.mainTexture = backgroundTexture;
             *
             * 이제 Material에 직접 설정한 Texture를 사용하기 때문에
             * 여기서는 Texture를 덮어쓰지 않음.
             */

            if (backgroundMaterial == null)
            {
                Debug.LogWarning(
                    "[InfiniteBackground] Background Material이 없습니다.",
                    this
                );

                return;
            }


            backgroundMaterial.SetFloat(
                "_Shockwave",
                0
            );

            resetOffset = Vector2.zero;

            backgroundMaterial.SetVector(
                "_ResetOffset",
                resetOffset
            );

            backgroundMaterial.SetInt(
                "_Resetting",
                0
            );

            previousResetPosition =
                playerTransform != null
                    ? playerTransform.position
                    : Vector2.zero;
        }


        public IEnumerator Shockwave(float distance)
        {
            if (backgroundMaterial == null ||
                playerTransform == null)
            {
                yield break;
            }


            float d = 0f;

            while (d < distance)
            {
                d += Time.deltaTime * 16f;

                backgroundMaterial.SetFloat(
                    "_Shockwave",
                    d
                );

                backgroundMaterial.SetVector(
                    "_PlayerPosition",
                    playerTransform.position
                );

                yield return null;
            }

            backgroundMaterial.SetFloat(
                "_Shockwave",
                0
            );
        }


        private void Update()
        {
            if (playerTransform == null ||
                backgroundMaterial == null)
            {
                return;
            }


            Vector2 toReset =
                previousResetPosition -
                (Vector2)playerTransform.position;


            if (toReset.sqrMagnitude >
                resetDistance * resetDistance)
            {
                StartCoroutine(
                    ResetBackground(toReset)
                );

                previousResetPosition =
                    playerTransform.position;
            }
        }


        private IEnumerator ResetBackground(
            Vector2 toReset)
        {
            backgroundMaterial.SetInt(
                "_Resetting",
                1
            );

            backgroundMaterial.SetVector(
                "_TempResetOffset",
                toReset
            );


            float t = 0f;

            while (t < resetDuration)
            {
                t += Time.deltaTime;

                backgroundMaterial.SetFloat(
                    "_ResetBlend",
                    t / resetDuration
                );

                yield return null;
            }


            resetOffset += toReset;

            backgroundMaterial.SetVector(
                "_ResetOffset",
                resetOffset
            );

            backgroundMaterial.SetInt(
                "_Resetting",
                0
            );
        }
    }
}