using System.Text.Json;
using BepInEx.Logging;
using Dbce.TripleScreen;
using UnityEngine;
using WodenTripleScreenProbe.Core;

namespace WodenTripleScreenProbe;

internal sealed class ProbeRenderer
{
    private const int TargetWidth = 192, TargetHeight = 108;
    private readonly LayoutV1 _layout;
    private readonly string _directory;
    private readonly ManualLogSource _log;
    private readonly NativeCamera _native;
    private readonly GameObject?[] _objects = new GameObject?[3];
    private readonly Camera?[] _cameras = new Camera?[3];
    private readonly RenderTexture?[] _targets = new RenderTexture?[3];
    private Texture2D? _readback;
    private Camera? _source;
    private int _sourceId, _lastObservedFrame = -1, _lastRenderFrame = -1, _completedFrames;
    private bool _failed, _stopped, _multiplePlayersObserved;

    internal ProbeRenderer(LayoutV1 layout, string directory, ManualLogSource log)
    {
        _layout = layout; _directory = directory; _log = log;
        _native = new NativeCamera();
        WriteStatus("starting", 0, null, "WAITING_FOR_CHASE_CAMERA", "No private render frame observed yet.");
    }

    internal void Observe(Car_Cam owner)
    {
        if (_stopped || _failed) return;
        try
        {
            var car = owner.Maincar_;
            var source = owner.field_Private_Camera_0;
            if (car == null || !car.IsPlayer) return;
            if (car.PlayerIndex != 0)
            {
                _multiplePlayersObserved = true;
                Release();
                WriteStatus("rejected", 0, null, "MULTIPLE_PLAYERS",
                    "Private probe is limited to a single selected player.");
                return;
            }
            bool eligible = !_multiplePlayersObserved &&
                car.Status == MainCar.CarStatus.RACE && !car.Replay && !car.Respawning &&
                owner.Mode == Car_Cam.Cam_Mode.Chase && !owner.Changing &&
                source != null && source.enabled && source.gameObject.activeInHierarchy &&
                !source.orthographic && Application.isFocused;
            if (!eligible)
            {
                if (_sourceId != 0)
                {
                    Release();
                    WriteStatus("inactive", 0, null, "CAMERA_INELIGIBLE", "Selected chase camera is unavailable.");
                }
                return;
            }

            int frame = Time.frameCount;
            _lastObservedFrame = frame;
            if (frame == _lastRenderFrame || frame % 30 != 0) return;
            _lastRenderFrame = frame;
            if (_source == null || _sourceId != source!.GetInstanceID())
            {
                Release();
                _source = source;
                _sourceId = source!.GetInstanceID();
                CreatePrivateTargets();
            }

            // Toolkit planes are eye-relative with forward -Z. Reflect Z to Unity's local
            // transform convention (+Z forward); Unity's Frustum supplies its own depth matrix.
            var plans = _layout.Projections(Math.Max(source!.nearClipPlane, .01f), source.farClipPlane)
                .Select(UnityPanelPlan.From).ToArray();
            for (int i = 0; i < 3; i++)
            {
                var camera = _cameras[i]!;
                camera.enabled = false;
                _native.SetTarget(camera, _targets[i]);
                _native.SetMask(camera, source.cullingMask);
                _native.SetClear(camera, source.clearFlags);
                _native.SetNear(camera, source.nearClipPlane);
                camera.farClipPlane = source.farClipPlane;
                camera.fieldOfView = source.fieldOfView;
                camera.depthTextureMode = source.depthTextureMode;
                camera.rect = new Rect(0, 0, 1, 1);
                camera.transform.position = source.transform.position;
                var p = plans[i];
                var forward = new Vector3((float)p.LocalForward.X, (float)p.LocalForward.Y,
                    (float)p.LocalForward.Z);
                var up = new Vector3((float)p.LocalUp.X, (float)p.LocalUp.Y,
                    (float)p.LocalUp.Z);
                camera.transform.rotation = source.transform.rotation * Quaternion.LookRotation(forward, up);
                camera.projectionMatrix = Matrix4x4.Frustum((float)p.Left, (float)p.Right,
                    (float)p.Bottom, (float)p.Top, (float)p.Near, (float)p.Far);
                _native.Render(camera);
            }

            _completedFrames++;
            if (_completedFrames % 4 == 0) SampleFrame(frame);
        }
        catch (Exception ex)
        {
            _failed = true;
            _log.LogError("Triple-screen private render failed; probe latched off: " + ex);
            Release();
            WriteStatus("error", 0, null, "PRIVATE_RENDER_FAILED", ex.GetType().Name);
        }
    }

    internal void Tick()
    {
        if (_stopped || _failed) return;
        if (_sourceId != 0 && (Time.frameCount - _lastObservedFrame > 90 || _source == null))
        {
            Release();
            WriteStatus("inactive", 0, null, "CAMERA_STALE", "Player chase camera no longer observed.");
        }
    }

