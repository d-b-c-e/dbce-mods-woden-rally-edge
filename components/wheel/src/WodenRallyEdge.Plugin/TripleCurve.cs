using Dbce.TripleScreen;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace WodenRallyEdge;

/// <summary>
/// Curved panels ([Triple] CurveRadiusMm): the three views render full-rect into textures (one session-wide size, see
/// <see cref="SizeBucket"/>), and one presenter camera (depth source+5, under the HUD camera at +10) redraws each texture
/// over its third of the window through a command buffer: three 64x36 grid meshes whose UVs follow the eye rays
/// (<see cref="CurvedPanelWarp.SourceUv"/>) drawn with the always-included copy shader under an explicit full-window
/// viewport and a 0..1 orthographic projection. Presentation evidence is the engine's static Camera.onPostRender callback
/// for the presenter camera (the camera completed a frame with its command buffer; it does not by itself prove the three
/// draws were visible, captures do), counted in source frames; a build that never presents, or stops presenting, for 120
/// source frames latches flat with the reason until the rig changes. Textures are rebuilt only when the window size or
/// the session high-water source scale grows; UV grids are rewritten when the rig changes. Same approach as art of rally.
/// </summary>
internal static class TripleCurve
{
    internal const int Columns = 64, Rows = 36;
    private const string ShaderName = "Hidden/BlitCopy";
    private static string _key = "";       // output size and high-water scale: only these rebuild targets and presenter
    private static string _gridKey = "";   // the rig key: a change rewrites the mesh UVs only
    private static double _highX = 1d, _highY = 1d;   // session high-water source scale, never shrinks (the eye oscillates with a speed-varying FOV)
    private static readonly CurveEvidence Evidence = new();
    private static float _depth;
    private static readonly RenderTexture?[] Targets = new RenderTexture?[3];
    private static readonly Mesh?[] Meshes = new Mesh?[3];
    private static readonly Material?[] Materials = new Material?[3];
    private static GameObject? _presenterGo;
    private static Camera? _presenter;
    private static CommandBuffer? _commands;
    private static Camera.CameraCallback? _callback;   // our own delegate, kept so Reset removes exactly it
    private static bool _loggedCallback;

    /// <summary>Why the curve is off for the current key, or null.</summary>
    internal static string? Failure { get; private set; }
    /// <summary>Presenter camera frames seen since the build.</summary>
    internal static long Frames => Evidence.Drawn;
    internal static bool Active => Failure == null && _presenter != null && Targets[0] != null && Targets[1] != null && Targets[2] != null;
    /// <summary>The presenter camera rendered within the last two source frames.</summary>
    internal static bool Presented => Active && Evidence.Presented;
    internal static string Note => Presented ? ", curved" : Active ? ", curved: waiting for the first presented frame" : Failure != null ? " (curve off: " + Failure + ")" : "";
    internal static RenderTexture? Target(int index) => Targets[index];

    internal static bool Owns(Texture? texture)
    {
        if (texture == null) return false;
        foreach (var target in Targets) if (target != null && target.Pointer == texture.Pointer) return true;
        return false;
    }

