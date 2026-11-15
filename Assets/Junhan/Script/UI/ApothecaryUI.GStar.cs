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
        bool IsGStarPage => GStarDemo.Enabled && (Page=="result" || Page=="gstarNext" || Page=="gstarSurvey");
        static string GStage(int stage) => stage==0?"구강":stage==2?"대장":"위장";

        string GStarInitialPage(bool prepare)
        {
            if(GStarSession.VisitorFinished) return "gstarSurvey";
            if(prepare) return "prepare";
            if(GStarSession.HasCompletedAttempt)
            {
                var ended=GStarLeaderboard.GetCompletedRun(GStarSession.LastRunId);
                if(ended!=null && GStarLeaderboard.GetRunRank(ended.runId)==null)
                { passed=ended.data.success;gstarBoard=ended.stageId;return "result"; }
                return "gstarNext";
            }
            return "prepare";
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
        TextMeshProUGUI GText(string value,float x,float y,float w,float h,float size,Color? color=null,bool heading=false)
        {
            var t=Label(content,value,x/Gw,1-(y+h)/Gh,(x+w)/Gw,1-y/Gh,size*720/Gh);
            t.name="GStar text "+value;t.color=color??GInk;t.enableWordWrapping=false;t.fontStyle=FontStyles.Bold;
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
            var image=rect.gameObject.AddComponent<GStarPillGraphic>();
            image.face=primary?new Color(1,.19f,.38f):new Color(.68f,1,.81f);
            image.color=painted?Color.white:Color.clear;
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
            var logo=GRect("GStar title badge",content,674,25,324,56).gameObject.AddComponent<GStarPillGraphic>();
            logo.face=new Color(1,.96f,.87f);logo.gloss=false;logo.raycastTarget=false;
            GText("지스타 체험판",675,28,322,52,40);
            string[] titles={"01 기록 · 랭킹","02 다음 도전","03 체험 종료"};
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
        GStarLeaderboard.CompletedRun GCurrentRun()
        {
            var score=RunScore.Current;
            if(score!=null&&score.IsFinished && score.SessionId==GStarSession.SessionId)
                return new GStarLeaderboard.CompletedRun{data=score.Data,runId=score.RunId,sessionId=score.SessionId,stageId=score.StageId,exclusion=score.Exclusion};
            return GStarLeaderboard.GetCompletedRun(GStarSession.LastRunId);
        }
        TMP_InputField GInput(string key,string value,string hint,float x,float y,float w,float h,bool phone)
        {
            var rect=GRect("GStar input "+key,content,x,y,w,h);
            var bg=rect.gameObject.AddComponent<Image>();bg.color=Color.clear;
            var field=rect.gameObject.AddComponent<TMP_InputField>();field.targetGraphic=bg;
            var viewport=Rect("Viewport",rect,.035f,.1f,.965f,.9f);viewport.gameObject.AddComponent<RectMask2D>();
            var text=Label(viewport,"",0,0,1,1,29*720/Gh);text.richText=false;text.alignment=TextAlignmentOptions.MidlineLeft;
            var placeholder=Label(viewport,hint,0,0,1,1,(phone?21:26)*720/Gh);placeholder.color=new Color(.63f,.62f,.64f);placeholder.alignment=TextAlignmentOptions.MidlineLeft;
            placeholder.fontSizeMin=15*720/Gh;placeholder.enableWordWrapping=false;
            field.textViewport=viewport;field.textComponent=text;field.placeholder=placeholder;
            field.contentType=TMP_InputField.ContentType.Standard;field.lineType=TMP_InputField.LineType.SingleLine;
            field.characterLimit=phone?15:12;field.keyboardType=phone?TouchScreenKeyboardType.PhonePad:TouchScreenKeyboardType.Default;
            field.customCaretColor=true;field.caretColor=new Color(.95f,.08f,.28f);field.caretWidth=3;field.caretBlinkRate=.85f;
            field.selectionColor=new Color(.38f,.85f,.77f,.40f);
            var focus=Rect("Input focus outline",rect,0,0,1,1).gameObject.AddComponent<GStarInputFocus>();
            focus.input=field;focus.hint=placeholder;focus.raycastTarget=false;
            field.text=value??"";field.onValueChanged.AddListener(v=>{if(phone)gstarDraftPhone=v;else gstarDraftName=v;});
            return field;
        }
        void GStarResult()
        {
            var run=GCurrentRun();int stage=run?.stageId??GStarSession.StageId;
            if(gstarBoard<0)gstarBoard=stage;
            bool registered=run!=null&&GStarLeaderboard.GetRunRank(run.runId)!=null;
            GHeader(0);
            GText(passed?"스테이지 클리어!":"스테이지 클리어 실패",472,210,729,87,75,passed?GGold:new Color(.68f,.34f,1),true);
            GText("이번 도전 점수",413,317,284,55,37);
            GText($"{(run?.data.Total??0):N0}점",379,384,379,95,77,GGold,true).name="GStar total score";
            // The stage badge is live, including its organ icon, rather than baked as stomach.
            var badge=GRect("GStar live stage badge",content,413,483,296,78).gameObject.AddComponent<GStarPillGraphic>();
            badge.face=passed?new Color(.68f,1,.81f):new Color(.84f,.70f,.98f);badge.raycastTarget=false;
            GText(GStage(stage)+(passed?" · 클리어":" · 실패"),430,496,264,49,32);
            GText("이름",138,590,168,61,35);GText("전화번호",138,669,168,61,32);
            gstarName=GInput("name",gstarDraftName??GStarSession.PlayerName,"별명으로 하셔도 괜찮아요",325,590,415,63,false);
            gstarPhone=GInput("phone",gstarDraftPhone??GStarSession.Phone,"필수로 입력하지 않으셔도 됩니다!",325,669,415,62,true);
            gstarName.interactable=gstarPhone.interactable=!registered;
            string note=registered?"등록 완료 · 연락처는 순위표에 표시되지 않아요":gstarNotice;
            if(!string.IsNullOrEmpty(GStarLeaderboard.LastError))note=GStarLeaderboard.LastError;
            if(string.IsNullOrEmpty(note))note=string.IsNullOrEmpty(run?.exclusion)?"전화번호는 경품 안내에만 사용됩니다.":"연습 실행 · 순위 등록에서 제외됩니다.";
            GText(note,147,741,596,29,21,string.IsNullOrEmpty(run?.exclusion)?GInk:new Color(.55f,.18f,.50f));
            if(!registered)
            {
                var consent=GButton("consent",gstarConsent?"✓":"",155,771,25,25,()=>{gstarConsent=!gstarConsent;Render();},false,true);
                GText("연락처 입력 시 경품 안내용 저장에 동의합니다",186,768,550,29,18);
            }
            GButton("register",registered?"등록 완료":"등록하기",132,800,624,100,RegisterGStarScore,true,!registered&&run!=null&&string.IsNullOrEmpty(run.exclusion));
            GText("스테이지별 1·2·3위 상품 증정",905,315,545,54,37);
            for(int i=0;i<3;i++)
            {
                int s=i;var b=GButton("ranking stage "+i,GStage(i),835+i*225,384,226,60,()=>{gstarBoard=s;Render();},i==gstarBoard);
                if(i!=gstarBoard)b.GetComponent<GStarPillGraphic>().face=new Color(1,.94f,.83f);
                b.GetComponentInChildren<TextMeshProUGUI>().fontSize=33*720/Gh;
            }
            GText(GStage(gstarBoard)+" TOP 3",979,451,407,48,37);
            var mine=GStarLeaderboard.GetParticipantRank(GStarSession.Phone,GStarSession.SessionId,gstarBoard);
            if(mine==null)GText(registered?"이 스테이지의 등록 기록이 없어요":"등록하면 내 전체 순위가 여기에 표시돼요",835,516,688,51,29);
            else
            {
                GText($"내 순위  {mine.rank:N0}위",832,514,285,51,32,new Color(0,.25f,.23f));
                var name=GText(mine.name,1115,514,195,51,29);name.richText=false;name.overflowMode=TextOverflowModes.Ellipsis;
                GText($"{mine.score:N0}점",1310,514,210,51,31);
            }
            var rows=GStarLeaderboard.GetTop(gstarBoard,3);
            for(int i=0;i<3;i++)
            {
                float y=594+i*84;var row=i<rows.Count?rows[i]:null;
                GText((i+1).ToString(),851,y,62,57,39,Color.white,true);
                var n=GText(row?.name??"첫 도전을 기다려요",954,y,269,57,31);n.richText=false;n.overflowMode=TextOverflowModes.Ellipsis;
                GText(row!=null?$"{row.score:N0}점":"—",1227,y,195,57,31);
            }
            GButton("result next","다음",1383,834,181,73,()=>Show("gstarNext"));
            GButton("score details","점수 내역",817,850,235,46,ShowGStarScoreDetails,false,true,false);
            if(registered && mine!=null && run!=null && mine.score>run.data.Total)
                GText("순위는 스테이지별 개인 최고 기록 기준",1060,847,315,52,17);
        }
        void RegisterGStarScore()
        {
            var run=GCurrentRun();if(run==null)return;
            if(!string.IsNullOrWhiteSpace(gstarPhone.text)&&!gstarConsent){gstarNotice="연락처 저장 동의를 선택해 주세요.";Render();return;}
            if(!GStarLeaderboard.CaptureCompleted(run.runId,run.sessionId,run.stageId,run.data,run.exclusion))
            {gstarNotice=GStarLeaderboard.LastError;Render();return;}
            GStarSession.CompleteAttempt(run.data.success);
            var result=GStarLeaderboard.Submit(gstarName.text,gstarPhone.text,run.stageId,run.runId,run.sessionId,run.data,run.exclusion);
            gstarNotice=result.message;
            if(result.success){GStarSession.SetIdentity(gstarName.text,gstarPhone.text);gstarBoard=run.stageId;}
            Render();
        }
        void GStarNext()
        {
            GHeader(1);int left=GStarSession.RemainingAttempts;
            GText("남은 도전 기회",503,251,499,84,64,new Color(0,.12f,.16f));
            GText(left.ToString(),1001,243,103,101,87,left==1?new Color(1,.1f,.2f):GGold,true);
            GText("회!",1100,251,136,84,64,new Color(0,.12f,.16f));
            GText(left>0?"같은 스테이지도 다시 도전할 수 있어요!":"모든 도전을 마쳤어요! 체험 마치기를 눌러 주세요.",493,344,763,62,37);
            for(int i=0;i<3;i++)
            {
                int stage=i;float x=187+i*460;
                GText($"스테이지 {i} · {GStage(i)}",205+i*460,636,348,49,35);
                GButton("challenge "+i,"도전하기",x,700,381,90,()=>LaunchGStar(stage),true,left>0&&!starting&&!GStarSession.VisitorFinished);
            }
            GText(string.IsNullOrEmpty(gstarNotice)?(GStarSession.Interrupted?"이전 도전이 중단됐어요. 남은 기회로 도전해 주세요.":"장비를 바꾸어 다시 도전할 수 있어요."):gstarNotice,358,842,665,52,25);
            GButton("change character","캐릭터 변경",1059,833,233,72,()=>ReturnToLobby(true),false,left>0);
            GButton("finish experience","체험 마치기",1304,833,233,72,EndGStarVisitor);
        }
        void LaunchGStar(int stage)
        {
            if(GStarSession.RemainingAttempts<=0||GStarSession.VisitorFinished||starting)return;
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
            gstarNotice=exported.success?"QA 자료 저장 완료 · "+exported.directory+
                (string.IsNullOrEmpty(exported.error)?"":"\n확인 필요: "+exported.error):exported.error;
            if(root==null)return;
            var toast=Rect("GStar QA export notice",root,.12f,.025f,.88f,.14f);
            var background=toast.gameObject.AddComponent<Image>();background.color=new Color(.025f,.06f,.10f,.94f);background.raycastTarget=false;
            var label=Label(toast,gstarNotice,.025f,.08f,.975f,.92f,18);label.color=Color.white;label.enableWordWrapping=true;
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
            // Real QR assets can be supplied later without a code change.
            var survey=Resources.Load<Sprite>("GStarDemo/survey-qr");if(survey!=null)GImage(survey,496,418,426,376).preserveAspect=true;
            var site=Resources.Load<Sprite>("GStarDemo/homepage-qr");if(site!=null)GImage(site,1211,549,181,169).preserveAspect=true;
            if(gstarOperator)GStarOperator();
        }
        void GStarOperator()
        {
            var shade=GImage(null,0,0,Gw,Gh);shade.color=new Color(0,0,0,.78f);shade.raycastTarget=true;
            Panel(content,.18f,.18f,.82f,.82f);
            GText("직원용 · 다음 참가자 준비",355,224,962,70,45);
            GText("새 참가자는 도전 기회 3회로 시작합니다.\n누적 랭킹과 경품 연락처는 그대로 보관됩니다.",346,316,980,101,33).enableWordWrapping=true;
            GButton("export records","경품 기록 저장",374,463,425,86,()=>{gstarNotice=GStarLeaderboard.ExportCsv()?"저장 완료 · "+GStarLeaderboard.ExportDirectory:GStarLeaderboard.LastError;Render();});
            GButton("new visitor","새 참가자 시작",856,463,425,86,()=>
            {
                if(!GStarSession.ResetVisitor()){gstarNotice=GStarSession.StorageError;Render();return;}
                LobbyLoadoutData.Clear();CrossSceneData.StartingLobbyItems=Array.Empty<MerchantItemBlueprint>();
                gstarDraftName=gstarDraftPhone=null;gstarConsent=false;gstarOperator=false;ReturnToLobby(true);
            },true);
            GButton("export qa","QA 자료 저장 · F8",570,565,532,65,ExportGStarQa);
            GText(gstarNotice,344,633,980,40,18).enableWordWrapping=true;
            GButton("close staff","닫기",660,662,352,66,()=>{gstarOperator=false;Render();});
        }
        void ShowGStarScoreDetails()
        {
            var run=GCurrentRun();if(run==null)return;var d=run.data;
            var shade=GImage(null,0,0,Gw,Gh);shade.color=new Color(0,0,0,.72f);shade.raycastTarget=true;
            Panel(content,.20f,.13f,.80f,.88f);
            GText("이번 도전 점수 내역",379,159,916,65,45);
            string[] lines={
                $"일반 몬스터 {d.normal:N0} × 10     특수 몬스터 {d.special:N0} × 75",
                $"엘리트 {d.elite:N0} × 250     미니보스 {d.miniBoss:N0} × 1,500",
                $"보스 {d.boss:N0} × 5,000",
                $"미니스테이지 성공 {d.cleared:N0} × 1,500",
                $"과충전 성공 {d.overcharge:N0} × 1,000",
                $"골드 보상 {d.gold:N0} × 1     실버 보상 {d.silver:N0} × 2",
                $"스테이지 클리어 {(d.success?10000:0):N0}점",
                $"합계 {d.Total:N0}점"};
            for(int i=0;i<lines.Length;i++)GText(lines[i],372,243+i*56,928,52,31);
            GButton("close details","닫기",660,723,352,72,Render);
        }
    }
}
