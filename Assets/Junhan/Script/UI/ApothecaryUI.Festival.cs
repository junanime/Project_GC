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
            Label(content,"페스티벌 · 60초 생존",.05f,.895f,.75f,.975f,36);
            ActionButton(content,"설정",.85f,.91f,.96f,.975f,OpenSettings);
            var characters = Config.characters.Where(c => c != null && FestivalDemo.Preset(c) != null).Take(4).ToArray();
            if (!characters.Contains(character)) character = characters.FirstOrDefault();
            for (int i=0;i<characters.Length;i++)
            {
                var candidate=characters[i];float x=.035f+i*.239f;
                var card=Rect("Festival character "+OctoberArt.CharacterKey(candidate),content,x,.505f,x+.223f,.88f);
                var art=ImageAt(card,AugmentPanelTheme.Frame(AugmentUpgradeGrade.Epic),0,0,1,1,false);
                var click=card.gameObject.AddComponent<Button>();click.targetGraphic=art;
                click.onClick.AddListener(()=>{if(starting)return;character=candidate;characterIndex=Array.IndexOf(Config.characters,candidate);Render();});
                var hit=card.gameObject.AddComponent<Image>();hit.color=Color.clear;hit.raycastTarget=true;
                ImageAt(card,CharacterProfile(candidate),.14f,.29f,.86f,.89f);
                Label(card,candidate.name,.08f,.14f,.92f,.29f,28).color=Ink;
                Label(card,FestivalDemo.Preset(candidate).description,.045f,.035f,.955f,.15f,17).color=Ink;
                if(character==candidate)
                {
                    var selected=ImageAt(card,null,.06f,.90f,.94f,.985f,false);selected.color=new Color(.16f,.47f,.38f);
                    Label(card,"선택됨",.06f,.90f,.94f,.985f,18).color=Color.white;
                }
            }
            var preset=FestivalDemo.Preset(character);
            if (preset==null) return;
            Panel(content,.035f,.175f,.965f,.49f);
            Label(content,character.name+" · 시작 보유 구성",.05f,.43f,.55f,.48f,23);
            Label(content,"특수 증강 4종 모두 지급",.61f,.43f,.94f,.48f,19);
            var relic=FestivalDemo.Relic(character);
            Label(content,"유물",.05f,.345f,.11f,.40f,18);
            FestivalEquipment(relic?.icon,relic?.relicName??"유물 없음",relic?.Description,.12f,.32f,.32f,.42f);
            Label(content,"아이템",.35f,.345f,.43f,.40f,18);
            var items=FestivalDemo.Items(character);
            for(int i=0;i<items.Length;i++) FestivalEquipment(items[i].itemIcon,items[i].itemName,items[i].description,.44f+i*.245f,.32f,.67f+i*.245f,.42f);
            Label(content,"특수 증강",.05f,.21f,.14f,.28f,18);
            for(int i=0;i<4;i++)
            {
                var weapon=Config.weapons.FirstOrDefault(w=>w!=null&&(int)w.Type==preset.weapons[i]);
                FestivalEquipment(weapon?.Image,weapon?.Name??"무기 정보 없음",weapon?.Description,.15f+i*.195f,.195f,.335f+i*.195f,.30f);
            }
            Label(content,$"WASD 이동 · Shift 대시 · {GameInput.ActiveSkillKeyLabel} 액티브(신이는 길게) · 퀘스트 완료 시 고귀 증강 자동 획득",.04f,.105f,.96f,.16f,19);
            ActionButton(content,"행사 리더보드",.045f,.025f,.275f,.095f,()=>Show("festivalRanking"));
            festivalNote=Label(content,message,.29f,.025f,.68f,.095f,17);
            ActionButton(content,"이 캐릭터로 시작",.70f,.025f,.955f,.095f,StartFestival,true,!starting);
        }
        void FestivalEquipment(Sprite icon,string name,string description,float x,float y,float right,float top)
        {
            var root=Rect("Starting equipment "+name,content,x,y,right,top);
            ImageAt(root,icon,0,.08f,.27f,.92f);
            var label=Label(root,name,.30f,.06f,1,.94f,18);label.richText=false;
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
            Label(content,passed?"60초 생존 성공!":"생존 종료",.12f,.86f,.88f,.96f,43);
            ImageAt(content,CharacterProfile(character),.08f,.48f,.30f,.82f);
            Label(content,character?.name??"",.08f,.41f,.30f,.48f,28);
            Label(content,$"{FestivalLeaderboard.Score(data):N0}점",.34f,.69f,.91f,.82f,48);
            Label(content,$"생존 {data?.seconds??0:0.0}초 / 60초  ·  처치 {stats?.MonstersKilled??0:N0}",.34f,.60f,.91f,.68f,26);
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
                {festivalSavedRun=run.FestivalRunId;submit.interactable=false;department.interactable=name.interactable=false;festivalNote.text="행사 기록 등록 완료";}
                else festivalNote.text=error;
            },true,!saved&&run!=null);
            ActionButton(content,"다음 참가자",.085f,.075f,.325f,.155f,()=>ReturnToLobby(false));
            ActionButton(content,"리더보드",.675f,.075f,.915f,.155f,()=>Show("festivalRanking"));
        }
        TMP_InputField FestivalInput(string caption,string value,int limit,float x,float y,float right,float top)
        {
            Label(content,caption,x,top+.005f,right,top+.05f,22);
            var box=ImageAt(content,null,x,y,right,top,false);box.color=new Color(1,.97f,.89f);box.raycastTarget=true;
            var outline=box.gameObject.AddComponent<Outline>();outline.effectColor=new Color(.35f,.40f,.38f);outline.effectDistance=new Vector2(2,-2);
            var field=box.gameObject.AddComponent<TMP_InputField>();
            var text=Label(box.transform,value,.035f,.05f,.965f,.95f,26);text.richText=false;text.alignment=TextAlignmentOptions.MidlineLeft;
            field.textViewport=box.rectTransform;field.textComponent=text;field.characterLimit=limit;field.text=value;
            var placeholder=Label(box.transform,caption+" 입력",.035f,.05f,.965f,.95f,22);placeholder.alignment=TextAlignmentOptions.MidlineLeft;placeholder.color=new Color(.45f,.43f,.38f);
            field.placeholder=placeholder;field.customCaretColor=true;field.caretColor=Ink;
            field.lineType=TMP_InputField.LineType.SingleLine;
            field.ForceLabelUpdate(); // The placeholder is assigned after restoring existing input.
            return field;
        }
        void BuildFestivalRanking()
        {
            Label(content,"페스티벌 리더보드",.12f,.87f,.88f,.96f,37);
            Label(content,"이 PC의 참가 기록 · 생존 성공 우선 → 점수 → 생존 시간",.12f,.80f,.88f,.86f,19);
            string error=null;
            System.Collections.Generic.List<FestivalRecord> rows;
            try{rows=FestivalLeaderboard.Read();}catch(Exception){rows=new System.Collections.Generic.List<FestivalRecord>();error="저장된 기록을 읽을 수 없습니다. 기존 파일은 보존됩니다.";}
            Label(content,"순위",.04f,.74f,.11f,.79f,18);
            Label(content,"학과 / 이름",.12f,.74f,.50f,.79f,18);
            Label(content,"캐릭터",.51f,.74f,.65f,.79f,18);
            Label(content,"기록",.66f,.74f,.81f,.79f,18);
            Label(content,"점수",.82f,.74f,.96f,.79f,18);
            for(int i=0;i<Mathf.Min(10,rows.Count);i++)
            {
                var row=rows[i];float y=.685f-i*.052f;
                ImageAt(content,null,.04f,y,.96f,y+.047f,false).color=new Color(1,.96f,.84f,i%2==0?.92f:.65f);
                Label(content,(i+1).ToString(),.045f,y,.11f,y+.047f,20);
                var participant=Label(content,row.department+" / "+row.name,.12f,y,.50f,y+.047f,19);
                participant.richText=false;participant.enableWordWrapping=false;participant.overflowMode=TextOverflowModes.Ellipsis;
                Label(content,row.character,.51f,y,.65f,y+.047f,19).richText=false;
                Label(content,row.success?"생존 성공":row.seconds.ToString("0.0")+"초",.66f,y,.81f,y+.047f,19);
                Label(content,row.score.ToString("N0"),.82f,y,.955f,y+.047f,21);
            }
            if(rows.Count==0)Label(content,error??"첫 번째 참가자의 기록을 기다리고 있습니다",.15f,.40f,.85f,.58f,25);
            ActionButton(content,level!=null?"결과로 돌아가기":"캐릭터 선택",.34f,.07f,.66f,.15f,()=>Show(level!=null?"result":"festival"),true);
        }
    }
}
