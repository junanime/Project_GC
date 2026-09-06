using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 일반 Pierce와 완전히 분리된 보스 전용 관통 수치를 관리합니다.
    ///
    /// Boss Pierce 0:
    /// 보스 파츠 하나에 적중하면 일반 Pierce가 아무리 많아도
    /// 다음 보스 파츠를 관통하지 못합니다.
    ///
    /// Boss Pierce 1:
    /// 예) LeftArm -> Core
    ///
    /// 향후 조건부 증강 등에서
    /// AddBossPierce(1)을 호출하여 확장할 수 있습니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BossPierceRuntime : MonoBehaviour
    {
        [Header("Boss Pierce / 보스 관통")]

        [Tooltip(
            "일반 Pierce와 별도로 보스 파츠를 추가 관통할 수 있는 횟수입니다. " +
            "0이면 첫 보스 파츠에서 막히며, 1이면 팔을 맞힌 뒤 Core까지 진행할 수 있습니다."
        )]
        [SerializeField, Min(0)]
        private int bossPierceCount = 0;

        [Header("Debug")]

        [Tooltip(
            "체크하면 Boss Pierce 값이 변경될 때 Console에 로그를 출력합니다."
        )]
        [SerializeField]
        private bool debugLog = false;

        public int BossPierceCount =>
            Mathf.Max(0, bossPierceCount);

        private void OnValidate()
        {
            bossPierceCount =
                Mathf.Max(0, bossPierceCount);
        }

        /// <summary>
        /// 현재 Boss Pierce 값을 직접 설정합니다.
        /// </summary>
        public void SetBossPierceCount(int count)
        {
            bossPierceCount =
                Mathf.Max(0, count);

            if (debugLog)
            {
                Debug.Log(
                    $"[BossPierce] Boss Pierce 설정 = {bossPierceCount}",
                    this
                );
            }
        }

        /// <summary>
        /// 현재 Boss Pierce에 값을 추가합니다.
        /// 향후 증강에서 +1 등을 적용할 때 사용합니다.
        /// </summary>
        public void AddBossPierce(int amount)
        {
            bossPierceCount =
                Mathf.Max(
                    0,
                    bossPierceCount + amount
                );

            if (debugLog)
            {
                Debug.Log(
                    $"[BossPierce] Boss Pierce 변경 | " +
                    $"Amount={amount}, Current={bossPierceCount}",
                    this
                );
            }
        }

        /// <summary>
        /// Character에 붙은 BossPierceRuntime을 찾아 현재 값을 반환합니다.
        /// 컴포넌트가 없으면 0입니다.
        /// </summary>
        public static int GetBossPierceCount(
            Character character)
        {
            if (character == null)
            {
                return 0;
            }

            BossPierceRuntime runtime =
                character.GetComponent<BossPierceRuntime>();

            if (runtime == null)
            {
                runtime =
                    character.GetComponentInChildren
                    <
                        BossPierceRuntime
                    >(true);
            }

            return runtime != null
                ? runtime.BossPierceCount
                : 0;
        }

        /// <summary>
        /// 향후 증강 코드에서 사용할 수 있는 생성 헬퍼입니다.
        /// </summary>
        public static BossPierceRuntime GetOrCreate(
            Character character)
        {
            if (character == null)
            {
                return null;
            }

            BossPierceRuntime runtime =
                character.GetComponent<BossPierceRuntime>();

            if (runtime == null)
            {
                runtime =
                    character.gameObject.AddComponent
                    <
                        BossPierceRuntime
                    >();
            }

            return runtime;
        }

        [ContextMenu("Reset Boss Pierce To 0")]
        private void DebugResetBossPierce()
        {
            SetBossPierceCount(0);
        }

        [ContextMenu("Set Boss Pierce To 1")]
        private void DebugSetBossPierceToOne()
        {
            SetBossPierceCount(1);
        }
    }
}