using System.Collections;
using UnityEngine;
namespace Vampire
{
    public static class BossEscapePresentation
    {
        public const float Duration=2.6f;
        public static IEnumerator Play(LevelManager level,FoodBossKind kind,Transform visual,Vector3 position)
        {
            var progression=StageProgression.Ensure(level);var hole=progression.BeginEscape(kind,position);
            var holeSprite=hole.GetComponent<SpriteRenderer>();float size=4.8f/holeSprite.sprite.bounds.size.x;
            Vector3 original=visual.localPosition,scale=visual.localScale;
            var renderers=visual.GetComponentsInChildren<SpriteRenderer>();
            var colors=new Color[renderers.Length];for(int i=0;i<colors.Length;i++)colors[i]=renderers[i].color;
            // Duck / brace, open a cavity, then dive. Preserve each boss's original proportions.
            for(float t=0;t<Duration;t+=Time.deltaTime)
            {
                float open=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.3f,1.15f,t));hole.localScale=Vector3.one*size*Mathf.Max(.001f,open);
                float dive=Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.1f,2.55f,t));
                float brace=Mathf.Sin(Mathf.Clamp01(t/.8f)*Mathf.PI);
                float hop=kind==FoodBossKind.Maribi||kind==FoodBossKind.Hurumi?Mathf.Sin(Mathf.Clamp01((t-.5f)/1.1f)*Mathf.PI)*.55f:0;
                visual.localPosition=original+new Vector3(Mathf.Sin(t*28)*.025f*(1-dive),hop-dive*.35f,0);
                visual.localScale=Vector3.Scale(scale,new Vector3(1+brace*.035f,1-brace*.07f,1))*(1-dive*.94f);
                visual.localRotation=Quaternion.Euler(0,0,(kind==FoodBossKind.Rolpangi?22:kind==FoodBossKind.Denikani?-10:12)*dive);
                float alpha=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.78f,1,dive));
                for(int i=0;i<renderers.Length;i++)if(renderers[i]!=null){var c=colors[i];c.a*=alpha;renderers[i].color=c;}
                yield return null;
            }
            hole.localScale=Vector3.one*size;visual.gameObject.SetActive(false);
        }
    }
}
