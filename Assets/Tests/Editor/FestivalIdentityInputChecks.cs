using System;
using System.Collections;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    // Exercise TMP's actual composition/keyboard/rendering path, not a replacement sanitizer.
    public sealed class FestivalCompositionInput : BaseInput
    {
        public string composition = "";
        public override string compositionString => composition;
    }

    public static class FestivalIdentityInputChecks
    {
        static string Visible(TMP_InputField field)
        {
            field.ForceLabelUpdate();
            field.textComponent.ForceMeshUpdate();
            return field.textComponent.text.TrimEnd('\u200B');
        }
        static void Cursor(TMP_InputField field,int position)
        {
            field.stringPosition=position;
            Visible(field);
        }
        static void Type(TMP_InputField field,string text)
        {
            foreach(char c in text)field.ProcessEvent(new Event{type=EventType.KeyDown,character=c});
            Visible(field);
        }
        static void Key(TMP_InputField field,KeyCode key)
        {
            field.ProcessEvent(new Event{type=EventType.KeyDown,keyCode=key});Visible(field);
        }
        public static IEnumerator Run(Action<bool,string> check)
        {
            var ui=ApothecaryUI.Instance;
            ui.Show("result");yield return null;yield return null;
            var fields=ui.GetComponentsInChildren<TMP_InputField>();
            check(fields.Length==2,"result has department and name fields");
            var module=EventSystem.current.currentInputModule;
            var previous=module.inputOverride;
            var host=new GameObject("IME regression input");
            var ime=host.AddComponent<FestivalCompositionInput>();
            string clipboard=GUIUtility.systemCopyBuffer;
            module.inputOverride=ime;
            try
            {
                foreach(var field in fields)
                {
                    string label=field.name;
                    check(!field.richText&&!field.textComponent.richText,label+" uses plain text on both components");
                    check(!field.textComponent.parseCtrlCharacters,label+" preserves literal backslashes");
                    field.text="김";Cursor(field,1);
                    // Reproduce the reported v6 bug with the previous conflicting settings.
                    field.richText=true;field.textComponent.richText=false;ime.composition="ㅈ";
                    check(Visible(field)=="김<u>ㅈ</u>",label+" reproduces the old visible /u composition tag");
                    field.richText=false;
                    foreach(string syllable in new[]{"ㅈ","주","준"})
                    {
                        ime.composition=syllable;
                        check(Visible(field)=="김"+syllable,label+" composition "+syllable+" displays without tags");
                        check(field.text=="김",label+" composition does not duplicate committed input");
                    }
                    ime.composition="";Type(field,"준한");
                    check(field.text=="김준한"&&Visible(field)=="김준한",label+" commits Hangul without dropped characters");
                    Key(field,KeyCode.Backspace);check(field.text=="김준",label+" backspace removes one character");
                    Type(field,"한");Key(field,KeyCode.LeftArrow);Type(field,"수");
                    check(field.text=="김준수한",label+" cursor insertion preserves both sides");
                    field.selectionStringAnchorPosition=1;field.selectionStringFocusPosition=3;Visible(field);Type(field,"민");
                    check(field.text=="김민한",label+" selected Hangul replacement");
                    field.text="";Cursor(field,0);
                    GUIUtility.systemCopyBuffer=@"컴퓨터공학과 / 테스트\u";
                    field.ProcessEvent(new Event{type=EventType.KeyDown,keyCode=KeyCode.V,modifiers=EventModifiers.Control});
                    check(field.text==GUIUtility.systemCopyBuffer&&Visible(field)==field.text,label+" paste preserves literal text");
                    field.text="";Cursor(field,0);Type(field,new string('가',field.characterLimit+3));
                    check(field.text.Length==field.characterLimit,label+" character limit applies without corruption");
                }
                fields.First(f=>f.name.EndsWith("학과")).text="컴퓨터공학과";
                fields.First(f=>f.name.EndsWith("이름")).text="입력검증";
                ui.Show("festivalRanking");yield return null;yield return null;
                ui.Show("result");yield return null;yield return null;
                fields=ui.GetComponentsInChildren<TMP_InputField>();
                check(fields.First(f=>f.name.EndsWith("학과")).text=="컴퓨터공학과","department survives visiting leaderboard");
                check(fields.First(f=>f.name.EndsWith("이름")).text=="입력검증","name survives visiting leaderboard");
                string path=Path.GetFullPath("Library/FestivalQA/ime-ledger-"+Guid.NewGuid().ToString("N")+".json");
                var record=new FestivalRecord{runId="ime-test",department="컴퓨터공학과",name="입력검증",score=30,seconds=45};
                check(FestivalLeaderboard.Save(record,out _,path),"Hangul record saves to isolated test ledger");
                var saved=FestivalLeaderboard.Read(path).Single();
                check(saved.department==record.department&&saved.name==record.name,"saved Hangul round trip preserves exact spelling");
            }
            finally
            {
                ime.composition="";module.inputOverride=previous;
                GUIUtility.systemCopyBuffer=clipboard;Object.Destroy(host);
            }
        }
    }
}
