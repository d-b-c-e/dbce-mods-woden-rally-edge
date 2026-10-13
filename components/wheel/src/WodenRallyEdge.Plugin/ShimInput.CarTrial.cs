namespace WodenRallyEdge;

internal static partial class ShimInput
{
    private static bool _carTrial, _carTrialDelivery, _carTrialEnded;
    internal static void ConfigureCarTrial(string nonce,string session,string batch,DateTimeOffset expires,bool fencesVerified)
    {
        ConfigureMenuTrial(nonce,session,batch,expires,fencesVerified);
        _carTrial=true;
    }
    // One arm after native countdown and identity checks, then permanent neutral.
    internal static void CarTrialDelivery(bool enabled)
    {
        if (!_carTrial || enabled && (!Ready || _carTrialDelivery || _carTrialEnded || !ShiftReady(Runtime.Local)))
            throw new InvalidOperationException("Car trial delivery requires actual Manual car and cannot rearm");
        _carTrialDelivery=enabled;
        if (!enabled) _carTrialEnded=true;
        _buttons.Observe(Actions,-1,false);_context=-1;
    }
}
