// Overlay HUD Export - re-entry point for round restarts.
//
// mapspawn_addon.nut runs once per chapter. A same-map round restart - a full-team wipe,
// or a "mission restart" - rebuilds the map's entities without re-running it, which kills
// the exporter's EntFire chain and leaves the app reading a file nothing writes any more.
// This entry point does run on that path, so it is what puts the chain back.
//
// It executes in the map-script scope rather than the root table, so the include is aimed
// explicitly at the root - the same place mapspawn_addon.nut puts everything, and where
// ::OvlHud must live for the scheduled RunScriptCode ticks to resolve it.
//
// Additive: every addon shipping a scriptedmode_addon.nut gets it run, so this cannot
// displace another addon's copy. Nothing about scripted mode itself is touched here.

try
{
	if (!("OvlHud" in getroottable()))
	{
		// Never loaded this session (mapspawn_addon did not run, or ran before this addon
		// was enabled). Including it boots the exporter itself - no re-arm needed on top.
		IncludeScript("overlay_hud_export", getroottable())
	}
	else
	{
		::OvlHud.Rearm("scriptedmode")
	}
}
catch (e)
{
	printl("[OVLHUD] FATAL: scriptedmode re-entry failed - " + e)
}
