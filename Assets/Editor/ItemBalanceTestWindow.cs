using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Vampire.EditorTools
{
    // Editor assembly only: no cheat input or UI is compiled into player builds.
    public sealed class ItemBalanceTestWindow : EditorWindow
    {
        MerchantItemBlueprint[] catalog;
        Vector2 scroll;
        string search = "", message = "";
        bool showRetired;
        OctoberItemRuntime overridden;

        [MenuItem("Tools/QA/아이템 밸런스 테스트")]
        public static void Open() => GetWindow<ItemBalanceTestWindow>("아이템 테스트");

        void OnEnable()
        {
            catalog=Resources.LoadAll<MerchantItemBlueprint>("OctoberContent/Catalog").OrderBy(i=>i.octoberId).ToArray();
            EditorApplication.playModeStateChanged+=PlayModeChanged;
        }
        void OnDisable()
        {
            ResetClock();
            EditorApplication.playModeStateChanged-=PlayModeChanged;
        }
        void PlayModeChanged(PlayModeStateChange state)
        { if(state==PlayModeStateChange.ExitingPlayMode)ResetClock();Repaint(); }
        void ResetClock() { if(overridden!=null)overridden.SetDebugHour(-1);overridden=null; }
        void OnGUI()
        {
            EditorGUILayout.HelpBox("플레이 모드에서 사용합니다. 지급 효과는 현재 런에 적용됩니다. 조합을 초기화하려면 플레이를 다시 시작하세요.",MessageType.Info);
            var level=Object.FindObjectOfType<LevelManager>();
            var player=level!=null?level.PlayerCharacter:null;
            var runtime=player!=null?player.GetComponent<OctoberItemRuntime>():null;
            if(overridden!=null&&overridden!=runtime)ResetClock();
            bool active=EditorApplication.isPlaying&&player!=null&&player.IsAlive;
            using(new EditorGUI.DisabledScope(!active))
            {
                using(new EditorGUILayout.HorizontalScope())
                {
                    if(GUILayout.Button(EditorApplication.isPaused?"플레이 재개":"에디터 일시정지"))EditorApplication.isPaused=!EditorApplication.isPaused;
                    if(GUILayout.Button("런 골드 +1,000"))
                    {
                        var stats=Object.FindObjectOfType<StatsManager>();
                        if(stats!=null){stats.IncreaseCoinsGained(1000);message="현재 런에 골드 1,000 추가";}
                    }
                    if(GUILayout.Button("체력 회복"))player.GainHealth(player.MaxHealth);
                }
                EditorGUILayout.LabelField("아이템 시간대 테스트",EditorStyles.boldLabel);
                EditorGUILayout.LabelField("스폰·보스 시간은 그대로 유지됩니다. 창을 닫으면 실제 시간대로 복귀합니다.",EditorStyles.wordWrappedLabel);
                using(new EditorGUILayout.HorizontalScope())
                {
                    ClockButton(player,"낮 · 12시",12);ClockButton(player,"밤 · 18시",18);ClockButton(player,"마지막 · 23시",23);
                    if(GUILayout.Button("실제 시간"))ResetClock();
                }
                EditorGUILayout.LabelField("현재 설정",runtime!=null&&runtime.DebugHour>=0?runtime.DebugHour+"시 고정":"실제 런 시간");
            }
            search=EditorGUILayout.TextField("이름 / ID 검색",search);
            showRetired=EditorGUILayout.Toggle("판매 종료 아이템도 표시",showRetired);
            EditorGUILayout.LabelField(message,EditorStyles.wordWrappedLabel);
            scroll=EditorGUILayout.BeginScrollView(scroll);
            foreach(var item in catalog)
            {
                if(!showRetired&&!ItemOfferPolicy.CanOffer(item))continue;
                if(!string.IsNullOrWhiteSpace(search)&&!item.itemName.Contains(search)&&item.octoberId.ToString().IndexOf(search,System.StringComparison.Ordinal)<0)continue;
                using(new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField($"{item.octoberId:00} · {item.itemName}"+(ItemOfferPolicy.CanOffer(item)?"":" [판매 종료]"),EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"골드 {item.cost} / 실버 {item.silverCost}");
                    EditorGUILayout.LabelField(item.description,EditorStyles.wordWrappedLabel);
                    bool owned=runtime!=null&&runtime.Has(item.octoberId);
                    using(new EditorGUI.DisabledScope(!active||owned))
                    {
                        if(GUILayout.Button(owned?"보유 중":"현재 캐릭터에게 지급"))
                        {
                            runtime=OctoberItemRuntime.Get(player);
                            message=runtime.Give(item)?item.itemName+" 지급 완료":"이미 보유한 아이템입니다.";
                        }
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }
        void ClockButton(Character player,string label,int hour)
        {
            if(!GUILayout.Button(label))return;
            overridden=OctoberItemRuntime.Get(player);overridden.SetDebugHour(hour);
            message="시간대 변경 · 시계 충격파는 다음 업데이트에 1회 발동";
        }
    }
}
