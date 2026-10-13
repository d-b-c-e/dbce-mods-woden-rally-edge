namespace WodenRallyEdge;

internal static class HudDepthChecks
{
    internal static void Run(Action<bool, string> check)
    {
        check(TripleHudDepth.Production == new TripleHudDepth(100, 110),
            "ordinary HUD depth uses the sampled plane and far clip without force or build gates");
        double tan = Math.Tan(TripleHudDepth.FieldOfView * Math.PI / 360);
        double worldQuantum = Math.Abs(MathF.BitIncrement(TripleHudDepth.CameraY) - TripleHudDepth.CameraY);
        check(worldQuantum == 1d / 1024, "actual float32 spacing at preserved isolated camera origin");
        double oldQuantum = worldQuantum * 1440 / (2 * tan), newQuantum = oldQuantum / 100;
        check(oldQuantum > 1.21 && oldQuantum < 1.22 && newQuantum > .0121 && newQuantum < .0122,
            "projected spacing falls 100 times; not a rendered-jitter assertion");
        foreach (var size in new[] { (1280, 720), (1920, 1080), (2560, 1440), (3840, 2160) })
        foreach (var p in new[] { new TripleHudDepth(1, 10), new TripleHudDepth(100, 110) })
        {
            check(p.Plane > TripleHudDepth.Near && p.Plane < p.Far, "flat canvas inside unchanged near and selected far planes");
            double height = 2 * p.Plane * tan, width = height * size.Item1 / size.Item2;
            double maxError = 0;
            for (int i = 0; i <= 1000; i++)
            {
                double u = i / 1000d, v = (1000 - i) / 1000d;
                double x = (u - .5) * width, y = (v - .5) * height;
                double pixelX = (x / width + .5) * size.Item1, pixelY = (y / height + .5) * size.Item2;
                check(Math.Abs(pixelX - u * size.Item1) < 1e-9 && Math.Abs(pixelY - v * size.Item2) < 1e-9,
                    "same ideal normalized point reaches same pixels at either plane depth");
                // Model an explicit world-space rounding step. Native crops
                // sample static HUD edges, not every engine or scanout path.
                float worldY = TripleHudDepth.CameraY + (float)y;
                float cameraY = worldY - TripleHudDepth.CameraY;
                maxError = Math.Max(maxError, Math.Abs((cameraY / height + .5) * size.Item2 - pixelY));
            }
            check(maxError <= worldQuantum * size.Item2 / height * .51, "float32 projection error bounded without claiming an engine path");
            if (p.Plane == 100) check(maxError < .01, "depth100 worst sampled Y error remains subpixel through 2160p");
        }
        double newTop = TripleHudDepth.CameraY + 110 * tan;
        check(newTop < -9936, "extended frustum still does not approach origin Y");
        // Counterexample prevents the fixture from claiming identical culling:
        // a foreign UI-layer point 50 units forward was clipped at 10, not at 110.
        check(50 > new TripleHudDepth(1, 10).Far && 50 < new TripleHudDepth(100, 110).Far,
            "foreign UI volume inventory remains necessary");
        float fixedLocal = .12345f;
        float fixedProjected = (TripleHudDepth.CameraY + fixedLocal) - TripleHudDepth.CameraY;
        for (int i = 0; i < 100; i++)
            check((TripleHudDepth.CameraY + fixedLocal) - TripleHudDepth.CameraY == fixedProjected,
                "fixed geometry does not spontaneously jitter from a fixed offset");
    }
}
