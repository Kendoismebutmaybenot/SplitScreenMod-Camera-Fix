using BmSDK;
using BmSDK.BmGame;
using BmSDK.Engine;
using BmSDK.GFxUI;

namespace Etkramer.SplitScreen.Gameplay;

// The cryptographic sequencer assumes there is only one player: its codes and
// stick input go through "the" player (P1). So P2 could open it but never crack a
// code. While P2's sequencer (the gadget or its minigame screen) runs, we briefly
// put P2 in P1's slot (same approach as the doors and tightrope). The screen also
// reads sticks from its owner player slot, so we keep that pointed at whoever is
// really using it. Solo is untouched.
[Script]
public class SplitScreenSequencer : Script
{
    // Real player index currently swapped into slot 0, or -1
    static int s_swapped = -1;

    // Real player index of whoever last started the sequencer, or -1
    static int s_userIndex = -1;

    static bool s_loggedOpen;
    static readonly Dictionary<string, int> s_logCounts = [];

    // --- The gadget in the player's hand ---

    [Redirect(typeof(RResonatorTuner), nameof(RResonatorTuner.BeginResonate), AllowSubtypes = true)]
    private static void BeginResonateRedirect(RResonatorTuner self, Actor ResonateTarget)
    {
        var index = TunerIndex(self);
        SetUser(index);
        AsPlayer(index, () => self.BeginResonate(ResonateTarget));
    }

    [Redirect(typeof(RResonatorTuner), nameof(RResonatorTuner.CanResonateTarget), AllowSubtypes = true)]
    private static bool CanResonateTargetRedirect(RResonatorTuner self, Actor Target)
    {
        var result = false;
        AsPlayer(TunerIndex(self), () => result = self.CanResonateTarget(Target));
        return result;
    }

    [Redirect(typeof(RResonatorTuner), nameof(RResonatorTuner.AcquireTarget), AllowSubtypes = true)]
    private static void AcquireTargetRedirect(RResonatorTuner self) =>
        AsPlayer(TunerIndex(self), self.AcquireTarget);

    [Redirect(typeof(RResonatorTuner), nameof(RResonatorTuner.StartMiniGame), AllowSubtypes = true)]
    private static void StartMiniGameRedirect(RResonatorTuner self, bool MultiStage)
    {
        var index = TunerIndex(self);
        SetUser(index);
        Log("start", $"P{index + 1} started the sequencer, codes={self.PasswordArray.Count}");
        AsPlayer(index, () => self.StartMiniGame(MultiStage));
    }

    [Redirect(typeof(RResonatorTuner), nameof(RResonatorTuner.BeginMinigame), AllowSubtypes = true)]
    private static void BeginMinigameRedirect(
        RResonatorTuner self,
        SwfMovie mg_movie_info,
        TextureRenderTarget2D mg_texture,
        Material mg_material,
        FString mg_loadstring
    ) =>
        AsPlayer(
            TunerIndex(self),
            () => self.BeginMinigame(mg_movie_info, mg_texture, mg_material, mg_loadstring)
        );

    [Redirect(typeof(RResonatorTuner), nameof(RResonatorTuner.StartFlashMovie), AllowSubtypes = true)]
    private static void StartFlashMovieRedirect(RResonatorTuner self) =>
        AsPlayer(TunerIndex(self), self.StartFlashMovie);

    // Not PushPasswords: BmSDK can't pass its interface argument back to the game,
    // so redirecting it crashed the game.

    [Redirect(typeof(RResonatorTuner), nameof(RResonatorTuner.AddPassword), AllowSubtypes = true)]
    private static void AddPasswordRedirect(RResonatorTuner self, int pw_index, FString the_password)
    {
        var index = TunerIndex(self);
        Log($"add{index}", $"P{index + 1} got code {pw_index}");
        AsPlayer(index, () => self.AddPassword(pw_index, the_password));
    }

    [Redirect(typeof(RResonatorTuner), nameof(RResonatorTuner.SetStage), AllowSubtypes = true)]
    private static void SetStageRedirect(RResonatorTuner self, int stage) =>
        AsPlayer(TunerIndex(self), () => self.SetStage(stage));