    private void CreatePrivateTargets()
    {
        for (int i = 0; i < 3; i++)
        {
            var gameObject = new GameObject("Woden Triple-Screen Private Probe " + i);
            gameObject.hideFlags = HideFlags.HideInHierarchy;
            var camera = gameObject.AddComponent<Camera>();
            camera.enabled = false;
            var target = new RenderTexture(TargetWidth, TargetHeight, 16, RenderTextureFormat.ARGB32);
            target.name = "WodenTripleScreenPrivate" + i;
            if (!target.Create()) throw new InvalidOperationException("Private render target creation failed.");
            _native.SetTarget(camera, target);
            _objects[i] = gameObject; _cameras[i] = camera; _targets[i] = target;
        }
        _readback = new Texture2D(TargetWidth, TargetHeight, TextureFormat.RGB24, false);
    }

    private void SampleFrame(int frame)
    {
        // Readback is deliberately rare and tiny. Successful Camera.Render calls alone are
        // insufficient evidence, so record sampled target pixels and separate camera IDs.
        var previous = RenderTexture.active;
        var samples = new string[3];
        try
        {
            for (int i = 0; i < 3; i++)
            {
                RenderTexture.active = _targets[i];
                _readback!.ReadPixels(new Rect(0, 0, TargetWidth, TargetHeight), 0, 0);
                _readback.Apply();
                samples[i] = Pixel(_readback.GetPixelBilinear(.125f, .25f)) +
                    Pixel(_readback.GetPixelBilinear(.5f, .5f)) +
                    Pixel(_readback.GetPixelBilinear(.875f, .75f));
            }
        }
        finally { RenderTexture.active = previous; }

        var now = DateTimeOffset.UtcNow;
        var evidence = JsonSerializer.Serialize(new
        {
            utc = now, gameFrame = frame, layoutSha256 = _layout.Sha256,
            cameraIds = _cameras.Select(c => c!.GetInstanceID()).ToArray(),
            targetIds = _targets.Select(t => t!.GetInstanceID()).ToArray(),
            sampledRgb = samples, privateWidthPx = TargetWidth, privateHeightPx = TargetHeight,
            renderCallsCompleted = 3
        });
        File.AppendAllText(Path.Combine(_directory, "probe-frame-evidence.jsonl"), evidence + Environment.NewLine);
        WriteStatus("degraded", 3, now, "PRIVATE_TARGETS_ONLY",
            "Three low-resolution private targets were sampled; no display or visual seam is verified.");
    }

    private static string Pixel(Color color) => $"{(byte)(Math.Clamp(color.r, 0, 1) * 255):X2}" +
        $"{(byte)(Math.Clamp(color.g, 0, 1) * 255):X2}{(byte)(Math.Clamp(color.b, 0, 1) * 255):X2}";

    private void WriteStatus(string state, int cameraCount, DateTimeOffset? lastFrame,
        string code, string message)
    {
        try
        {
            Directory.CreateDirectory(_directory);
            var status = new
            {
                schemaVersion = 1, adapterId = "woden-triplescreen-probe",
                adapterVersion = Plugin.Version, gameId = "super-woden-rally-edge",
                gameBuild = "21802346", state,
                acceptedLayoutSha256 = _layout.Sha256, layoutContractVersion = 1,
                topology = (string?)null, activeCameraCount = cameraCount,
                activeCapabilities = Array.Empty<string>(), lastSuccessfulFrameUtc = lastFrame,
                diagnostics = new[] { new { code, severity = state is "error" or "rejected" ? "error" : "info", message } }
            };
            string path = Path.Combine(_directory, "probe-runtime-status.json");
            string temporary = path + ".probe.tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(status));
            File.Move(temporary, path, true);
        }
        catch (Exception ex) { _log.LogWarning("Triple-screen status write failed: " + ex.Message); }
    }

    private void Release()
    {
        _source = null; _sourceId = 0;
        for (int i = 0; i < 3; i++)
        {
            try
            {
                if (_cameras[i] != null) _native.SetTarget(_cameras[i]!, null);
                if (_targets[i] != null) { _targets[i]!.Release(); UnityEngine.Object.Destroy(_targets[i]); }
                if (_objects[i] != null) UnityEngine.Object.Destroy(_objects[i]);
            }
            catch (Exception ex) { _log.LogWarning("Private target cleanup: " + ex.Message); }
            _cameras[i] = null; _targets[i] = null; _objects[i] = null;
        }
        try { if (_readback != null) UnityEngine.Object.Destroy(_readback); }
        catch (Exception ex) { _log.LogWarning("Private readback cleanup: " + ex.Message); }
        _readback = null;
    }

    internal void Stop()
    {
        if (_stopped) return;
        _stopped = true;
        Release();
        WriteStatus("inactive", 0, null, "STOPPED", "Probe unloaded; no camera ownership retained.");
    }
}
