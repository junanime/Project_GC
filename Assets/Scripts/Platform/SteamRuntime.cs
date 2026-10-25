using System;
using System.Threading.Tasks;
using UnityEngine;
#if UNITY_STANDALONE_WIN
using Steamworks;
#endif

namespace Vampire
{
    [Serializable] public sealed class SteamReleaseSettings { public uint appId; }
    // Steam is opted into by the dedicated build, never by a local appid file.
    // Non-Steam desktop, WebGL and the editor retain their existing startup path.
    public sealed class SteamRuntime : MonoBehaviour
    {
        public const string TicketIdentity = "unityauthenticationservice";
        public static bool IsSteamBuild
        {
            get {
#if GC_STEAM && UNITY_STANDALONE_WIN && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }
        public static bool Ready { get; private set; }
        public static string AccountId { get; private set; }
        public static string Error { get; private set; }
        static bool attempted, nativeReady;
        float nextIdentityCheck;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { attempted = nativeReady = Ready = false; AccountId = Error = null; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot() { if (IsSteamBuild) EnsureInitialized(); }
        public static void EnsureInitialized()
        {
            if (!IsSteamBuild || attempted) return;
            attempted = true;
#if UNITY_STANDALONE_WIN
            try
            {
                var asset = Resources.Load<TextAsset>("SteamReleaseSettings");
                var config = asset != null ? JsonUtility.FromJson<SteamReleaseSettings>(asset.text) : null;
                if (config == null || config.appId == 0 || config.appId == 480) throw new InvalidOperationException("Missing production Steam App ID");
                if (SteamAPI.RestartAppIfNecessary(new AppId_t(config.appId))) { Error = "Steam에서 게임을 다시 시작하고 있습니다."; Application.Quit(); return; }
                if (!Packsize.Test() || !DllCheck.Test() || !SteamAPI.Init()) throw new InvalidOperationException("Steam initialization failed");
                nativeReady = true;
                if (SteamUtils.GetAppID().m_AppId != config.appId || !SteamApps.BIsSubscribed()) throw new InvalidOperationException("Steam application access mismatch");
                var account = SteamUser.GetSteamID();
                if (!account.IsValid()) throw new InvalidOperationException("Steam identity unavailable");
                AccountId = account.m_SteamID.ToString(System.Globalization.CultureInfo.InvariantCulture); Ready = true;
                var host = new GameObject("Steam lifecycle"); DontDestroyOnLoad(host); host.AddComponent<SteamRuntime>();
            }
            catch (Exception e) { Error = "Steam에 로그인한 뒤 라이브러리에서 게임을 실행해 주세요."; Debug.LogError("[Steam] " + e.GetType().Name); if (nativeReady) { SteamAPI.Shutdown(); nativeReady = false; } }
#endif
        }
        void Update()
        {
#if UNITY_STANDALONE_WIN
            if (!nativeReady) return;
            SteamAPI.RunCallbacks();
            if (Time.realtimeSinceStartup >= nextIdentityCheck)
            {
                nextIdentityCheck = Time.realtimeSinceStartup + 1;
                if (SteamUser.GetSteamID().m_SteamID.ToString() != AccountId)
                { Ready = false; Error = "Steam 계정이 변경되었습니다. 게임을 다시 실행해 주세요."; }
            }
#endif
        }
        void OnApplicationQuit()
        {
            ProgressPrefs.Save();
#if UNITY_STANDALONE_WIN
            if (nativeReady) SteamAPI.Shutdown();
#endif
            Ready = nativeReady = false;
        }
        public sealed class Ticket : IDisposable
        {
            public string Hex { get; private set; }
            readonly Action cancel;
            bool disposed;
            public Ticket(string hex, Action cancel) { Hex = hex; this.cancel = cancel; }
            public void Dispose() { if (disposed) return; disposed = true; cancel?.Invoke(); Hex = null; }
        }
        public static async Task<Ticket> GetTicketAsync()
        {
            EnsureInitialized();
#if UNITY_STANDALONE_WIN
            if (!Ready || !SteamUser.BLoggedOn()) throw new InvalidOperationException("Steam online authentication unavailable");
            var completion = new TaskCompletionSource<string>();
            HAuthTicket handle = HAuthTicket.Invalid;
            using (var callback = Callback<GetTicketForWebApiResponse_t>.Create(result =>
            {
                if (result.m_hAuthTicket != handle) return;
                if (result.m_eResult != EResult.k_EResultOK || result.m_cubTicket <= 0 || result.m_cubTicket > result.m_rgubTicket.Length)
                    completion.TrySetException(new InvalidOperationException("Steam ticket rejected"));
                else completion.TrySetResult(BitConverter.ToString(result.m_rgubTicket, 0, result.m_cubTicket).Replace("-", ""));
            }))
            {
                handle = SteamUser.GetAuthTicketForWebApi(TicketIdentity);
                try
                {
                    if (handle == HAuthTicket.Invalid) throw new InvalidOperationException("Steam ticket unavailable");
                    if (await Task.WhenAny(completion.Task, Task.Delay(20000)) != completion.Task) throw new TimeoutException("Steam ticket timed out");
                    string hex = await completion.Task;
                    if (!Ready) throw new InvalidOperationException("Steam identity changed");
                    return new Ticket(hex, () => { if (nativeReady) SteamUser.CancelAuthTicket(handle); });
                }
                catch { if (handle != HAuthTicket.Invalid && nativeReady) SteamUser.CancelAuthTicket(handle); throw; }
            }
#else
            await Task.CompletedTask;
            throw new InvalidOperationException("Steam authentication requires a Steam Windows build");
#endif
        }
    }
}
