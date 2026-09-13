# Provisional force model v1

This game-side estimate uses the toolkit v0.12.0 ForceShaper and native v0.5.0 constant-force device lifecycle. It does not change vehicle physics or assists. Physical direction and normalization have not yet been validated. [woden-rally-edge-wheel, 2026-09-12]

Front WheelHit force magnitudes weight `tanh(sidewaysSlip / slipScale)`. The signed sum is negated, normalized by a user-set reference front load, and clamped to -1..1. Calibrated steering velocity contributes opposing damping capped at 0.5 normalized units. The user strength percentage scales the combined estimate. Shared conditioning adds smoothing, soft saturation, slew limiting, a fade between 3 and 12 km/h, a half-second ramp and a hard peak cap. Default strength is 10%; default peak cap is 25%. The estimated force units do not establish rack torque or calibrated tyre forces.

The preview runs while disarmed when valid driving samples exist. Native initialization requires explicit session arming, an exact saved physical FFB GUID, stock Logitech ownership transfer, focus, a closed panel and valid forward-driving contact samples. Enabled wheel input must be available. A single grounded front wheel can contribute; zero front load or unavailable front-contact data suppresses output. Reverse is suppressed pending sign validation.

0.2.2 also requires recent positive player-camera ownership. Game takeover,
replay/photo/finish, camera transitions and an external pose writer release the
mounted camera and suspend FFB. Native output acceptance does not establish that
these transitions have been physically tested; that remains an attended gate.

The device controller closes read slots before initializing FFB, then reopens them to share the native wheel handle. Teardown sends zero, stops effects, shuts down FFB, then reopens affected readers. Initialization sets a 150 ms constant-force hold watchdog and requires exit guards. Failed output writes stop the device and defer reconnect attempts. No condition or periodic effects bypass the hold watchdog. F8 disarms; arming is never stored on disk.

The game ships Logitech steering components. Mod ownership disables those readers, stops their coroutines, and resolves `LogiSteeringShutdown` only from an already-loaded game SDK module. The official [Logitech steering SDK](https://www.logitechg.com/sdk/LogitechSteeringWheelSDK_8.75.30.zip) declares its void shutdown export. Failure to resolve a loaded SDK's shutdown blocks mod force output. Native ownership behavior and physical feel need separate validation.

Woden's Unity binding layer strips GUI controls and style/cursor setters. The F6 panel uses fixed IMGUI rectangles/events, with named engine bindings for style/cursor properties. Signatures were checked against local generated code and Unity's [GUIStyle source](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Modules/IMGUI/GUIStyle.bindings.cs) and [Cursor source](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Runtime/Export/Input/Cursor.bindings.cs). Runtime rendering confirmed those optional bindings resolve for the guarded game build.

The first contact sampling crash exposed an unrelated generated wrapper defect: `GetGroundHit(out WheelHit)` offered 8 bytes for a 72-byte result. WheelContact now uses actual IL2CPP value storage, as verified during the subsequent level-entry run. Do not restore the generated out wrapper. Contact telemetry and this force estimate both depend on that corrected boundary.
