using System;
using System.Collections;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vampire.Editor
{
    public static class OctoberUiCatalogChecks
    {
        const string Output="Library/OctoberQa";
        static int failures;
        static void Audit(string context)
        {
            Canvas.ForceUpdateCanvases();
            foreach(var label in ApothecaryUI.Instance.GetComponentsInChildren<TMP_Text>())
            {
                if(string.IsNullOrWhiteSpace(label.text)||label.GetComponentInParent<ScrollRect>()!=null)continue;
                label.ForceMeshUpdate();
                if(!label.isTextOverflowing)continue;
                failures++;
                File.AppendAllText(Output+"/catalog-text.tsv",context+"\t"+label.name+"\t"+label.text.Replace('\n',' ')+"\n");
            }
        }
        public static IEnumerator Run(LevelManager level)
        {
            failures=0;File.WriteAllText(Output+"/catalog-text.tsv","");
            var ui=ApothecaryUI.Instance;ui.CloseRunBook();
            foreach(var item in ui.Config.items.Where(i=>i!=null))OctoberItemRuntime.Get(level.PlayerCharacter).Give(item);
            foreach(var relic in ui.Config.relics.Where(r=>r!=null))level.PlayerCharacter.Relics.AddReward(relic);
            ui.OpenRunBook();yield return null;yield return null;
            int visited=0;
            for(int section=0;section<3;section++)
            {
                for(int page=0;page<20;page++)
                {
                    for(int slot=0;slot<(section==0?6:10);slot++)
                    {
                        var button=ui.GetComponentsInChildren<Button>().FirstOrDefault(b=>b.name==$"Record {section} slot {slot}"&&b.interactable);
                        if(button==null)continue;
                        button.onClick.Invoke();yield return null;yield return null;
                        Audit($"section={section},page={page},slot={slot}");visited++;
                        var scroll=ui.GetComponentsInChildren<ScrollRect>().FirstOrDefault(s=>s.name=="Record description scroll "+section);
                        if(scroll!=null)
                        {scroll.verticalNormalizedPosition=0;yield return null;scroll.verticalNormalizedPosition=1;}
                    }
                    var next=ui.GetComponentsInChildren<Button>().FirstOrDefault(b=>b.name==$"Record {section} next"&&b.interactable);
                    if(next==null)break;
                    next.onClick.Invoke();yield return null;yield return null;
                }
            }
            File.AppendAllText(Output+"/checks.tsv",(failures==0?"PASS":"FAIL")+"\tTAB catalog descriptions and titles: "+visited+" selections, "+failures+" text overflows\n");
            var image=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Output+"/catalog-tab-final.png",image.EncodeToPNG());UnityEngine.Object.Destroy(image);
            if(failures>0)Debug.LogError("TAB catalog contains "+failures+" text overflows; see catalog-text.tsv");
            ui.CloseRunBook();
        }
    }
}
