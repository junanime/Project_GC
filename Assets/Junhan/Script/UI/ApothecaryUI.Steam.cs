using UnityEngine;

namespace Vampire
{
    public sealed partial class ApothecaryUI
    {
        string SteamGuardPage(string requested)
        {
            if (!SteamRuntime.IsSteamBuild) return requested;
            SteamRuntime.EnsureInitialized();
            if (!string.IsNullOrEmpty(SteamRuntime.Error) || !string.IsNullOrEmpty(ProgressPrefs.Error)) return "platformError";
            if (ProgressPrefs.NeedsImportChoice) return "steamImport";
            return requested;
        }
        void SteamAccountPanel()
        {
            if (Page == "platformError")
            {
                OctoberFrame("실행 확인");
                var note = Label(content, SteamRuntime.Error ?? ProgressPrefs.Error, .18f, .38f, .82f, .69f, 25);
                note.richText = false;
                ActionButton(content, "게임 종료", .36f, .19f, .64f, .30f, Application.Quit, true);
                return;
            }
            OctoberFrame("탐험 기록 가져오기");
            Label(content, "이 PC에 이전 테스트 버전의 탐험 기록이 있습니다.\n현재 Steam 계정으로 재화·해금 기록을 가져올까요?\n\n기존 원본은 보관되며, 온라인 랭킹 기록은 이전되지 않습니다.", .18f, .37f, .82f, .72f, 24);
            ActionButton(content, "새로 시작", .22f, .18f, .47f, .30f, () => { ProgressPrefs.ChooseLegacyImport(false); Show("main"); });
            ActionButton(content, "기록 가져오기", .53f, .18f, .78f, .30f, () => { ProgressPrefs.ChooseLegacyImport(true); Show("main"); }, true);
        }
    }
}
