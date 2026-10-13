#Requires -Version 7.0
# The full-session flag adds package assertions to the established ordinary probe and verdict contract.
function Resolve-ControlMode {
    [CmdletBinding()]
    param([bool]$ShimHudStopOnly,[bool]$ShimHudOrdinary,[bool]$ShimForceCapture,
          [bool]$OrdinarySavedForward,[bool]$OrdinaryFullSession,[bool]$ShimAppliedForwardCapture,
          [bool]$ShimCarTrial,[bool]$PrivateApply,[bool]$ShimMenuTrial,[bool]$ShimAdmission)
    if($OrdinaryFullSession -and !$OrdinarySavedForward){throw 'Full-session assertion requires the ordinary Saved forward route.'}
    if($ShimHudStopOnly){return 'shim-hud-stop-only'}
    if($ShimHudOrdinary){return 'shim-hud-ordinary'}
    if($ShimForceCapture){return 'shim-force-capture'}
    if($OrdinarySavedForward){return 'shim-applied-forward-ordinary'}
    if($ShimAppliedForwardCapture){return 'shim-applied-forward-capture'}
    if($ShimCarTrial -and $PrivateApply){return 'shim-applied-car-trial'}
    if($ShimCarTrial){return 'shim-car-trial'}
    if($ShimMenuTrial){return 'shim-menu-trial'}
    if($ShimAdmission){return 'shim-admission'}
    return 'raw'
}
