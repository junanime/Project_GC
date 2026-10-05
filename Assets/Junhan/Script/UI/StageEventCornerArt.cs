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
                case StageEventVisualKind.Reflux: return sodaCorners??(sodaCorners=new[]{AcidToadArt.Frame("AcidFx",0),AcidToadArt.Frame("AcidFx",1),AcidToadArt.Frame("AcidFx",2),AcidToadArt.Frame("AcidFx",3)});
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
                case StageEventVisualKind.Infection: return "몰려오는 적을 돌파하세요";
                case StageEventVisualKind.Gold: return "적을 처치하고 골드를 모으세요";
                case StageEventVisualKind.AcidRain: return "위산 장판을 피하세요";
                case StageEventVisualKind.Reflux: return "환타 두꺼비의 볼이 부풀면 탄산액 발사 경로를 피하세요";
                case StageEventVisualKind.Drift: return "좌우로 바뀌는 기류에 주의하세요";
                case StageEventVisualKind.Coffee: return "빨라진 몬스터를 조심하세요";
                default: return "거품 안으로 이동하세요";
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
