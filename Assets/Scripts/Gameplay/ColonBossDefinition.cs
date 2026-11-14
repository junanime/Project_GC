using System;
using UnityEngine;

namespace Vampire
{
    public enum ColonBossKind { Flamingo, Shark, Pudding, Hedgehog, Turtle, Manatee }

    // Fixed encounter tuning: deliberately independent of the player's current damage.
    public sealed class ColonBossDefinition
    {
        public ColonBossKind Kind { get; private set; }
        public string Id { get; private set; }
        public string Name { get; private set; }
        public float Health { get; private set; }
        public float ContactDamage { get; private set; }
        public float AttackDamage { get; private set; }
        public float MoveSpeed { get; private set; }
        public float Size { get; private set; }
        public float HabitatRadius { get; private set; }
        public Color Warning { get; private set; }
        // Includes the unique dash; no inherited basic/radial barrage or second phase.
        public int PatternCount { get; private set; }
        public int GoldReward { get; private set; }
        public int ExperienceReward { get; private set; }
        public const string RequiredFinalBossName = "변비왕";
        public const bool RequiredFinalBossImplemented = true;

        ColonBossDefinition(ColonBossKind kind, string id, string name, float health, float contact,
            float attack, float speed, float size, float radius, Color warning, int patterns)
        {
            Kind=kind;Id=id;Name=name;Health=health;ContactDamage=contact;AttackDamage=attack;
            MoveSpeed=speed;Size=size;HabitatRadius=radius;Warning=warning;PatternCount=patterns;
            GoldReward=40;ExperienceReward=60;
        }

        // Larger budgets reward the long exposed windows of the slow, broad-bodied bosses.
        // Burrowing and pipe-only exposure reduce the shark/manatee budgets; shell poses
        // are visual and do not grant extra damage resistance. These values never scale
        // with the player's damage, entry route or current build.
        static readonly ColonBossDefinition[] definitions = {
            new ColonBossDefinition(ColonBossKind.Flamingo,"flamingo","도넛플라밍고",6500,12,16,.82f,2.55f,3.2f,new Color(1,.35f,.65f),3),
            new ColonBossDefinition(ColonBossKind.Shark,"shark","바게트상어",6000,14,20,1f,4.4f,3.4f,new Color(1,.68f,.18f),4),
            new ColonBossDefinition(ColonBossKind.Pudding,"pudding","푸딩바다사자",8000,12,18,.62f,4.2f,3.4f,new Color(1,.5f,.16f),5),
            new ColonBossDefinition(ColonBossKind.Hedgehog,"hedgehog","밤송이고슴도치",7000,12,18,.78f,4f,3.3f,new Color(.9f,.44f,.25f),4),
            new ColonBossDefinition(ColonBossKind.Turtle,"turtle","멜론바다거북",9000,14,18,.52f,4.3f,3.5f,new Color(.4f,.9f,.45f),4),
            new ColonBossDefinition(ColonBossKind.Manatee,"manatee","배매너티",5500,12,18,.7f,4.4f,5.8f,new Color(.27f,.8f,1),5)
        };
        public static ColonBossDefinition ForCurrentRun(ColonBossKind kind)
        {
            return Get(kind);
        }
        public static ColonBossDefinition Get(ColonBossKind kind)
        {
            int index=(int)kind;
            if(index<0||index>=definitions.Length)throw new ArgumentOutOfRangeException(nameof(kind));
            return definitions[index];
        }
    }
}
