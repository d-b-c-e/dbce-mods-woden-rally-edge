namespace Woden.ControlsTesting;

// Pure consumer gate. Native enable and every raw submission remain outside it.
// Revocation is terminal for the nonce, including an arm already on disk.
public sealed class SessionGate
{
    public bool Armed { get; private set; }
    public bool Revoked { get; private set; }
    public int ArmSequence { get; private set; }
    public SessionClient? Client { get; private set; }
    public void Arm(SessionClient client, int sequence, bool nativeFocused, bool gameFocused)
    {
        if(Revoked || Armed || sequence<=0 || !nativeFocused || !gameFocused)
            throw new InvalidDataException("Session arm refused; no current native/game focus or terminal nonce.");
        Client=client;ArmSequence=sequence;Armed=true;
    }
    public bool AdmitRow(bool nativeFocused, bool gameFocused)
    {
        if(!Armed || Revoked)return false;
        if(!nativeFocused || !gameFocused){Revoke();return false;}
        return true;
    }
    public void Revoke(){Revoked=true;Armed=false;}
}
