using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Vampire
{
    // Text, icons, animation and hit targets are live UI, never flattened into the artwork.
    public sealed partial class ApothecaryUI : MonoBehaviour
    {
        public static ApothecaryUI Instance { get; private set; }
        public string Page { get; private set; }
        public int Tab { get; private set; }
        public ApothecaryUIConfig Config { get; private set; }
        RectTransform root, content, safe;
        TMP_FontAsset runtimeFont;
        TextMeshProUGUI silverLabel;
        Image portrait;
        CharacterBlueprint previewCharacter;
        Image fullScreenBackdrop;
        CharacterBlueprint character;
        int characterIndex, selection, pageIndex;
        bool dirty, starting, ownsPause, passed;
        float previousTime;
        string message = "";
        LevelManager level;
        StatsManager stats;
        readonly List<MerchantItemBlueprint> runItems = new List<MerchantItemBlueprint>();
        static readonly Color Ink = new Color(.25f,.10f,.08f);
        static readonly Color Paper = new Color(1,.91f,.76f,.85f);
        public const int ItemsPerPage = 6;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            Instance = null;
            prepareOnReturn = false;
            SceneManager.sceneLoaded -= Install;
            SceneManager.sceneLoaded += Install;
        }
        static void Install(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Additive || (scene.buildIndex != 0 && scene.buildIndex != 1)) return;
            var config = Resources.Load<ApothecaryUIConfig>("ApothecaryUIConfig");
            if (config == null) return;
            var ui = new GameObject("Apothecary UI").AddComponent<ApothecaryUI>();
            ui.Initialize(config, scene.buildIndex == 0);
        }
        public void Initialize(ApothecaryUIConfig config, bool lobby)
        {
            Instance = this;
            Config = config;
            if (config.font != null && config.font.sourceFontFile != null)
                runtimeFont = TMP_FontAsset.CreateFontAsset(config.font.sourceFontFile);
            level = FindObjectOfType<LevelManager>();
            stats = FindObjectOfType<StatsManager>();
            character = CrossSceneData.CharacterBlueprint != null ? CrossSceneData.CharacterBlueprint : config.characters.FirstOrDefault();
            characterIndex = Mathf.Max(0, Array.IndexOf(config.characters, character));
            if (EventSystem.current == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            if (lobby)
            {
                // Hide the old title/selector/shop canvas, retaining its data and scene references.
                foreach (Canvas canvas in FindObjectsOfType<Canvas>()) canvas.gameObject.SetActive(false);
                foreach (MainMenu menu in FindObjectsOfType<MainMenu>()) menu.enabled = false;
            }
            else
            {
                runItems.AddRange(CrossSceneData.StartingLobbyItems ?? Array.Empty<MerchantItemBlueprint>());
                foreach (MapPanelToggle toggle in FindObjectsOfType<MapPanelToggle>(true)) toggle.enabled = false;
                // Legacy map remains available inside the status page via its live texture.
                if (ExplorationMapSystem.Instance != null) ExplorationMapSystem.Instance.SetFullMapPanelVisible(false);
            }
            var canvasObject = new GameObject("Apothecary Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvasComponent = canvasObject.GetComponent<Canvas>();
            canvasComponent.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasComponent.sortingOrder = 200;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280,720);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            fullScreenBackdrop=ImageAt(canvasObject.transform,null,0,0,1,1,false);
            fullScreenBackdrop.color=new Color(.19f,.08f,.07f);fullScreenBackdrop.raycastTarget=true;
            safe = Rect("Safe Area", canvasObject.transform, 0,0,1,1);
            root = Rect("Landscape Frame", safe, .5f,.5f,.5f,.5f);
            root.sizeDelta = new Vector2(1280,720);
            SilverWallet.OnChanged += OnSilver;
            LobbyLoadoutData.OnChanged += OnLoadout;
            AudioListener.volume = GamePreferences.Current.muted ? 0 : GamePreferences.Current.master;
            Show(lobby ? "main" : "hud");
        }
        void OnDestroy()
        {
            SilverWallet.OnChanged -= OnSilver;
            LobbyLoadoutData.OnChanged -= OnLoadout;
            if (Instance == this) Instance = null;
            if (runtimeFont != null)
            {
                foreach (Texture2D atlas in runtimeFont.atlasTextures) if (atlas != null) Destroy(atlas);
                Destroy(runtimeFont.material); Destroy(runtimeFont);
            }
        }
        void OnSilver(int value) { if (silverLabel != null) silverLabel.text = $"실버  {value:N0}"; dirty = true; }
        void OnLoadout() { dirty = true; }
        void Update()
        {
            UpdateSkillUI();
            if (safe == null) return;
            UpdatePreferencesUI();
            Rect area = Screen.safeArea;
            safe.anchorMin = new Vector2(area.xMin / Screen.width,area.yMin / Screen.height);
            safe.anchorMax = new Vector2(area.xMax / Screen.width,area.yMax / Screen.height);
            float scale = Mathf.Min(safe.rect.width / 1280, safe.rect.height / 720);
            root.localScale = Vector3.one * scale;
            if (dirty && !starting) { dirty = false; Render(); }
            if (portrait != null && portrait.GetComponentInParent<CharacterIdlePreview>() == null && previewCharacter != null && !(Page == "result" && !passed))
            {
                Sprite[] frames = previewCharacter.idleSpriteSequence;
                if (frames != null && frames.Length > 0)
                    portrait.sprite = frames[(int)(Time.unscaledTime / Mathf.Max(.05f, previewCharacter.idleFrameTime)) % frames.Length];
            }
            if (level != null && GameInput.GetKeyDown(KeyCode.Tab))
            {
                if (Page == "hud") OpenRunBook();
                else if (Page == "run") CloseRunBook();
            }
            if (GameInput.GetKeyDown(KeyCode.Escape)) Back();
        }
        public void Show(string page, int tab = 0)
        {
            if(page=="settings" && Page!="settings")BeginSettings();
            Page = page; Tab = tab; selection = pageIndex = 0; message = "";
            Render();
        }
        public void Back()
        {
            if (starting || Page == "result" || (level != null && level.PlayerCharacter != null && level.PlayerCharacter.Skills != null && level.PlayerCharacter.Skills.IsCutin)) return;
            if(Page=="settings"){CancelSettings();return;}
            if (Page == "run") { CloseRunBook(); return; }
            if (Page == "hud") { OpenRunBook(); return; }
            if (Page == "main") Show("exit"); else Show("main");
        }
        void Render()
        {
            if (content != null) { content.gameObject.SetActive(false); Destroy(content.gameObject); }
            ClearSkillUI();
            portrait = null; previewCharacter = character; silverLabel = null;
            content = Rect("Page " + Page, root,0,0,1,1);
            fullScreenBackdrop.gameObject.SetActive(Page!="hud");
            if (Page == "hud")
            {
                BuildSkillHud();
                ActionButton(content,"상태 / TAB", .81f,.87f,.97f,.97f, OpenRunBook);
                return;
            }
            var blocker=content.gameObject.AddComponent<Image>();blocker.color=Color.clear;blocker.raycastTarget=true;
            ImageAt(content,Page == "main" ? Config.mainBackground : Page=="settings"||Page=="exit"?Config.panelBackground:Config.bookBackground,0,0,1,1,false);
            if (Page == "main") { Main(); return; }
            string title = Page == "prepare" ? "출전 준비" : Page == "unlock" ? "잠금 해제" : Page == "run" ? "탐험 기록" : Page == "result" ? (passed ? "스테이지 클리어!" : "탐험 실패") : Page == "exit" ? "게임 종료" : "설정";
            Label(content,title,.30f,.865f,.70f,.965f,34);
            if (Page == "prepare" || Page == "unlock")
                silverLabel = Label(content,$"실버  {SilverWallet.Silver:N0}",.72f,.885f,.90f,.95f,22);
            if (Page == "prepare") Prepare();
            else if (Page == "unlock") Unlock();
            else if (Page == "run") RunBook();
            else if (Page == "result") Result();
            else if (Page == "settings") Settings();
            else if (Page == "exit")
            {
                Label(content,"게임을 종료할까요?",.2f,.45f,.8f,.65f,32);
                ActionButton(content,"돌아가기",.24f,.25f,.48f,.37f,()=>Show("main"));
                ActionButton(content,"종료",.52f,.25f,.76f,.37f,Application.Quit,true);
            }
        }
        void Main()
        {
            portrait = IdlePreview(character,.41f,.395f,.59f,.665f);
            string[] labels = {"게임 시작","잠금 해제","설정","종료"};
            string[] pages = {"prepare","unlock","settings","exit"};
            for (int i=0;i<4;i++) { int n=i; ActionButton(content,labels[i],.355f,.29f-i*.078f,.645f,.36f-i*.078f,()=>Show(pages[n]),i==0); }
        }
        public static Sprite CharacterSprite(CharacterBlueprint c)
        {
            if (c == null) return null;
            if (c.idleSpriteSequence != null && c.idleSpriteSequence.Length > 0) return c.idleSpriteSequence[0];
            return c.walkSpriteSequence != null && c.walkSpriteSequence.Length > 0 ? c.walkSpriteSequence[0] : null;
        }
        public static Sprite CharacterProfile(CharacterBlueprint c) => c != null && c.profileSprite != null ? c.profileSprite : CharacterSprite(c);
        public CharacterBlueprint SelectedCharacter => character;
        public CharacterBlueprint PreviewCharacter => previewCharacter;
        public Image CharacterPreview => portrait;
        Image IdlePreview(CharacterBlueprint data,float x,float y,float right,float top)
        {
            var holder=Rect("Character idle preview",content,x,y,right,top);
            var preview=holder.gameObject.AddComponent<CharacterIdlePreview>();preview.Bind(data);return preview.Image;
        }
        bool Owned(CharacterBlueprint c) => c != null && LobbyUnlockSave.IsUnlocked("Character",c.name,c.owned);
        void ChangeCharacter(int direction)
        {
            characterIndex = (characterIndex + direction + Config.characters.Length) % Config.characters.Length;
            character = Config.characters[characterIndex]; Render();
        }
        void Prepare()
        {
            character = Config.characters[characterIndex];
            previewCharacter = character;
            ImageAt(content,Config.characterStage,.175f,.55f,.435f,.85f);
            portrait = IdlePreview(character,.215f,.59f,.395f,.82f);
            Ribbon(character.name,.19f,.51f,.42f,.57f,26);
            ActionButton(content,"<",.13f,.60f,.195f,.70f,()=>ChangeCharacter(-1));
            ActionButton(content,">",.405f,.60f,.47f,.70f,()=>ChangeCharacter(1));
            Panel(content,.135f,.335f,.305f,.50f); Panel(content,.315f,.335f,.47f,.50f);
            Label(content,$"기본 능력치\n체력 {character.hp:0}  방어 {character.armor}\n이동 {character.movespeed:0.##}  행운 {character.luck:0.##}",.15f,.35f,.29f,.48f,18);
            ProfileSkills(character,.323f,.345f,.462f,.492f);
            Label(content,character.description,.13f,.30f,.48f,.332f,16);
            // Exactly one row: relic, item 1, item 2. No quantities or second row.
            RelicBlueprint equipped = Config.relics.FirstOrDefault(r=>RelicSaveData.IsEquipped(r.relicId));
            Slot(.13f,"유물",equipped != null ? equipped.icon : null,equipped != null ? equipped.relicName : "미장착",()=>SwitchTab(0));
            for(int i=0;i<2;i++)
            {
                var item = i < LobbyLoadoutData.SelectedCarryItems.Count ? LobbyLoadoutData.SelectedCarryItems[i] : null;
                Slot(.25f+i*.12f,"아이템 "+(i+1),item != null ? item.itemIcon : null,item != null ? item.itemName : "빈 슬롯",()=>SwitchTab(1));
            }
            ActionButton(content,"유물",.52f,.75f,.69f,.825f,()=>SwitchTab(0),false,true,Tab==0);
            ActionButton(content,"아이템",.71f,.75f,.88f,.825f,()=>SwitchTab(1),false,true,Tab==1);
            if (Tab==0) RelicSelection(); else ItemSelection();
            Label(content,message,.52f,.155f,.88f,.198f,16).color=new Color(1,.96f,.81f);
            ActionButton(content,"뒤로",.12f,.07f,.27f,.14f,()=>Show("main"));
            ActionButton(content,Owned(character)?"출전하기":"캐릭터 잠금 해제 필요",.53f,.07f,.88f,.15f,StartRun,true,Owned(character)&&!starting);
        }
        void Slot(float x,string kind,Sprite icon,string name,Action click)
        {
            var b=ActionButton(content,"",x,.16f,x+.105f,.295f,click);
            SlotArt(b);
            var visual=b.transform.Find("Visual");
            ImageAt(visual,icon,.10f,.23f,.90f,.86f);
            var caption=ImageAt(visual,null,.06f,.03f,.94f,.23f,false);caption.color=new Color(1,.91f,.74f,.95f);
            var heading=ImageAt(visual,null,.08f,.81f,.92f,.98f,false);heading.color=new Color(.48f,.13f,.10f,.95f);
            Label(visual,kind,.06f,.81f,.94f,.98f,12).color=new Color(1,.97f,.85f);
            Label(visual,name,.06f,.03f,.94f,.23f,12);
        }
        void SwitchTab(int tab) { Tab=tab; selection=pageIndex=0;message="";Render(); }
        void RelicSelection()
        {
            var available=Config.relics.Where(r=>RelicSaveData.IsUnlocked(r.relicId)).ToArray();
            if(available.Length==0)
            {
                Label(content,"보유한 유물이 없습니다.\n잠금 해제에서 유물을 확인하세요.",.54f,.4f,.86f,.62f,23);
                ActionButton(content,"잠금 해제",.56f,.26f,.85f,.34f,()=>Show("unlock",1)); return;
            }
            selection=Mathf.Clamp(selection,0,available.Length-1);
            Grid(available.Length,i=>available[i].icon,i=>available[i].relicName,.52f,.57f,.88f,.73f,1);
            var relic=available[selection];
            Panel(content,.52f,.20f,.88f,.55f);
            ImageAt(content,relic.icon,.54f,.33f,.69f,.53f);
            Label(content,relic.relicName,.68f,.44f,.86f,.52f,25);
            Label(content,RelicDescription(relic),.68f,.34f,.86f,.44f,21);
            bool equipped=RelicSaveData.IsEquipped(relic.relicId);
            ActionButton(content,equipped?"장착 중":"유물 장착",.57f,.205f,.84f,.28f,()=>{RelicSaveData.Equip(relic.relicId);message="유물을 장착했습니다.";Render();},true,!equipped);
        }
        void ItemSelection()
        {
            var items=Config.items.Where(i=>i!=null&&i.canBuyInLobby).ToArray();
            if(items.Length==0) { Label(content,"구매 가능한 아이템이 없습니다.",.53f,.4f,.88f,.65f,22);return; }
            selection=Mathf.Clamp(selection,0,items.Length-1);
            Panel(content,.52f,.20f,.88f,.355f);
            Grid(items.Length,i=>items[i].itemIcon,i=>items[i].itemName,.52f,.43f,.88f,.73f);
            var item=items[selection];
            bool full=LobbyLoadoutData.SelectedCarryItems.Count>=2, bought=LobbyLoadoutData.IsEquipped(item), afford=SilverWallet.CanSpend(item.silverCost);
            Label(content,item.description,.53f,.29f,.88f,.355f,18);
            string label=full?"구매 불가 · 2 / 2":bought?"구매 완료":!afford?"실버 부족":$"구매하기 · {item.silverCost} 실버";
            ActionButton(content,label,.55f,.205f,.86f,.28f,()=>{LobbyLoadoutData.TryBuyAndEquip(item,item.silverCost,out message);Render();},true,!full&&!bought&&afford);
        }
        public void StartRun()
        {
            if(starting || !Owned(character)) return;
            starting=true;
            CrossSceneData.CharacterBlueprint=character;
            CrossSceneData.StartingLobbyItems=LobbyLoadoutData.ConsumeSelectedCarryItems();
            Time.timeScale=1;
            SceneManager.LoadScene(1);
        }
        void Unlock()
        {
            string[] tabs={"캐릭터","유물","아이템","증강"};
            for(int i=0;i<4;i++) {int t=i;ActionButton(content,tabs[i],.12f+i*.195f,.745f,.30f+i*.195f,.825f,()=>SwitchTab(t),false,true,Tab==i);}
            string title="",description="",action="해금 완료";Sprite icon=null; Action unlock=null;bool enabled=false;
            if(Tab==0 && Config.characters.Length>0)
            {
                selection=Mathf.Clamp(selection,0,Config.characters.Length-1);
                Grid(Config.characters.Length,i=>CharacterProfile(Config.characters[i]),i=>Config.characters[i].name+(Owned(Config.characters[i])?"":" · 잠김"),.12f,.27f,.56f,.70f);
                var c=Config.characters[selection];title=c.name;description=c.description;icon=CharacterSprite(c);
                previewCharacter=c;
                bool owned=Owned(c);action=owned?"선택하고 출전 준비":SilverWallet.CanSpend(c.cost)?$"잠금 해제 · {c.cost} 실버":$"실버 부족 · {c.cost}";enabled=owned||SilverWallet.CanSpend(c.cost);
                unlock=()=>{if(Owned(c)){character=c;characterIndex=Array.IndexOf(Config.characters,c);CrossSceneData.CharacterBlueprint=c;Show("prepare");return;}if(SilverWallet.TrySpend(c.cost)){LobbyUnlockSave.Unlock("Character",c.name);message="새로운 동료를 해금했습니다.";}Render();};
            }
            else if(Tab==1 && Config.relics.Length>0)
            {
                selection=Mathf.Clamp(selection,0,Config.relics.Length-1);
                Grid(Config.relics.Length,i=>Config.relics[i].icon,i=>Config.relics[i].relicName,.12f,.27f,.56f,.70f);
                var r=Config.relics[selection];title=r.relicName;description=RelicDescription(r);icon=r.icon;
                bool owned=RelicSaveData.IsUnlocked(r.relicId);action=owned?"해금 완료":SilverWallet.CanSpend(r.price)?$"잠금 해제 · {r.price} 실버":$"실버 부족 · {r.price}";enabled=!owned&&SilverWallet.CanSpend(r.price);
                unlock=()=>{if(!RelicSaveData.IsUnlocked(r.relicId)&&SilverWallet.TrySpend(r.price)){RelicSaveData.Unlock(r.relicId);message="출전 준비에서 장착할 수 있습니다.";}Render();};
            }
            else if(Tab==2 && Config.items.Length>0)
            {
                selection=Mathf.Clamp(selection,0,Config.items.Length-1);
                Grid(Config.items.Length,i=>Config.items[i].itemIcon,i=>Config.items[i].itemName,.12f,.27f,.56f,.70f);
                var item=Config.items[selection];title=item.itemName;description=item.description+"\n출전 준비에서 실버로 구매합니다.";icon=item.itemIcon;action="사용 가능";
            }
            else if(Tab==3 && Config.augments.Length>0)
            {
                selection=Mathf.Clamp(selection,0,Config.augments.Length-1);
                Grid(Config.augments.Length,i=>Config.augments[i].icon,i=>Config.augments[i].title,.12f,.27f,.56f,.70f);
                var a=Config.augments[selection];title=a.title;description=a.description;icon=a.icon;action="플레이 중 획득";
            }
            Panel(content,.595f,.22f,.88f,.70f);
            Label(content,title,.61f,.63f,.86f,.70f,27);
            if(Tab==0) portrait=IdlePreview(previewCharacter,.63f,.43f,.85f,.63f);
            else ImageAt(content,icon,.68f,.43f,.80f,.62f);
            if(Tab==0) { ProfileSkills(previewCharacter,.63f,.305f,.85f,.445f); }
            else Label(content,description,.615f,.29f,.86f,.43f,18);
            ActionButton(content,action,.615f,.22f,.86f,.29f,()=>unlock?.Invoke(),true,enabled);
            Label(content,message,.34f,.12f,.87f,.20f,18);
            ActionButton(content,"메인으로",.12f,.07f,.30f,.15f,()=>Show("main"));
        }
        void Grid(int count,Func<int,Sprite> icon,Func<int,string> name,float x,float y,float right,float top,int rows=2)
        {
            int capacity=3*rows;
            int pages=Mathf.Max(1,Mathf.CeilToInt(count/(float)capacity));pageIndex=Mathf.Clamp(pageIndex,0,pages-1);
            float w=(right-x)/3,h=(top-y)/rows;
            for(int j=0;j<capacity;j++)
            {
                int i=pageIndex*capacity+j;if(i>=count)break;
                float left=x+(j%3)*w,bottom=top-(j/3+1)*h;
                var button=ActionButton(content,"",left+.003f,bottom+.009f,left+w-.01f,bottom+h-.009f,()=>{selection=i;Render();},false,true,selection==i);
                SlotArt(button);
                var visual=button.transform.Find("Visual");
                ImageAt(visual,icon(i),.07f,.25f,.93f,.93f);
                var caption=ImageAt(visual,null,.07f,.03f,.93f,.23f,false);caption.color=new Color(1,.91f,.74f,.95f);
                Label(visual,name(i),.07f,.03f,.93f,.23f,15);
            }
            if(pages>1)
            {
                ActionButton(content,"<",x,y-.065f,x+.07f,y-.003f,()=>{pageIndex--;Render();},false,pageIndex>0);
                Label(content,$"{pageIndex+1} / {pages}",x+.08f,y-.065f,right-.08f,y,16);
                ActionButton(content,">",right-.07f,y-.065f,right,y-.003f,()=>{pageIndex++;Render();},false,pageIndex<pages-1);
            }
        }
        public void OpenRunBook()
        {
            if(level==null || level.IsLevelEnded || Time.timeScale==0 || Page!="hud")return;
            previousTime=Time.timeScale;ownsPause=true;Time.timeScale=0;Show("run");
        }
        public void CloseRunBook()
        {
            if(ownsPause && level!=null&&!level.IsLevelEnded)Time.timeScale=previousTime;
            ownsPause=false;Show("hud");
        }
        void RunBook()
        {
            string[] tabs={"상태","유물","아이템","도감"};
            for(int i=0;i<4;i++){int t=i;ActionButton(content,tabs[i],.12f+i*.195f,.745f,.30f+i*.195f,.825f,()=>SwitchTab(t),false,true,Tab==i);}
            var player=level != null ? level.PlayerCharacter : null;
            if(Tab==0 && player!=null)
            {
                portrait=ImageAt(content,CharacterSprite(character),.18f,.59f,.30f,.73f);
                Label(content,$"{player.DisplayName}  Lv.{player.CurrentLevel}\nHP {player.CurrentHealth:0} / {player.MaxHealth:0}\n공격 x{player.DamageMultiplier:0.00}\n방어 {player.CurrentArmor:0} · 이동 {player.CurrentMoveSpeed:0.##}\n치명타 {player.CritChance*100:0}%",.13f,.20f,.37f,.44f,23);
                ProfileSkills(player.Blueprint,.14f,.455f,.36f,.60f);
                Label(content,"탐험 지도",.45f,.65f,.85f,.72f,26);
                var map=ExplorationMapSystem.Instance;
                if(map!=null && map.FullMapTexture!=null)
                {
                    var raw=Rect("Live exploration map",content,.46f,.24f,.83f,.65f).gameObject.AddComponent<RawImage>();raw.texture=map.FullMapTexture;raw.raycastTarget=false;
                    map.CopyBookMarkers(raw.rectTransform);
                }
                else Label(content,"지도를 준비하고 있습니다.",.43f,.3f,.86f,.6f,23);
            }
            else if(Tab==1)
            {
                var relic=Config.relics.FirstOrDefault(r=>RelicSaveData.IsEquipped(r.relicId));
                ImageAt(content,relic!=null?relic.icon:null,.40f,.43f,.60f,.69f);
                Label(content,relic!=null?relic.relicName+"\n"+RelicDescription(relic):"장착한 유물이 없습니다.",.23f,.24f,.77f,.43f,28);
            }
            else if(Tab==2)
            {
                Label(content,"이번 출전에 가져온 아이템",.22f,.62f,.78f,.7f,27);
                for(int i=0;i<runItems.Count;i++)
                {
                    float x=.20f+i*.32f;var item=runItems[i];ImageAt(content,item.itemIcon,x,.40f,x+.25f,.60f);
                    Label(content,item.itemName+"\n"+item.description,x,.24f,x+.27f,.40f,21);
                }
                if(runItems.Count==0)Label(content,"가져온 아이템이 없습니다.",.25f,.32f,.75f,.52f,25);
            }
            else if(Tab==3)
            {
                var entries=AugmentHistoryManager.Instance != null ? AugmentHistoryManager.Instance.Entries : null;
                if(entries!=null&&entries.Count>0)Grid(entries.Count,i=>entries[i].icon,i=>entries[i].displayName+" Lv."+entries[i].level,.15f,.24f,.85f,.70f);
                else Label(content,"획득한 증강이 없습니다.",.23f,.35f,.77f,.60f,27);
            }
            Label(content,$"이번 탐험 골드  {(stats!=null?stats.CoinsGained:0):N0}",.55f,.08f,.85f,.15f,21);
            ActionButton(content,"닫기 / TAB",.12f,.07f,.32f,.15f,CloseRunBook);
            ActionButton(content,"설정",.35f,.07f,.49f,.15f,()=>Show("settings"));
        }
        public static bool TryShowResult(bool success)
        {
            if(Instance==null || Instance.level==null)return false;
            Instance.passed=success;Instance.ownsPause=false;Time.timeScale=0;Instance.Show("result");return true;
        }
        void Result()
        {
            bool isAshi=character==Config.characters.FirstOrDefault();
            ImageAt(content,Config.characterStage,.14f,.30f,.47f,.79f);
            portrait=ImageAt(content,!passed&&isAshi?Config.failureAshi:CharacterSprite(character),.17f,.35f,.46f,.75f);
            if(!passed&&!isAshi)portrait.rectTransform.localRotation=Quaternion.Euler(0,0,-75);
            Ribbon(character != null ? character.name : "",.19f,.29f,.44f,.36f,29);
            ProfileSkills(character,.22f,.19f,.43f,.29f);
            float time=level!=null?level.CurrentLevelTime:0;
            string values=$"생존 시간   {(int)time/60:00}:{(int)time%60:00}\n처치 몬스터   {(stats!=null?stats.MonstersKilled:0):N0}\n획득 골드   {(stats!=null?stats.CoinsGained:0):N0}\n도달 레벨   {(level!=null&&level.PlayerCharacter!=null?level.PlayerCharacter.CurrentLevel:1)}";
            Panel(content,.50f,.35f,.86f,.70f);Label(content,values,.53f,.38f,.83f,.68f,29);
            Label(content,passed?"위장 구역을 지켜냈어요!":"다시 힘을 모아 도전해요.",.52f,.23f,.85f,.31f,25);
            ActionButton(content,"출전 준비",.24f,.09f,.48f,.18f,()=>ReturnToLobby(true),true);
            ActionButton(content,"메인으로",.52f,.09f,.76f,.18f,()=>ReturnToLobby(false));
        }
        static bool prepareOnReturn;
        void ReturnToLobby(bool prepare) {prepareOnReturn=prepare;Time.timeScale=1;SceneManager.LoadScene(0);}
        void Start() {if(prepareOnReturn&&Page=="main"){prepareOnReturn=false;Show("prepare");}}
        public static string RelicDescription(RelicBlueprint r)
        {
            switch(r.effectType)
            {
                case RelicBlueprint.RelicEffectType.MaxHealth:return $"최대 체력 +{r.effectValue:0}";
                case RelicBlueprint.RelicEffectType.MoveSpeed:return $"이동 속도 +{r.effectValue:0.##}";
                default:return $"치명타 확률 +{r.effectValue*100:0.#}%";
            }
        }
        static RectTransform Rect(string name,Transform parent,float x,float y,float right,float top)
        {
            var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);
            r.anchorMin=new Vector2(x,y);r.anchorMax=new Vector2(right,top);r.offsetMin=r.offsetMax=Vector2.zero;return r;
        }
        Image ImageAt(Transform parent,Sprite sprite,float x,float y,float r,float t,bool aspect=true)
        {
            var image=Rect("Art",parent,x,y,r,t).gameObject.AddComponent<Image>();image.sprite=sprite;image.preserveAspect=aspect;
            image.raycastTarget=false;if(sprite==null&&aspect)image.color=Color.clear;return image;
        }
        void Panel(Transform parent,float x,float y,float r,float t)
        {
            var p=ImageAt(parent,Config.scrollPanel,x,y,r,t,false);p.type=Image.Type.Sliced;p.pixelsPerUnitMultiplier=7;
        }
        void Ribbon(string text,float x,float y,float r,float t,float size)
        {
            var p=ImageAt(content,Config.sectionRibbon,x,y,r,t,false);p.type=Image.Type.Sliced;p.pixelsPerUnitMultiplier=10;
            Label(content,text,x+.006f,y+.005f,r-.006f,t-.005f,size).color=new Color(1,.94f,.8f);
        }
        void SlotArt(Button button)
        {
            var body=button.transform.Find("Visual").GetComponent<Image>();body.sprite=Config.inventorySlot;body.type=Image.Type.Sliced;body.pixelsPerUnitMultiplier=16;
        }
        TextMeshProUGUI Label(Transform parent,string text,float x,float y,float r,float t,float size)
        {
            var label=Rect(text,parent,x,y,r,t).gameObject.AddComponent<TextMeshProUGUI>();
            label.font=runtimeFont!=null?runtimeFont:Config.font;label.text=text;label.fontSize=size;label.color=Ink;
            label.alignment=TextAlignmentOptions.Center;label.raycastTarget=false;label.enableAutoSizing=true;label.fontSizeMin=size*.8f;label.fontSizeMax=size;
            return label;
        }
        Button ActionButton(Transform parent,string text,float x,float y,float r,float t,Action action,bool primary=false,bool enabled=true,bool chosen=false,bool clickSound=true)
        {
            var hit=Rect("Button "+text,parent,x,y,r,t);var hitImage=hit.gameObject.AddComponent<Image>();hitImage.color=Color.clear;
            var button=hit.gameObject.AddComponent<Button>();button.targetGraphic=hitImage;button.transition=Selectable.Transition.None;button.interactable=enabled;
            var visual=Rect("Visual",hit,0,0,1,1);
            var body=visual.gameObject.AddComponent<Image>();body.sprite=primary?Config.primaryButton:Config.buttonBody;body.type=Image.Type.Sliced;body.pixelsPerUnitMultiplier=12;body.raycastTarget=false;
            var surface=Rect("Orbiting edge light",visual,0,0,1,1).gameObject.AddComponent<TitleMenuSurface>();
            surface.button=button;surface.hideIcon=true;surface.decorationOnly=true;surface.raycastTarget=false;
            var label=Label(visual,text,.04f,.04f,.96f,.96f,24);label.fontStyle=FontStyles.Bold;
            if(primary)label.color=new Color(1,.97f,.88f);
            var feedback=hit.gameObject.AddComponent<ApothecaryButtonFeedback>();feedback.button=button;feedback.visual=visual;feedback.body=body;feedback.sparkle=surface;feedback.primary=primary;feedback.chosen=chosen;
            button.onClick.AddListener(()=>{if(button.IsInteractable()){if(clickSound)GameAudioManager.PlaySfx(GameAudioManager.GameSfxId.UiClick);action?.Invoke();}});
            return button;
        }
    }
}
