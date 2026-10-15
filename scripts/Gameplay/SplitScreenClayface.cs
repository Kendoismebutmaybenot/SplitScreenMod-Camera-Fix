using System.Numerics;
using BmSDK.BmGame;
using BmSDK.BmScript;

namespace Etkramer.SplitScreen.Gameplay;

// Clayface's mud men in the final fight pick "Batman" (P1) as their target when they
// spawn and keep chasing him, so they ignored P2. In co-op, each one goes after
// whichever player is closer, sticking with its current target unless the other
// player is clearly closer. Solo is untouched.
[Script]
public sealed class SplitScreenClayface : Script
{
    // How much closer another player has to be before a mud man switches to them
    const float SwitchMargin = 800f;

    static readonly HashSet<string> s_logged = [];

    [Redirect(typeof(RBMBehaviour_ClayfaceDrone), nameof(RBMBehaviour_ClayfaceDrone.OnActivate))]
    private static void DroneOnActivateRedirect(RBMBehaviour_ClayfaceDrone self)
    {
        self.OnActivate();
        Retarget(self.HostPawn, self.Batman, p => self.Batman = p);
    }

    [Redirect(typeof(RBMBehaviour_ClayfaceDrone), nameof(RBMBehaviour_ClayfaceDrone.Tick))]
    private static void DroneTickRedirect(RBMBehaviour_ClayfaceDrone self, float deltaTime)
    {
        Retarget(self.HostPawn, self.Batman, p => self.Batman = p);
        self.Tick(deltaTime);
    }

    [Redirect(typeof(RBMBehaviour_ClayfaceDroneSmall), nameof(RBMBehaviour_ClayfaceDroneSmall.OnActivate))]
    private static void DroneSmallOnActivateRedirect(RBMBehaviour_ClayfaceDroneSmall self)
    {
        self.OnActivate();
        Retarget(self.HostPawn, self.Batman, p => self.Batman = p);
    }

    private static void Retarget(RBMPawnAI? host, RPawnPlayer? current, Action<RPawnPlayer> set)
    {
        var players = Game.GetEngine().GamePlayers;
        if (players.Count < 2 || host == null)
        {
            return;
        }

        RPawnPlayer? closest = null;
        var closestDistance = float.MaxValue;
        var currentDistance = float.MaxValue;
        for (var i = 0; i < players.Count; i++)
        {
            if (players[i]?.Actor?.Pawn is not RPawnPlayer player || player.Health <= 0)
            {
                continue;
            }

            var distance = Vector3.Distance(host.Location, player.Location);
            if (ReferenceEquals(player, current))
            {
                currentDistance = distance;
            }

            if (distance < closestDistance)
            {
                closest = player;
                closestDistance = distance;
            }
        }

        if (closest == null || ReferenceEquals(closest, current) || closestDistance + SwitchMargin > currentDistance)
        {
            return;
        }

        set(closest);
        if (host is RPawnVillain villain)
        {
            villain.SetTargetPlayer(closest);
        }

        if (s_logged.Add(closest.Name.ToString()))
        {
            Debug.Log($"SplitScreenClayface: a mud man went after {closest.Name}");
        }
    }
}
