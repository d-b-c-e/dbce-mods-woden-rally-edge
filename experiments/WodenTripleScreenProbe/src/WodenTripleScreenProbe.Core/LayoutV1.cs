using System.Security.Cryptography;
using System.Text.Json;
using Dbce.TripleScreen;

namespace WodenTripleScreenProbe.Core;

/// <summary>Strict reader for the pinned v1 layout contract. No game or Unity dependency.</summary>
public sealed class LayoutV1
{
    private LayoutV1(int widthPx, int heightPx, double widthMm, double heightMm,
        double eyeDistanceMm, double eyeHeightMm, double leftYaw, double rightYaw,
        double? curveRadiusMm, double bezelMm, string outputMode, string sha256)
    {
        WidthPx = widthPx; HeightPx = heightPx; WidthMm = widthMm; HeightMm = heightMm;
        EyeDistanceMm = eyeDistanceMm; EyeHeightMm = eyeHeightMm;
        LeftYawDegrees = leftYaw; RightYawDegrees = rightYaw;
        CurveRadiusMm = curveRadiusMm; BezelMm = bezelMm; OutputMode = outputMode;
        Sha256 = sha256;
    }

    public int WidthPx { get; }
    public int HeightPx { get; }
    public double WidthMm { get; }
    public double HeightMm { get; }
    public double EyeDistanceMm { get; }
    public double EyeHeightMm { get; }
    public double LeftYawDegrees { get; }
    public double RightYawDegrees { get; }
    public double? CurveRadiusMm { get; }
    public double BezelMm { get; }
    public string OutputMode { get; }
    public string Sha256 { get; }

    public static LayoutV1 Parse(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == 0 || bytes.Length > 65536)
            throw new InvalidDataException("Layout file must be 1 to 65536 bytes.");
        using var document = JsonDocument.Parse(bytes.ToArray(), new JsonDocumentOptions
        { AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow });
        var root = document.RootElement;
        Only(root, "schemaVersion", "panel", "geometry", "output");
        if (Integer(root, "schemaVersion") != 1) throw new InvalidDataException("Unsupported layout schemaVersion.");

        var panel = Object(root, "panel");
        Only(panel, "count", "nativeWidthPx", "nativeHeightPx", "physicalWidthMm",
            "physicalHeightMm", "curveRadiusMm", "bezelWidthMm");
        if (Integer(panel, "count") != 3) throw new InvalidDataException("Exactly three panels are required.");
        int widthPx = PositiveInt(panel, "nativeWidthPx"), heightPx = PositiveInt(panel, "nativeHeightPx");
        double widthMm = Number(panel, "physicalWidthMm"), heightMm = Number(panel, "physicalHeightMm");
        if (widthMm <= 0 || heightMm <= 0) throw new InvalidDataException("Panel dimensions must be positive.");
        double? curve = OptionalNumber(panel, "curveRadiusMm");
        if (curve <= 0) throw new InvalidDataException("curveRadiusMm must be positive when supplied.");
        double bezel = OptionalNumber(panel, "bezelWidthMm") ?? 0;
        if (bezel < 0) throw new InvalidDataException("bezelWidthMm cannot be negative.");

        var geometry = Object(root, "geometry");
        Only(geometry, "eyeDistanceMm", "eyeHeightAbovePanelCenterMm", "leftYawDegrees", "rightYawDegrees");
        double eyeDistance = Number(geometry, "eyeDistanceMm");
        double eyeHeight = OptionalNumber(geometry, "eyeHeightAbovePanelCenterMm") ?? 0;
        double leftYaw = Number(geometry, "leftYawDegrees"), rightYaw = Number(geometry, "rightYawDegrees");
        if (eyeDistance <= 0 || leftYaw < 0 || leftYaw >= 90 || rightYaw < 0 || rightYaw >= 90)
            throw new InvalidDataException("Invalid eye distance or panel yaw.");

        var output = Object(root, "output");
        Only(output, "mode", "combinedWidthPx", "combinedHeightPx");
        string mode = Text(output, "mode");
        if (mode is not ("nvidia-surround" or "borderless-span" or "separate-displays"))
            throw new InvalidDataException("Unsupported output mode.");
        OptionalPositiveInt(output, "combinedWidthPx");
        OptionalPositiveInt(output, "combinedHeightPx");
        string hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        return new(widthPx, heightPx, widthMm, heightMm, eyeDistance, eyeHeight,
            leftYaw, rightYaw, curve, bezel, mode, hash);
    }

    /// <summary>Owner-specific safety gate; a stale 60-degree profile is rejected.</summary>
    public void RequireCurrentOwnerRig()
    {
        if (WidthPx != 2560 || HeightPx != 1440 || Math.Abs(EyeDistanceMm - 660) > .001 ||
            Math.Abs(LeftYawDegrees - 70) > .001 || Math.Abs(RightYawDegrees - 70) > .001 ||
            Math.Abs(BezelMm - 8) > .001 || CurveRadiusMm is not 1500)
            throw new InvalidDataException("Layout does not match the owner's reported 70-degree rig.");
    }

    public IReadOnlyList<OffAxisProjection> Projections(double near, double far)
    {
        var rig = new TripleRigDefinition(WidthMm, HeightMm, EyeDistanceMm,
            LeftYawDegrees, RightYawDegrees, EyeHeightMm);
        return TripleRigBuilder.Build(rig)
            .Select(surface => ProjectionCalculator.Calculate(surface, new Vector3d(0, 0, 0), near, far))
            .ToArray();
    }

    private static void Only(JsonElement value, params string[] names)
    {
        if (value.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Expected an object.");
        var allowed = names.ToHashSet(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
            if (!allowed.Contains(property.Name) || !seen.Add(property.Name))
                throw new InvalidDataException("Unknown or duplicate layout property: " + property.Name);
    }
    private static JsonElement Object(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var result) || result.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Missing object: " + name);
        return result;
    }
    private static int Integer(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var value) || !value.TryGetInt32(out int result))
            throw new InvalidDataException("Missing integer: " + name);
        return result;
    }
    private static int PositiveInt(JsonElement parent, string name)
    {
        int result = Integer(parent, name);
        if (result <= 0) throw new InvalidDataException(name + " must be positive.");
        return result;
    }
    private static void OptionalPositiveInt(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null) return;
        if (!value.TryGetInt32(out int result) || result <= 0)
            throw new InvalidDataException(name + " must be positive when supplied.");
    }
    private static double Number(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Number ||
            !value.TryGetDouble(out double result) || !double.IsFinite(result))
            throw new InvalidDataException("Missing finite number: " + name);
        return result;
    }
    private static double? OptionalNumber(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null) return null;
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out double result) || !double.IsFinite(result))
            throw new InvalidDataException("Invalid finite number: " + name);
        return result;
    }
    private static string Text(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
            throw new InvalidDataException("Missing text: " + name);
        return value.GetString()!;
    }
}
