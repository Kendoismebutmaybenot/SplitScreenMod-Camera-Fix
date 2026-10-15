using System.Numerics;
using BmSDK;
using BmSDK.BmGame;
using BmSDK.Engine;

namespace Etkramer.SplitScreen.World;

// Going in or out of buildings (while the city loads/unloads) can leave an extra
// player's camera stuck in place, e.g. staring at water, until a checkpoint
// restart. The game sets up and releases these transition cameras for "the"
// player (P1) only. If another player's camera stays frozen while they move, or
// sits frozen far away from them, put it back on them. Solo is untouched.
[Script]
public class SplitScreenCameraRescue : Script
{
    // Frozen this long counts as stuck
    const long FrozenMs = 4000;

    // Camera moving less than this counts as frozen
    const float FrozenDistance = 50f;

    // Player moving more than this while the camera is frozen counts as stuck
    const float PlayerMovedDistance = 300f;

    // Camera this far from its player while frozen counts as stuck too
    const float FarDistance = 5000f;

    // Gone or deleted view target: fix after this long
    const long LostTargetMs = 1000;

    static readonly Watch[] s_watches = new Watch[4];

    struct Watch
    {
        public long Since;
        public Vector3 CameraStart;
        public Vector3 PawnStart;
        public long LostSince;
    }

    public override void OnTick()
    {
        base.OnTick();

        var players = Game.GetEngine().GamePlayers;
        if (players.Count < 2 || players[0]?.Actor is not RPlayerController p1)
        {
            return;
        }

        // Cutscenes move cameras on purpose
        if (InCutscene(p1))
        {
            Array.Clear(s_watches);
            return;
        }

        var now = Environment.TickCount64;
        for (var i = 1; i < players.Count && i < s_watches.Length; i++)
        {
            if (
                players[i]?.Actor is not RPlayerController pc
                || pc.Pawn is not Pawn pawn
                || pc.PlayerCamera is not R3rdPersonCamera camera
                || InCutscene(pc)
            )
            {
                s_watches[i] = default;
                continue;
            }

            ref var watch = ref s_watches[i];
            var cameraLoc = camera.CameraCache.POV.Location;
            var pawnLoc = pawn.Location;

            // View target gone: nothing will ever move this camera again
            var target = camera.ViewTarget.Target;
            if (target == null || target.bDeleteMe)
            {
                if (watch.LostSince == 0)
                {
                    watch.LostSince = now;
                }
                else if (now - watch.LostSince >= LostTargetMs)
                {
                    Rescue(i, pc, pawn, camera, "view target gone");
                    watch = default;
                }
                continue;
            }
            watch.LostSince = 0;

            // Start a new window whenever the camera moves
            if (watch.Since == 0 || Vector3.Distance(cameraLoc, watch.CameraStart) > FrozenDistance)
            {
                watch.Since = now;
                watch.CameraStart = cameraLoc;
                watch.PawnStart = pawnLoc;
                continue;
            }

            if (now - watch.Since < FrozenMs)
            {
                continue;
            }

            var pawnMoved = Vector3.Distance(pawnLoc, watch.PawnStart) > PlayerMovedDistance;
            var far = Vector3.Distance(cameraLoc, pawnLoc) > FarDistance;
            if (pawnMoved || far)
            {
                Rescue(i, pc, pawn, camera, pawnMoved ? "frozen while player moved" : "frozen far away");
                watch = default;
            }
        }
    }

    private static bool InCutscene(RPlayerController pc) =>
        pc.bCinematicMode || pc.bForceCinematicMode || pc.IsMatineePlaing();

    private static void Rescue(
        int index,
        RPlayerController pc,
        Pawn pawn,
        R3rdPersonCamera camera,
        string reason
    )
    {
        var target = camera.ViewTarget.Target;
        Debug.Log(
            $"SplitScreenCameraRescue: P{index + 1} camera stuck ({reason}): target={target?.Name.ToString() ?? "none"}, "
                + $"locked={camera.CameraLocked}, lockInPlace={camera.LockInPlace}, "
                + $"distance={Vector3.Distance(camera.CameraCache.POV.Location, pawn.Location):0}; resetting"
        );

        if (camera.CameraLocked || camera.LockInPlace)
        {
            camera.UnlockCamera();
            camera.LockInPlace = false;
        }

        if (!ReferenceEquals(target, pawn))
        {
            pc.SetViewTarget(pawn, default);
        }

        camera.ResetToPlayerCam();
    }
}
