using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Vampire
{
    /// <summary>Departure-only overlay: real async progress drives the route, scale and gauge.</summary>
    public sealed class StageEntryLoading : MonoBehaviour
    {
        public static StageEntryLoading Instance { get; private set; }
        public static bool IsLoading => Instance != null && Instance.running;
        public float Progress { get; private set; }
        public string CharacterKey { get; private set; }
        public RectTransform RunnerRect => runner != null ? runner.rectTransform : null;
        public float RunnerAlpha => runner != null ? runner.color.a : 0;
        const float ReadyProgress = .94f;
        static readonly Color Ink = new Color(.055f,.09f,.14f), Cream = new Color(1,.95f,.8f);
        RectTransform surface, frame, fill, tip;
        Image runner;
        Graphic shadow;
        TextMeshProUGUI heading, percentage;
        TMP_FontAsset font;
        Sprite[] frames;
        Image[] peepers;
        Sprite[][] peeperFrames;
        float elapsed, baseRunnerHeight;
        bool running, ownsPause, ownsFont;
        AsyncOperation operation;
        Action failed;
        StageEntryLoadingArt art;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { Instance = null; }

        public static bool Begin(CharacterBlueprint character, Action onFailure = null)
        {
            if (IsLoading) return false;
            if (!Application.CanStreamedLevelBeLoaded(1)) { Debug.LogError("[StageEntryLoading] Level 1 is not in the build."); return false; }
            StageEntryLoading view;
            try { view = Create(character); }
            catch (Exception error)
            {
                Debug.LogException(error);
                if (Instance != null) Destroy(Instance.gameObject);
                return false;
            }
            view.failed = onFailure;
            view.running = true;
            DontDestroyOnLoad(view.gameObject);
            view.StartCoroutine(view.Load());
            return true;
        }

        // Shared by editor visual QA; does not start a scene load or pause gameplay.
        public static StageEntryLoading Create(CharacterBlueprint character)
        {
            var view = new GameObject("Stage entry loading", typeof(RectTransform)).AddComponent<StageEntryLoading>();
            Instance = view;
            view.CharacterKey = OctoberArt.CharacterKey(character);
            view.Build();
            view.SetProgress(0);
            return view;
        }

        IEnumerator Load()
        {
            // Let the complete overlay render before starting IO / scene deserialization.
            yield return null;
            yield return null;
            try { operation = SceneManager.LoadSceneAsync(1, LoadSceneMode.Single); }
            catch (Exception error) { Debug.LogException(error); Fail(); yield break; }
            if (operation == null) { Fail(); yield break; }
            operation.allowSceneActivation = false;
            while (operation.progress < .9f || Progress < ReadyProgress - .0001f)
            {
                float target = NormalizedProgress(operation.progress);
                SetProgress(Mathf.MoveTowards(Progress, target, Time.unscaledDeltaTime * .8f));
                yield return null;
            }
            // Data is ready. Finish the final few steps into the opening, then activate Level 1.
            heading.text = "위장 진입 준비 완료";
            while (Progress < 1)
            {
                SetProgress(Mathf.MoveTowards(Progress, 1, Time.unscaledDeltaTime * .2f));
                yield return null;
            }
            ownsPause = true;
            Time.timeScale = 0; // No combat or timer elapses beneath the activation frame.
            operation.allowSceneActivation = true;
            while (!operation.isDone) yield return null;
            yield return null; // LevelManager.Start finishes while the game is still paused.
            running = false;
            RestorePause();
            Destroy(gameObject);
        }

        void Fail()
        {
            running = false;
            var callback = failed;
            Destroy(gameObject);
            callback?.Invoke();
        }
        void RestorePause()
        {
            if (!ownsPause) return;
            // Other pause owners must not be resumed by this overlay.
            if (!TutorialGuide.IsOpen && Time.timeScale == 0) Time.timeScale = 1;
            ownsPause = false;
        }
        void OnDestroy()
        {
            RestorePause();
            if (Instance == this) Instance = null;
            if (font != null && ownsFont)
            {
                foreach (var texture in font.atlasTextures) if (texture != null) Destroy(texture);
                Destroy(font.material); Destroy(font);
            }
        }

        public static float NormalizedProgress(float raw) => Mathf.Clamp01(raw / .9f) * ReadyProgress;
        public static Vector2 Route(float progress)
        {
            float t = Mathf.Clamp01(progress), u = 1-t;
            return u*u*u*new Vector2(275,215) + 3*u*u*t*new Vector2(750,232)
                 + 3*u*t*t*new Vector2(977,398) + t*t*t*new Vector2(1055,478);
        }
        public static float Perspective(float progress) => Mathf.Lerp(1,.07f,Mathf.Clamp01(progress));
        public void SetProgress(float progress)
        {
            Progress = Mathf.Clamp01(progress);
            if (fill == null) return;
            fill.anchorMax = new Vector2(Progress,1);
            tip.anchorMin = tip.anchorMax = new Vector2(Progress,.5f);
            percentage.text = Mathf.FloorToInt(Progress * 100) + "%";
            DrawRunner();
        }
        void Update()
        {
            elapsed += Time.unscaledDeltaTime;
            frame.localScale = Vector3.one * Mathf.Min(surface.rect.width/1280, surface.rect.height/720);
            DrawRunner();
            for (int i=0; i<peepers.Length; i++)
            {
                var sequence=peeperFrames[i];
                if (sequence == null || sequence.Length == 0) continue;
                peepers[i].sprite=sequence[GamePreferences.Current.reducedMotion ? 0 : (int)(elapsed/.22f+i)%sequence.Length];
                peepers[i].rectTransform.anchoredPosition = new Vector2(0,GamePreferences.Current.reducedMotion ? -4 : -6+Mathf.Sin(elapsed*1.6f+i*2)*5);
            }
        }
        void DrawRunner()
        {
            if (runner == null || frames == null || frames.Length == 0) return;
            bool reduced=GamePreferences.Current.reducedMotion;
            runner.sprite=frames[reduced ? 0 : (int)(elapsed/.085f)%frames.Length];
            float scale=Perspective(Progress);
            float hop=reduced ? 0 : Mathf.Abs(Mathf.Sin(elapsed*Mathf.PI/.34f))*4*scale;
            runner.rectTransform.anchoredPosition=Route(Progress)+Vector2.up*hop;
            runner.rectTransform.localScale=Vector3.one*scale;
            float alpha=1-Mathf.InverseLerp(.94f,1,Progress);
            runner.color=new Color(1,1,1,alpha);
            shadow.rectTransform.anchoredPosition=Route(Progress)+Vector2.up*2;
            shadow.rectTransform.localScale=new Vector3(scale*(1-hop/45),scale,1);
            shadow.color=new Color(.3f,.07f,.12f,.24f*alpha);
        }

        void Build()
        {
            art=Resources.Load<StageEntryLoadingArt>("StageEntryLoadingArt");
            if (art == null) throw new InvalidOperationException("StageEntryLoadingArt is missing.");
            frames=art.Runner(CharacterKey);
            if(frames==null || frames.Length!=8) throw new InvalidOperationException("Missing rear running frames for "+CharacterKey);
            var canvas=gameObject.AddComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.sortingOrder=32760;
            gameObject.AddComponent<GraphicRaycaster>();
            var scaler=gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1280,720);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            surface=(RectTransform)transform;
            Box("Letterbox",surface,Vector2.zero,Vector2.one,new Color(.18f,.07f,.13f)).raycastTarget=true;
            frame=Rect("1280x720 stage",surface,new Vector2(.5f,.5f),new Vector2(.5f,.5f));frame.sizeDelta=new Vector2(1280,720);
            var backdrop=Box("Soft stomach corridor",frame,Vector2.zero,Vector2.one,Color.white);backdrop.sprite=art.corridor;
            var source=Resources.Load<Font>("TrainingUI/Cafe24Ssurround");
            font=source!=null?TMP_FontAsset.CreateFontAsset(source):null;
            var fallback=Resources.Load<ApothecaryUIConfig>("ApothecaryUIConfig")?.font;
            ownsFont=font!=null;
            if(font==null)font=fallback;
            Label("Title","24시간의 사투",frame,new Rect(40,647,360,44),34,Cream,TextAlignmentOptions.Left);
            var badge=Plate("Stage badge",frame,new Rect(1005,638,233,53),new Color(.09f,.39f,.38f),Cream);
            Label("Stage","STAGE 01  ·  위장",badge,new Rect(0,0,233,53),22,Ink);

            peepers=new Image[3];peeperFrames=new[]{art.bacteria,art.slime,art.bacteria};
            Vector2[] positions={new Vector2(45,434),new Vector2(549,350),new Vector2(1170,410)};
            for(int i=0;i<3;i++)
            {
                var hole=Fixed("Wall crevice "+i,frame,new Rect(positions[i].x-35,positions[i].y-24,70,65));
                hole.gameObject.AddComponent<RectMask2D>();
                var image=Box("Existing scene monster",hole,new Vector2(.5f,0),new Vector2(.5f,0),Color.white);
                image.rectTransform.pivot=new Vector2(.5f,0);image.rectTransform.sizeDelta=new Vector2(63,65);image.preserveAspect=true;peepers[i]=image;
            }
            shadow=Rect("Runner ground shadow",frame,Vector2.zero,Vector2.zero).gameObject.AddComponent<StageEntryShadow>();
            shadow.raycastTarget=false;
            shadow.rectTransform.sizeDelta=new Vector2(83,12);
            runner=Box("Selected character rear run",frame,Vector2.zero,Vector2.zero,Color.white);
            runner.rectTransform.pivot=new Vector2(.5f,0);
            baseRunnerHeight=CharacterKey=="Hyuki"?164:CharacterKey=="Ari"?128:148;
            runner.rectTransform.sizeDelta=new Vector2(baseRunnerHeight*frames[0].rect.width/frames[0].rect.height,baseRunnerHeight);
            runner.preserveAspect=true;

            var panel=Plate("Mint medicine loading panel",frame,new Rect(150,23,980,139),Ink,new Color(.075f,.27f,.29f,.97f));
            heading=Label("Loading heading","위장으로 이동 중…",panel,new Rect(140,88,700,36),28,Cream);
            var track=Plate("Gauge gold rim",panel,new Rect(110,49,760,25),new Color(.91f,.73f,.36f),new Color(.025f,.16f,.2f));
            var inner=Fixed("Gauge content",track,new Rect(5,5,750,15));
            fill=Box("Actual load fill",inner,Vector2.zero,Vector2.one,new Color(.39f,.85f,.67f)).rectTransform;
            Box("Fill highlight",fill,new Vector2(0,.67f),Vector2.one,new Color(.91f,.96f,.66f));
            var tipImage=Box("Needle progress pointer",inner,new Vector2(0,.5f),new Vector2(0,.5f),Cream);
            tip=tipImage.rectTransform;tip.sizeDelta=new Vector2(32,24);
            tipImage.sprite=Resources.Load<ApothecaryUIConfig>("ApothecaryUIConfig")?.basicNeedle;tipImage.preserveAspect=true;
            percentage=Label("Progress percent","0%",panel,new Rect(883,44,70,34),19,Cream);
            Label("Tip","TIP   캐릭터와 시작 침은 따로 선택할 수 있어요.",panel,new Rect(100,11,780,27),18,new Color(.75f,.91f,.85f));
            // Small cream/gold corner stitches; no herb clutter or large ornaments.
            foreach(float x in new[]{18f,948f})foreach(float y in new[]{18f,107f})
                BoxAt("Corner stitch",panel,new Rect(x,y,14,3),new Color(.91f,.73f,.36f));
        }
        static RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max)
        {
            var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);
            rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;return rect;
        }
        static RectTransform Fixed(string name,Transform parent,Rect bounds)
        {var r=Rect(name,parent,Vector2.zero,Vector2.zero);r.pivot=Vector2.zero;r.anchoredPosition=bounds.position;r.sizeDelta=bounds.size;return r;}
        static Image Box(string name,Transform parent,Vector2 min,Vector2 max,Color color)
        {var image=Rect(name,parent,min,max).gameObject.AddComponent<Image>();image.color=color;image.raycastTarget=false;return image;}
        static Image BoxAt(string name,Transform parent,Rect bounds,Color color)
        {var image=Fixed(name,parent,bounds).gameObject.AddComponent<Image>();image.color=color;image.raycastTarget=false;return image;}
        static RectTransform Plate(string name,Transform parent,Rect bounds,Color border,Color inside)
        {
            var root=Fixed(name,parent,bounds);float w=bounds.width,h=bounds.height;
            BoxAt("Stepped outer rim",root,new Rect(3,0,w-6,h),border);BoxAt("Stepped side rim",root,new Rect(0,3,w,h-6),border);
            BoxAt("Inset",root,new Rect(4,4,w-8,h-8),inside);return root;
        }
        TextMeshProUGUI Label(string name,string text,Transform parent,Rect bounds,float size,Color color,TextAlignmentOptions align=TextAlignmentOptions.Center)
        {
            var label=Fixed(name,parent,bounds).gameObject.AddComponent<TextMeshProUGUI>();label.text=text;label.font=font;label.fontSize=size;
            label.alignment=align;label.color=color;label.raycastTarget=false;label.enableWordWrapping=false;return label;
        }
    }

    // Native UI geometry, so the contact shadow stays soft and oval at every perspective scale.
    public sealed class StageEntryShadow : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();var r=rectTransform.rect;const int segments=32;
            mesh.AddVert(r.center,color,Vector2.zero);
            for(int i=0;i<=segments;i++)
            {
                float angle=i*Mathf.PI*2/segments;
                mesh.AddVert(r.center+new Vector2(Mathf.Cos(angle)*r.width*.5f,Mathf.Sin(angle)*r.height*.5f),color,Vector2.zero);
                if(i>0)mesh.AddTriangle(0,i,i+1);
            }
        }
    }
}
