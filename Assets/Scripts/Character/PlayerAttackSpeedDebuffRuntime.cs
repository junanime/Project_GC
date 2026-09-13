using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 몬스터 디버퍼에게 피격되었을 때 플레이어에게 적용되는 이동 방향 반전 디버프입니다.
    ///
    /// 같은 디버프가 다시 들어오면 중첩하지 않고 지속 시간만 갱신합니다.
    /// 양방향 화살표가 비처럼 내려오는 임시 연출도 함께 처리합니다.
    /// </summary>
    [DisallowMultipleComponent]
    // Keep the original class/file identity for existing prefab and serialized references.
    public class PlayerAttackSpeedDebuffRuntime : MonoBehaviour
    {
        private class ArrowVisual
        {
            public TextMeshPro text;
            public Vector3 localPosition;
            public float fallSpeed;
        }

        private Character character;
        private bool active;
        private float remainingTime;
        public bool IsActive => active;
        public float RemainingTime => remainingTime;

        private GameObject arrowRoot;
        private readonly List<ArrowVisual> arrows = new List<ArrowVisual>();

        private Color arrowColor = Color.yellow;
        private float arrowYOffset = 1.4f;
        private float arrowWidth = 0.8f;
        private float arrowHeight = 1.1f;
        private float arrowFallSpeed = 1.5f;
        private int arrowCount = 5;
        private float arrowFontSize = 2.2f;
        private int arrowSortingOrder = 150;

        private bool debugLog;

        private void Awake()
        {
            character = GetComponent<Character>();
        }

        public void Apply(
            float duration,
            Color arrowColor,
            float arrowYOffset,
            float arrowWidth,
            float arrowHeight,
            float arrowFallSpeed,
            int arrowCount,
            float arrowFontSize,
            int arrowSortingOrder,
            bool debugLog)
        {
            if (character == null)
            {
                character = GetComponent<Character>();
            }

            if (character == null)
            {
                return;
            }

            this.arrowColor = arrowColor;
            this.arrowYOffset = arrowYOffset;
            this.arrowWidth = Mathf.Max(0.1f, arrowWidth);
            this.arrowHeight = Mathf.Max(0.1f, arrowHeight);
            this.arrowFallSpeed = Mathf.Max(0.1f, arrowFallSpeed);
            this.arrowCount = Mathf.Max(1, arrowCount);
            this.arrowFontSize = Mathf.Max(0.1f, arrowFontSize);
            this.arrowSortingOrder = arrowSortingOrder;
            this.debugLog = debugLog;

            // Store the raw input on Character; inversion is resolved when movement is consumed.
            // Repeated hits refresh the timer and never toggle the direction back accidentally.
            character.SetMovementControlsReversed(true);

            remainingTime = Mathf.Max(0.1f, duration);
            active = true;

            EnsureArrowVisuals();

            if (this.debugLog)
            {
                Debug.Log($"[몬스터 디버프] 플레이어 이동 방향 반전 적용 | {remainingTime:0.##}초", this);
            }
        }

        private void Update()
        {
            if (!active)
            {
                return;
            }

            remainingTime -= Time.deltaTime;
            UpdateArrowRain();

            if (remainingTime <= 0f)
            {
                ClearDebuff();
            }
        }

        public void ClearDebuff()
        {
            if (!active)
            {
                return;
            }

            if (character != null)
            {
                character.SetMovementControlsReversed(false);
            }

            active = false;
            remainingTime = 0f;

            DestroyArrowVisuals();

            if (debugLog)
            {
                Debug.Log("[몬스터 디버프] 플레이어 이동 방향 반전 해제", this);
            }
        }

        private void EnsureArrowVisuals()
        {
            if (arrowRoot == null)
            {
                arrowRoot = new GameObject("Reversed Controls Arrows");
                arrowRoot.transform.SetParent(transform, false);
                arrowRoot.transform.localPosition = Vector3.zero;
            }

            while (arrows.Count < arrowCount)
            {
                GameObject arrowObject = new GameObject("Control Reversal Arrow");
                arrowObject.transform.SetParent(arrowRoot.transform, false);

                TextMeshPro text = arrowObject.AddComponent<TextMeshPro>();
                text.text = "↔";
                text.alignment = TextAlignmentOptions.Center;
                text.fontSize = arrowFontSize;
                text.color = arrowColor;

                MeshRenderer renderer = text.GetComponent<MeshRenderer>();

                if (renderer != null)
                {
                    renderer.sortingOrder = arrowSortingOrder;
                }

                ArrowVisual arrow = new ArrowVisual
                {
                    text = text,
                    localPosition = GetRandomArrowStartPosition(),
                    fallSpeed = arrowFallSpeed * Random.Range(0.8f, 1.25f)
                };

                arrowObject.transform.localPosition = arrow.localPosition;
                arrows.Add(arrow);
            }

            for (int i = 0; i < arrows.Count; i++)
            {
                if (arrows[i].text != null)
                {
                    arrows[i].text.gameObject.SetActive(i < arrowCount);
                    arrows[i].text.color = arrowColor;
                    arrows[i].text.fontSize = arrowFontSize;

                    MeshRenderer renderer = arrows[i].text.GetComponent<MeshRenderer>();

                    if (renderer != null)
                    {
                        renderer.sortingOrder = arrowSortingOrder;
                    }
                }
            }
        }

        private void UpdateArrowRain()
        {
            if (arrowRoot == null)
            {
                return;
            }

            for (int i = 0; i < arrows.Count; i++)
            {
                ArrowVisual arrow = arrows[i];

                if (arrow == null || arrow.text == null || !arrow.text.gameObject.activeSelf)
                {
                    continue;
                }

                arrow.localPosition += Vector3.down * arrow.fallSpeed * Time.deltaTime;

                if (arrow.localPosition.y <= arrowYOffset - arrowHeight)
                {
                    arrow.localPosition = GetRandomArrowStartPosition();
                    arrow.fallSpeed = arrowFallSpeed * Random.Range(0.8f, 1.25f);
                }

                arrow.text.transform.localPosition = arrow.localPosition;
            }
        }

        private Vector3 GetRandomArrowStartPosition()
        {
            return new Vector3(
                Random.Range(-arrowWidth * 0.5f, arrowWidth * 0.5f),
                arrowYOffset + Random.Range(0f, arrowHeight),
                0f);
        }

        private void DestroyArrowVisuals()
        {
            if (arrowRoot != null)
            {
                Destroy(arrowRoot);
                arrowRoot = null;
            }

            arrows.Clear();
        }

        private void OnDisable()
        {
            ClearDebuff();
        }

        private void OnDestroy()
        {
            ClearDebuff();
        }
    }
}