using TMPro;
using UnityEngine;
namespace Vampire
{
    public sealed partial class ApothecaryUI
    {
        public void WkuMonsterGallery()
        {
            foreach(Transform child in content){child.gameObject.SetActive(false);Destroy(child.gameObject);}
            ImageAt(content,null,0,0,1,1,false).color=new Color(1,.95f,.84f);
            var catalog=WkuMonsterLookCatalog.Current;var rows=catalog.looks;int columns=7,count=Mathf.CeilToInt(rows.Length/(float)columns);
            Label(content,"일반 몬스터 외형 "+rows.Length+"종 · 스테이지 0 / 1 / 2",.025f,.925f,.975f,.985f,28);
            Label(content,"같은 체력·이동속도·공격력으로 다양한 외형 등장 · 특수 몬스터 없음 · 기호 배지 없음",.025f,.875f,.975f,.925f,17);
            for(int i=0;i<rows.Length;i++)
            {
                float x=.02f+(i%columns)*.138f,top=.86f-(i/columns)*(.82f/count);var look=rows[i];
                var card=Rect("Ordinary appearance "+look.id,content,x,top-.79f/count,x+.132f,top);
                StitchPatchTheme.Attach(card,StitchPatchTheme.Thread,true);
                ImageAt(card,look.frames[0],.10f,.24f,.90f,.97f);
                Label(card,"S"+look.stage+" · "+look.label,.015f,.025f,.985f,.24f,11);
            }
        }
    }
}
