namespace CSVM.Mech3.Anim;

// ---- bind-time resolution census (--node= stages; see ResolutionLines) ---------------------
// Off unless ReportResolution is set, so a normal session collects nothing and logs nothing.
internal enum AnchorKind { ByName, BySymbol, ByRootLift, LiftSuppressed, None }
