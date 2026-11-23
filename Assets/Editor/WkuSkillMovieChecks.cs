using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
namespace Vampire.Editor
{
    public static class WkuSkillMovieChecks
    {
        public static IEnumerator Run(Action<bool,string> check,Action<string> capture)
        {
            float previous=Time.timeScale;Time.timeScale=0;
            var ui=ApothecaryUI.Instance;PreparationSkillMovie last=null;
            try
            {
                foreach(string key in new[]{"Ashi","Ari","Hyuki","Shini"})
                {
                    ui.GetComponentsInChildren<Button>().First(b=>b.name=="Festival character "+key).onClick.Invoke();yield return null;yield return null;
                    var film=ui.GetComponentInChildren<PreparationSkillMovie>();check(film!=null,"active skill movie appears for "+key);
                    var image=film.GetComponent<RawImage>();check(image.texture!=null&&image.uvRect.width<.15f,"correct film atlas frame for "+key);
                    check(Mathf.Abs(image.rectTransform.rect.width/image.rectTransform.rect.height-16f/9f)<.02f,"skill movie keeps widescreen aspect for "+key);
                    int before=film.Frame;var uv=image.uvRect;yield return new WaitForSecondsRealtime(.35f);
                    check(film.Frame!=before&&image.uvRect!=uv,"skill movie animates while menu time is paused for "+key);
                    capture("selection-"+key);last=film;
                }
                yield return new WaitForSecondsRealtime(6.3f);check(last.Loops>=1,"skill movie automatically loops");
                ui.GetComponentsInChildren<Button>().First(b=>b.name=="Festival character Ashi").onClick.Invoke();yield return null;yield return null;
            }
            finally{Time.timeScale=previous;}
        }
    }
}
