<!-- CHECKPOINT -->
- **Updated:** 2026-11-27 23:30
- **Current task:** 1.0.14 BUILT + FOLDED, NOT published, NOT committed. Owner report: Dvergr too frightened for too long, ignore "stay", leash doubtful.
  Causes found (decompile 1.0.31): (1) vanilla m_fleeIfHurtWhenTargetCantBeReached (MonsterAI.cs:436) never turned off; (2) the leash called
  SetTarget(null), a vanilla no-op (MonsterAI.cs:192-194), and existed only on Ally prefabs; (3) melee m_randomMoveInterval = 0 (cleric 5, ranged 10)
  made posted Dvergr pick a new idle spot every frame. Fix: new DvergrAllyLeash.cs (UpdateAI prefix, tamed ours only: flee off unless
  "Flee When Hurt And Stuck", real leash from leader OR stay post with 60% re-engage), old leash + interval overrides removed.
- **Checks:** build 0/0; refcheck OK client + server (3380 refs, 10 targets); fold OK (zip sha256 4c92cb12...). New autotest phase L (L1-L7) compiles; NOT RUN.
- **Next step:** owner's go for `~/valheim-testbed/heavy.sh 10G dvergrallies-client tools/dvergrallies-autotest/run-autotest.sh` (one game window; DA_AUTOTEST_ONLY=L for just the new phase), then commit + publish.
- **Blocked on:** the owner's go for the game window.
- **Files in progress:** DvergrAllyLeash.cs, DvergrCombatAI.cs, ConfigManager.cs, Phases.cs/AutoTest.cs (phase L), README.src.md, CHANGELOG, versions 1.0.14.
- **Open questions for the owner:** none beyond the test go. VersionStrictness.Patch: server and every client must move to 1.0.14 together.
<!-- /CHECKPOINT -->

# DvergrAllies status

## 1.0.13 (2026-11-23)
- Bug: a player could hit a tamed vanilla Dverger but not a tamed Ally. Vanilla's hit rule (Attack.cs 1152, Projectile.cs 548, Aoe.cs 489) lands a player's hit on anything `IsAggravatable()`; Allies had `m_aggravatable` forced off by DvergrCombatAI, while tamed wild Dvergr kept it on.
- Fix: `DvergrFactionSync` (DvergrFaction.cs) turns `m_aggravatable` off on every peer while the Dvergr is tamed, and the owner clears the saved `s_aggravated` flag on taming. Gated by `Tamed Join Player Faction`.
- Tests: autotest phase H (H1 to H9) in tools/dvergrallies-autotest/src/Phases.cs. A Windows tester confirmed it by hand.
- Test-harness fact: `Stand()` disables BaseAI, which removes it from `BaseAI.BaseAIInstances`, so `AggravateAllInArea` cannot see it. Phase H switches the AI on for its hit window.
