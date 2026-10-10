using Dbce.Wheel.Input;

namespace WodenRallyEdge.Core;

/// <summary>Project one raw coherent snapshot into the legacy exact eight-way
/// buttons and the opt-in STD-033 circular matches. Never mix two device reads.</summary>
public static class HatButtons
{
    public static void Project(byte[] physical, int[] pov, byte[] exact, byte[] profile)
    {
        if(physical.Length!=128 || pov.Length!=4 || exact.Length!=160 || profile.Length!=160)
            throw new ArgumentException("One complete input snapshot required");
        for(int i=0;i<128;i++) exact[i]=profile[i]=physical[i]; // native export already returns 0/1
        for(int h=0;h<4;h++)
        {
            int raw=pov[h]; bool valid=raw>=0 && raw<36000 && (raw&0xffff)!=0xffff;
            int direction=valid?((raw+2250)/4500)%8:-1;
            for(int d=0;d<8;d++)
            {
                exact[128+h*8+d]=(byte)(d==direction?1:0);
                profile[128+h*8+d]=(byte)(valid && ControlBinding.HatPressed(raw,d*4500)?1:0);
            }
        }
    }
}
