using BmSDK.BmGame;

namespace Etkramer.SplitScreen.World;

[ScriptComponent(AutoAttach = true)]
public sealed class RemoveSlowMoComponent : ScriptComponent<RGameInfo>
{
    /// <summary>
    /// The game speed the game last asked for, even when blocked here. Lets other scripts
    /// slow down just the thing the slow-mo was for (e.g. the remote control batarang).
    /// </summary>
    public static float RequestedSpeed { get; private set; } = 1.0f;

    /// <summary>
    /// Overrides the game's internal mechanism for time dilation. This redirect makes sure game
    /// speed is unchanged during multiplayer gameplay.
    /// </summary>
    [ComponentRedirect(nameof(RGameInfo.SetGameSpeed))]
    public void SetGameSpeed(float dt)
    {
        RequestedSpeed = dt;
        if (!GameState.IsMultiplayer())
        {
            Owner.SetGameSpeed(dt);
        }
    }

    /// <summary>
    /// Sometimes the developers set time dilation directly without going through the detour.
    /// Normal game speed is forced here each tick to prevent inconsistencies.
    /// </summary>
    public override void OnTick()
    {
        if (GameState.IsMultiplayer())
        {
            // A direct slow-mo set still counts as a request
            if (Owner.GameSpeed < 0.999f)
            {
                RequestedSpeed = Owner.GameSpeed;
            }

            Owner.GameSpeed = 1.0f;
            Owner.WorldInfo.TimeDilation = 1.0f;
        }
    }
}
