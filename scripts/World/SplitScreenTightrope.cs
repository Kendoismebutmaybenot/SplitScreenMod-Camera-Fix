using BmSDK;
using BmSDK.BmGame;
using BmSDK.BmScript;
using BmSDK.Engine;

namespace Etkramer.SplitScreen.World;

// The line launcher tightrope wire assumes there is only one player: it checks
// whether P1 is on it and removes itself if not. So when P2 flips up onto their
// own wire it vanishes under them. While a wire made by an extra player runs its
// checks, we briefly put that player in P1's slot (same approach as the doors).
// Solo is untouched.
[Script]
public class SplitScreenTightrope : Script
{
    // Wire path name -> pawn that fired it
    static readonly Dictionary<string, Pawn> s_wireOwners = [];
    static readonly HashSet<string> s_logged = [];

    [Redirect(
        typeof(RSpecialMoveInstance_LineLauncher),
        nameof(RSpecialMoveInstance_LineLauncher.SpawnLineLancherWire),
        AllowSubtypes = true
    )]
    private static void SpawnLineLancherWireRedirect(RSpecialMoveInstance_LineLauncher self)
    {
        if (!GameState.IsMultiplayer())
        {
            self.SpawnLineLancherWire();
            return;
        }

        var known = Game.FindObjects<RHidePoint_LineLauncherWire>()
            .Select(w => w.GetPathName(null!))
            .ToHashSet();

        self.SpawnLineLancherWire();

        var pawn = self.Player;
        if (pawn == null)
        {
            return;
        }

        foreach (var wire in Game.FindObjects<RHidePoint_LineLauncherWire>())
        {
            var path = wire.GetPathName(null!);
            if (!path.Contains("Default__") && !known.Contains(path))
            {
                s_wireOwners[path] = pawn;
                Debug.Log($"SplitScreenTightrope: {path} fired by P{FindPawnIndex(pawn) + 1}");
            }
        }
    }

    [Redirect(typeof(RHidePoint_LineLauncherWire), nameof(RHidePoint_LineLauncherWire.Tick))]
    private static void TickRedirect(RHidePoint_LineLauncherWire self, float DeltaTime) =>
        AsWireOwner(self, "Tick", () => self.Tick(DeltaTime));

    [Redirect(typeof(RHidePoint_LineLauncherWire), nameof(RHidePoint_LineLauncherWire.BMLeft))]
    private static void BMLeftRedirect(RHidePoint_LineLauncherWire self) =>
        AsWireOwner(self, "BMLeft", self.BMLeft);

    [Redirect(
        typeof(RHidePoint_LineLauncherWire),
        nameof(RHidePoint_LineLauncherWire.DestroyLLWire)
    )]
    private static void DestroyLLWireRedirect(RHidePoint_LineLauncherWire self) =>
        AsWireOwner(self, "DestroyLLWire", self.DestroyLLWire);

    [Redirect(typeof(RHidePoint_LineLauncherWire), nameof(RHidePoint_LineLauncherWire.Destroyed))]
    private static void DestroyedRedirect(RHidePoint_LineLauncherWire self)
    {
        if (GameState.IsMultiplayer())
        {
            var path = self.GetPathName(null!);
            Debug.Log($"SplitScreenTightrope: {path} destroyed");
            s_wireOwners.Remove(path);
            s_logged.RemoveWhere(k => k.StartsWith(path));
        }

        self.Destroyed();
    }

    private static void AsWireOwner(RHidePoint_LineLauncherWire self, string fn, Action original)
    {
        if (!GameState.IsMultiplayer())
        {
            original();
            return;
        }

        var engine = Game.GetEngine();
        var path = self.GetPathName(null!);
        var index = s_wireOwners.TryGetValue(path, out var pawn) ? FindPawnIndex(pawn) : FindWalkerIndex(path);

        if (fn != "Tick" && s_logged.Add($"{path}:{fn}"))
        {
            Debug.Log($"SplitScreenTightrope: {fn} on {path} (owner P{index + 1})");
        }

        if (index <= 0 || index >= engine.GamePlayers.Count)
        {
            original();
            return;
        }

        var saved = engine.GamePlayers[0];
        engine.GamePlayers[0] = engine.GamePlayers[index];
        engine.GamePlayers[index] = saved;
        try
        {
            original();
        }
        finally
        {
            engine.GamePlayers[index] = engine.GamePlayers[0];
            engine.GamePlayers[0] = saved;
        }
    }

    // Fallback for wires we didn't see spawn: whoever was last on it
    private static int FindWalkerIndex(string wirePath)
    {
        var players = Game.GetEngine().GamePlayers;
        for (var i = 0; i < players.Count; i++)
        {
            if (
                players[i]?.Actor?.Pawn is RPawnPlayer player
                && player.LastWirePoint != null
                && player.LastWirePoint.GetPathName(null!) == wirePath
            )
            {
                return i;
            }
        }
        return -1;
    }

    private static int FindPawnIndex(Pawn pawn)
    {
        var players = Game.GetEngine().GamePlayers;
        for (var i = 0; i < players.Count; i++)
        {
            if (ReferenceEquals(players[i]?.Actor?.Pawn, pawn))
            {
                return i;
            }
        }
        return -1;
    }
}
