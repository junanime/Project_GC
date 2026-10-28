using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.Leaderboards;
using Unity.Services.Leaderboards.Models;
using Unity.Services.Leaderboards.Exceptions;

namespace Vampire
{
    public static class QaLeaderboard
    {
        public const string Board = "qa_s1_v1";
        [Serializable] public sealed class Row { public int rank; public string playerId, name; public double score; }
        [Serializable] public sealed class Snapshot { public List<Row> rows = new List<Row>(); public Row mine; public string owner, updated; }
        [Serializable] sealed class Pending { public string playerId, payload; }
        [Serializable] public sealed class Reply { public string runId, rejected; public int score; }
        static string CacheKey => "Leaderboard." + Board + (SteamRuntime.IsSteamBuild ? ".steam." + (SteamRuntime.AccountId ?? "unavailable") : "");
        static string PendingKey => CacheKey + ".pending";
        static readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
        static Task initialization;
        static string authenticatedId;
        static bool refreshing, cacheLoaded;
        static float nextRefresh;
        public static Snapshot Cache { get; private set; } = new Snapshot();
        public static string Status { get; private set; } = "랭킹 연결 준비 중";
        public static int Revision { get; private set; }
        public static string AccountNotice { get; private set; }
        public static string LastRejection { get; private set; }
        public static string PlayerId => UnityServices.State == ServicesInitializationState.Initialized && AuthenticationService.Instance.IsSignedIn ? AuthenticationService.Instance.PlayerId : Cache.owner;
        public static string Nickname => UnityServices.State == ServicesInitializationState.Initialized && AuthenticationService.Instance.IsSignedIn ? DisplayName(AuthenticationService.Instance.PlayerName) : "탐험가";
        public static string DisplayName(string value)
        {
            if (string.IsNullOrEmpty(value)) return "탐험가";
            int hash = value.LastIndexOf('#');
            return hash > 0 ? value.Substring(0, hash) : value;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { initialization = null; authenticatedId=null; AccountNotice=null; LastRejection=null; refreshing = false; cacheLoaded = false; nextRefresh = 0; Cache = new Snapshot(); Status = "랭킹 연결 준비 중"; Revision = 0; }
        static async Task Connect()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
                await UnityServices.InitializeAsync(new InitializationOptions().SetEnvironmentName("production"));
            await GameAuthentication.SignInAsync();
            authenticatedId=AuthenticationService.Instance.PlayerId;
            await AuthenticationService.Instance.GetPlayerNameAsync();
            if (!string.IsNullOrEmpty(AuthenticationService.Instance.LastNotificationDate))
            {
                var notices=await AuthenticationService.Instance.GetNotificationsAsync();
                AccountNotice=string.Join("\n\n",notices.Select(n=>n.Message+"\nCase: "+n.CaseId));
            }
        }
        public static async Task Ready()
        {
            SteamRuntime.EnsureInitialized();
            if (SteamRuntime.IsSteamBuild && !SteamRuntime.Ready) throw new InvalidOperationException("Steam identity unavailable");
            if (initialization == null || initialization.IsFaulted || initialization.IsCanceled) initialization = Connect();
            await initialization;
            if (!AuthenticationService.Instance.IsSignedIn) { initialization = Connect(); await initialization; }
        }
        public static void LoadCache()
        {
            if (cacheLoaded) return;
            cacheLoaded = true;
            try { Cache = JsonUtility.FromJson<Snapshot>(PlayerPrefs.GetString(CacheKey, "")) ?? new Snapshot(); }
            catch { Cache = new Snapshot(); }
            // JsonUtility can deserialize a null nested object as an empty Row.
            Cache.rows = (Cache.rows ?? new List<Row>()).Where(ValidRow).ToList();
            if (!ValidRow(Cache.mine)) Cache.mine = null;
            if (!string.IsNullOrEmpty(Cache.updated)) Status = "저장된 순위 · " + Cache.updated;
        }
        static bool ValidRow(Row row) => row != null && row.rank > 0 && !string.IsNullOrEmpty(row.playerId);
        static Row Convert(LeaderboardEntry e) => new Row { rank = e.Rank + 1, playerId = e.PlayerId, name = DisplayName(e.PlayerName), score = e.Score };
        public static List<Row> VisibleRows(Snapshot snapshot, string self)
        {
            var sorted = snapshot.rows.OrderBy(r => r.rank).GroupBy(r => r.playerId).Select(g => g.First()).ToList();
            if (sorted.Take(9).Any(r => r.playerId == self)) return sorted.Take(10).ToList();
            var result = sorted.Take(9).ToList();
            while (result.Count < 9) result.Add(null);
            result.Add(snapshot.mine);
            return result;
        }
        public static async void Refresh(bool force = false)
        {
            if (refreshing || (!force && Time.realtimeSinceStartup < nextRefresh)) return;
            refreshing = true; nextRefresh = Time.realtimeSinceStartup + 120;
            try
            {
                await gate.WaitAsync();
                try
                {
                    await Ready();
                    await FlushPending();
                    var top = await LeaderboardsService.Instance.GetScoresAsync(Board, new GetScoresOptions { Limit = 10 });
                    Row mine = null;
                    try { mine = Convert(await LeaderboardsService.Instance.GetPlayerScoreAsync(Board)); }
                    catch (LeaderboardsException e) when (e.Reason == LeaderboardsExceptionReason.EntryNotFound) { }
                    Cache = new Snapshot { rows = top.Results.Select(Convert).ToList(), mine = mine, owner = PlayerId, updated = DateTime.Now.ToString("MM/dd HH:mm") };
                    cacheLoaded = true;
                    PlayerPrefs.SetString(CacheKey, JsonUtility.ToJson(Cache)); PlayerPrefs.Save();
                    Status = "한 판 최고점 · " + Cache.updated;
                }
                finally { gate.Release(); }
            }
            catch (Exception e) { Status = string.IsNullOrEmpty(Cache.updated) ? "연결 대기 · 플레이 가능" : "저장된 순위 · " + Cache.updated; Debug.LogWarning("[QA ranking] Refresh unavailable: " + e.GetType().Name); }
            finally { refreshing = false; Revision++; }
        }
        static Task<Reply> Call(string action, string payload) => CloudCodeService.Instance.CallEndpointAsync<Reply>("QaRun", new Dictionary<string, object> { { "action", action }, { "payload", payload } });
        static async Task<bool> FlushPending()
        {
            string json = PlayerPrefs.GetString(PendingKey, "");
            if (string.IsNullOrEmpty(json)) return true;
            var pending = JsonUtility.FromJson<Pending>(json);
            if (pending.playerId != PlayerId) return false;
            var reply=await Call("submit", pending.payload);
            LastRejection=reply.rejected;
            PlayerPrefs.DeleteKey(PendingKey); PlayerPrefs.Save(); return true;
        }
        public static async Task<string> BeginRun()
        {
            await gate.WaitAsync();
            try
            {
                await Ready();
                if (!await FlushPending()) return null;
                string id = Guid.NewGuid().ToString("N");
                var reply = await Call("begin", "{\"runId\":\"" + id + "\"}");
                return reply.runId;
            }
            catch (Exception e) { Debug.LogWarning("[QA ranking] Offline run: " + e.GetType().Name); return null; }
            finally { gate.Release(); }
        }
        public static async Task<bool> Submit(string id, RunScoreData data)
        {
            await gate.WaitAsync();
            try
            {
                string payload = JsonUtility.ToJson(data).TrimEnd('}') + ",\"runId\":\"" + id + "\"}";
                PlayerPrefs.SetString(PendingKey, JsonUtility.ToJson(new Pending { playerId = authenticatedId, payload = payload })); PlayerPrefs.Save();
                await Ready();
                bool sent = await FlushPending(); nextRefresh = 0; return sent;
            }
            catch (Exception e) { Debug.LogWarning("[QA ranking] Submission pending: " + e.GetType().Name); return false; }
            finally { gate.Release(); Revision++; }
        }
        public static async Task<bool> SetNickname(string name)
        {
            name = (name ?? "").Trim();
            if (name.Length < 2 || name.Length > 12 || !name.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '-')) return false;
            await gate.WaitAsync();
            try { await Ready(); await AuthenticationService.Instance.UpdatePlayerNameAsync(name); nextRefresh = 0; return true; }
            catch { return false; }
            finally { gate.Release(); Revision++; }
        }
    }
}