    [Redirect(typeof(RResonatorTuner), nameof(RResonatorTuner.SetThumbStickPositions), AllowSubtypes = true)]
    private static void SetThumbStickPositionsRedirect(RResonatorTuner self, float LeftPos, float RightPos) =>
        AsPlayer(TunerIndex(self), () => self.SetThumbStickPositions(LeftPos, RightPos));

    // --- The minigame screen ---

    [Redirect(typeof(RGFxMovieResonatorMinigame), nameof(RGFxMovieResonatorMinigame.MinigameTick))]
    private static void MinigameTickRedirect(RGFxMovieResonatorMinigame self)
    {
        LogOpen(self);
        AsMovieUser(self, self.MinigameTick);
    }

    [Redirect(typeof(RGFxMovieResonatorMinigame), nameof(RGFxMovieResonatorMinigame.StartResonatorMiniGame))]
    private static void StartResonatorMiniGameRedirect(RGFxMovieResonatorMinigame self) =>
        AsMovieUser(self, self.StartResonatorMiniGame);

    [Redirect(typeof(RGFxMovieResonatorMinigame), nameof(RGFxMovieResonatorMinigame.FE_StartResonator))]
    private static void FE_StartResonatorRedirect(RGFxMovieResonatorMinigame self) =>
        AsMovieUser(self, self.FE_StartResonator);

    [Redirect(
        typeof(RGFxMovieResonatorMinigame),
        nameof(RGFxMovieResonatorMinigame.FE_TellFlashAboutControllerState)
    )]
    private static void FE_TellFlashAboutControllerStateRedirect(RGFxMovieResonatorMinigame self) =>
        AsMovieUser(self, self.FE_TellFlashAboutControllerState);

    [Redirect(typeof(RGFxMovieResonatorMinigame), nameof(RGFxMovieResonatorMinigame.SetResonatorPassword))]
    private static void SetResonatorPasswordRedirect(
        RGFxMovieResonatorMinigame self,
        int password_index,
        FString the_password
    ) => AsMovieUser(self, () => self.SetResonatorPassword(password_index, the_password));

    [Redirect(typeof(RGFxMovieResonatorMinigame), nameof(RGFxMovieResonatorMinigame.FE_NotifyPasswordFound))]
    private static void FE_NotifyPasswordFoundRedirect(RGFxMovieResonatorMinigame self)
    {
        var index = UserIndex(self);
        Log($"found{index}", $"P{index + 1} cracked a code");
        AsMovieUser(self, self.FE_NotifyPasswordFound);
    }

    [Redirect(
        typeof(RGFxMovieResonatorMinigame),
        nameof(RGFxMovieResonatorMinigame.FE_SetRadioStationFound)
    )]
    private static void FE_SetRadioStationFoundRedirect(RGFxMovieResonatorMinigame self, FString RadioStationName) =>
        AsMovieUser(self, () => self.FE_SetRadioStationFound(RadioStationName));

    [Redirect(
        typeof(RGFxMovieResonatorMinigame),
        nameof(RGFxMovieResonatorMinigame.FE_SetCurrentRadioDecrypted)
    )]
    private static void FE_SetCurrentRadioDecryptedRedirect(RGFxMovieResonatorMinigame self) =>
        AsMovieUser(self, self.FE_SetCurrentRadioDecrypted);

    [Redirect(typeof(RGFxMovieResonatorMinigame), nameof(RGFxMovieResonatorMinigame.FE_CloseResonator))]
    private static void FE_CloseResonatorRedirect(RGFxMovieResonatorMinigame self) =>
        AsMovieUser(self, self.FE_CloseResonator);

    [Redirect(typeof(RGFxMovieResonatorMinigame), nameof(RGFxMovieResonatorMinigame.RequestQuit))]
    private static void RequestQuitRedirect(RGFxMovieResonatorMinigame self) =>
        AsMovieUser(self, self.RequestQuit);

    [Redirect(typeof(RGFxMovieResonatorMinigame), nameof(RGFxMovieResonatorMinigame.OnCloseMinigame))]
    private static void OnCloseMinigameRedirect(RGFxMovieResonatorMinigame self)
    {
        s_loggedOpen = false;
        AsMovieUser(self, self.OnCloseMinigame);
    }

    // --- Helpers ---

