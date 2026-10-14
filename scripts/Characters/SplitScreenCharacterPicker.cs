using System.IO;
using System.Text.RegularExpressions;
using BmSDK;
using BmSDK.Engine;

namespace Etkramer.SplitScreen.Characters;

// At the main menu, press C / V / B to cycle P2's / P3's / P4's character and suit.
// The choice shows on screen for a few seconds, applies the next time a game loads,
// and is saved to mod.toml. The list comes from the character packages installed
// with the game.
[Script]
public class SplitScreenCharacterPicker : Script
{
    const long MessageMs = 3000;

    static List<(string Character, string Suit)>? s_choices;
    static FString? s_message;
    static long s_messageUntil;

    // Most-used first; anything else after, alphabetically
    static readonly string[] s_characterOrder =
    [
        "Playable_Batman",
        "Playable_Catwoman",
        "Playable_Nightwing",
        "Playable_RobinStoryDLC",
        "Playable_Robin",
    ];

    public override void OnKeyDown(Keys key)
    {
        // Index into "players" in mod.toml: 0 = P2, 1 = P3, 2 = P4
        var slot = key switch
        {
            Keys.C => 0,
            Keys.V => 1,
            Keys.B => 2,
            _ => -1,
        };
        if (slot < 0 || SplitScreen.Instance == null || !Game.GetGameInfo().IsFrontend())
        {
            return;
        }

        var label = $"P{slot + 2}";
        var playerConfigs = (TomlArray)SplitScreen.Instance.Options["players"];
        if (slot >= playerConfigs.Count)
        {
            Show($"{label}: not set up in mod.toml");
            return;
        }

        var choices = s_choices ??= FindChoices();
        if (choices.Count == 0)
        {
            Show($"{label}: no characters found");
            return;
        }

        var current = (TomlArray)playerConfigs[slot]!;
        var currentIndex = choices.FindIndex(c =>
            c.Character.Equals((string)current[0]!, StringComparison.OrdinalIgnoreCase)
            && c.Suit.Equals((string)current[1]!, StringComparison.OrdinalIgnoreCase)
        );

        var nextIndex = (currentIndex + 1) % choices.Count;
        var next = choices[nextIndex];
        playerConfigs[slot] = new TomlArray { next.Character, next.Suit };
        Save(slot, next);

        Show($"{label}: {Pretty(next)}  ({nextIndex + 1}/{choices.Count})");
        Debug.Log($"SplitScreenCharacterPicker: {label} is now {next.Character} {next.Suit}");
    }

    [Redirect(typeof(GameViewportClient), nameof(GameViewportClient.PostRender), AllowSubtypes = true)]
    private static void PostRenderRedirect(GameViewportClient self, Canvas canvas)
    {
        self.PostRender(canvas);

        if (canvas == null || s_message is not { } message || Environment.TickCount64 > s_messageUntil)
        {
            return;
        }

        var scale = Math.Max(1f, canvas.ClipY / 540f);
        canvas.TextSize(message, out var width, out _);
        var x = (canvas.ClipX - width * scale) / 2f;
        var y = canvas.ClipY * 0.12f;

        // Shadow, then text
        canvas.SetDrawColor(0, 0, 0, 255);
        canvas.SetPos(x + scale, y + scale, 1f);
        canvas.DrawText(message, false, scale, scale, out _);

        canvas.SetDrawColor(255, 255, 255, 255);
        canvas.SetPos(x, y, 1f);
        canvas.DrawText(message, false, scale, scale, out _);
    }

    private static void Show(string text)
    {
        s_message?.Dispose();
        s_message = new FString(text);
        s_messageUntil = Environment.TickCount64 + MessageMs;
    }

    // Every installed character/suit: Playable_<Character>_<Suit>_SF.upk, where the
    // character's own package (Playable_<Character>_SF.upk) also exists
    private List<(string Character, string Suit)> FindChoices()
    {
        var choices = new List<(string Character, string Suit)>();
        try
        {
            var cooked = Path.GetFullPath(Path.Combine(Mod.DirectoryPath, "..", "..", "CookedPCConsole"));
            var names = Directory
                .GetFiles(cooked, "Playable_*_SF.upk")
                .Select(f => Path.GetFileNameWithoutExtension(f)[..^3])
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var name in names)
            {
                // Playable_X_Std_1.. are the story's battle-damaged suits of character X
                var damaged = Regex.Match(name, @"^(.+)_(Std_\d+)$", RegexOptions.IgnoreCase);
                if (damaged.Success && names.Contains(damaged.Groups[1].Value))
                {
                    choices.Add((damaged.Groups[1].Value, damaged.Groups[2].Value));
                    continue;
                }

                var character = names
                    .Where(b => name.StartsWith(b + "_", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(b => b.Length)
                    .FirstOrDefault();
                if (character == null || character.Equals("Playable_BruceWayne", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // A suit's package (e.g. Playable_Batman_Std) isn't a character of its own
                var suit = name[(character.Length + 1)..];
                if (character.EndsWith("_Std", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                choices.Add((character, suit));
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"SplitScreenCharacterPicker: couldn't list characters: {e.Message}");
        }

        return choices
            .OrderBy(c =>
            {
                var order = Array.FindIndex(
                    s_characterOrder,
                    o => o.Equals(c.Character, StringComparison.OrdinalIgnoreCase)
                );
                return order < 0 ? s_characterOrder.Length : order;
            })
            .ThenBy(c => c.Character)
            .ThenBy(c =>
                c.Suit.Equals("Std", StringComparison.OrdinalIgnoreCase) ? 0
                : c.Suit.StartsWith("Std_", StringComparison.OrdinalIgnoreCase) ? 2
                : 1
            )
            .ThenBy(c => c.Suit)
            .ToList();
    }

    // Write the choice into that player's entry of "players" in mod.toml
    private void Save(int slot, (string Character, string Suit) choice)
    {
        try
        {
            var path = Path.Combine(Mod.DirectoryPath, "mod.toml");
            var text = File.ReadAllText(path);
            var players = Regex.Match(text, @"^\s*players\s*=\s*\[", RegexOptions.Multiline);
            var entries = players.Success
                ? new Regex(@"\[\s*""([^""]*)""\s*,\s*""([^""]*)""\s*,?\s*\]").Matches(text, players.Index + players.Length)
                : null;
            if (entries == null || slot >= entries.Count)
            {
                Debug.LogWarning($"SplitScreenCharacterPicker: couldn't find P{slot + 2} in mod.toml, not saved");
                return;
            }

            var entry = entries[slot];
            var character = entry.Groups[1];
            var suit = entry.Groups[2];
            text =
                text[..character.Index]
                + choice.Character
                + text[(character.Index + character.Length)..suit.Index]
                + choice.Suit
                + text[(suit.Index + suit.Length)..];
            File.WriteAllText(path, text);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"SplitScreenCharacterPicker: couldn't save to mod.toml: {e.Message}");
        }
    }

    private static string Pretty((string Character, string Suit) choice)
    {
        var character = choice.Character.Replace("Playable_", "") switch
        {
            "RobinStoryDLC" => "Robin",
            "Robin" => "Robin (challenge maps)",
            var other => other,
        };
        var suit =
            choice.Suit.Equals("Std", StringComparison.OrdinalIgnoreCase) ? "Default"
            : choice.Suit.StartsWith("Std_", StringComparison.OrdinalIgnoreCase) ? $"Battle damaged {choice.Suit[4..]}"
            : choice.Suit.Replace('_', ' ');
        return $"{character} - {suit}";
    }
}
