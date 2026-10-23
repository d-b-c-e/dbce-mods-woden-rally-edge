using System.Text;

namespace WodenRallyEdge.Core;

/// <summary>
/// Stock prompts use Language.BUTTON* characters (①..⑩) that the game font draws as pad buttons, or keyboard keys
/// when the game last saw the keyboard: 「Enter」/「Esc」 in the arcade header, [Enter]/[Esc] in photo mode. Only tokens
/// whose native button the wheel already dispatches are replaced, in the game's own 「…」 key style; every other token
/// and all wording stay stock.
/// </summary>
public static class WheelPromptText
{
    /// <summary>Stock token → wheel action. ① is pad A (Confirm), ② pad B (Back), ⑩ pad Start (Pause).</summary>
    public static readonly IReadOnlyList<(string Token, string Action)> Tokens = new[]
    {
        ("①", "Confirm"), ("「Enter」", "Confirm"), ("[Enter]", "Confirm"), ("②", "Back"), ("「Esc」", "Back"), ("[Esc]", "Back"),
        ("⑩", "Pause"),
    };

    /// <summary>The text with each mapped token replaced by 「label」; tokens whose label is null stay stock.</summary>
    public static string Rewrite(string text, Func<string, string?> label)
    {
        StringBuilder? result = null;
        for (int i = 0; i < text.Length;)
        {
            var match = Tokens.FirstOrDefault(t => string.CompareOrdinal(text, i, t.Token, 0, t.Token.Length) == 0);
            string? name = match.Token == null ? null : label(match.Action);
            if (name == null) { result?.Append(text[i]); i++; continue; }
            result ??= new StringBuilder(text, 0, i, text.Length + 16);
            result.Append('「').Append(name).Append('」');
            i += match.Token!.Length;
        }
        return result?.ToString() ?? text;
    }
}

/// <summary>Whether the mod's wheel or the game's own pad/keyboard delivered the most recent menu press.</summary>
public sealed class MenuInputSource
{
    private long _sequence, _wheel, _native;
    public void Wheel() => _wheel = ++_sequence;
    public void Native() => _native = ++_sequence;
    public bool WheelLast => _wheel > _native;
    public void Reset() => _sequence = _wheel = _native = 0;
}
