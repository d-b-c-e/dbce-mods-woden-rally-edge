namespace WodenRallyEdge.Core;

// CameraTuning's optional binding-editor dependency. The assertions execute its
// real defaults/math and the real renderer planning helpers; no device provider,
// native input, Unity, BepInEx or settings instance is loaded by this fixture.
public sealed class Bindings
{
    public Dictionary<string, string> CameraKeys { get; set; } = new();
    public Dictionary<string, int> Buttons { get; set; } = new();
}