    /// <summary>True when curved output is ready for <paramref name="key"/>. Builds once per output size; a failure keeps flat output until the key changes.</summary>
    internal static bool Configure(CurvedPanelWarp[] warps, string key, Camera source, int thirdPx, int heightPx)
    {
        var (scaleX, scaleY) = SizeBucket(warps);
        _highX = Math.Max(_highX, scaleX); _highY = Math.Max(_highY, scaleY);
        string resourceKey = thirdPx + "x" + heightPx + "|" + _highX + "|" + _highY;
        if (resourceKey == _key)
        {
            if (!Active) return false;
            try
            {
                if (key != _gridKey) { Grids(warps); _gridKey = key; }
                float depth = source.depth + 5;
                if (depth != _depth) { _depth = depth; TripleNative.SetDepth(_presenter!, depth); }
            }
            catch (Exception ex) { Fail("rig update: " + ex.Message); return false; }
            if (Evidence.SourceFrame() is { } stalled) { Fail(stalled); return false; }
            return true;
        }
        Release();
        _key = resourceKey; _gridKey = ""; Failure = null; Evidence.Reset();
        try
        {
            if (!TripleNative.TargetAvailable) throw new InvalidOperationException("engine binding Camera.set_targetTexture is missing");
            var shader = Shader.Find(ShaderName);
            if (shader == null || !shader.isSupported) throw new InvalidOperationException("the copy shader is unavailable");
            InstallCallback();
            int width = Math.Clamp((int)Math.Round(thirdPx * _highX), 1, thirdPx * 2), height = Math.Clamp((int)Math.Round(heightPx * _highY), 1, heightPx * 2);
            for (var index = 0; index < 3; index++)
            {
                var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                target.name = "Woden.Triple.Curved" + index;
                Targets[index] = target; // owned before Create, so a failed Create is released with the rest
                if (!target.Create()) throw new InvalidOperationException("render target " + index + " could not be created");
                var material = new Material(shader);   // one material per target: deferred commands sample their own texture
                Materials[index] = material;
                material.mainTexture = target;
                Meshes[index] = new Mesh();   // owned before its arrays are set, so a failed setter is released with the rest
                FillMesh(Meshes[index]!, index);
            }
            Grids(warps);
            _gridKey = key;
            _presenterGo = new GameObject("Woden.Triple.Presenter");
            Object.DontDestroyOnLoad(_presenterGo);
            _presenter = _presenterGo.AddComponent(Il2CppType.Of<Camera>()).Cast<Camera>();
            _presenter.enabled = false;
            TripleNative.SetCullingMask(_presenter, 0);
            // The three panels cover every pixel of the window, so a depth-only clear is enough (no background colour setter needed).
            TripleNative.SetClearFlags(_presenter, CameraClearFlags.Depth);
            _depth = source.depth + 5;
            TripleNative.SetDepth(_presenter, _depth);
            _presenter.rect = new Rect(0f, 0f, 1f, 1f);
            _commands = new CommandBuffer();
            _commands.name = "Woden curved panels";
            _commands.SetViewport(new Rect(0f, 0f, Screen.width, Screen.height));
            _commands.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.Ortho(0f, 1f, 0f, 1f, -1f, 1f));
            for (var index = 0; index < 3; index++) _commands.DrawMesh(Meshes[index]!, Matrix4x4.identity, Materials[index]!, 0, 0);
            _presenter.AddCommandBuffer(CameraEvent.AfterEverything, _commands);
            _presenter.enabled = true;
            Evidence.SourceFrame();
            Runtime.Log.LogInfo($"Curved triples on: targets {width}x{height} x3 (scale {_highX}x{_highY}), presenter depth {_depth}, evidence Camera.onPostRender");
            return true;
        }
        catch (Exception ex)
        {
            Fail(ex.Message);
            return false;
        }
    }

    // The engine's static callback sees every camera; ours is the presenter (exact identity). Our delegate is combined with
    // any existing subscriber once and removed again by Reset.
    private static void InstallCallback()
    {
        if (_callback != null) return;
        Camera.CameraCallback mine = (Action<Camera>)OnCameraPostRender;
        var previous = Camera.onPostRender;
        Camera.onPostRender = previous == null ? mine : Il2CppSystem.Delegate.Combine(previous, mine).Cast<Camera.CameraCallback>();
        _callback = mine;
    }

    private static void RemoveCallback()
    {
        if (_callback == null) return;
        try
        {
            var current = Camera.onPostRender;
            Camera.onPostRender = current == null ? null : Il2CppSystem.Delegate.Remove(current, _callback)?.TryCast<Camera.CameraCallback>();
        }
        catch { }
        _callback = null;
    }

    private static void OnCameraPostRender(Camera camera)
    {
        try
        {
            if (_presenter == null || camera == null || camera.Pointer != _presenter.Pointer) return;
            Evidence.Drew();
            if (!_loggedCallback) { _loggedCallback = true; Runtime.Log.LogInfo("Curved triples: presenter camera rendered (Camera.onPostRender)"); }
        }
        catch { }
    }

    /// <summary>The largest source-to-panel scale over the three panels, rounded up to an eighth (at least 1).</summary>
    internal static (double X, double Y) SizeBucket(CurvedPanelWarp[] warps)
    {
        double x = 1d, y = 1d;
        foreach (var warp in warps)
        {
            x = Math.Max(x, warp.SourceSurface.Width / warp.Panel.Width);
            y = Math.Max(y, warp.SourceSurface.Height / warp.Panel.Height);
        }
        return (Math.Ceiling(x * 8d) / 8d, Math.Ceiling(y * 8d) / 8d);
    }

    // One third of the window in the 0..1 orthographic space, (Columns+1)x(Rows+1) vertices; UVs come from Grids.
    private static void FillMesh(Mesh mesh, int index)
    {
        mesh.name = "Woden.Triple.Warp" + index;
        var vertices = new Il2CppStructArray<Vector3>((Columns + 1) * (Rows + 1));
        float x0 = index / 3f, width = 1f / 3f;
        for (var row = 0; row <= Rows; row++)
        for (var column = 0; column <= Columns; column++)
            vertices[(row * (Columns + 1)) + column] = new Vector3(x0 + (width * column / Columns), (float)row / Rows, 0f);
        var triangles = new Il2CppStructArray<int>(Columns * Rows * 6);
        var t = 0;
        for (var row = 0; row < Rows; row++)
        for (var column = 0; column < Columns; column++)
        {
            int a = (row * (Columns + 1)) + column, b = a + 1, c = a + Columns + 1, d = c + 1;
            triangles[t++] = a; triangles[t++] = c; triangles[t++] = b;
            triangles[t++] = b; triangles[t++] = c; triangles[t++] = d;
        }
        mesh.vertices = vertices;
        mesh.uv = new Il2CppStructArray<Vector2>((Columns + 1) * (Rows + 1));
        mesh.triangles = triangles;
    }

    private static void Grids(CurvedPanelWarp[] warps)
    {
        for (var index = 0; index < 3; index++)
        {
            var mesh = Meshes[index];
            if (mesh == null) continue;
            var warp = warps[index];
            var uv = new Il2CppStructArray<Vector2>((Columns + 1) * (Rows + 1));
            for (var row = 0; row <= Rows; row++)
            for (var column = 0; column <= Columns; column++)
            {
                warp.SourceUv((double)column / Columns, (double)row / Rows, out var u, out var v);
                uv[(row * (Columns + 1)) + column] = new Vector2((float)u, (float)v);
            }
            mesh.uv = uv;
        }
    }

    internal static void Fail(string reason)
    {
        Release();
        Failure = reason;
        Runtime.Log.LogWarning("Curved triples off, flat views kept: " + reason);
    }

    /// <summary>Drops the command buffer, presenter, meshes, materials and targets in that order; the key and failure stay so nothing is retried until the rig changes.</summary>
    internal static void Release()
    {
        // Destroy is deferred: disable the old presenter and detach its buffer now so nothing draws beside a presenter rebuilt this frame.
        try { if (_presenter != null) _presenter.enabled = false; } catch { }
        try { if (_presenter != null && _commands != null) _presenter.RemoveCommandBuffer(CameraEvent.AfterEverything, _commands); } catch { }
        try { _commands?.Dispose(); } catch { }
        _commands = null;
        try { if (_presenterGo != null) Object.Destroy(_presenterGo); } catch { }
        _presenterGo = null; _presenter = null;
        for (var index = 0; index < 3; index++)
        {
            try { if (Meshes[index] != null) Object.Destroy(Meshes[index]); } catch { }
            try { if (Materials[index] != null) Object.Destroy(Materials[index]); } catch { }
            try { Targets[index]?.Release(); if (Targets[index] != null) Object.Destroy(Targets[index]); } catch { }
            Meshes[index] = null; Materials[index] = null; Targets[index] = null;
        }
    }

    /// <summary>For Stop: everything released, our callback removed, the high-water size kept for the session, the next start allowed to try again.</summary>
    internal static void Reset()
    {
        Release();
        RemoveCallback();
        _key = ""; _gridKey = ""; Failure = null; Evidence.Reset();
    }
}
