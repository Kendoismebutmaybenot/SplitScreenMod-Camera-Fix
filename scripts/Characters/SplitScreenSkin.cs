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
        var suitData = ExtraPlayerSuit(self, out var index);
        var gri = Game.GetGameRI();
        if (suitData == null || gri == null)
        {
            self.SetupPlayerMesh(bParam);
            return;
        }

        // Same character as P1: let the game's lookup find this player's suit instead
        var p1Slot = gri.PlayerCharacters[0];
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

    // When P1 wears a non-default suit, the game puts that suit on the player's
    // pawn again after spawning, which would cover up an extra player's own suit.
    // Only steps in when the game is putting P1's exact suit on an extra player.
    [Redirect(typeof(RPawnPlayer), nameof(RPawnPlayer.ApplyMeshData), AllowSubtypes = true)]
    private static void ApplyMeshDataRedirect(RPawnPlayer self, RAddContentPlayerCharacterMesh MeshData, bool InPlay)
    {
        var suitData = ExtraPlayerSuit(self, out var index);
        var gri = Game.GetGameRI();
        if (
            suitData != null
            && gri != null
            && MeshData != null
            && !ReferenceEquals(MeshData, suitData)
            && ReferenceEquals(MeshData, gri.PlayerCharacters[0].MeshData)
        )
        {
            if (s_logged.Add($"keep{index}:{MeshData.Name}"))
            {
                Debug.Log(
                    $"SplitScreenSkin: kept P{index + 1} as {suitData.Name} (game tried P1's {MeshData.Name})"
                );
            }
            MeshData = suitData;
        }

        self.ApplyMeshData(MeshData, InPlay);
    }

    // The suit an extra player should wear when they're the same character as P1,
    // or null when the game's own choice is already right
    private static RAddContentPlayerCharacterMesh? ExtraPlayerSuit(RPawnPlayer self, out int index)
    {
        index = -1;
        if (!GameState.IsMultiplayer() || SplitScreen.Instance == null)
        {
            return null;
        }

        var gri = Game.GetGameRI();
        index =
            self.Controller != null && Game.GetGameInfo() is RGameInfo gameInfo
                ? gameInfo.GetPlayerCharacterIndex(self.Controller)
                : s_spawningIndex;

        if (gri == null || index <= 0 || index >= gri.PlayerCharacters.Count)
        {
            return null;
        }

        var playerConfigs = (TomlArray)SplitScreen.Instance.Options["players"];
        var p1Slot = gri.PlayerCharacters[0];
        var ownSlot = gri.PlayerCharacters[index];
        if (index - 1 >= playerConfigs.Count)
        {
            return null;
        }

        if (!SameCharacter(ownSlot.CharacterName.ToString(), p1Slot.CharacterName.ToString()))
        {
            if (s_logged.Add($"diff{index}:{p1Slot.CharacterName}"))
            {
                Debug.Log(
                    $"SplitScreenSkin: P{index + 1} is {ownSlot.CharacterName}, P1 is {p1Slot.CharacterName} ({p1Slot.MeshName}), leaving to the game"
                );
            }
            return null;
        }

        var playerConfig = (TomlArray)playerConfigs[index - 1]!;
        var suitData = FindMeshData($"{(string)playerConfig[0]!}_{(string)playerConfig[1]!}");
        return suitData == null || suitData == p1Slot.MeshData ? null : suitData;
    }

    // Picking a suit can change P1's character name (e.g. Playable_Batman_Animated),
    // but the game still treats every player as that character, so count suits of
    // the same character as the same
    private static bool SameCharacter(string a, string b) =>
        a == b || a.StartsWith(b + "_") || b.StartsWith(a + "_");

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
