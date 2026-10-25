using UnityEngine;
using UnityEngine.UI;
using TMPro;
namespace Vampire
{
    public sealed class CapsuleDoctorDialogue:MonoBehaviour
    {
        public const string SeenKey="24tu.tutorial.first-boss-capsule.v1";
        public static CapsuleDoctorDialogue Instance {get;private set;}
        public static bool Seen=>ProgressPrefs.GetInt(SeenKey,0)==1;
        public bool Finished {get;private set;}
        public int Page {get;private set;}
        public Button NextButton;public TMP_Text Message,Counter,ButtonLabel;
        public static string Greeting(string key)=>Name(key)+"! 위장의 일을 해결했구나. 정말 수고 많았다!";
        static string Name(string key)=>key=="Ari"?"아리":key=="Hyuki"?"혁이":key=="Shini"?"신이":"아시";
        public static CapsuleDoctorDialogue Show(Character player)
        {
            if(Seen)return null;
            var view=ApothecaryUI.Instance.BuildCapsuleDialogue();Instance=view;
            view.Message.text=Greeting(OctoberArt.CharacterKey(player.Blueprint));view.Counter.text="1 / 2";
            view.NextButton.onClick.AddListener(view.Advance);
            ProgressPrefs.SetInt(SeenKey,1);ProgressPrefs.Save();return view;
        }
        public void Advance(){if(Finished)return;if(Page==0){Page=1;Message.text="계속 수고해라.";Counter.text="2 / 2";ButtonLabel.text="끝";}else{Finished=true;gameObject.SetActive(false);}}
        void OnDestroy(){if(Instance==this)Instance=null;}
    }
}
