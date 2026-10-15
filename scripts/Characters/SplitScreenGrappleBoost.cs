using BmSDK;
using BmSDK.BmGame;
using BmSDK.Engine;

namespace Etkramer.SplitScreen.Characters;

// Grapnel boost and dive bomb are Batman upgrades. Unlocking them switches on
// bCanGrappleBoost / bCanDiveBoost on Batman's pawn, which the engine checks; other
// characters (Robin etc.) never get them switched on. In co-op, give extra Robin
// players each one whenever P1 has it (with P1's grapple boost strength if theirs is
// unset). Not Nightwing or Catwoman: their grapple moves get stuck with it. Solo is
// untouched.
[Script]
public class SplitScreenGrappleBoost : Script
{
    static readonly HashSet<string> s_logged = [];

    public override void OnTick()
    {
        base.OnTick();

        var players = Game.GetEngine().GamePlayers;
        if (
            players.Count < 2
            || players[0]?.Actor?.Pawn is not RPawnPlayer p1
            || (!p1.bCanGrappleBoost && !p1.bCanDiveBoost)
        )
        {
            return;
        }

        for (var i = 1; i < players.Count; i++)
        {
            if (
                players[i]?.Actor is not Controller controller
                || controller.Pawn is not RPawnPlayer pawn
                || !IsRobin(controller)
            )
            {
                continue;
            }

            if (p1.bCanDiveBoost && !pawn.bCanDiveBoost)
            {
                pawn.bCanDiveBoost = true;
                if (s_logged.Add($"dive{i}:{pawn.GetType().Name}"))
                {
                    Debug.Log($"SplitScreenGrappleBoost: gave P{i + 1} ({pawn.GetType().Name}) dive bomb");
                }
            }

            if (!p1.bCanGrappleBoost || pawn.bCanGrappleBoost)
            {
                continue;
            }

            var factor = pawn.GrappleBoostFactor;
            pawn.bCanGrappleBoost = true;
            if (factor <= 0f)
            {
                pawn.GrappleBoostFactor = p1.GrappleBoostFactor;
            }

            if (s_logged.Add($"{i}:{pawn.GetType().Name}"))
            {
                Debug.Log(
                    $"SplitScreenGrappleBoost: gave P{i + 1} ({pawn.GetType().Name}) grapnel boost, "
                        + $"factor {factor} -> {pawn.GrappleBoostFactor} (P1 {p1.GrappleBoostFactor})"
                );
            }
        }
    }

    // Robin (story or challenge maps), going by the player's character slot
    private static bool IsRobin(Controller controller)
    {
        var gri = Game.GetGameRI();
        if (gri == null || Game.GetGameInfo() is not RGameInfo gameInfo)
        {
            return false;
        }

        var slot = gameInfo.GetPlayerCharacterIndex(controller);
        return slot >= 0
            && slot < gri.PlayerCharacters.Count
            && gri.PlayerCharacters[slot].CharacterName.ToString().StartsWith("Playable_Robin", StringComparison.OrdinalIgnoreCase);
    }
}
