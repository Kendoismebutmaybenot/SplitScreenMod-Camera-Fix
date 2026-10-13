using BmSDK.BmGame;
using BmSDK.Engine;

namespace Etkramer.SplitScreen.Characters;

// When an extra player uses the same character as P1 (e.g. two Batmen), the
// game gives them P1's suit: RPawnPlayer.GetPlayerConfig finds the first
// RGameRI.PlayerCharacters slot with a matching character name, which is P1's.
// So we load each extra player's suit into their own slot, and while their pawn
// runs SetupPlayerMesh we briefly put it in slot 0 so the game builds them with
// it. (Swapping the mesh on a live pawn with ApplyMeshData crashed when it moved.)
[Script]
public class SplitScreenSkin : Script
{
    static readonly Dictionary<string, string> s_meshPaths = [];
    static readonly HashSet<string> s_failed = [];
    static readonly HashSet<string> s_logged = [];

    public override void OnTick()
    {
        base.OnTick();

        if (SplitScreen.Instance == null || Game.GetEngine().GamePlayers.Count < 2)
        {
            return;
        }

        // Don't load or look up packages while the game is loading or streaming
        var gri = Game.GetGameRI();
        if (gri == null || gri.IsAsyncLoading())
        {
            return;
        }

        // Keep each extra player's suit loaded in their own slot, ready for spawn
        var playerConfigs = (TomlArray)SplitScreen.Instance.Options["players"];
        for (var i = 0; i < playerConfigs.Count && i + 1 < gri.PlayerCharacters.Count; i++)
        {
            var slot = gri.PlayerCharacters[i + 1];
            var playerConfig = (TomlArray)playerConfigs[i]!;
            var meshName = $"{(string)playerConfig[0]!}_{(string)playerConfig[1]!}";

            // The game hasn't loaded this player's character yet, or it's already right
            if (slot.MeshData == null || slot.MeshData.Name.ToString() == meshName)
            {
                continue;
            }

            var meshData = FindMeshData(meshName);
            if (meshData == null || meshData == slot.MeshData)
            {
                continue;
            }

            slot.MeshData = meshData;
            gri.PlayerCharacters[i + 1] = slot;
        }
    }

    // Player slot of the controller whose pawn is being spawned right now. The pawn
    // builds its mesh during spawn, before it has a controller to ask.
    static int s_spawningIndex = -1;

    [Redirect(typeof(RGameInfo), nameof(RGameInfo.SpawnDefaultPawnFor), AllowSubtypes = true)]
    private static Pawn SpawnDefaultPawnForRedirect(RGameInfo self, Controller NewPlayer, NavigationPoint StartSpot)
    {
        if (!GameState.IsMultiplayer())
        {
            return self.SpawnDefaultPawnFor(NewPlayer, StartSpot);
        }

        var previous = s_spawningIndex;
        s_spawningIndex = NewPlayer != null ? self.GetPlayerCharacterIndex(NewPlayer) : -1;
        try
        {
            return self.SpawnDefaultPawnFor(NewPlayer, StartSpot);
        }
        finally
        {
            s_spawningIndex = previous;
        }
    }

    [Redirect(typeof(RPawnPlayer), nameof(RPawnPlayer.SetupPlayerMesh), AllowSubtypes = true)]
    private static void SetupPlayerMeshRedirect(RPawnPlayer self, bool bParam)
    {
        if (!GameState.IsMultiplayer() || SplitScreen.Instance == null)
        {
            self.SetupPlayerMesh(bParam);
            return;
        }

        var gri = Game.GetGameRI();
        var index =
            self.Controller != null && Game.GetGameInfo() is RGameInfo gameInfo
                ? gameInfo.GetPlayerCharacterIndex(self.Controller)
                : s_spawningIndex;

        if (gri == null || index <= 0 || index >= gri.PlayerCharacters.Count)
        {
            self.SetupPlayerMesh(bParam);
            return;
        }

        var playerConfigs = (TomlArray)SplitScreen.Instance.Options["players"];
        var p1Slot = gri.PlayerCharacters[0];
        var ownSlot = gri.PlayerCharacters[index];
        if (index - 1 >= playerConfigs.Count || ownSlot.CharacterName.ToString() != p1Slot.CharacterName.ToString())
        {
            self.SetupPlayerMesh(bParam);
            return;
        }

        var playerConfig = (TomlArray)playerConfigs[index - 1]!;
        var suitData = FindMeshData($"{(string)playerConfig[0]!}_{(string)playerConfig[1]!}");
        if (suitData == null || suitData == p1Slot.MeshData)
        {
            self.SetupPlayerMesh(bParam);
            return;
        }

        // Same character as P1: let the game's lookup find this player's suit instead
        var p1Mesh = p1Slot.MeshData;
        p1Slot.MeshData = suitData;
        gri.PlayerCharacters[0] = p1Slot;
        try
        {
            self.SetupPlayerMesh(bParam);
        }
        finally
        {
            p1Slot.MeshData = p1Mesh;
            gri.PlayerCharacters[0] = p1Slot;
        }

        var suit = suitData.Name.ToString();
        if (s_logged.Add(suit))
        {
            Debug.Log($"SplitScreenSkin: built P{index + 1} as {suit}");
        }
    }

    private static RAddContentPlayerCharacterMesh? FindMeshData(string meshName)
    {
        // Fast path: exact path found earlier
        if (s_meshPaths.TryGetValue(meshName, out var path))
        {
            var cached = Game.FindObject<RAddContentPlayerCharacterMesh>(path);
            if (cached != null)
            {
                return cached;
            }
        }

        if (s_failed.Contains(meshName))
        {
            return null;
        }

        var meshData = ScanLoaded(meshName);
        if (meshData == null)
        {
            // Not loaded yet - load its seek-free package once and retry
            Game.LoadPackage($"{meshName}_SF");
            meshData = ScanLoaded(meshName);
        }

        if (meshData == null)
        {
            s_failed.Add(meshName);
            Debug.LogWarning($"SplitScreenSkin: couldn't find mesh data for {meshName}");
            return null;
        }

        s_meshPaths[meshName] = meshData.GetPathName(null!);
        return meshData;
    }

    // Match by path name, since cooked packages don't always keep the outer we expect
    private static RAddContentPlayerCharacterMesh? ScanLoaded(string meshName) =>
        Game.FindObjects<RAddContentPlayerCharacterMesh>()
            .FirstOrDefault(m =>
            {
                var pathName = m.GetPathName(null!);
                return !pathName.Contains("Default__")
                    && pathName.Split('.').Any(part =>
                        part.Equals(meshName, StringComparison.OrdinalIgnoreCase)
                    );
            });
}
