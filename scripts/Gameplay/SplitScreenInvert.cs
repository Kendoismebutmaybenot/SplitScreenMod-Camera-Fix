using BmSDK;
using BmSDK.BmGame;
using BmSDK.Engine;

namespace Etkramer.SplitScreen.Gameplay;

// Extra players don't load P1's options, so their remote control batarang flew with
// inverted controls even when P1 had that option off. Copy P1's batarang invert
// setting to every other player. Solo is untouched.
[Script]
public class SplitScreenInvert : Script
{
    public override void OnTick()
    {
        base.OnTick();

        var players = Game.GetEngine().GamePlayers;
        if (players.Count < 2 || players[0]?.Actor is not PlayerController p1 || p1.PlayerInput is not RPlayerInput p1Input)
        {
            return;
        }

        var invert = p1Input.bInvertBatarang;
        for (var i = 1; i < players.Count; i++)
        {
            if (players[i]?.Actor is not PlayerController pc || pc.PlayerInput is not RPlayerInput input)
            {
                continue;
            }

            if (input.bInvertBatarang != invert)
            {
                input.bInvertBatarang = invert;
                Debug.Log($"SplitScreenInvert: P{i + 1} batarang invert set to {invert} (following P1)");
            }
        }
    }
}
