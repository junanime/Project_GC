using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 전설증강 조건부 특수증강의 획득 여부와 런타임 효과 상태를 보관하는 컴포넌트입니다.
    ///
    /// 이 스크립트는 직접 공격을 만들지 않습니다.
    /// 각 전설 컨트롤러와 SyringeDartAbility, Character 등이 이 값을 읽어서 실제 효과를 적용합니다.
    ///
    /// 예:
    /// - 한 방 회피 획득 여부
    /// - 혈류 차단 획득 여부
    /// - 이기어침 궤도 반전 획득 여부
    /// - 샷건침 탄창 확장 획득 여부
    /// </summary>
    public class LegendaryConditionalSpecialRuntime : MonoBehaviour
    {
        private readonly HashSet<string> acquiredIds = new HashSet<string>();

        [Header("LifeBurn / 생명연소")]
        [Tooltip("절박한 침끝: 플레이어 가까이에 있는 적에게 침 피해가 증가합니다.")]
        public bool lifeBurnDesperateNeedleTip;

        [Tooltip("한 방 회피: 몬스터 100마리 처치마다 1회 피해 무시 실드를 얻습니다.")]
        public bool lifeBurnOneHitEvade;

        [Tooltip("혈류 차단: 회복 판정이 발생하면 회복 대신 공격속도 스택을 얻습니다.")]
        public bool lifeBurnBloodFlowBlock;

        [Tooltip("강심장: 움직이지 않는 시간에 따라 데미지 스택을 얻고, 움직이면 초기화됩니다.")]
        public bool lifeBurnStrongHeart;

        [Header("LifeBurn Runtime")]
        [Tooltip("한 방 회피 실드를 얻기 위한 현재 처치 수입니다.")]
        public int lifeBurnOneHitKillCounter;

        [Tooltip("현재 한 방 회피 실드 보유 여부입니다.")]
        public bool lifeBurnHasOneHitShield;

        [Tooltip("혈류 차단 공격속도 스택입니다. 최대 10스택입니다.")]
        public int lifeBurnBloodFlowAttackSpeedStacks;

        [Tooltip("강심장 데미지 스택입니다. 최대 10스택입니다.")]
        public int lifeBurnStrongHeartStacks;

        [Header("CloneCulture / 분신배양")]
        [Tooltip("분신 혈류 연결: 분신이 적을 맞히면 본체 공격속도가 짧게 증가합니다.")]
        public bool cloneBloodFlowLink;

        [Tooltip("다중그림자분신술: 분신 수가 1 증가합니다.")]
        public bool cloneMultiShadowClone;

        [Tooltip("쌍침 협공: 본체와 분신이 같은 적을 공격하면 추가 피해를 줍니다.")]
        public bool cloneTwinNeedleCoop;

        [Header("HedgehogNeedle / 고슴도침")]
        [Tooltip("가시 반동: 전방위 발사 시 주변 적 넉백이 증가합니다.")]
        public bool hedgehogSpineRecoil;

        [Tooltip("침가시 밀도 증가: 전방위 발사 침 개수가 증가합니다.")]
        public bool hedgehogNeedleDensity;

        [Tooltip("다층 가시: 전방위 발사가 두 겹으로 나가지만 쿨타임이 증가합니다.")]
        public bool hedgehogMultiLayerSpines;

        [Header("HeavySnipe / 대물침")]
        [Tooltip("약점 관통선: 대물침 경로에 선이 남고, 선 위로 발사한 침 피해/속도가 증가합니다.")]
        public bool heavySnipeWeaknessPiercingLine;

        [Tooltip("과충전 억제: 최대 차지 시간이 줄고, 중간 차지 피해 효율이 증가합니다.")]
        public bool heavySnipeOverchargeSuppress;

        [Tooltip("저격 호흡: 차지 중 피격당하지 않으면 치명타 확률이 증가합니다.")]
        public bool heavySnipeSniperBreathing;

        [Header("CursorControl / 이기어침")]
        [Tooltip("점막 궤도 윤활: 궤도 속도 증가, 같은 적 재타격 간격 감소.")]
        public bool cursorMucosalOrbitLubrication;

        [Tooltip("궤도 반전: 일정 시간마다 궤도 진행 방향이 반전되고, 반전 직후 피해가 증가합니다.")]
        public bool cursorOrbitReverse;

        [Tooltip("궤도 확장: 무한궤도 좌우 반경과 침 속도가 증가합니다.")]
        public bool cursorOrbitExpansion;

        [Header("NeuralBlock / 신경차단")]
        [Tooltip("차단 표식: 차단된 적이 받는 피해가 증가합니다.")]
        public bool neuralBlockMark;

        [Tooltip("짧고 자주: 차단 지속시간이 감소하고 발동 주기도 감소합니다.")]
        public bool neuralShortAndFrequent;

        [Tooltip("길고 드물게: 차단 지속시간이 증가하고 발동 주기도 증가합니다.")]
        public bool neuralLongAndRare;

        [Tooltip("신경 과부하: 차단 종료 후 대상이 짧게 느려집니다.")]
        public bool neuralOverload;

        [Header("OrganCompression / 장기압착")]
        [Tooltip("압착 강화: 압착 범위와 피해가 증가합니다.")]
        public bool organCompressionEnhance;

        [Tooltip("압착 회수: 압착으로 다수 처치 시 즉시 압착을 재발동합니다.")]
        public bool organCompressionRecovery;

        [Tooltip("압착균열: 장기압착 쿨타임이 감소합니다.")]
        public bool organCompressionCrack;

        [Header("GastricPeristalsisWave / 위산 연동파")]
        [Tooltip("안쪽 파동 강화: 가까운 적에게 파동 피해가 증가합니다.")]
        public bool gastricInnerWaveEnhance;

        [Tooltip("연동 회수: 파동으로 처치 시 다음 파동 쿨타임이 감소합니다.")]
        public bool gastricWaveRecovery;

        [Tooltip("이중 파동: 위산 연동파 발생 후 작은 위산 연동파가 1회 추가 발생합니다.")]
        public bool gastricDoubleWave;

        [Header("MucosalFortress / 점막 요새")]
        [Tooltip("점막 반격침: 실드가 깨질 때 사방으로 침을 발사합니다.")]
        public bool mucosalCounterNeedle;

        [Tooltip("요새 집중: 실드 최대치일 때 피해가 증가하고, 피격 시 해제됩니다.")]
        public bool mucosalFortressFocus;

        [Tooltip("방어 본능: 실드가 없을 때 이동속도가 증가합니다.")]
        public bool mucosalDefenseInstinct;

        [Header("HungrySpirit / 헝그리정신")]
        [Tooltip("소화 보류: 픽업 초기화 시 스택 일부를 보존합니다.")]
        public bool hungryDigestionHold;

        [Tooltip("공복 감각: 최대 스택 도달 후 10초간 픽업 불가, 대신 공격력이 증가합니다.")]
        public bool hungryFastingSense;

        [Tooltip("공복 집중: 픽업을 안 먹은 시간이 길수록 치명타 확률이 증가합니다. 최대 30%.")]
        public bool hungryFastingFocus;

        [Header("NeedleShotgun / 샷건침")]
        [Tooltip("재장전 역류: 0발 상태에서 대쉬 시 2발 장전합니다.")]
        public bool shotgunReloadReflux;

        [Tooltip("탄창 확장: 최대 장전 수가 증가합니다.")]
        public bool shotgunMagazineExpansion;

        [Tooltip("전술 장전: 장전속도가 증가합니다.")]
        public bool shotgunTacticalReload;

        public bool HasAcquired(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return false;
            }

            return acquiredIds.Contains(id);
        }

        public bool TryAcquire(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return false;
            }

            if (acquiredIds.Contains(id))
            {
                return false;
            }

            acquiredIds.Add(id);
            return true;
        }

        public void AddLifeBurnKillForOneHitEvade(int amount)
        {
            if (!lifeBurnOneHitEvade)
            {
                return;
            }

            if (lifeBurnHasOneHitShield)
            {
                return;
            }

            lifeBurnOneHitKillCounter += Mathf.Max(0, amount);

            if (lifeBurnOneHitKillCounter >= 100)
            {
                lifeBurnOneHitKillCounter = 0;
                lifeBurnHasOneHitShield = true;
            }
        }

        public bool TryConsumeLifeBurnOneHitShield()
        {
            if (!lifeBurnOneHitEvade)
            {
                return false;
            }

            if (!lifeBurnHasOneHitShield)
            {
                return false;
            }

            lifeBurnHasOneHitShield = false;
            return true;
        }

        public void AddBloodFlowAttackSpeedStack()
        {
            if (!lifeBurnBloodFlowBlock)
            {
                return;
            }

            lifeBurnBloodFlowAttackSpeedStacks = Mathf.Clamp(
                lifeBurnBloodFlowAttackSpeedStacks + 1,
                0,
                10);
        }

        public void SetStrongHeartStacks(int stacks)
        {
            lifeBurnStrongHeartStacks = Mathf.Clamp(stacks, 0, 10);
        }

        public float GetStrongHeartDamageMultiplier()
        {
            if (!lifeBurnStrongHeart)
            {
                return 1f;
            }

            if (lifeBurnStrongHeartStacks <= 0)
            {
                return 1f;
            }

            // 0스택 1.0배, 10스택 3.0배
            return Mathf.Lerp(1f, 3f, lifeBurnStrongHeartStacks / 10f);
        }

        public static LegendaryConditionalSpecialRuntime GetOrCreate(Character playerCharacter)
        {
            if (playerCharacter == null)
            {
                return null;
            }

            LegendaryConditionalSpecialRuntime runtime =
                playerCharacter.GetComponent<LegendaryConditionalSpecialRuntime>();

            if (runtime == null)
            {
                runtime = playerCharacter.gameObject.AddComponent<LegendaryConditionalSpecialRuntime>();
            }

            return runtime;
        }
    }
}