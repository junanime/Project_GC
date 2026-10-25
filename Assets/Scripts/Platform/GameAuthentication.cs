using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;

namespace Vampire
{
    public static class GameAuthentication
    {
        public static async Task SignInAsync()
        {
            var auth = AuthenticationService.Instance;
            if (!SteamRuntime.IsSteamBuild)
            { if (!auth.IsSignedIn) await auth.SignInAnonymouslyAsync(); return; }
            SteamRuntime.EnsureInitialized();
            if (!SteamRuntime.Ready) throw new InvalidOperationException("Steam identity unavailable");
            string profile = "steam_" + SteamRuntime.AccountId;
            if (auth.Profile != profile)
            {
                if (auth.IsSignedIn) auth.SignOut();
                auth.SwitchProfile(profile);
            }
            if (auth.IsSignedIn) return;
            // No anonymous fallback or implicit account linking. A rejected ticket
            // disables online ranking for that attempt, while offline play remains.
            using (var ticket = await SteamRuntime.GetTicketAsync())
                await auth.SignInWithSteamAsync(ticket.Hex, SteamRuntime.TicketIdentity);
        }
    }
}
