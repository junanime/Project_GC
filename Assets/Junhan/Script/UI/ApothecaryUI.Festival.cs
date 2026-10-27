using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    public sealed partial class ApothecaryUI
    {
        string festivalName = "", festivalDepartment = "", festivalSavedRun;
        TextMeshProUGUI festivalNote;

        void BuildFestivalSelection()
        {
            Label(content,"페스티벌 · 45초 생존",.05f,.895f,.72f,.975f,36);
            ActionButton(content,"QA 데이터",.735f,.91f,.84f,.975f,()=>Show("festivalQa"));
            ActionButton(content,"설정",.85f,.91f,.96f,.975f,OpenSettings);
            var characters = Config.characters.Where(c => c != null && FestivalDemo.Preset(c) != null).Take(4).ToArray();
            if (!characters.Contains(character)) character = characters.FirstOrDefault();
            for (int i=0;i<characters.Length;i++)
            {
                var candidate=characters[i];float x=.035f+i*.174f;
                var card=Rect("Festival character "+OctoberArt.CharacterKey(candidate),content,x,.505f,x+.163f,.88f);
                var art=ImageAt(card,AugmentPanelTheme.Frame(AugmentUpgradeGrade.Epic),0,0,1,1,false);
                var click=card.gameObject.AddComponent<Button>();click.targetGraphic=art;
                click.onClick.AddListener(()=>{if(starting)return;character=candidate;characterIndex=Array.IndexOf(Config.characters,candidate);Render();});
                var hit=card.gameObject.AddComponent<Image>();hit.color=Color.clear;hit.raycastTarget=true;
                ImageAt(card,CharacterProfile(candidate),.14f,.29f,.86f,.89f);
                Label(card,candidate.name,.08f,.14f,.92f,.29f,25).color=Ink;
                Label(card,FestivalDemo.Preset(candidate).description,.045f,.035f,.955f,.15f,15).color=Ink;
                if(character==candidate)
                {
                    var selected=ImageAt(card,null,.06f,.90f,.94f,.985f,false);selected.color=new Color(.16f,.47f,.38f);
                    Label(card,"선택됨",.06f,.90f,.94f,.985f,18).color=Color.white;
                }
            }
            var preset=FestivalDemo.Preset(character);
            if (preset==null) return;
            Panel(content,.035f,.175f,.724f,.49f);
            BuildFestivalSkills();
            Label(content,character.name+" · 시작 보유 구성",.05f,.43f,.36f,.48f,21);
            Label(content,"오리지널 전체 보유 · 추가 강화 0",.36f,.43f,.71f,.48f,16);
            var relic=FestivalDemo.Relic(character);
            Label(content,"유물",.05f,.345f,.11f,.40f,18);
            FestivalEquipment(relic?.icon,relic?.relicName??"유물 없음",relic?.Description,.10f,.32f,.275f,.42f);
            Label(content,"아이템",.28f,.345f,.34f,.40f,18);
            var items=FestivalDemo.Items(character);
            for(int i=0;i<items.Length;i++) FestivalEquipment(items[i].itemIcon,items[i].itemName,items[i].description,.35f+i*.18f,.32f,.52f+i*.18f,.42f);
            Label(content,"특수 증강",.05f,.21f,.14f,.28f,18);
            for(int i=0;i<4;i++)
            {
                var weapon=Config.weapons.FirstOrDefault(w=>w!=null&&(int)w.Type==preset.weapons[i]);
                FestivalEquipment(weapon?.Image,weapon?.Name??"무기 정보 없음",weapon?.Description,.14f+i*.14f,.195f,.275f+i*.14f,.30f);
            }
            Label(content,$"WASD 이동 · Shift 대시 · {GameInput.ActiveSkillKeyLabel} 액티브(신이는 길게) · 경험치 없음 · 시작 시 무작위 고귀 1개",.04f,.105f,.96f,.16f,19);
            ActionButton(content,"행사 리더보드",.045f,.025f,.275f,.095f,()=>Show("festivalRanking"));
            festivalNote=Label(content,string.IsNullOrEmpty(message)?(FestivalQaRecorder.Problem??"캐릭터 선택·FPS·끊김·오류를 익명 기록합니다"):message,.29f,.025f,.68f,.095f,13);
            ActionButton(content,"이 캐릭터로 시작",.70f,.025f,.955f,.095f,StartFestival,true,!starting);
            StyleFestivalButtons();
        }
        void FestivalEquipment(Sprite icon,string name,string description,float x,float y,float right,float top)
        {
            var root=Rect("Starting equipment "+name,content,x,y,right,top);
            ImageAt(root,icon,0,.08f,.27f,.92f);
            var label=Label(root,name,.30f,.06f,1,.94f,16);label.richText=false;
            var hit=root.gameObject.AddComponent<Image>();hit.color=Color.clear;hit.raycastTarget=true;
            HudTooltip.Bind(root.gameObject,()=>name,()=>description??"");
        }
        void StartFestival()
        {
            if(starting||StageEntryLoading.IsLoading)return;
            string error=FestivalDemo.Validate(character);
            if(error!=null){if(festivalNote!=null)festivalNote.text=error;return;}
            CrossSceneData.CharacterBlueprint=character;
            CrossSceneData.ClearPendingRunSceneTransfer();
            CrossSceneData.ClearStartingLobbyItems();
            festivalName=festivalDepartment="";festivalSavedRun=null;
            starting=true;
            if(!StageEntryLoading.Begin(character,()=>{starting=false;if(this!=null)Show("festival");}))starting=false;
            if(!starting&&festivalNote!=null)festivalNote.text="로딩을 시작하지 못했습니다. 다시 시도해 주세요.";
        }
        void BuildFestivalResult()
        {
            var run=RunScore.Current;
            var data=run?.Data;
            Label(content,passed?"45초 생존 성공!":"생존 종료",.12f,.86f,.88f,.96f,43);
            ImageAt(content,CharacterProfile(character),.08f,.48f,.30f,.82f);
            Label(content,character?.name??"",.08f,.41f,.30f,.48f,28);
            Label(content,$"{FestivalLeaderboard.Score(data):N0}점",.34f,.69f,.91f,.82f,48);
            Label(content,$"생존 {data?.seconds??0:0.0}초 / 45초  ·  처치 {stats?.MonstersKilled??0:N0}",.34f,.60f,.91f,.68f,26);
            Label(content,"학과와 이름을 입력해 행사 순위에 기록하세요",.34f,.51f,.92f,.58f,21);
            var department=FestivalInput("학과",festivalDepartment,40,.13f,.27f,.57f,.38f);
            var name=FestivalInput("이름",festivalName,24,.60f,.27f,.87f,.38f);
            department.onValueChanged.AddListener(value=>festivalDepartment=value);
            name.onValueChanged.AddListener(value=>festivalName=value);
            bool saved=run!=null&&festivalSavedRun==run.FestivalRunId;
            department.interactable=name.interactable=!saved;
            festivalNote=Label(content,saved?"행사 기록 등록 완료":"이 PC에 저장 · 생존 성공 우선, 점수순",.12f,.17f,.88f,.24f,19);
            Button submit=null;
            submit=ActionButton(content,saved?"등록 완료":"기록 등록",.37f,.075f,.63f,.155f,()=>
            {
                if(run==null||!run.Finished||festivalSavedRun==run.FestivalRunId)return;
                if(run.Exclusion!=null){festivalNote.text="연습 기능을 사용한 판은 순위 등록에서 제외됩니다.";return;}
                var record=new FestivalRecord{runId=run.FestivalRunId,name=festivalName,department=festivalDepartment,
                    character=character?.name,score=FestivalLeaderboard.Score(data),seconds=data.seconds,success=data.success,utc=DateTime.UtcNow.ToString("o")};
                if(FestivalLeaderboard.Save(record,out string error))
                {festivalSavedRun=run.FestivalRunId;submit.interactable=false;department.interactable=name.interactable=false;festivalNote.text="행사 기록 등록 완료";Show("festivalRanking");}
                else festivalNote.text=error;
            },true,!saved&&run!=null);
            ActionButton(content,"다음 참가자",.085f,.075f,.325f,.155f,()=>ReturnToLobby(false));
            ActionButton(content,"리더보드",.675f,.075f,.915f,.155f,()=>Show("festivalRanking"));
            StyleFestivalButtons();
        }
        TMP_InputField FestivalInput(string caption,string value,int limit,float x,float y,float right,float top)
        {
            Label(content,caption,x,top+.005f,right,top+.05f,22);
            var box=ImageAt(content,null,x,y,right,top,false);box.color=new Color(1,.97f,.89f);box.raycastTarget=true;
            box.name="Festival identity "+caption;
            var patch=StitchPatchTheme.Attach(box.transform,StitchPatchTheme.Thread,true,true);patch.inset=3;
            var tab=Rect("Identity stitch tag",box.transform,.02f,.62f,.13f,.91f);
            var star=tab.gameObject.AddComponent<StitchPatchGraphic>();star.starOnly=true;star.accent=StitchPatchTheme.Coral;star.raycastTarget=false;
            var field=box.gameObject.AddComponent<TMP_InputField>();
            var viewport=Rect("Text viewport",box.transform,.14f,.12f,.95f,.88f);viewport.gameObject.AddComponent<RectMask2D>();
            var text=Label(viewport,value,0,0,1,1,26);text.richText=false;text.alignment=TextAlignmentOptions.MidlineLeft;
            field.textViewport=viewport;field.textComponent=text;field.characterLimit=limit;field.text=value;
            var placeholder=Label(viewport,caption+" 입력",0,0,1,1,22);placeholder.alignment=TextAlignmentOptions.MidlineLeft;placeholder.color=new Color(.45f,.43f,.38f);
            field.placeholder=placeholder;field.customCaretColor=true;field.caretColor=Ink;
            field.lineType=TMP_InputField.LineType.SingleLine;
            field.ForceLabelUpdate(); // The placeholder is assigned after restoring existing input.
            return field;
        }
        void StyleFestivalButtons()
        {
            foreach(var feedback in content.GetComponentsInChildren<ApothecaryButtonFeedback>())
                StitchPatchTheme.Button(feedback,feedback.primary||feedback.chosen);
        }
        void BuildFestivalSkills()
        {
            var root=Rect("Festival skill summaries",content,.74f,.175f,.965f,.88f);
            StitchPatchTheme.Attach(root,PreparationIdentity.ColorFor(character),true,true);
            Label(root,"캐릭터 능력",.08f,.88f,.92f,.98f,24);
            string passive,active;
            switch(OctoberArt.CharacterKey(character))
            {
                case "Hyuki":passive="상태이상에 걸린 적에게\n추가 피해를 줍니다.";active="주변 적을 잠시 얼립니다.\n빙결침으로 지속시간을\n늘릴 수 있습니다.";break;
                case "Shini":passive="적을 처치하면 불씨를 모읍니다.\n불씨는 화염 방사의 연료입니다.";active="전방으로 불을 뿜습니다.\nSpace를 길게 누르면\n불씨로 연속 발사합니다.";break;
                case "Ari":passive="구르며 부딪힌 적에게\n피해를 주고 밀어냅니다.";active="암석 봉황으로 변신해\n강한 돌진을 반복합니다.";break;
                default:passive="대시 후 잠시 침 개수와\n공격 속도가 증가합니다.";active="10초간 침 개수와\n침 속도가 증가합니다.";break;
            }
            var skill=character.skills;
            ImageAt(root,skill.passiveIcon,.08f,.73f,.31f,.86f);
            Label(root,"패시브",.34f,.79f,.93f,.86f,17);
            Label(root,skill.passiveName,.30f,.70f,.96f,.80f,20);
            var p=Label(root,passive,.09f,.52f,.91f,.69f,19);p.alignment=TextAlignmentOptions.MidlineLeft;
            ImageAt(root,skill.activeIcon,.08f,.36f,.31f,.49f);
            Label(root,"액티브 · Space",.32f,.42f,.94f,.49f,17);
            Label(root,skill.activeName,.30f,.33f,.96f,.43f,20);
            var a=Label(root,active,.09f,.06f,.91f,.32f,19);a.alignment=TextAlignmentOptions.MidlineLeft;
        }
        void BuildFestivalRanking()
        {
            Label(content,"페스티벌 리더보드",.12f,.89f,.88f,.97f,37);
            string error=null;
            System.Collections.Generic.List<FestivalRecord> rows;
            try{rows=FestivalLeaderboard.Read();}catch(Exception){rows=new System.Collections.Generic.List<FestivalRecord>();error="저장된 기록을 읽을 수 없습니다. 기존 파일은 보존됩니다.";}
            int people=rows.Select(r=>r.department+"\u001f"+r.name).Distinct().Count();
            Label(content,$"참가자 {people:N0}명 · 전체 기록 {rows.Count:N0}개 · 성공 우선 → 점수 → 생존 시간",.07f,.83f,.93f,.89f,19);
            int current=rows.FindIndex(r=>r.runId==festivalSavedRun);
            float top=current>=0?.66f:.75f;
            if(current>=0)
            {
                var pinned=Rect("Current festival record",content,.04f,.715f,.95f,.81f);
                StitchPatchTheme.Attach(pinned,StitchPatchTheme.Coral,true);
                Label(pinned,"이번 플레이",.01f,.62f,.15f,.90f,15);
                FestivalRankingRow(pinned,rows[current],current+1,true);
            }
            Label(content,"순위",.04f,top,.15f,top+.045f,18);
            Label(content,"학과 / 이름",.16f,top,.52f,top+.045f,18);
            Label(content,"캐릭터",.53f,top,.66f,top+.045f,18);
            Label(content,"성공 / 생존",.67f,top,.82f,top+.045f,18);
            Label(content,"점수",.83f,top,.95f,top+.045f,18);
            var view=Rect("Festival ranking scroll",content,.04f,.215f,.95f,top-.01f);
            var background=view.gameObject.AddComponent<Image>();background.color=new Color(1,.97f,.88f,.95f);
            var scroll=view.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.vertical=true;
            scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=34;
            var viewport=Rect("Viewport",view,0,0,.98f,1);viewport.gameObject.AddComponent<RectMask2D>();
            var body=Rect("All participant records",viewport,0,1,1,1);
            body.pivot=new Vector2(.5f,1);body.sizeDelta=new Vector2(0,rows.Count*58);
            scroll.viewport=viewport;scroll.content=body;
            for(int i=0;i<rows.Count;i++)
            {
                var row=Rect("Festival rank "+(i+1),body,0,1,1,1);
                row.pivot=new Vector2(.5f,1);row.sizeDelta=new Vector2(0,56);row.anchoredPosition=new Vector2(0,-i*58);
                row.gameObject.AddComponent<Image>().color=i%2==0?new Color(1,.96f,.84f):new Color(.96f,.90f,.76f);
                FestivalRankingRow(row,rows[i],i+1,false);
            }
            var rail=ImageAt(view,null,.984f,.02f,.999f,.98f,false);rail.color=new Color(.76f,.64f,.52f,.5f);rail.raycastTarget=true;
            var bar=rail.gameObject.AddComponent<Scrollbar>();bar.direction=Scrollbar.Direction.BottomToTop;
            var thumb=ImageAt(rail.transform,null,0,0,1,1,false);thumb.color=StitchPatchTheme.Coral;thumb.raycastTarget=true;
            bar.handleRect=thumb.rectTransform;bar.targetGraphic=thumb;scroll.verticalScrollbar=bar;
            scroll.verticalNormalizedPosition=1;
            if(rows.Count==0)Label(viewport,error??"첫 번째 참가자의 기록을 기다리고 있습니다",.08f,.3f,.92f,.7f,25);
            Label(content,"마우스 휠 또는 목록을 위아래로 드래그 · 같은 참가자의 여러 도전 기록도 모두 표시",.06f,.165f,.94f,.205f,16);
            ActionButton(content,level!=null?"결과로 돌아가기":"캐릭터 선택",.34f,.055f,.66f,.135f,()=>Show(level!=null?"result":"festival"),true);
            StyleFestivalButtons();
        }
        void FestivalRankingRow(RectTransform root,FestivalRecord row,int rank,bool pinned)
        {
            float y=pinned?.04f:0,top=pinned?.72f:1;
            if(rank<=3)
            {
                var medal=Rect("Medal "+rank,root,.012f,y+.12f,.05f,top-.12f);
                var tail=ImageAt(medal,null,.23f,-.15f,.77f,.44f,false);tail.color=new Color(.74f,.28f,.30f);
                var disc=Rect("Medal face",medal,0,.12f,1,1);var shape=disc.gameObject.AddComponent<IdentityShape>();shape.radius=30;shape.glossy=true;shape.raycastTarget=false;
                shape.color=rank==1?new Color(1,.75f,.20f):rank==2?new Color(.72f,.81f,.88f):new Color(.77f,.45f,.24f);
                Label(disc,rank.ToString(),0,.05f,1,.95f,17);
            }
            Label(root,rank+"위",.052f,y,.123f,top,20);
            var participant=Label(root,row.department+" / "+row.name,.132f,y,.52f,top,19);
            participant.richText=false;participant.enableWordWrapping=false;participant.overflowMode=TextOverflowModes.Ellipsis;
            Label(root,row.character,.53f,y,.67f,top,19).richText=false;
            Label(root,row.success?(row.rules!=FestivalDemo.Rules?"성공 · 이전판":"성공"):row.seconds.ToString("0.0")+"초",.68f,y,.85f,top,19).color=row.success?new Color(.13f,.43f,.28f):Ink;
            Label(root,row.score.ToString("N0"),.86f,y,.99f,top,21);
        }
    }
}
