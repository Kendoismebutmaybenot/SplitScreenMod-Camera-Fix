using System.Numerics;
using BmSDK;
using BmSDK.BmGame;
using BmSDK.Engine;

namespace Etkramer.SplitScreen.Rendering;

// Fixes stretched views when letterboxing is off. The engine's CalcSceneView
// (Steam 0x7CA020) builds the projection from the whole window's aspect ratio
// unless the player camera has bConstrainAspectRatio set, in which case it uses
// ConstrainedAspectRatio. So in split-screen we constrain each camera to the
// real aspect of its slice, and widen the FOV so the vertical view matches solo.
[Script]
public sealed class SplitScreenAspect : Script
{
    static bool? s_originalConstrain;
    static float s_originalConstrainedAspect;
    static readonly float[] s_lastWrittenFov = new float[4];
    static bool s_loggedRedirect;
    static bool s_applied;

    public override void OnTick()
    {
        ApplyAll();
        base.OnTick();
    }

    // Camera state is recalculated each frame, so reapply right after the update
    [Redirect(typeof(R3rdPersonCamera), nameof(R3rdPersonCamera.UpdateCamera))]
    private static void UpdateCameraRedirect(R3rdPersonCamera self, float DeltaTime)
    {
        self.UpdateCamera(DeltaTime);

        if (!s_loggedRedirect)
        {
            s_loggedRedirect = true;
            Debug.Log("SplitScreenAspect: UpdateCamera redirect active");
        }

        ApplyAll();
    }

    private static void ApplyAll()
    {
        if (SplitScreen.Instance == null)
        {
            return;
        }

        var engine = Game.GetEngine();
        if (engine.GamePlayers.Count < 2 || SplitScreen.Instance.UseLetterboxing)
        {
            RestoreSolo();
            return;
        }

        var viewportSize = new Vector2();
        Game.GetGameViewportClient().GetViewportSize(out viewportSize);
        var screenAspect = viewportSize.X / viewportSize.Y;
        if (!float.IsFinite(screenAspect) || screenAspect <= 0f)
        {
            return;
        }

        for (var i = 0; i < engine.GamePlayers.Count && i < s_lastWrittenFov.Length; i++)
        {
            var player = engine.GamePlayers[i];
            var camera = player.Actor?.PlayerCamera;
            if (camera == null || player.Size.X <= 0f || player.Size.Y <= 0f)
            {
                continue;
            }

            if (s_originalConstrain == null)
            {
                s_originalConstrain = camera.bConstrainAspectRatio;
                s_originalConstrainedAspect = camera.ConstrainedAspectRatio;
                Debug.Log(
                    $"SplitScreenAspect: original constrain={camera.bConstrainAspectRatio}, aspect={camera.ConstrainedAspectRatio}"
                );
            }

            var aspect = screenAspect * player.Size.X / player.Size.Y;

            // Projection uses this aspect instead of the whole window's
            camera.bConstrainAspectRatio = true;
            camera.ConstrainedAspectRatio = aspect;
            s_applied = true;

            // The constrained projection keeps horizontal FOV, so widen it to
            // keep the same vertical FOV as solo play (Hor+)
            var cache = camera.CameraCache;
            var fov = cache.POV.FOV;
            if (fov > 0f && fov < 170f && fov != s_lastWrittenFov[i])
            {
                var halfRad = fov * MathF.PI / 360f;
                var widened = MathF.Atan(MathF.Tan(halfRad) * aspect / screenAspect) * 360f / MathF.PI;
                widened = Math.Clamp(widened, 1f, 170f);

                var pov = cache.POV;
                pov.FOV = widened;
                cache.POV = pov;
                camera.CameraCache = cache;
                s_lastWrittenFov[i] = widened;
            }
        }
    }

    private static void RestoreSolo()
    {
        if (!s_applied || s_originalConstrain == null)
        {
            return;
        }

        foreach (var player in Game.GetEngine().GamePlayers)
        {
            var camera = player.Actor?.PlayerCamera;
            if (camera == null)
            {
                continue;
            }

            camera.bConstrainAspectRatio = s_originalConstrain.Value;
            camera.ConstrainedAspectRatio = s_originalConstrainedAspect;
        }

        Array.Clear(s_lastWrittenFov);
        s_applied = false;
    }
}
