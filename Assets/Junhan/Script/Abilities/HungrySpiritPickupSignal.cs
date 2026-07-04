using System;

namespace Vampire
{
    /// <summary>
    /// 전설증강 '헝그리정신'이 경험치/골드 픽업 순간을 감지하기 위한 전역 신호 클래스입니다.
    /// ExpGem, Coin 원본 구조를 크게 바꾸지 않고 OnCollected()에서 한 줄만 호출하기 위해 분리했습니다.
    /// </summary>
    public static class HungrySpiritPickupSignal
    {
        public static event Action<Character> OnExpOrCoinPickedUp;

        public static void NotifyExpOrCoinPickedUp(Character collector)
        {
            if (collector == null)
            {
                return;
            }

            OnExpOrCoinPickedUp?.Invoke(collector);
        }
    }
}