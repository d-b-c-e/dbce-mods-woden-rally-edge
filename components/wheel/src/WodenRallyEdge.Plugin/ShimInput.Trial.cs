using WodenRallyEdge.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WodenRallyEdge;

// Narrow developer seam, configured only by the cold, hash-verified controls
// addon before ownership. No saved option or ordinary-launch activation.
internal static partial class ShimInput
{
    private static string? _trialNonce, _trialSession, _trialBatch;
    private static DateTimeOffset _trialExpiry;
    private static int _trialScope;
    private static ulong _sequence;
    internal static byte Run { get; private set; }
    internal static ulong Sequence => _sequence;
    private static bool TrialFences => Runtime.DiagnosticNoForce && !Runtime.Settings.FfbEnabled &&
        !Runtime.Settings.TelemetryEnabled && !Runtime.Settings.Record && (_carTrial || Runtime.Local==null) && !Panel.Open &&
        DateTimeOffset.UtcNow < _trialExpiry;
    private static string TrialUnsupported => string.Concat(ShimActionMap.Names.Where(id=>_carTrial ?
        id is not ("steer" or "throttle" or "brake" or "handbrake" or "shiftUp" or "shiftDown") : id is not ("confirm" or "start" or "navLeft" or "navRight"))
        .Select(id=>id+"=outside bounded Woden control trial\n"));
    internal static void ConfigureMenuTrial(string nonce,string session,string batch,DateTimeOffset expires,bool fencesVerified)
    {
        if (Owns || _trialNonce!=null || !_requested || !fencesVerified || !Runtime.DiagnosticNoForce ||
            Runtime.Settings.FfbEnabled || Runtime.Settings.TelemetryEnabled || Runtime.Settings.Record ||
            nonce.Length!=32 || nonce.Any(c=>!Uri.IsHexDigit(c)) || !Path.IsPathFullyQualified(session) || !Path.IsPathFullyQualified(batch) ||
            !File.Exists(session) || File.Exists(batch) || expires<=DateTimeOffset.UtcNow || expires>DateTimeOffset.UtcNow.AddMinutes(5))
            throw new InvalidOperationException("Menu trial must be cold, bounded and independently muted before ownership.");
        _trialNonce=nonce;_trialSession=session;_trialBatch=batch;_trialExpiry=expires;_trialScope=0;
    }
    // Monotonic scope: none -> daily confirm -> title start -> map left/right -> none.
    internal static void MenuTrialScope(int scope)
    {
        if(_carTrial || _trialNonce==null || scope!=_trialScope+1 || scope>4) throw new InvalidOperationException("Menu trial scope cannot widen or repeat.");
        _trialScope=scope;_buttons.Observe(Actions,-1,false);_context=-1;
    }
    private static bool TrialDelivers(string label) => _trialNonce==null || (_carTrial ?
        _carTrialDelivery && label is "Gear up" or "Gear down" : _trialScope switch {
        1=>label=="Confirm",2=>label=="Pause",3=>label is "Menu left" or "Menu right",_=>false });
    private static ulong KnownNeutralMask => _trialNonce!=null && Ready && Frame[41]==1 && Frame[42]==1 ?
        ShimActionMap.Buttons.Where(p=>TrialDelivers(p.Key)).Aggregate(0UL,(mask,p)=>mask|(1UL<<ShimActionMap.Index(p.Value))) : 0;
    internal static bool KnownNeutral(string id) => Ready && (KnownNeutralMask&(1UL<<ShimActionMap.Index(id)))!=0 &&
        Action(id) is {Status:ShimStatus.TestNeutral,Value:0,Injected:false};
    internal static bool TrialNativeMenuAllowed()
    {
        if(_trialNonce==null) return true;
        if(!Ready) return false;
        try
        {
            if(Input.anyKey || Keyboard.current?.anyKey.isPressed==true || Mouse.current?.leftButton.isPressed==true ||
                Mouse.current?.rightButton.isPressed==true || Mouse.current?.middleButton.isPressed==true)
            { Stop("Physical keyboard/mouse input interrupted menu trial");return false; }
            return Allowed;
        }
        catch(Exception e) { Stop("Cannot verify physical idle: "+e.Message);return false; }
    }
    private static bool AcceptFrame(ulong sequence,byte run)
    {
        if(sequence<=_sequence) { RefuseOrStop("Non-increasing shared frame sequence");return false; }
        _sequence=sequence;Run=run;
        bool latched=_trialNonce!=null;
        if(Frame[41]!=(latched?(byte)1:(byte)0) || Frame[42]!=(latched?(byte)1:(byte)0) ||
            !latched && Actions.Any(a=>a.Injected || a.Status==ShimStatus.TestNeutral))
        { RefuseOrStop("Unexpected latched/native-suppression frame provenance");return false; }
        if(latched && (!TrialFences || !InputFocused || run is 5 or 6 or 7))
        { RefuseOrStop("Menu trial fence, focus, expiry or Core terminal failure");return false; }
        return true;
    }
}
