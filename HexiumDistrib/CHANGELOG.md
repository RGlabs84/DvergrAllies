# Changelog

## 1.0.8
- **Valkyrie's Cargo compatibility: Ingvar is never tamed.** Valkyrie's Cargo's travelling merchant, Ingvar, is not a prefab of his own - he is a plain vanilla Dverger that the mod tags on its save data (`VCargo_ingvar`) and dresses up at runtime. Dvergr Allies makes every Dverger tameable at load time, so he was inheriting the taming, breeding and genetics components too. The visible symptom: with both mods installed, pressing **[E] on Ingvar petted him** (Shift+E offered to rename him) instead of opening the trade, because the taming component sat ahead of the merchant script in his interaction lookup. Less visibly, Valkyrie's Cargo marks him tamed for its own reasons, so he could have bred with your Dvergr, been given a gender and an owner tag on data that mod owns, and been counted as one of your tamed Dvergr in the BarrkBOT export.
  - He is now recognised by that tag the moment he appears on your machine, and every Dvergr Allies component is removed from him before you can look at him: no taming, no petting, no renaming, no follow/stay commands, no breeding, no gender, no stealth flag, no owner tag, no place in the census. The per-frame wild-Dvergr AI adjustments leave him alone as well - his behaviour is the merchant script's to drive.
  - Wild Dvergr are untouched: the prefab-level taming injection is exactly as before, and the check costs a wild Dvergr one integer read as it spawns.
  - Recognition is by the save-data tag, not by whether Valkyrie's Cargo is installed. A merchant left behind in a world after that mod is removed still keeps his hands off your taming pens.
  - Startup logs `[Compat] Valkyrie's Cargo detected` when it is present, and one `[Compat] Valkyrie's Cargo merchant ... woke on this machine` line each time Ingvar lands, so the exclusion can be seen working.
  - One more Harmony patch (`Tameable.Awake`), so a healthy log now reads `[Patches] 8 applied, none failed.`

## 1.0.7
- **Valheim 1.0.7 compatibility.** Rebuilt for Valheim 1.0.7 (client build 25185596 / dedicated server build 25185644, network version 39) against BepInExPack 5.4.2350 and **Jotunn 2.30.0** - the first Jotunn release for Valheim 1.0, which is now the minimum required version. The Jotunn build reference is pinned to exactly 2.30.0 so a rebuild cannot silently pick up a pre-1.0 Jotunn and produce a DLL that only fails once it is in the game.
- **Fixed: the mod could not be loaded on Valheim 1.0.** Valheim 1.0 added a member to the game's `Hoverable` interface - `GetHoverOffset`, an extra reach the player's cursor allows for that object. `DvergrTameable`, which puts the pregnancy / hunger lines on a Dvergr's hover text, implements that interface, and a class that is missing an interface member cannot be loaded by the runtime at all ("VTable setup failed"), so on 1.0 everything that touches it - the taming setup for the ally prefabs and for wild Dvergr - would have thrown. It now provides the member, forwarding to the creature's own value, so a Dvergr's interact reach is exactly what its prefab declares (zero, for every vanilla creature) and nothing about hovering or interacting changes.
- Nothing else needed to change. All seven Harmony patches (`Character.SetTamed` x2, `Character.OnDeath`, `MonsterAI.UpdateAI`, `ZNetScene.Awake`, `ZNet.Shutdown`, `Trader.Start`) resolve on 1.0.7 unchanged, and the 1.0 engine changes that broke other mods (sector ids, equipment visuals, save paths, `Inventory.Load`) are all in code this mod never touches. Verified on a 1.0.7 dedicated server: `[Patches] 7 applied, none failed`, 21 prefabs / 10 items / 1 status effect registered through Jotunn, Taming/Breeding components injected into 8 wild Dvergr prefabs, BarrkBOT export armed.

## 1.0.6
- **Critical fix: the stats export never started, and the Haldor contract quietly disappeared.** In 1.0.4 and 1.0.5 one Harmony patch aimed at a method that does not exist where the code claimed it did. Harmony refuses to guess, so it threw — and because the game applies patches in one all-or-nothing pass, that single mistake took down everything after it: the BarrkBOT stats exporter was never even created, and the **Dvergr Contract stopped appearing in Haldor's stock**. Both are fixed and working again.
  - If you ran 1.0.4 or 1.0.5 on a server, the contract has been missing from Haldor since you installed it, and `BepInEx/config/DvergrAllies/` was never created. No save data was harmed and nothing was lost — the features simply never switched on.
  - The failure left **no trace in `LogOutput.log`**, because Unity's own error log is not written to disk by default. That is the real reason it went unnoticed, and it is what the rest of this release addresses.
- **A broken patch can no longer take the mod down with it.** Patches are now applied one at a time. If a future game update moves or renames something, that one feature switches off, says so by name in the log, and everything else keeps working — instead of the whole mod silently going quiet.
  - The log now always reports the tally: `[Patches] N applied, none failed.`
- **The export says what it is doing.** It now logs one line when it arms at startup, and one more whenever the answer to "is it writing?" changes — waiting for the world, idle because the setting is off, or running. A healthy export and an export that never started can no longer look identical.
- **The export loop can no longer die in silence.** A hiccup reading the world database costs that single cycle and is reported; previously it could stop the export for the rest of the session with nothing to show for it.

