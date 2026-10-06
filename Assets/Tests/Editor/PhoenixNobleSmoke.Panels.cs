using System.Collections;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    public static partial class PhoenixNobleSmoke
    {
        static object Field(object target, string name) => target.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(target);
        static void PanelCapture(string name)
        {
            typeof(Ver4PlaySmoke).GetMethod("CaptureView",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{"phoenix-"+name+".png"});
        }
        static IEnumerator TestPanels()
        {
            var level = Object.FindObjectOfType<LevelManager>();
            var dialog = level.EntityManager.AbilitySelectionDialog;
            var manager = Object.FindObjectOfType<AbilityManager>();
            var abilities = manager.GetComponentsInChildren<SyringeLegendaryAugmentAbility>(true).Where(a=>a.AvailableAsNoble).ToArray();
            Check(abilities.Select(a=>a.Type).Distinct().Count()==10,"ten noble abilities available; scattered feathers withheld");
            var config = Resources.Load<ApothecaryUIConfig>("ApothecaryUIConfig");
            foreach(var ability in abilities)
            {
                var icon = PhoenixNobleArt.Get(ability.Type.ToString());
                Check(icon != null && ability.Image == icon,"runtime icon "+ability.Type);
                Check(config.augments.Any(a=>a.title==ability.Name && a.icon==icon),"catalog icon "+ability.Type);
            }
            if(dialog.MenuOpen) dialog.Close();
            level.SetRunFlowPaused(true);
            dialog.Open(false);
            yield return new WaitForSecondsRealtime(.7f);
            var cards = dialog.GetComponentsInChildren<AbilityCard>();
            Check(cards.Length==3,"three live reward cards");
            for(int start=0;start<abilities.Length;start+=3)
            {
                typeof(AbilitySelectionDialog).GetMethod("ClearScrollReveal",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(dialog,null);
                for(int i=0;i<cards.Length;i++)cards[i].Init(dialog,abilities[(start+i)%abilities.Length],0);
                yield return new WaitForSecondsRealtime(.6f);
                Canvas.ForceUpdateCanvases();
                foreach(var card in cards)
                {
                    Check(card.UsesNoblePanel && ((Image)Field(card,"cardBackgroundImage")).sprite==PhoenixNobleArt.Get("Scroll"),"shared hanji frame");
                    var icon=(Image)Field(card,"abilityImage");
                    Check(icon.enabled && icon.sprite!=null && icon.rectTransform.rect.width>100,"separate ink illustration visible");
                    foreach(string label in new[]{"nameText","descriptionText"})
                    {
                        var text=(TextMeshProUGUI)Field(card,label);text.ForceMeshUpdate();
                        Check(text.textBounds.size.y<=text.rectTransform.rect.height+2 && text.textBounds.size.x<=text.rectTransform.rect.width+2,"text fits "+text.text);
                    }
                    Check(card.transform.Find("Phoenix heading").gameObject.activeSelf,"shared heading visible");
                }
                PanelCapture("panels-"+start);
            }
            typeof(AbilitySelectionDialog).GetMethod("PrepareScrollReveal",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(dialog,null);
            Check(dialog.ScrollRevealPending && !dialog.CanSelectRevealedCard,"sealed cards cannot be selected");
            Check(cards.All(c=>!c.transform.Find("Noble scroll seal/Rolling jade rod").gameObject.activeSelf),"closed covers have no duplicate moving rods");
            PanelCapture("sealed");
            cards[0].Selected();
            yield return new WaitForSecondsRealtime(.4f);
            Check(dialog.ScrollRevealPending && dialog.MenuOpen,"first click only rolls covers, does not choose");
            PanelCapture("rolling");
            yield return new WaitForSecondsRealtime(.6f);
            Check(!dialog.ScrollRevealPending && dialog.CanSelectRevealedCard && dialog.MenuOpen,"all covers finish rolling before selection becomes available");
            Check(cards.All(c=>!c.transform.Find("Noble scroll seal/Rolling jade rod").gameObject.activeSelf),"revealed frames have no duplicate moving rods");
            PanelCapture("revealed");
            var regular=manager.GetComponentsInChildren<SyringeSpecialAugmentAbility>(true).First();
            cards[0].Init(dialog,regular,0);yield return new WaitForSecondsRealtime(.5f);
            Check(!cards[0].UsesNoblePanel && !cards[0].transform.Find("Phoenix heading").gameObject.activeSelf,"pooled card returns to regular theme");
            dialog.Close();level.SetRunFlowPaused(false);Time.timeScale=1;
        }
    }
}
