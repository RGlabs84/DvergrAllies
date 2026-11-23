<!-- CHECKPOINT -->
- **Updated:** 2026-11-23
- **Current task:** none open. 1.0.13 (no friendly fire on tamed wild Dvergr) is live on Hexium (upload id 523) and Thunderstore, both downloaded back and compared (Thunderstore 7 of 7 files identical; Hexium 6 of 7, only its rewritten manifest.json differs).
- **Next step:** when the next change starts, bump manifest, csproj `<Version>` and `Plugin.cs` PluginVersion together; run `~/valheim-testbed/heavy.sh 10G dvergrallies-client tools/dvergrallies-autotest/run-autotest.sh` (60 checks at 1.0.13), then `python3 -I tools/fold-package.py --check-badges`.
- **Blocked on:** nothing.
- **Files in progress:** none. main = `v1.0.13` on github.com/RGlabs84/DvergrAllies, with a GitHub release carrying the shipped zip.
- **Open questions for the owner:**
  - 1.0.13's release gate was not complete: no server smoke test (no `tools/server-smoke.sh` here; BlightedHeart's can be ported), no DESIGN.md for a design check, no load of a 1.0.12 world, no two-player run.
<!-- /CHECKPOINT -->

# DvergrAllies status

## 1.0.13 (2026-11-23)
- Bug: a player could hit a tamed vanilla Dverger but not a tamed Ally. Vanilla's hit rule (Attack.cs 1152, Projectile.cs 548, Aoe.cs 489) lands a player's hit on anything `IsAggravatable()`; Allies had `m_aggravatable` forced off by DvergrCombatAI, while tamed wild Dvergr kept it on.
- Fix: `DvergrFactionSync` (DvergrFaction.cs) turns `m_aggravatable` off on every peer while the Dvergr is tamed, and the owner clears the saved `s_aggravated` flag on taming. Gated by `Tamed Join Player Faction`.
- Tests: autotest phase H (H1 to H9) in tools/dvergrallies-autotest/src/Phases.cs. A Windows tester confirmed it by hand.
- Test-harness fact: `Stand()` disables BaseAI, which removes it from `BaseAI.BaseAIInstances`, so `AggravateAllInArea` cannot see it. Phase H switches the AI on for its hit window.