## 1.0.5
- **Failure-Path Test Switch (`Force Census Unavailable`):** A new testing-only setting under `6 - BarrkBOT Export` that makes the export report `census_unavailable` on purpose, so server owners can confirm the failure path behaves correctly on a live server without having to break anything to see it.
  - Population counts are written as `null` rather than `0` — "we couldn't count" never masquerades as "nothing is alive".
  - Lifetime history is untouched and keeps recording normally while the switch is on.
  - The export states plainly that the condition was *forced*, so a drill can never be mistaken for a real fault — and anyone who leaves the switch on by accident can see exactly why their numbers disappeared.
  - Leave it off in normal play.

## 1.0.4
- **BarrkBOT Stats Export (New):** The server now writes your Dvergr empire's stats to JSON so BarrkBOT can answer questions about it in Discord — who has the biggest warband, who's lost the most, who got lucky on contracts.
  - **Full-world census.** Reads the save database directly rather than only what's loaded, so Dvergrs standing in zones nobody has visited for weeks are still counted.
  - **History that survives restarts.** Births, deaths, contracts and gacha pulls are banked to a per-world counter file as they happen. Deaths especially — a dead Dvergr leaves nothing behind to count later, so it has to be recorded at the moment it dies.
  - **Per-player attribution.** Dvergrs are tagged with whoever tamed or summoned them, and offspring inherit the tag from their parent, so bloodlines stay credited to the right player. A player whose entire warband died still appears, with their losses intact.
  - Written to `BepInEx/config/DvergrAllies/`. Off-by-default settings live under `6 - BarrkBOT Export`; the export is server-side only and costs clients nothing.
  - Now requires **JsonDotNET** (added to dependencies).
- **Honest failure reporting.** If the exporter runs but cannot scan the world, it still writes the files, marks them `census_unavailable` with the reason, and reports population counts as `null` instead of `0` — so "nothing is alive" can never be confused with "we couldn't count". A missing file now means one thing only: the exporter never ran.

## 1.0.3
- **Shadows of Midgard Compatibility:** Added a `SoMStealthExempt` ZDO flag to Dvergr Allies. This ensures Shadows of Midgard's stealth AI correctly ignores them, allowing them to follow their designated combat AI without interference.
  - The flag is applied **only to your allies** — custom Ally prefabs, tamed wild Dvergrs, and their offspring. Wild, untamed Dvergrs are explicitly left alone so Shadows of Midgard's stealth system keeps working on them and you can still sneak past them.
  - The flag is now applied at the moment a wild Dvergr is tamed, not just when it spawns, so Dvergrs you recruit mid-session are covered immediately.
  - Wild Dvergrs are actively stamped as *not* exempt, which clears the flag from any that were incorrectly marked in an existing world.

## 1.0.2
- **Deep Compatibility Overhaul for BalrondIdleActors:**
  - Added aggressive script-stripping to prevent Balrond's mod from hijacking our custom tamed Dvergr Allies.
  - Strips `BalrondActor`, `BalrondActorTalker`, and `BalrondActorNamePicker` directly from all custom Ally prefabs (`AllyDvergrWarrior`, `AllyDvergrCleric`, etc.) at load time.
  - Added a runtime failsafe (`DvergrCombatAI`) that scans and dynamically destroys Balrond components if they are forcibly injected into active Ally game objects after spawning.
  - Completely disabled our `MonsterAI` prefix patch on wild Dvergrs when Balrond's mod is detected, ensuring wild NPCs retain their custom idle behaviors without interference.
  - Ensures your tamed Allies will properly fight and follow you instead of standing idle or breaking their custom AI.
- **Manifest Correction:** Updated `manifest.json` to correctly list required dependencies and the accurate Discord website URL.

## 1.0.1
- **Compatibility Patch:** Added an explicit compatibility check for `BalrondIdleActors`. If detected, DvergrAllies will automatically skip modifying wild Dvergrs to prevent conflicts with Idle NPCs. Dvergr allies purchased from Haldor or spawned by command will still function properly.

## 1.0.0
- **Initial Release!**
- Tame vanilla Dvergrs and recruit them as an Ally base-class.
- **Mercenary Contracts (Gacha):** Buy Dvergr Contracts from Haldor to instantly summon randomized tamed allies!
- Custom Genetic Class System:
  - **Warrior** (Aggressive melee tank)
  - **Berserker** (The ultimate 2-star melee evolution, wielding a Blackmetal Battleaxe)
  - **Cleric** (AoE heal-over-time support)
  - **Spellsword (Fire & Ice)** (Hybrid magic + melee combat, inherits specific magic based on parents)
  - **Elemental Mage** (Dynamically rotates between Fire, Ice, and Goblin Fire magic)
- Full Elementalist breeding tree system: Combine different traits to discover powerful hybrids.
- Star-level dynamic weapon scaling (Gear visually and statistically upgrades from Bronze -> Iron -> Blackmetal as they level up).
- **Aggressive Combat AI**: Forced behavioral overrides ensure tamed Dvergrs fight aggressively, hold their ground, and don't panic around fire or destroy player structures.
- **Follow Leash System**: Configurable `MaxFollowLeash` (default 40m) ensures your Dvergrs never chase enemies too far away while following you.
