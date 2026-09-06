using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 보스 여부와 보스 파츠/루트를 문자열 클래스명 검사 없이 찾기 위한 공통 유틸리티입니다.
    ///
    /// 이후 용도:
    /// - 일반 관통침은 보스 파츠에서 관통 종료
    /// - 보스 전용 관통 증강은 예외 허용
    /// - 보스 추가 피해 아이템
    /// - 보스 전용 특수효과
    /// - 보스 자폭 피해 중복 방지
    /// </summary>
    public static class BossTargetUtility
    {
        public static bool TryGetBossPart(
            Component component,
            out BossPartDamageTestPart bossPart)
        {
            bossPart = null;

            if (component == null)
            {
                return false;
            }

            bossPart = component.GetComponent<BossPartDamageTestPart>();

            if (bossPart == null)
            {
                bossPart = component.GetComponentInParent<BossPartDamageTestPart>(true);
            }

            return bossPart != null;
        }

        public static BossPartDamageTestRootController GetBossPartRoot(
            Component component)
        {
            if (component == null)
            {
                return null;
            }

            if (TryGetBossPart(component, out BossPartDamageTestPart part))
            {
                BossPartDamageTestRootController partRoot =
                    part.GetComponentInParent<BossPartDamageTestRootController>(true);

                if (partRoot != null)
                {
                    return partRoot;
                }
            }

            return component.GetComponentInParent<BossPartDamageTestRootController>(true);
        }

        public static BossController GetBossController(Component component)
        {
            if (component == null)
            {
                return null;
            }

            return component.GetComponentInParent<BossController>(true);
        }

        public static bool IsBossTarget(Component component)
        {
            if (component == null)
            {
                return false;
            }

            if (TryGetBossPart(component, out _))
            {
                return true;
            }

            if (GetBossController(component) != null)
            {
                return true;
            }

            return GetBossPartRoot(component) != null;
        }
    }
}
