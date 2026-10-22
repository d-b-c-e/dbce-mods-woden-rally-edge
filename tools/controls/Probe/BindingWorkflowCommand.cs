#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using Dbce.Wheel.Input;

namespace Woden.ControlsTesting
{
    // DRIVE 5919002/Kiki14529ccb request/step/reply convention, over the existing
    // admission mailbox and actual shared host. No binding parser or writer.
    internal sealed class BindingWorkflowCommand
    {
        private readonly HashSet<string> steps = new HashSet<string>(StringComparer.Ordinal);
        private string? request;
        internal void Claim(string[] words, string? ownedRequest, bool ready, string verb)
        {
            if (!ready || !Guid.TryParseExact(ownedRequest, "N", out _) || words.Length < 4 || words.Length > 6 ||
                words[0] != verb || words[1] != ownedRequest || !Guid.TryParseExact(words[2], "N", out var step) ||
                step == Guid.Empty || step.ToString("N") != words[2])
                throw new InvalidOperationException("Bindings require the owned private muted request and a unique step ID.");
            if (request != ownedRequest) { request = ownedRequest; steps.Clear(); }
            if (steps.Count >= 64 || !steps.Add(words[2]))
                throw new InvalidOperationException("Bindings refuse repeated steps or more than 64 steps per request.");
        }
        internal (object Reply, bool Saved) Run(string[] words, string? ownedRequest, bool ready, bool panelOpen,
            bool focused, bool ownedForeground, bool offStage, Func<ISharedBindingHost?> getHost)
        {
            Claim(words, ownedRequest, ready, "bindings");
            if (!offStage || (words[3] != "status") && (!panelOpen || !focused || !ownedForeground))
                throw new InvalidOperationException("Bindings require the real foreground F6 editor, focused in the menus.");
            string operation = words[3];
            int count = operation == "invert" || operation == "begin" ? 6 : 4;
            if (words.Length != count || operation != "status" && operation != "invert" && operation != "save" &&
                operation != "discard" && operation != "begin" && operation != "cancel")
                throw new ArgumentException("Bindings accepts status, invert <action> on|off, save, discard, begin <action> <fresh-device-guid>, or cancel.");
            var host = getHost() ?? throw new InvalidOperationException("No shared binding host is active.");
            if (operation != "status" && host.Access != SharedBindingAccess.Editable) throw new InvalidOperationException(host.AccessReason);
            bool accepted = true;
            string message;
            switch (operation)
            {
                case "invert":
                    var row = host.Rows.SingleOrDefault(r => r.Action == words[4]);
                    if (row == null || !row.IsAxis || host.CapturingAction != null || words[5] != "on" && words[5] != "off")
                        throw new ArgumentException("Invert requires an existing axis, on|off and no capture.");
                    bool inverted = words[5] == "on";
                    host.SetInverted(words[4], inverted);
                    var after = host.Rows.Single(r => r.Action == words[4]);
                    accepted = (after.DraftInverted ?? after.NextStartInverted ?? after.ActiveInverted) == inverted;
                    message = host.CaptureMessage; break;
                case "save": accepted = host.Save(out message); break;
                case "discard": host.Discard(); message = host.CaptureMessage; break;
                case "begin":
                    if (host.CapturingAction != null || !Guid.TryParse(words[5], out var instance) ||
                        !host.Devices.Any(d => Guid.TryParse(d.InstanceGuid, out var available) && instance == available))
                        throw new ArgumentException("Begin requires an explicitly selected fresh device and no capture.");
                    host.BeginCapture(words[4], instance.ToString("D"));
                    accepted = host.CapturingAction == words[4]; message = host.CaptureMessage; break;
                case "cancel": host.CancelCapture(); message = host.CaptureMessage; break;
                default: message = host.CaptureMessage; break;
            }
            return (new {
                schema = "woden.binding-workflow-step@1", requestId = ownedRequest, stepId = words[2], operation,
                admitted = true, accepted, focused, ownedForeground, panelOpen, offStage, access = host.Access.ToString(), message,
                activeSource = host.ActiveSource, hasDraft = host.HasDraft, savedPendingRestart = host.SavedPendingRestart,
                capturingAction = host.CapturingAction, devices = host.Devices.ToArray(), rows = host.Rows.ToArray()
            }, operation == "save" && accepted);
        }
    }

    internal static class BindingWorkflowEvidence
    {
        internal const int Limit = 256 * 1024;
        internal static string Persist(string directory, string request, string step, string json)
        {
            if (!Guid.TryParseExact(request, "N", out var nonce) || nonce == Guid.Empty || nonce.ToString("N") != request ||
                !Guid.TryParseExact(step, "N", out var id) || id == Guid.Empty || id.ToString("N") != step)
                throw new InvalidDataException("Workflow evidence requires canonical owned request and step IDs.");
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            if (bytes.Length > Limit) throw new InvalidDataException("Workflow reply exceeds the 256 KiB evidence limit; no reply was truncated.");
            using var document = JsonDocument.Parse(bytes);
            var root = document.RootElement;
            if (root.GetProperty("requestId").GetString() != request || root.GetProperty("stepId").GetString() != step)
                throw new InvalidDataException("Workflow reply identity differs.");
            bool accepted = root.GetProperty("accepted").GetBoolean();
            string name = "f6-reply-" + step + ".json";
            using (var file = new FileStream(Path.Combine(directory, name), FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                file.Write(bytes, 0, bytes.Length);
            return JsonSerializer.Serialize(new { schema = "woden.workflow-evidence@1", requestId = request, stepId = step,
                accepted, artifact = name, bytes = bytes.Length, sha256 = Convert.ToHexString(SHA256.HashData(bytes)) });
        }
    }
}
