namespace WodenRallyEdge.Core;

public enum MountedView { Stock, Bonnet, Bumper }

// Extra views live only in mod memory. Stock save data always retains a legal
// stock preset index, including when the plugin is removed.
public sealed class CameraCycle
{
    public MountedView View { get; private set; }
    public int ExpectedStockPreset { get; private set; } = -1;
    public bool Advance(bool bonnet, bool bumper)
    {
        if (View == MountedView.Stock) return false;
        View = View == MountedView.Bonnet && bumper ? MountedView.Bumper : MountedView.Stock;
        return true;
    }
    public void StockChanged(int before, int after, bool bonnet, bool bumper)
    {
        ExpectedStockPreset = after;
        if (after <= before) View = bonnet ? MountedView.Bonnet : bumper ? MountedView.Bumper : MountedView.Stock;
    }
    public void Handoff() { View = MountedView.Stock; ExpectedStockPreset = -1; }
    public bool Reconcile(int currentPreset, bool bonnet, bool bumper)
    {
        if (View == MountedView.Stock) return false;
        if (currentPreset != ExpectedStockPreset || View == MountedView.Bonnet && !bonnet || View == MountedView.Bumper && !bumper)
        { Handoff(); return true; }
        return false;
    }
}
