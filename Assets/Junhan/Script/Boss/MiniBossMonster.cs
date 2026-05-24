using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 1차 미니보스.
    /// 현재는 MeleeMonster 기반의 강화 몬스터이며,
    /// 추후 패턴을 추가할 수 있도록 별도 클래스로 분리해둔다.
    /// </summary>
    public class MiniBossMonster : MeleeMonster
    {
        private MiniBossMonsterBlueprint miniBossBlueprint;
        private Vector3 miniBossOriginalLocalScale = Vector3.one;
        private bool originalScaleCached = false;

        protected override void Awake()
        {
            base.Awake();

            miniBossOriginalLocalScale = transform.localScale;
            originalScaleCached = true;
        }

        public override void Setup(
            int monsterIndex,
            Vector2 position,
            MonsterBlueprint monsterBlueprint,
            float hpBuff = 0f)
        {
            MiniBossMonsterBlueprint incomingBlueprint =
                monsterBlueprint as MiniBossMonsterBlueprint;

            if (!originalScaleCached)
            {
                miniBossOriginalLocalScale = transform.localScale;
                originalScaleCached = true;
            }

            if (incomingBlueprint != null)
            {
                float scaleMultiplier = Mathf.Max(0.05f, incomingBlueprint.visualScaleMultiplier);
                transform.localScale = miniBossOriginalLocalScale * scaleMultiplier;
            }
            else
            {
                transform.localScale = miniBossOriginalLocalScale;
            }

            base.Setup(monsterIndex, position, monsterBlueprint, hpBuff);

            miniBossBlueprint = incomingBlueprint;

            if (miniBossBlueprint == null)
            {
                Debug.LogError(
                    "[MiniBossMonster] MiniBossMonsterBlueprint가 아닌 블루프린트가 들어왔습니다. " +
                    $"현재 Blueprint: {(monsterBlueprint != null ? monsterBlueprint.name : "NULL")} / " +
                    $"Type: {(monsterBlueprint != null ? monsterBlueprint.GetType().Name : "NULL")}",
                    this
                );

                return;
            }

            if (miniBossBlueprint.debugLog)
            {
                Debug.Log(
                    $"[MiniBossMonster] 스폰 완료 | " +
                    $"Name={miniBossBlueprint.name} | " +
                    $"HP={currentHealth:0.##} | " +
                    $"ATK={miniBossBlueprint.atk:0.##} | " +
                    $"Scale={miniBossBlueprint.visualScaleMultiplier:0.##}",
                    this
                );
            }
        }
    }
}