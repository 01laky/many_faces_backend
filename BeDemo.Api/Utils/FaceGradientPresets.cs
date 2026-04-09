using System.Text.Json;

namespace BeDemo.Api.Utils;

/// <summary>
/// Default animated gradients for faces (JSON matches fe_demo useAnimatedGradient / GradientPicker).
/// </summary>
public static class FaceGradientPresets
{
    private sealed record Preset(string Type, string[] Colors, int Angle, string Animation, double AnimationSpeed);

    /// <summary>
    /// Deterministic preset per face index; unknown indices get a stable hash-based variant.
    /// </summary>
    public static string JsonForFaceIndex(string? faceIndex)
    {
        var key = faceIndex?.Trim().ToLowerInvariant() ?? "";
        var preset = key switch
        {
            "public" => new Preset("linear", ["#6366f1", "#06b6d4", "#a78bfa"], 118, "rotate", 16),
            "basic" => new Preset("linear", ["#047857", "#34d399", "#065f46"], 52, "shift", 11),
            "koncept" => new Preset("linear", ["#ea580c", "#facc15", "#dc2626"], 195, "pulse", 4.5),
            _ => PresetFromHash(key),
        };

        return JsonSerializer.Serialize(
            new
            {
                type = preset.Type,
                colors = preset.Colors,
                angle = preset.Angle,
                animation = preset.Animation,
                animationSpeed = preset.AnimationSpeed,
            });
    }

    private static Preset PresetFromHash(string key)
    {
        // Stable across runs (do not use string.GetHashCode — randomized per process in .NET).
        var h = StableStringHash(key);
        var palettes = new[]
        {
            (new[] { "#7c3aed", "#ec4899", "#8b5cf6" }, 90, "rotate", 14.0),
            (new[] { "#0ea5e9", "#22d3ee", "#0369a1" }, 45, "shift", 10.0),
            (new[] { "#f97316", "#fb7185", "#c026d3" }, 160, "pulse", 5.0),
            (new[] { "#14b8a6", "#84cc16", "#0d9488" }, 72, "rotate", 20.0),
        };
        var anim = new[] { "rotate", "shift", "pulse" };
        var idx = Math.Abs(h) % palettes.Length;
        var aIdx = Math.Abs(h >> 3) % anim.Length;
        var (colors, angle, _, speed) = palettes[idx];
        var animation = anim[aIdx];
        return new Preset("linear", colors, angle, animation, speed);
    }

    private static int StableStringHash(string? s)
    {
        if (string.IsNullOrEmpty(s)) return 0;
        unchecked
        {
            var h = 0;
            foreach (var c in s)
                h = h * 31 + c;
            return h;
        }
    }
}
