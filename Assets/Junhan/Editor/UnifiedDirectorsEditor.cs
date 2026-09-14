#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Vampire.Editor
{
    [CustomEditor(typeof(StageEventDirector))]
    public sealed class StageEventDirectorEditor : UnityEditor.Editor
    {
        private int eventTab;
        private static readonly string[] Names = { "감염 증식", "골드 러시", "위산 분비", "산성 역류", "연동운동", "커피수혈", "제산 반응" };
        private static readonly string[] Groups = { "basicEvents", "basicEvents", "basicEvents", "advancedEvents", "advancedEvents", "advancedEvents", "antacidEvents" };
        private static readonly string[] Lists = { "monsterSurgeEvents", "goldRushEvents", "acidSecretionEvents", "acidRefluxWaveEvents", "peristalsisDriftEvents", "coffeeTransfusionEvents", "" };
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox("스테이지 이벤트 7종을 이 컴포넌트에서 관리합니다. 기존 이벤트별 수치와 시작 시간은 보존됩니다.", MessageType.Info);
            eventTab = EditorGUILayout.Popup("편집할 이벤트", eventTab, Names);
            var group = serializedObject.FindProperty(Groups[eventTab]);
            if (group.arraySize == 0)
            {
                if (GUILayout.Button("이 이벤트 설정 추가")) group.arraySize = 1;
            }
            for (int i = 0; i < group.arraySize; i++)
            {
                var module = group.GetArrayElementAtIndex(i);
                var field = module.Copy();
                var end = module.GetEndProperty();
                bool enter = true;
                while (field.NextVisible(enter) && !SerializedProperty.EqualContents(field, end))
                {
                    enter = false;
                    if (System.Array.IndexOf(Lists, field.name) >= 0 && field.name != Lists[eventTab]) continue;
                    EditorGUILayout.PropertyField(field, true);
                }
                if (!string.IsNullOrEmpty(Lists[eventTab]) && GUILayout.Button(Names[eventTab] + " 일정 추가"))
                    module.FindPropertyRelative(Lists[eventTab]).arraySize++;
            }
            serializedObject.ApplyModifiedProperties();
        }
    }

    [CustomEditor(typeof(TimedSpecialMonsterSpawner))]
    public sealed class FieldSpawnsEditor : UnityEditor.Editor
    {
        private int tab;
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox("추가할 종류의 목록에서 +로 설정을 추가하세요. Module Enabled를 끄면 해당 설정만 멈춥니다. 접힌 항목의 기존 수치는 유지됩니다.", MessageType.Info);
            tab = GUILayout.Toolbar(tab, new[] { "특수 몬스터", "보스", "필드 오브젝트" });
            if (tab == 0)
            {
                Draw("timedSpawns", "시간표 / 함정 개수 유지");
                Draw("enzymeSpawns", "소화효소 생성");
                Draw("eliteSpawns", "엘리트 생성 단계");
                Draw("bloodClotSpawns", "혈전 생성 / 미니스테이지 입구");
            }
            else if (tab == 1)
            {
                Draw("miniBossSpawns", "미니보스 추가 시간표");
                Draw("bossSpawns", "최종보스 추가 시간표 / 테스트");
                Draw("levelBossSchedule", "레벨 데이터의 기본 보스 시간표");
            }
            else Draw("summonObjectSpawns", "엘리트 소환 오브젝트");
            serializedObject.ApplyModifiedProperties();
        }
        private void Draw(string property, string label) => EditorGUILayout.PropertyField(serializedObject.FindProperty(property), new GUIContent(label), true);
    }

    [CustomEditor(typeof(MiniStageCollectionRoom), true)]
    public sealed class CollectionRoomEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var kind = serializedObject.FindProperty("rewardKind");
            EditorGUILayout.PropertyField(kind, new GUIContent("수집 종류 (Gold / Experience)"));
            bool gold = kind.enumValueIndex == 0;
            DrawPropertiesExcluding(serializedObject, "m_Script", "rewardKind",
                gold ? "gemSpawnWeights" : "coinSpawnWeights",
                gold ? "fallbackExpGemPrefab" : "fallbackCoinPrefab",
                gold ? "m_Script" : "collectableDuringSpawn");
            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
