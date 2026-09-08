using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    public class ResultCharacterAnimator : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Image targetImage;

        [Header("Fallback")]
        [SerializeField] private float fallbackIdleFrameTime = 0.2f;

        [Header("Random Action")]
        [SerializeField] private bool playRandomAction = true;
        [SerializeField] private float minRandomActionInterval = 2.0f;
        [SerializeField] private float maxRandomActionInterval = 4.0f;

        private Sprite[] idleSprites;
        private float idleFrameTime;

        private Sprite[] actionASprites;
        private Sprite[] actionBSprites;
        private float actionFrameTime;

        private Coroutine animationRoutine;


        private void Awake()
        {
            if (targetImage == null)
            {
                targetImage = GetComponent<Image>();
            }
        }


        private void OnDisable()
        {
            StopAnimation();
        }


        public void SetCharacter(CharacterBlueprint blueprint)
        {
            if (blueprint == null)
            {
                Clear();
                return;
            }

            // 결과창 전용 idle이 있으면 그걸 쓰고,
            // 없으면 기존 walkSpriteSequence를 fallback으로 사용
            idleSprites = HasSprites(blueprint.resultIdleSpriteSequence)
                ? blueprint.resultIdleSpriteSequence
                : blueprint.walkSpriteSequence;

            idleFrameTime = blueprint.resultIdleFrameTime > 0f
                ? blueprint.resultIdleFrameTime
                : fallbackIdleFrameTime;

            actionASprites = blueprint.resultActionASpriteSequence;
            actionBSprites = blueprint.resultActionBSpriteSequence;
            actionFrameTime = blueprint.resultActionFrameTime > 0f
                ? blueprint.resultActionFrameTime
                : 0.12f;

            if (targetImage != null)
            {
                targetImage.sprite = HasSprites(idleSprites) ? idleSprites[0] : null;
                targetImage.enabled = targetImage.sprite != null;
                targetImage.preserveAspect = true;
            }

            StartAnimation();
        }


        public void Clear()
        {
            StopAnimation();

            idleSprites = null;
            actionASprites = null;
            actionBSprites = null;

            if (targetImage != null)
            {
                targetImage.sprite = null;
                targetImage.enabled = false;
            }
        }


        private void StartAnimation()
        {
            StopAnimation();

            if (!isActiveAndEnabled)
                return;

            if (targetImage == null)
                return;

            if (!HasSprites(idleSprites))
                return;

            animationRoutine = StartCoroutine(AnimationLoop());
        }


        private void StopAnimation()
        {
            if (animationRoutine != null)
            {
                StopCoroutine(animationRoutine);
                animationRoutine = null;
            }
        }


        private IEnumerator AnimationLoop()
        {
            int idleIndex = 0;

            while (true)
            {
                float nextActionDelay = Random.Range(
                    minRandomActionInterval,
                    maxRandomActionInterval
                );

                float elapsed = 0f;

                // idle 루프
                while (elapsed < nextActionDelay)
                {
                    if (HasSprites(idleSprites))
                    {
                        targetImage.sprite = idleSprites[idleIndex];
                        idleIndex = (idleIndex + 1) % idleSprites.Length;
                    }

                    yield return WaitUnscaled(idleFrameTime);
                    elapsed += idleFrameTime;
                }

                // 랜덤 행동
                if (playRandomAction)
                {
                    Sprite[] actionSprites = GetRandomActionSprites();

                    if (HasSprites(actionSprites))
                    {
                        for (int i = 0; i < actionSprites.Length; i++)
                        {
                            targetImage.sprite = actionSprites[i];
                            yield return WaitUnscaled(actionFrameTime);
                        }
                    }
                }
            }
        }


        private Sprite[] GetRandomActionSprites()
        {
            bool hasA = HasSprites(actionASprites);
            bool hasB = HasSprites(actionBSprites);

            if (hasA && hasB)
            {
                return Random.value < 0.5f
                    ? actionASprites
                    : actionBSprites;
            }

            if (hasA) return actionASprites;
            if (hasB) return actionBSprites;

            return null;
        }


        private bool HasSprites(Sprite[] sprites)
        {
            return sprites != null && sprites.Length > 0;
        }


        private IEnumerator WaitUnscaled(float duration)
        {
            float t = 0f;

            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }
    }
}