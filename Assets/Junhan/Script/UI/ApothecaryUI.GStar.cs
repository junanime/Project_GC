using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    public sealed partial class ApothecaryUI
    {
        const float Gw=1672, Gh=941;
        static readonly Color GInk=new Color(.025f,.035f,.065f);
        static readonly Color GMint=new Color(.05f,.80f,.64f);
        static readonly Color GGold=new Color(1,.81f,.09f);
        static readonly Dictionary<string,Sprite> gstarArt=new Dictionary<string,Sprite>();
        TMP_InputField gstarName, gstarPhone;
        string gstarDraftName, gstarDraftPhone;
        int gstarBoard=-1;
        bool gstarConsent, gstarOperator;
        string gstarNotice="";
        string gstarOperatorNotice="";
        bool IsGStarPage => GStarDemo.Enabled && (Page=="result" || Page=="gstarNext" || Page=="gstarSurvey");
        static string GStage(int stage) => stage==0?"구강":stage==2?"대장":"위장";

        string GStarInitialPage(bool prepare)
        {
            if(GStarSession.VisitorFinished) return "gstarSurvey";
            if(GStarSession.RegistrationRequested)
            {
                var ended=GStarLeaderboard.GetVisitorRuns(GStarSession.SessionId).LastOrDefault();
                passed=ended?.data.success??GStarSession.LastSuccess;gstarBoard=ended?.stageId??GStarSession.StageId;
                return "result";
            }
            if(prepare) return "prepare";
            if(GStarSession.HasCompletedAttempt) return "gstarNext";
            return "main";
        }
        bool UpdateGStar()
        {
            if(!IsGStarPage)return false;
            if(Page=="gstarSurvey" && GameInput.GetKeyDown(KeyCode.F9))
            {gstarOperator=!gstarOperator;Render();}
            // The public end screen cannot reset the visitor by Escape or gamepad back.
            return true;
        }
        static Sprite GArt(string key)
        {
            if(gstarArt.TryGetValue(key,out var found)&&found!=null)return found;
            var texture=Resources.Load<Texture2D>("GStarDemo/"+key);
            if(texture==null)return null;
            var sprite=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f),100);
            sprite.name="GStar "+key;gstarArt[key]=sprite;return sprite;
        }
        static Sprite GSlice(string key,float x,float y,float w,float h,Vector4 border=default)
        {
            string id=key+"/"+x+"/"+y+"/"+w+"/"+h;
            if(gstarArt.TryGetValue(id,out var found)&&found!=null)return found;
            var source=GArt(key);if(source==null)return null;
            float sx=source.texture.width/Gw,sy=source.texture.height/Gh;
            var s=Sprite.Create(source.texture,new Rect(x*sx,(Gh-y-h)*sy,w*sx,h*sy),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,border*sx);
            s.name=id;gstarArt[id]=s;return s;
        }
        RectTransform GRect(string name,Transform parent,float x,float y,float w,float h) => Rect(name,parent,x/Gw,1-(y+h)/Gh,(x+w)/Gw,1-y/Gh);
        Image GImage(Sprite sprite,float x,float y,float w,float h)
        {
            var i=GRect("GStar art",content,x,y,w,h).gameObject.AddComponent<Image>();i.sprite=sprite;i.raycastTarget=false;return i;
        }
        void GQr(string key,float x,float y,float side)
        {
            var sprite=Resources.Load<Sprite>("GStarDemo/"+key);if(sprite==null)return;
            var image=GImage(sprite,x,y,side,side);
            image.name="GStar "+key;image.preserveAspect=true;
        }
        TextMeshProUGUI GText(string value,float x,float y,float w,float h,float size,Color? color=null,bool heading=false)
        {
            var t=Label(content,value,x/Gw,1-(y+h)/Gh,(x+w)/Gw,1-y/Gh,size*720/Gh);
            t.name="GStar text "+value;t.color=color??GInk;t.enableWordWrapping=false;t.fontStyle=FontStyles.Bold;
            t.overflowMode=TextOverflowModes.Ellipsis;
            if(heading)
            {
                // Explicit back glyphs also work with the project's mobile TMP shader.
                float stroke=size>=65?3.4f:2.2f;
                for(int i=0;i<8;i++)
                {
                    float a=i*Mathf.PI/4,dx=Mathf.Cos(a)*stroke,dy=Mathf.Sin(a)*stroke+1.2f;
                    var outline=Label(content,"<color=#080B11>"+value+"</color>",(x+dx)/Gw,1-(y+dy+h)/Gh,(x+dx+w)/Gw,1-(y+dy)/Gh,size*720/Gh);
                    outline.name="GStar heading outline";outline.fontStyle=FontStyles.Bold;outline.enableWordWrapping=false;
                    outline.transform.SetSiblingIndex(t.transform.GetSiblingIndex());
                }
                t.enableVertexGradient=true;t.color=Color.white;
                var c=color??GGold;t.colorGradient=new VertexGradient(Color.Lerp(c,Color.white,.8f),Color.Lerp(c,Color.white,.8f),c,c);
            }
            return t;
        }
        Button GButton(string name,string caption,float x,float y,float w,float h,Action action,bool primary=false,bool interactable=true,bool painted=true)
        {
            var rect=GRect("GStar "+name,content,x,y,w,h);
            // These controls already have their complete frames in the approved plate.
            // Reuse the exact pixels instead of putting a second, differently shaped pill over them.
            bool plateSkin=painted && ((Page=="result" && (name=="register" || name=="result next")) ||
                (Page=="gstarNext" && (name.StartsWith("challenge ",StringComparison.Ordinal) || name=="change character" || name=="finish experience")));
            Graphic image;
            if(plateSkin)
            {
                var hit=rect.gameObject.AddComponent<Image>();hit.color=Color.clear;
                // Tint only the rounded button, never the rectangular paper corners of a crop.
                var maskRect=Rect("Plate skin mask",rect,0,0,1,1);
                var maskShape=maskRect.gameObject.AddComponent<GStarPillGraphic>();maskShape.raycastTarget=false;
                var mask=maskRect.gameObject.AddComponent<Mask>();mask.showMaskGraphic=false;
                var skin=Rect("Plate button skin",maskRect,0,0,1,1).gameObject.AddComponent<Image>();
                skin.sprite=GSlice(Page=="result"?(passed?"clear":"failed"):"next",x,y,w,h);
                skin.color=Color.white;skin.raycastTarget=false;image=skin;
            }
            else
            {
                var pill=rect.gameObject.AddComponent<GStarPillGraphic>();
                pill.face=primary?new Color(1,.19f,.38f):new Color(.68f,1,.81f);
                pill.color=painted?Color.white:Color.clear;image=pill;
            }
            var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;button.interactable=interactable;
            var c=button.colors;c.highlightedColor=new Color(1,.92f,.71f);c.pressedColor=new Color(.73f,.86f,.91f);c.disabledColor=new Color(.55f,.55f,.55f);button.colors=c;
            float captionSize=Mathf.Min(43,h*.56f, w/Mathf.Max(1,caption.Length)*.91f);
            var text=Label(rect,caption,.035f,.08f,.965f,.92f,captionSize*720/Gh);
            text.name="Caption";text.color=primary?Color.white:GInk;text.fontStyle=FontStyles.Bold;
            if(primary){text.outlineColor=new Color(.65f,.07f,.23f);text.outlineWidth=.13f;}
            button.onClick.AddListener(()=>{if(!button.interactable)return;GameAudioManager.PlaySfx(GameAudioManager.GameSfxId.UiClick);action?.Invoke();});
            return button;
        }
        void GHeader(int active)
        {
            if(Page!="result")
            {
                var logo=GRect("GStar title badge",content,674,25,324,56).gameObject.AddComponent<GStarPillGraphic>();
                logo.face=new Color(1,.96f,.87f);logo.gloss=false;logo.raycastTarget=false;
            }
            GText("지스타 체험판",675,28,322,52,40);
            string[] titles={"01 다음 도전","02 기록 · 랭킹","03 체험 종료"};
            if(Page!="gstarSurvey")
            {
                // The approved plates have the old active step baked in. Replace the
                // whole arrow strip once, using the same artwork with the correct step.
                string bar=Page=="gstarNext"?"clear":"next";
                var strip=GRect("GStar progress strip",content,346,137,979,66);
                var shape=strip.gameObject.AddComponent<GStarPillGraphic>();shape.raycastTarget=false;
                var mask=strip.gameObject.AddComponent<Mask>();mask.showMaskGraphic=false;
                var art=Rect("Approved step arrows",strip,0,0,1,1).gameObject.AddComponent<Image>();
                art.sprite=GSlice(bar,346,137,979,66);art.raycastTarget=false;
            }
            float[] x={356,700,1014};float[] widths={328,303,303};
            for(int i=0;i<3;i++)GText(titles[i],x[i],145,widths[i],49,34,i==active?Color.white:GInk);
        }
        void BuildGStarPage()
        {
            string plate=Page=="result"?(passed?"clear":"failed"):Page=="gstarNext"?"next":"exit";
            ImageAt(content,GArt(plate),0,0,1,1,false);
            if(Page=="result")GStarResult();
            else if(Page=="gstarNext")GStarNext();
            else GStarSurvey();
        }
        TMP_InputField GInput(string key,string value,string hint,float x,float y,float w,float h,bool phone)
        {
            var rect=GRect("GStar input "+key,content,x,y,w,h);
            var bg=rect.gameObject.AddComponent<Image>();bg.color=Color.clear;
            var field=rect.gameObject.AddComponent<TMP_InputField>();field.targetGraphic=bg;
            var viewport=Rect("Viewport",rect,.035f,.1f,.965f,.9f);viewport.gameObject.AddComponent<RectMask2D>();
            var text=Label(viewport,"",0,0,1,1,29*720/Gh);text.alignment=TextAlignmentOptions.MidlineLeft;
            var placeholder=Label(viewport,hint,0,0,1,1,(phone?21:26)*720/Gh);placeholder.color=new Color(.63f,.62f,.64f);placeholder.alignment=TextAlignmentOptions.MidlineLeft;
            placeholder.fontSizeMin=15*720/Gh;placeholder.enableWordWrapping=false;
            field.textViewport=viewport;field.textComponent=text;field.placeholder=placeholder;
            // TMP_InputField itself inserts formatting around IME pre-edit when rich text is on.
            field.richText=false;
            field.contentType=TMP_InputField.ContentType.Standard;field.lineType=TMP_InputField.LineType.SingleLine;
            field.characterLimit=phone?15:12;field.keyboardType=phone?TouchScreenKeyboardType.PhonePad:TouchScreenKeyboardType.Default;
            field.customCaretColor=true;field.caretColor=new Color(.95f,.08f,.28f);field.caretWidth=3;field.caretBlinkRate=.85f;
            field.selectionColor=new Color(.38f,.85f,.77f,.40f);
            var focus=Rect("Input focus outline",rect,0,0,1,1).gameObject.AddComponent<GStarInputFocus>();
            focus.input=field;focus.hint=placeholder;focus.raycastTarget=false;
            field.text=value??"";field.onValueChanged.AddListener(v=>{if(phone)gstarDraftPhone=v;else gstarDraftName=v;});
            return field;
        }
        void GConsent()
        {
            // A real square checkbox with a full-row hit area: never a tiny orphan pill.
            var row=GRect("GStar consent",content,155,768,585,26);
            var target=row.gameObject.AddComponent<Image>();target.color=Color.clear;
            var button=row.gameObject.AddComponent<Button>();button.targetGraphic=target;
            button.transition=Selectable.Transition.None;
            var box=Rect("GStar consent box",row,0,.04f,24f/585f,.96f).gameObject.AddComponent<Image>();
            box.color=GInk;box.raycastTarget=false;
            var fill=Rect("Checkbox fill",box.transform,.10f,.10f,.90f,.90f).gameObject.AddComponent<Image>();
            fill.color=gstarConsent?new Color(.68f,1,.81f):Color.white;fill.raycastTarget=false;
            if(gstarConsent)
            {
                // Geometry avoids a missing check glyph in the Korean display font.
                var a=Rect("Check short",fill.transform,.12f,.34f,.49f,.48f).gameObject.AddComponent<Image>();
                a.color=GInk;a.raycastTarget=false;a.rectTransform.localRotation=Quaternion.Euler(0,0,-42);
                var b=Rect("Check long",fill.transform,.34f,.45f,.94f,.59f).gameObject.AddComponent<Image>();
                b.color=GInk;b.raycastTarget=false;b.rectTransform.localRotation=Quaternion.Euler(0,0,42);
            }
            var label=Label(row,"연락처 입력 시 경품 안내용 저장에 동의합니다",36f/585f,0,1,1,18*720/Gh);
            label.name="GStar consent label";label.alignment=TextAlignmentOptions.MidlineLeft;label.fontStyle=FontStyles.Bold;
            button.onClick.AddListener(()=>{gstarConsent=!gstarConsent;GameAudioManager.PlaySfx(GameAudioManager.GameSfxId.UiClick);Render();});
        }
        void DrawGStarResultPortrait()
        {
            // Sample the matching paper above the old baked mascot. The actor itself
            // is the expressive, transparent sprite from the title screen's atlas.
            string skin = passed ? "clear" : "failed";
            GImage(GSlice(skin, 99, 224, 220, 60), 98, 286, 307, 273).name = "GStar result portrait paper";
            var actor = GImage(OctoberArt.Get("OctoberUI/AshiStates", passed ? "2" : "1"), 106, 290, 292, 265);
            actor.name = "GStar result portrait";
            actor.preserveAspect = true;
        }

        void GStarResult()
        {
            EnsureLastGStarScore();
            var runs=GStarLeaderboard.GetVisitorRuns(GStarSession.SessionId);
            var run=runs.LastOrDefault();
            int stage=run?.stageId??GStarSession.StageId;
            if(gstarBoard<0)gstarBoard=stage;
            bool eligible=runs.Any(r=>r.stageBest);
            bool registered=eligible&&runs.Where(r=>r.stageBest).All(r=>r.registered);
            GHeader(1);
            DrawGStarResultPortrait();
            string title=runs.Count>1?"두 판의 도전 기록":run==null?"이번 체험 기록":passed?"스테이지 클리어!":"스테이지 클리어 실패";
            DrawGStarResultHeadings(title);
            DrawGStarAttempts(runs);
            GText("이름",138,590,168,61,35);GText("전화번호",138,669,168,61,32);
            gstarName=GInput("name",gstarDraftName??GStarSession.PlayerName,"별명으로 하셔도 괜찮아요",325,590,415,63,false);
            string contact=gstarDraftPhone??GStarSession.Phone;
            gstarPhone=GInput("phone",registered&&!string.IsNullOrEmpty(contact)?"연락처 저장 완료":contact,
                registered?"미입력":"필수로 입력하지 않으셔도 됩니다!",325,669,415,62,true);
            gstarName.interactable=gstarPhone.interactable=eligible&&!registered;
            string note=registered?"등록 완료 · 연락처는 순위표에 표시되지 않아요":gstarNotice;
            if(!string.IsNullOrEmpty(GStarLeaderboard.LastError))note=GStarLeaderboard.LastError;
            if(string.IsNullOrEmpty(note) && GStarSession.Interrupted && runs.Count<GStarSession.UsedAttempts)
                note="중단된 도전을 제외한 완료 기록만 등록합니다.";
            if(string.IsNullOrEmpty(note))note=eligible?"전화번호는 경품 안내에만 사용됩니다.":"등록할 기록이 없어요. 설문으로 이동할 수 있어요.";
            // Replace the baked orange flourishes under the two live contact lines with
            // matching paper from the same plate; the checkbox/text must stay unobstructed.
            GImage(GSlice(passed?"clear":"failed",755,737,20,61),142,737,601,61).name="GStar contact note backing";
            GText(note,147,741,596,29,21,string.IsNullOrEmpty(run?.exclusion)?GInk:new Color(.55f,.18f,.50f));
            if(eligible&&!registered) GConsent();
            GButton("register",registered?"등록 완료":"등록하기",132,800,624,100,RegisterGStarScore,true,!registered&&eligible);
            for(int i=0;i<3;i++)
            {
                int stageTab=i;
                bool selected=stageTab==gstarBoard;
                var tab=GButton("ranking stage "+i,GStage(i),829+i*237,383,217,61,()=>{gstarBoard=stageTab;Render();},selected);
                if(!selected)tab.GetComponent<GStarPillGraphic>().face=new Color(1,.94f,.83f);
            }
            GText(GStage(gstarBoard)+" TOP 3",979,451,407,48,37);
            var mine=GStarLeaderboard.GetParticipantRank(GStarSession.Phone,GStarSession.SessionId,gstarBoard);
            if(mine==null)GText(registered?"이 스테이지의 등록 기록이 없어요":"등록하면 내 전체 순위가 여기에 표시돼요",835,516,688,51,29);
            else
            {
                GText($"내 순위  {mine.rank:N0}위",832,514,285,51,32,new Color(0,.25f,.23f));
                var name=GText(mine.name,1115,514,195,51,29);name.richText=false;name.overflowMode=TextOverflowModes.Ellipsis;
                GText($"{mine.score:N0}점",1310,514,210,51,31).fontSizeMin=16*720/Gh;
            }
            var rows=GStarLeaderboard.GetTop(gstarBoard,3);
            for(int i=0;i<3;i++)
            {
                float y=594+i*84;var row=i<rows.Count?rows[i]:null;
                GText((i+1).ToString(),851,y,62,57,39,Color.white,true);
                var n=GText(row?.name??"첫 도전을 기다려요",954,y,269,57,31);n.richText=false;n.overflowMode=TextOverflowModes.Ellipsis;
                GText(row!=null?$"{row.score:N0}점":"—",1227,y,195,57,31).fontSizeMin=16*720/Gh;
            }
            GButton("result next",registered?"설문으로":"등록 없이 설문으로",1383,834,181,73,EndGStarVisitor);
            if(run!=null)GButton("score details","점수 내역",817,850,235,46,()=>ShowGStarScoreDetails(runs),false,true,false);
        }
        void DrawGStarResultHeadings(string resultTitle)
        {
            string skin=passed?"clear":"failed";
            // Both original illustrations have a fixed pink middle tab and a narrow prize plaque.
            // Reuse each illustration's paper so only the three live controls carry tab borders.
            var paper=GSlice(skin,812,208,315,77);
            GImage(paper,378,204,146,92).name="GStar result heading paper";
            GImage(paper,797,204,756,245).name="GStar ranking paper";

            var leftAccent=passed?GSlice(skin,458,205,62,90):GSlice(skin,386,216,73,71);
            var rightAccent=passed?GSlice(skin,1151,205,62,90):GSlice(skin,1212,214,75,78);
            GImage(leftAccent,111,231,47,60).preserveAspect=true;
            GImage(rightAccent,699,231,47,60).preserveAspect=true;
            GText(resultTitle,157,223,540,79,58,passed?GGold:new Color(.68f,.34f,1),true).name="GStar result heading";

            GText("랭킹에 등록하면",902,205,547,43,35,GInk).name="GStar prize draw lead";
            GImage(GSlice(skin,804,294,738,85),804,250,738,92).name="GStar prize draw banner";
            var gift=GImage(Resources.Load<Sprite>("GStarDemo/prize-draw-gift"),918,262,76,74);
            gift.name="GStar prize draw gift";
            gift.preserveAspect=true;
            GText("누구나 경품 추첨!",998,261,429,75,61,new Color(.21f,.055f,.105f)).name="GStar prize draw headline";
            GText("순위가 높을수록 당첨 확률이 올라가요",846,343,657,35,27,GInk).name="GStar prize draw odds";
        }
        void DrawGStarAttempts(List<GStarLeaderboard.CompletedRun> runs)
        {
            // Cover the previous score heading and the protruding stomach badge together.
            string skin=passed?"clear":"failed";
            GImage(GSlice(skin,100,230,200,45),393,303,371,260).name="GStar attempts paper";
            if(runs.Count==0)
            {
                GText("완료한 기록이 없어요",395,377,363,81,33);return;
            }
            int visible=Mathf.Min(3,runs.Count); // Includes visitors saved by the earlier three-round booth build.
            float height=visible==1?160:visible==2?111:76;
            for(int i=0;i<visible;i++)
            {
                var run=runs[i];float top=visible==1?339:307+i*(height+7);
                var card=GRect("GStar attempt "+(i+1),content,403,top,346,height).gameObject.AddComponent<GStarPillGraphic>();
                card.face=run.stageBest?new Color(.85f,1,.89f):new Color(.96f,.92f,.91f);
                card.gloss=false;card.raycastTarget=false;
                float line=height*.30f;
                GText($"{i+1}차  ·  {GStage(run.stageId)}  ·  {(run.data.success?"클리어":"실패")}",414,top+height*.055f,324,line,visible==3?18:23);
                var score=GText($"{run.data.Total:N0}점",414,top+height*.32f,324,line*1.25f,visible==3?27:visible==2?37:45,GInk);
                score.name="GStar attempt score "+(i+1);
                bool repeated=runs.Count(r=>r.stageId==run.stageId)>1;
                string label=!string.IsNullOrEmpty(run.exclusion)?"연습 기록":!run.stageBest?"도전 기록":repeated?"이번 체험 최고 기록":GStage(run.stageId)+" 랭킹";
                if(run.registered && run.stageBest)
                {
                    var rank=GStarLeaderboard.GetRunRank(run.runId);
                    if(rank!=null)label=$"등록 완료 · {GStage(run.stageId)} {rank.rank:N0}위";
                }
                GText(label,414,top+height*.73f,324,height*.21f,visible==3?15:19,new Color(.13f,.29f,.25f));
            }
        }

        void RegisterGStarScore()
        {
            if(!string.IsNullOrWhiteSpace(gstarPhone.text)&&!gstarConsent){gstarNotice="연락처 저장 동의를 선택해 주세요.";Render();return;}
            var result=GStarLeaderboard.SubmitVisitor(gstarName.text,gstarPhone.text,GStarSession.SessionId);
            gstarNotice=result.message;
            if(result.success)
            {
                if(!GStarSession.SetIdentity(gstarName.text,gstarPhone.text))gstarNotice=GStarSession.StorageError;
                var best=GStarLeaderboard.GetVisitorRuns(GStarSession.SessionId).FirstOrDefault(r=>r.stageBest);
                if(best!=null)gstarBoard=best.stageId;
            }
            Render();
        }
        static readonly string[] StageHooks = { "혀 끝에서 이빨로 대시!", "달콤한 냄새를 따라 보스 찾기", "잠든 중간보스를 골라 깨우기" };

        bool EnsureLastGStarScore()
        {
            var score=RunScore.Current;
            if(score==null || !score.IsFinished || score.SessionId!=GStarSession.SessionId ||
               score.RunId!=GStarSession.LastRunId || GStarLeaderboard.GetCompletedRun(score.RunId)!=null) return true;
            if(GStarLeaderboard.CaptureCompleted(score.RunId,score.SessionId,score.StageId,score.Data,score.Exclusion)) return true;
            gstarNotice=GStarLeaderboard.LastError;return false;
        }
        void GStarNext()
        {
            GHeader(0);int left=GStarSession.RemainingAttempts;
            GText("남은 도전 기회",503,251,499,84,64,new Color(0,.12f,.16f));
            GText(left.ToString(),1001,243,103,101,87,left==1?new Color(1,.1f,.2f):GGold,true);
            GText("회!",1100,251,136,84,64,new Color(0,.12f,.16f));
            var previous=GStarLeaderboard.GetCompletedRun(GStarSession.LastRunId);
            string outcome=previous==null?"같은 스테이지도 다시 도전할 수 있어요!":
                $"{GStage(previous.stageId)} {(previous.data.success?"클리어!":"도전 종료")}  ·  {previous.data.Total:N0}점";
            GText(outcome,493,344,763,62,37);
            for(int i=0;i<3;i++)
            {
                int stage=i;float x=187+i*460;
                GText($"스테이지 {i} · {GStage(i)}",205+i*460,630,348,32,29);
                GText(StageHooks[i],x,662,381,23,20,new Color(.27f,.19f,.23f));
                GButton("challenge "+i,"도전하기",x,700,381,90,()=>LaunchGStar(stage),true,left>0&&!starting&&!GStarSession.VisitorFinished);
            }
            // The old right separator crosses the new, wider instruction. Keep its paper
            // texture while clearing that decoration before rendering the live message.
            GImage(GSlice("next",360,834,385,63),345,834,701,63).name="GStar next instruction backing";
            GText(string.IsNullOrEmpty(gstarNotice)?(GStarSession.Interrupted?"이전 도전이 중단됐어요. 남은 기회로 도전해 주세요.":"같은 스테이지에도 다시 도전할 수 있어요."):gstarNotice,358,842,665,52,25);
            GButton("change character","캐릭터 변경",1059,833,233,72,()=>
            {
                if(EnsureLastGStarScore())ReturnToLobby(true);else Render();
            },false,left>0);
            GButton("finish experience","그만하기",1304,833,233,72,()=>
            {
                if(!EnsureLastGStarScore()){Render();return;}
                if(!GStarSession.RequestRegistration()){gstarNotice=GStarSession.StorageError;Render();return;}
                gstarNotice="";Show(GStarInitialPage(false));
            });
        }
        void LaunchGStar(int stage)
        {
            if(GStarSession.RemainingAttempts<=0||GStarSession.VisitorFinished||starting)return;
            if(!EnsureLastGStarScore()){Render();return;}
            LaunchStage(stage);
            if(!starting){gstarNotice=string.IsNullOrEmpty(GStarSession.StorageError)?"출전을 시작하지 못했어요. 다시 선택해 주세요.":GStarSession.StorageError;Render();}
        }
        void EndGStarVisitor()
        {
            if(!GStarSession.FinishVisitor()){gstarNotice=GStarSession.StorageError;Render();return;}
            GStarLeaderboard.ExportCsv();Show("gstarSurvey");
        }
        void ExportGStarQa()
        {
            var exported=GStarAnalytics.Export();
            string exportNotice=exported.success?"QA 자료 저장 완료 · "+exported.directory+
                (string.IsNullOrEmpty(exported.error)?"":"\n확인 필요: "+exported.error):exported.error;
            gstarOperatorNotice=exported.success?"QA 자료 저장 완료 · 저장 폴더의 analytics/exports에서 확인하세요.":"QA 자료 저장 실패 · "+exported.error;
            if(root==null)return;
            var toast=Rect("GStar QA export notice",root,.12f,.025f,.88f,.14f);
            var background=toast.gameObject.AddComponent<Image>();background.color=new Color(.025f,.06f,.10f,.94f);background.raycastTarget=false;
            var label=Label(toast,exportNotice,.025f,.08f,.975f,.92f,18);label.color=Color.white;label.enableWordWrapping=true;
            label.overflowMode=TextOverflowModes.Ellipsis;
            StartCoroutine(CloseGStarNotice(toast.gameObject));
        }
        System.Collections.IEnumerator CloseGStarNotice(GameObject notice)
        {yield return new WaitForSecondsRealtime(7);if(notice!=null)Destroy(notice);}
        void GStarSurvey()
        {
            GHeader(2);
            GText("플레이해 주셔서 감사합니다!",249,213,916,85,66,GGold,true);
            GText("여러분의 의견을 들려주세요!",248,309,767,76,57,GMint,true);
            GText("체험 설문",173,430,269,80,59,Color.black);
            GText("휴대폰 카메라로\nQR을 스캔해\n주세요.",165,565,284,165,40).enableWordWrapping=true;
            GText("게임 소식이 궁금하다면?",1095,456,414,49,31);
            GText("게임 홈페이지",1181,749,251,49,32);
            GText("설문을 마치셨다면 다음 참가자에게 자리를 양보해 주세요.",408,838,847,43,28);
            GText("화면은 그대로 두셔도 괜찮아요.",474,882,724,31,23,new Color(.35f,.16f,.09f));
            // Keep each full square, including its encoded quiet zone, inside the painted frame.
            GQr("survey-qr",521,418,376);
            GQr("homepage-qr",1217,549,169);
            if(gstarOperator)GStarOperator();
        }
        void GStarOperator()
        {
            var shade=GImage(null,0,0,Gw,Gh);shade.color=new Color(0,0,0,.78f);shade.raycastTarget=true;
            Panel(content,.18f,.18f,.82f,.82f);
            GText("직원용 · 다음 참가자 준비",355,224,962,70,45);
            GText($"새 참가자는 도전 기회 {GStarDemo.AttemptLimit}회로 시작합니다.\n누적 랭킹과 경품 연락처는 그대로 보관됩니다.",346,316,980,101,33).enableWordWrapping=true;
            GButton("export records","경품 기록 저장",374,463,425,86,()=>{gstarOperatorNotice=GStarLeaderboard.ExportCsv()?"경품 기록 저장 완료 · 지스타 저장 폴더에서 확인하세요.":GStarLeaderboard.LastError;Render();});
            GButton("new visitor","새 참가자 시작",856,463,425,86,()=>
            {
                if(!GStarSession.ResetVisitor()){gstarNotice=GStarSession.StorageError;Render();return;}
                LobbyLoadoutData.Clear();CrossSceneData.StartingLobbyItems=Array.Empty<MerchantItemBlueprint>();
                gstarDraftName=gstarDraftPhone=null;gstarConsent=false;gstarOperator=false;gstarNotice=gstarOperatorNotice="";ReturnToLobby(false);
            },true);
            GButton("export qa","QA 자료 저장 · F8",570,565,532,65,ExportGStarQa);
            var status=GText(gstarOperatorNotice,344,636,980,48,20);status.name="GStar staff status";
            status.enableWordWrapping=true;status.overflowMode=TextOverflowModes.Ellipsis;
            GButton("close staff","닫기",660,700,352,66,()=>{gstarOperator=false;Render();});
        }
        void ShowGStarScoreDetails(List<GStarLeaderboard.CompletedRun> runs,int index=0)
        {
            var run=runs[index];var d=run.data;
            var shade=GImage(null,0,0,Gw,Gh);shade.color=new Color(0,0,0,.72f);shade.raycastTarget=true;
            Panel(content,.20f,.13f,.80f,.88f);
            GText($"{index+1}차 · {GStage(run.stageId)} 점수 내역",379,159,916,65,45);
            string[] lines={
                $"일반 몬스터 {d.normal:N0} × 10     특수 몬스터 {d.special:N0} × 75",
                $"엘리트 {d.elite:N0} × 250     미니보스 {d.miniBoss:N0} × 1,500",
                $"보스 {d.boss:N0} × 5,000",
                $"미니스테이지 성공 {d.cleared:N0} × 1,500",
                $"과충전 성공 {d.overcharge:N0} × 1,000",
                d.gold > 0 ? $"골드 보상 {d.gold:N0} × 1     실버 보상 {d.silver:N0} × 2" : $"실버 보상 {d.silver:N0} × 2",
                $"스테이지 클리어 {(d.success?10000:0):N0}점",
                $"합계 {d.Total:N0}점"};
            for(int i=0;i<lines.Length;i++)GText(lines[i],372,243+i*56,928,52,31);
            if(runs.Count>1)
            {
                int other=(index+1)%runs.Count;
                GButton("other details",$"{other+1}차 점수 내역",423,723,382,72,()=>{Render();ShowGStarScoreDetails(runs,other);});
            }
            GButton("close details","닫기",runs.Count>1?859:660,723,352,72,Render);
        }
    }
}
