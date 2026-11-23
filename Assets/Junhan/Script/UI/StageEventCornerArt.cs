using UnityEngine;

namespace Vampire
{
    public enum StageEventVisualKind { Infection, Gold, AcidRain, Reflux, Drift, Coffee, Antacid }

    // References the gameplay sprites directly: no duplicate/repainted antacid art.
    public sealed class StageEventCornerArt : ScriptableObject
    {
        public Sprite[] infection, gold, acidRain, reflux, drift, coffee, antacid;
        public Sprite antacidFoam;
        static Sprite[] sodaCorners;
        public Sprite[] Frames(StageEventVisualKind kind)
        {
            switch (kind)
            {
                case StageEventVisualKind.Infection: return infection;
                case StageEventVisualKind.Gold: return gold;
                case StageEventVisualKind.AcidRain: return acidRain;
                case StageEventVisualKind.Reflux: return sodaCorners??(sodaCorners=new[]{ChameleonArt.Fx(4)});
                case StageEventVisualKind.Drift: return drift;
                case StageEventVisualKind.Coffee: return coffee;
                default: return antacid;
            }
        }
        public static Color Accent(StageEventVisualKind kind)
        {
            switch (kind)
            {
                case StageEventVisualKind.Infection: return new Color(.82f,.58f,1f);
                case StageEventVisualKind.Gold: return new Color(1f,.79f,.24f);
                case StageEventVisualKind.AcidRain: return new Color(.7f,.96f,.22f);
                case StageEventVisualKind.Reflux: return new Color(1f,.55f,.12f);
                case StageEventVisualKind.Drift: return new Color(.52f,.92f,.87f);
                case StageEventVisualKind.Coffee: return new Color(.85f,.60f,.36f);
                default: return new Color(.79f,.82f,1f);
            }
        }
        public static string Instruction(StageEventVisualKind kind)
        {
            switch (kind)
            {
                case StageEventVisualKind.Infection: return "몰려오는 적 사이로 대시해 길을 여세요.";
                case StageEventVisualKind.Gold: return "적을 처치하고 떨어진 골드를 모으세요.";
                case StageEventVisualKind.AcidRain: return "바닥의 위산 웅덩이 밖으로 이동하세요.";
                case StageEventVisualKind.Reflux: return "톡톡의 볼이 부풀면 파도 옆으로 피하세요.";
                case StageEventVisualKind.Drift: return "꾸룩이 박수치면 바뀌는 방향을 살피세요.";
                case StageEventVisualKind.Coffee: return "빨라진 적과 거리를 벌리세요.";
                default: return "거품 안에 들어가 피해를 피하세요.";
            }
        }
    }

    public readonly struct StageEventPresentation
    {
        public readonly object key;
        public readonly StageEventVisualKind kind;
        public readonly string title;
        public readonly float direction;
        public StageEventPresentation(object key, StageEventVisualKind kind, string title, float direction = 1f)
        { this.key = key; this.kind = kind; this.title = title; this.direction = direction; }
    }
}
