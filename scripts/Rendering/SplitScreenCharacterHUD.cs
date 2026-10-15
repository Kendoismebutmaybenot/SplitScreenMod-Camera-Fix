using BmSDK;
using BmSDK.BmGame;
using BmSDK.Engine;

namespace Etkramer.SplitScreen.Rendering;

// An extra player's gadget bar loads their own character's gadgets, but is told to
// use P1's icon set (e.g. "Icons_BM" for Batman), so P2 as Robin got red question
// marks instead of Robin's icons. Point it at the player's own icon set
// ("Icons_" + their character acronym, e.g. "Icons_RB"). Solo is untouched.
[Script]
public class SplitScreenCharacterHUD : Script
{
    static readonly HashSet<string> s_logged = [];

    [Redirect(typeof(RHudExtensionGadgets), nameof(RHudExtensionGadgets.SetGadgetIconName))]
    private static void SetGadgetIconNameRedirect(RHudExtensionGadgets self, FString name)
    {
        var iconName = name.ToString();
        var index = PlayerIndex(self.RPC);
        var acronym = Acronym(index);
        if (
            index > 0
            && acronym.Length > 0
            && iconName.StartsWith("Icons_", StringComparison.OrdinalIgnoreCase)
            && !iconName.Equals($"Icons_{acronym}", StringComparison.OrdinalIgnoreCase)
        )
        {
            var ownIcons = $"Icons_{acronym}";
            if (s_logged.Add($"{index}:{ownIcons}"))
            {
                Debug.Log($"SplitScreenCharacterHUD: P{index + 1} gadget icons {iconName} -> {ownIcons}");
            }

            self.SetGadgetIconName(new FString(ownIcons));
            return;
        }

        self.SetGadgetIconName(name);
    }

    // The character acronym (BM, RB, CW, NW...) for this player's character slot
    private static string Acronym(int index)
    {
        var gri = Game.GetGameRI();
        var players = Game.GetEngine().GamePlayers;
        if (gri == null || index <= 0 || index >= players.Count || !GameState.IsMultiplayer())
        {
            return "";
        }

        var slot = index;
        if (players[index]?.Actor is Controller controller && Game.GetGameInfo() is RGameInfo gameInfo)
        {
            slot = gameInfo.GetPlayerCharacterIndex(controller);
        }

        if (slot < 0 || slot >= gri.PlayerCharacters.Count)
        {
            return "";
        }

        return gri.PlayerCharacters[slot].CharacterData?.CharacterAcronym.ToString() ?? "";
    }

    private static int PlayerIndex(Controller? controller)
    {
        var players = Game.GetEngine().GamePlayers;
        for (var i = 0; i < players.Count; i++)
        {
            if (controller != null && ReferenceEquals(players[i]?.Actor, controller))
            {
                return i;
            }
        }
        return -1;
    }
}