    private static void SetUser(int index)
    {
        if (index >= 0 && GameState.IsMultiplayer())
        {
            s_userIndex = index;
        }
    }

    private static int TunerIndex(RResonatorTuner tuner)
    {
        var pawn = tuner.Owner as Pawn ?? tuner.Instigator;
        return ControllerIndex(pawn?.Controller);
    }

    // Who is really using the screen: whoever started the sequencer
    private static int UserIndex(RGFxMovieResonatorMinigame movie) =>
        s_userIndex >= 0 ? s_userIndex : ControllerIndex(movie.RPC);

    // Real player index of a controller, even while players are swapped
    private static int ControllerIndex(Controller? controller)
    {
        if (controller == null)
        {
            return -1;
        }

        var players = Game.GetEngine().GamePlayers;
        for (var i = 0; i < players.Count; i++)
        {
            if (ReferenceEquals(players[i]?.Actor, controller))
            {
                if (s_swapped > 0 && i == 0)
                {
                    return s_swapped;
                }
                if (s_swapped > 0 && i == s_swapped)
                {
                    return 0;
                }
                return i;
            }
        }
        return -1;
    }

    // Runs the screen code as its real user, then leaves the screen reading that
    // player's sticks
    private static void AsMovieUser(RGFxMovieResonatorMinigame movie, Action original)
    {
        var index = UserIndex(movie);
        var players = Game.GetEngine().GamePlayers;
        if (!GameState.IsMultiplayer() || s_swapped >= 0 || index < 0 || index >= players.Count)
        {
            original();
            return;
        }

        if (players[index]?.Actor is RPlayerController pc)
        {
            movie.RPC = pc;
        }

        try
        {
            if (index == 0)
            {
                movie.LocalPlayerOwnerIndex = 0;
                original();
                return;
            }

            // Slot 0 is this player during the swap
            AsPlayer(
                index,
                () =>
                {
                    movie.LocalPlayerOwnerIndex = 0;
                    original();
                }
            );
        }
        finally
        {
            movie.LocalPlayerOwnerIndex = index;
        }
    }

    // Runs the original with that player in P1's slot. If they're already there
    // (nested call) or are P1, it just runs it.
    private static void AsPlayer(int index, Action original)
    {
        var engine = Game.GetEngine();
        if (
            s_swapped >= 0
            || index <= 0
            || index >= engine.GamePlayers.Count
            || !GameState.IsMultiplayer()
        )
        {
            original();
            return;
        }

        var saved = engine.GamePlayers[0];
        engine.GamePlayers[0] = engine.GamePlayers[index];
        engine.GamePlayers[index] = saved;
        s_swapped = index;
        try
        {
            original();
        }
        finally
        {
            s_swapped = -1;
            engine.GamePlayers[index] = engine.GamePlayers[0];
            engine.GamePlayers[0] = saved;
        }
    }

    // Once per open screen: who owns it, so the log shows where input comes from
    private static void LogOpen(RGFxMovieResonatorMinigame movie)
    {
        if (s_loggedOpen || !GameState.IsMultiplayer() || s_swapped >= 0)
        {
            return;
        }
        s_loggedOpen = true;

        var players = Game.GetEngine().GamePlayers;
        var holders = "";
        var pads = "";
        for (var i = 0; i < players.Count; i++)
        {
            var player = players[i];
            if (player?.Actor is RPlayerController pc && ReferenceEquals(pc.MinigameMovie, movie))
            {
                holders += $"P{i + 1} ";
            }
            pads += $"P{i + 1}=pad{player?.ControllerId} ";
        }
        Debug.Log(
            $"SplitScreenSequencer: screen open, user=P{UserIndex(movie) + 1}, rpc=P{ControllerIndex(movie.RPC) + 1}, "
                + $"owner=slot{movie.LocalPlayerOwnerIndex}, held by {holders}, {pads}"
        );
    }

    private static void Log(string key, string message)
    {
        if (!GameState.IsMultiplayer())
        {
            return;
        }

        s_logCounts.TryGetValue(key, out var count);
        s_logCounts[key] = count + 1;
        if (count < 5)
        {
            Debug.Log($"SplitScreenSequencer: {message}");
        }
    }
}
