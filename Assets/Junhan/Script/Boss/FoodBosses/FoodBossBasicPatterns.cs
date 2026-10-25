using System.Collections;
using UnityEngine;

namespace Vampire
{
    public sealed partial class FoodBossRuntime
    {
        // Preserve projectile art, size and authored base speeds; the bullet factory applies 1.4x. Denser volleys
        // deal 75% of the old basic hit; unique skills and radial attacks keep their damage.
        const float BasicDamage=6;

        FoodBossBullet BasicShot(Vector2 origin,Vector2 direction,bool sound=true)
        {
            Visual.Recoil=-.08f;
            if(sound)Audio.Play(FoodBossSound.Shot,.8f);
            return FoodBossBullet.Fire(this,origin,direction,BasicSpeed,BasicDamage);
        }

        IEnumerator BasicBeat(float duration)
        {
            yield return Wait(Mathf.Min(.08f,duration));
            Visual.Recoil=0;
            if(duration>.08f)yield return Wait(duration-.08f);
        }

        IEnumerator SpiralBasic()
        {
            for(int pass=0;pass<(PhaseTwo?2:1);pass++)
            {
                // Only the emitter rotates. Fired berries never home or change direction.
                Visual.Face(Aim);Vector2 origin=Mouth,aim=FireAim;
                for(int i=0;i<8;i++)
                {
                    float angle=Mathf.Lerp(70,-70,i/7f)*(pass==0?1:-1);
                    BasicShot(origin,Rotate(aim,angle));
                    yield return BasicBeat(i<7?.18f:.08f);
                }
                if(pass==0&&PhaseTwo)yield return Wait(.95f);
            }
        }

        IEnumerator ClawBasic()
        {
            Visual.Face(Aim);Vector2 direction=FireAim,side=Rotate(direction,90);
            Vector2 center=Vector2.up*.75f+direction*.85f;
            Visual.LeftClaw=Visual.RestClaw(0);Visual.RightClaw=Visual.RestClaw(1);Visual.ClawsActive=true;
            yield return PoseClaws(center-side*.9f,center+side*.9f,.32f);
            for(int pass=0;pass<(PhaseTwo?2:1);pass++)
            {
                for(int i=0;i<6;i++)
                {
                    int claw=pass==0?i%2:1-i%2;
                    Vector2 origin=Visual.ClawShotOrigin(claw);
                    // Shoot from the actual articulated pincer towards the opposite lane.
                    Vector2 target=(Vector2)transform.position+center+direction*4+side*(claw==0?.9f:-.9f);
                    BasicShot(origin,(target-origin).normalized);
                    yield return BasicBeat(i<5?.20f:.08f);
                }
                if(pass==0&&PhaseTwo)yield return Wait(.95f);
            }
            yield return PoseClaws(Visual.RestClaw(0),Visual.RestClaw(1),.28f);
        }

        IEnumerator RhythmBasic()
        {
            for(int burst=0;burst<(PhaseTwo?3:2);burst++)
            {
                // Reacquire only between bursts, not once per seed.
                Visual.Face(Aim);Vector2 origin=Mouth,direction=FireAim;
                for(int i=0;i<3;i++)
                {
                    BasicShot(origin,direction);
                    yield return BasicBeat(i<2?.20f:.08f);
                }
                if(burst<(PhaseTwo?2:1))yield return Wait(.85f);
            }
        }

        IEnumerator SkewerBasic()
        {
            Vector2 aim=FireAim;
            for(int bundle=0;bundle<(PhaseTwo?3:2);bundle++)
            {
                // Keep a visible gap between bunches; all grapes in a bunch share a heading.
                float offset=PhaseTwo?(bundle==0?-10:bundle==1?10:0):0;
                Vector2 direction=Rotate(aim,offset),origin=Mouth;
                for(int i=0;i<3;i++)
                {
                    BasicShot(origin,direction);
                    yield return BasicBeat(i<2?.24f:.08f);
                }
                if(bundle<(PhaseTwo?2:1))yield return Wait(.95f);
            }
        }

        IEnumerator WaveBasic()
        {
            Vector2 origin=Mouth,direction=FireAim,side=Rotate(direction,90);
            for(int pass=0;pass<(PhaseTwo?2:1);pass++)
            {
                for(int i=0;i<4;i++)
                {
                    // Both lanes have the same phase. The central corridor never pinches shut.
                    for(int lane=-1;lane<=1;lane+=2)
                    {
                        var bullet=BasicShot(origin+side*(lane*.95f),direction,lane==-1);
                        bullet.SetWave(pass==0?.32f:-.32f,3.8f);
                    }
                    yield return BasicBeat(i<3?.32f:.08f);
                }
                if(pass==0&&PhaseTwo)yield return Wait(1.1f);
            }
        }
    }
}
