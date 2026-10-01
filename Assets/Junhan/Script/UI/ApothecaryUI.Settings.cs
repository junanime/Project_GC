using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    public sealed partial class ApothecaryUI
    {
        GamePreferenceValues settingsOriginal, settingsDraft;
        string settingsReturn="main";
        bool confirmingDisplay;
        float displayDeadline;
        TextMeshProUGUI countdown, fpsLabel;
        float fpsTime;
        int fpsFrames;
        string pendingSessionAction;

        public void OpenSettings()
        {
            if(starting || (level != null && (level.IsLevelEnded || (level.PlayerCharacter != null && level.PlayerCharacter.Skills != null && level.PlayerCharacter.Skills.IsCutin)))) return;
            if(Page=="hud")
            {
                // Do not steal a pause owned by a level-up dialog or another system.
                if(Time.timeScale==0) return;
                previousTime=Time.timeScale; ownsPause=true; Time.timeScale=0;
            }
            Show("settings",3);
        }

        void BeginSettings()
        {
            settingsReturn=Page ?? "main";
            pendingSessionAction=null;
            settingsOriginal=GamePreferences.Current.Copy();settingsDraft=settingsOriginal.Copy();
            confirmingDisplay=false;
        }
        void CancelSettings()
        {
            if(pendingSessionAction!=null){pendingSessionAction=null;Render();return;}
            if(confirmingDisplay){RevertDisplay();return;}
            GamePreferences.Apply(settingsOriginal,false);confirmingDisplay=false;
            if(settingsReturn=="hud") CloseRunBook(); else Show(settingsReturn);
        }
        void ApplySettings()
        {
            bool display=!Application.isMobilePlatform&&(settingsDraft.width!=settingsOriginal.width||settingsDraft.height!=settingsOriginal.height||settingsDraft.displayMode!=settingsOriginal.displayMode);
            GamePreferences.Apply(settingsDraft,!display);
            if(display){confirmingDisplay=true;displayDeadline=Time.unscaledTime+15;Render();}
            else {settingsOriginal=settingsDraft.Copy();message="설정을 저장했습니다.";Render();}
        }
        void ConfirmDisplay()
        {
            GamePreferences.Apply(settingsDraft,true);settingsOriginal=settingsDraft.Copy();confirmingDisplay=false;message="설정을 저장했습니다.";Render();
        }
        void RevertDisplay()
        {
            GamePreferences.Apply(settingsOriginal,false);settingsDraft=settingsOriginal.Copy();confirmingDisplay=false;message="이전 설정으로 복구했습니다.";Render();
        }
        void UpdatePreferencesUI()
        {
            if(Page=="settings"&&confirmingDisplay)
            {
                if(Time.unscaledTime>=displayDeadline)RevertDisplay();
                else if(countdown!=null)countdown.text=$"화면이 잘 보이나요?\n{Mathf.CeilToInt(displayDeadline-Time.unscaledTime)}초 뒤 이전 설정으로 돌아갑니다.";
            }
            if(fpsLabel==null)
            {
                fpsLabel=Label(root,"",.01f,.95f,.12f,.99f,16);fpsLabel.color=Color.white;
            }
            fpsLabel.transform.SetAsLastSibling();fpsLabel.gameObject.SetActive(GamePreferences.Current.showFps);
            fpsFrames++;fpsTime+=Time.unscaledDeltaTime;
            if(fpsTime>=.5f){fpsLabel.text=$"{fpsFrames/fpsTime:0} FPS";fpsTime=0;fpsFrames=0;}
        }
        void Settings()
        {
            if(settingsDraft==null)BeginSettings();
            string[] tabs={"그래픽","사운드","편의 기능","게임 메뉴"};
            for(int i=0;i<4;i++){int n=i;ActionButton(content,tabs[i],.145f+i*.18f,.755f,.315f+i*.18f,.835f,()=>{Tab=n;Render();},false,true,Tab==i);}
            Panel(content,.145f,.205f,.855f,.735f);
            if(Tab==0)
            {
                SettingChoice("그래픽 품질",new[]{"낮음","보통","높음"}[settingsDraft.quality],0,()=>settingsDraft.quality=(settingsDraft.quality+1)%3);
                int[] limits={30,60,120,-1};
                SettingChoice("프레임 제한",settingsDraft.frameLimit<0?"제한 없음":settingsDraft.frameLimit+" FPS",1,()=>settingsDraft.frameLimit=limits[(Array.IndexOf(limits,settingsDraft.frameLimit)+1)%limits.Length]);
                if(!Application.isMobilePlatform)
                {
                    SettingChoice("수직 동기화",OnOff(settingsDraft.vSync),2,()=>settingsDraft.vSync=!settingsDraft.vSync);
                    SettingChoice("화면 모드",settingsDraft.displayMode==0?"전체 화면":"창 모드",3,()=>settingsDraft.displayMode=1-settingsDraft.displayMode);
                    SettingChoice("해상도",settingsDraft.width==0?"디스플레이 기본":$"{settingsDraft.width} × {settingsDraft.height}",4,()=>
                    {
                        var sizes=Screen.resolutions.Select(r=>new Vector2Int(r.width,r.height)).Where(r=>r.x>=640&&r.y>=360).Append(new Vector2Int(1280,720)).Append(Vector2Int.zero).Distinct().OrderBy(r=>r.x).ThenBy(r=>r.y).ToArray();
                        int index=Array.IndexOf(sizes,new Vector2Int(settingsDraft.width,settingsDraft.height));var size=sizes[(index+1)%sizes.Length];settingsDraft.width=size.x;settingsDraft.height=size.y;
                    });
                }
                Label(content,Application.isMobilePlatform?"화면 크기는 기기에 맞춰 자동으로 조절됩니다.":"수직 동기화가 켜지면 모니터 주사율을 따릅니다.",.20f,.218f,.80f,.265f,16);
            }
            else if(Tab==1)
            {
                AudioSlider("전체 음량",settingsDraft.master,0,v=>settingsDraft.master=v);
                AudioSlider("배경음",settingsDraft.music,1,v=>settingsDraft.music=v);
                AudioSlider("전투 효과음",settingsDraft.effects,2,v=>settingsDraft.effects=v);
                AudioSlider("UI 효과음",settingsDraft.ui,3,v=>settingsDraft.ui=v);
                SettingChoice("음소거",OnOff(settingsDraft.muted),4,()=>{settingsDraft.muted=!settingsDraft.muted;GamePreferences.PreviewAudio(settingsDraft);});
            }
            else if(Tab==2)
            {
                SettingChoice("움직임 줄이기",OnOff(settingsDraft.reducedMotion),0,()=>settingsDraft.reducedMotion=!settingsDraft.reducedMotion);
                SettingChoice("프레임 수 표시",OnOff(settingsDraft.showFps),1,()=>settingsDraft.showFps=!settingsDraft.showFps);
                SettingChoice("다른 창으로 이동 시 일시정지",OnOff(settingsDraft.pauseOnFocusLoss),2,()=>settingsDraft.pauseOnFocusLoss=!settingsDraft.pauseOnFocusLoss);
                SettingChoice("다른 창으로 이동 시 음소거",OnOff(settingsDraft.muteOnFocusLoss),3,()=>settingsDraft.muteOnFocusLoss=!settingsDraft.muteOnFocusLoss);
                Label(content,"모든 버튼은 터치로도 사용할 수 있습니다.",.20f,.25f,.80f,.32f,18);
            }
            else
            {
                ActionButton(content,level!=null?"계속하기":"이전 화면",.30f,.62f,.70f,.70f,()=>
                {
                    GamePreferences.Apply(settingsOriginal,false);
                    if(level!=null)CloseRunBook();else Show(settingsReturn);
                },true);
                ActionButton(content,"출전 준비로 돌아가기",.30f,.515f,.70f,.595f,()=>RequestSessionAction("prepare"));
                ActionButton(content,"메인 화면으로 돌아가기",.30f,.41f,.70f,.49f,()=>RequestSessionAction("main"));
                ActionButton(content,"게임 종료",.30f,.305f,.70f,.385f,()=>RequestSessionAction("quit"));
                Label(content,"TAB: 탐험 기록  /  ESC: 설정 열기·뒤로",.20f,.22f,.80f,.28f,17);
            }
            Label(content,message,.26f,.155f,.74f,.20f,17);
            ActionButton(content,"뒤로",.15f,.065f,.34f,.15f,CancelSettings);
            ActionButton(content,"기본값",.40f,.065f,.60f,.15f,()=>{settingsDraft=GamePreferences.Defaults();GamePreferences.PreviewAudio(settingsDraft);message="적용을 누르면 기본 설정을 저장합니다.";Render();});
            ActionButton(content,"적용",.66f,.065f,.85f,.15f,ApplySettings,true);
            if(confirmingDisplay)
            {
                var shade=ImageAt(content,null,0,0,1,1,false);shade.color=new Color(0,0,0,.65f);shade.raycastTarget=true;
                Panel(content,.23f,.30f,.77f,.69f);
                countdown=Label(content,"",.27f,.46f,.73f,.63f,27);
                ActionButton(content,"유지",.30f,.34f,.48f,.43f,ConfirmDisplay,true);
                ActionButton(content,"되돌리기",.52f,.34f,.70f,.43f,RevertDisplay);
            }
            if(pendingSessionAction!=null)
            {
                var shade=ImageAt(content,null,0,0,1,1,false);shade.color=new Color(0,0,0,.7f);shade.raycastTarget=true;
                Panel(content,.20f,.26f,.80f,.73f);
                string destination=pendingSessionAction=="quit"?"게임을 종료할까요?":pendingSessionAction=="prepare"?"출전 준비로 돌아갈까요?":"메인 화면으로 돌아갈까요?";
                Label(content,destination,.24f,.57f,.76f,.67f,28);
                Label(content,level!=null?"현재 탐험은 종료되며 이 판은 이어할 수 없습니다.\n획득 골드와 저장된 영구 성장은 유지됩니다.":"적용하지 않은 설정 변경은 저장되지 않습니다.",.24f,.41f,.76f,.55f,21);
                ActionButton(content,"취소 / ESC",.28f,.30f,.48f,.39f,()=>{pendingSessionAction=null;Render();});
                ActionButton(content,"확인",.52f,.30f,.72f,.39f,ConfirmSessionAction,true);
            }
        }
        void RequestSessionAction(string action){pendingSessionAction=action;Render();}
        void ConfirmSessionAction()
        {
            string action=pendingSessionAction;
            if(action==null || starting)return;
            starting=true;pendingSessionAction=null;
            GamePreferences.Apply(settingsOriginal,false);
            if(level!=null)level.AbandonRun();
            PlayerPrefs.Save();ownsPause=false;Time.timeScale=1;
            if(action=="quit")
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying=false;
#else
                Application.Quit();
#endif
            }
            else if(level!=null)ReturnToLobby(action=="prepare");
            else {starting=false;Show(action);}
        }
        static string OnOff(bool value)=>value?"켜짐":"꺼짐";
        void SettingChoice(string title,string value,int row,Action change)
        {
            float y=.63f-row*.077f;
            Label(content,title,.20f,y,.53f,y+.065f,23);
            ActionButton(content,value,.56f,y,.79f,y+.065f,()=>{change();message="변경 사항을 적용해 주세요.";Render();});
        }
        void AudioSlider(string title,float value,int row,Action<float> change)
        {
            float y=.63f-row*.077f;
            Label(content,title,.19f,y,.36f,y+.065f,23);
            var number=Label(content,$"{value*100:0}%",.70f,y,.78f,y+.065f,20);
            var sr=Rect(title+" slider",content,.39f,y,.69f,y+.065f);
            var hit=sr.gameObject.AddComponent<Image>();hit.color=Color.clear;hit.raycastTarget=true;
            var slider=sr.gameObject.AddComponent<Slider>();
            var track=ImageAt(sr,Config.sectionRibbon,0,.30f,1,.70f,false);track.type=Image.Type.Sliced;track.pixelsPerUnitMultiplier=12;
            var slideArea=Rect("Handle area",sr,.04f,0,.96f,1);
            var handle=ImageAt(slideArea,Config.inventorySlot,0,.05f,0,.95f,false);handle.rectTransform.sizeDelta=new Vector2(40,0);
            slider.handleRect=handle.rectTransform;slider.targetGraphic=handle;slider.SetValueWithoutNotify(value);
            slider.onValueChanged.AddListener(v=>{change(v);number.text=$"{v*100:0}%";GamePreferences.PreviewAudio(settingsDraft);});
            if(row==2||row==3)ActionButton(content,"듣기",.785f,y,.843f,y+.065f,()=>GameAudioManager.PlaySfx(row==2?GameAudioManager.GameSfxId.PlayerHit:GameAudioManager.GameSfxId.UiClick),clickSound:false);
        }
    }
}
