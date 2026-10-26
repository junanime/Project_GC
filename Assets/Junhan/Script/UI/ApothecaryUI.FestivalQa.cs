using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;

namespace Vampire
{
    public sealed partial class ApothecaryUI
    {
        string festivalQaExport;
        void BuildFestivalQa()
        {
            FestivalQaRecorder.Checkpoint();
            Label(content,"페스티벌 QA · 운영자",.08f,.86f,.92f,.97f,35);
            FestivalQaSnapshot data;
            try { data=FestivalQaStore.ReadAll(FestivalQaStore.Folder); }
            catch(Exception) { data=new FestivalQaSnapshot();data.unreadable=1; }
            Label(content,$"출전 {data.runs.Count:N0}회   ·   평균 {(data.Seconds>0?data.AverageFps.ToString("0.0"):"—")} FPS   ·   오류 {data.Errors:N0}회",.08f,.75f,.92f,.84f,25);
            Panel(content,.07f,.35f,.93f,.73f);
            Label(content,"캐릭터",.09f,.66f,.26f,.72f,19);
            Label(content,"출전 / 선택 비율",.27f,.66f,.52f,.72f,19);
            Label(content,"평균 FPS",.54f,.66f,.70f,.72f,19);
            Label(content,"끊김 / 심한 끊김",.70f,.66f,.91f,.72f,19);
            var keys=new[]{"Ashi","Ari","Hyuki","Shini"};
            for(int i=0;i<keys.Length;i++)
            {
                var rows=data.runs.Where(r=>r.character==keys[i]).ToArray();
                float y=.58f-i*.07f;double seconds=rows.Sum(r=>r.measuredSeconds);
                Label(content,FestivalQaStore.CharacterName(keys[i]),.09f,y,.26f,y+.06f,21);
                Label(content,$"{rows.Length:N0}회 / {(data.runs.Count>0?100d*rows.Length/data.runs.Count:0):0.#}%",.27f,y,.52f,y+.06f,20);
                Label(content,seconds>0?(rows.Sum(r=>r.frames)/seconds).ToString("0.0"):"—",.54f,y,.70f,y+.06f,20);
                Label(content,$"{rows.Sum(r=>r.stalls100):N0} / {rows.Sum(r=>r.stalls250):N0}",.70f,y,.91f,y+.06f,20);
            }
            Label(content,"선택창 클릭이 아닌 실제 출전 기준 · FPS는 전투 중만 측정",.08f,.285f,.92f,.335f,17);
            Label(content,"끊김 0.1초 이상 / 심한 끊김 0.25초 이상인 프레임 수 · 심한 끊김은 끊김에도 포함",.06f,.235f,.94f,.285f,16);
            Label(content,"QA에는 이름·학과·점수·대시·액티브 횟수를 저장하지 않습니다. 자동 전송도 없습니다.",.06f,.185f,.94f,.235f,15);
            string warning=FestivalQaRecorder.Problem;
            if(data.unreadable>0)warning=$"읽지 못한 QA 파일 {data.unreadable}개 · 원본은 보존됩니다.";
            var status=Label(content,warning??"내보내기를 누르면 바탕화면의 FestivalQA_exports 폴더에 ZIP 하나를 만듭니다.",.06f,.11f,.94f,.18f,16);
            ActionButton(content,"돌아가기",.07f,.025f,.30f,.10f,()=>Show("festival"));
            ActionButton(content,"QA 내보내기",.35f,.025f,.65f,.10f,()=>
            {
                try
                {
                    FestivalQaRecorder.Checkpoint();
                    festivalQaExport=FestivalQaStore.Export(FestivalQaStore.Folder,FestivalQaStore.ExportFolder);
                    status.text="완료: "+Path.GetFileName(festivalQaExport)+" · 이 ZIP만 전달해 주세요.";
                }
                catch(Exception){status.text="내보내기에 실패했습니다. 저장 공간과 바탕화면 폴더 권한을 확인해 주세요. 원본은 유지됩니다.";}
            },true);
            ActionButton(content,"저장 폴더 열기",.70f,.025f,.93f,.10f,()=>
            {
                try
                {
                    if(!Directory.Exists(FestivalQaStore.ExportFolder)){status.text="먼저 QA 내보내기를 눌러 주세요.";return;}
                    Application.OpenURL(new Uri(FestivalQaStore.ExportFolder+Path.DirectorySeparatorChar).AbsoluteUri);
                }
                catch(Exception){status.text="바탕화면의 FestivalQA_exports 폴더를 직접 열어 주세요.";}
            });
            StyleFestivalButtons();
        }
    }
}
