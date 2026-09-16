# DvergrAllies — Valheim 1.0.7 migration instructions

> **2026-09-15 — 1.0.8 released (Valkyrie's Cargo compatibility; no engine migration involved).** Rebuilt against the
> same `libs-Tools/assembly_valheim.dll` (byte-identical to `1.0/client/`). One Harmony target added -
> `Tameable.Awake()` postfix (`ValkyriesCargoCompat.cs`), private, declared on `Tameable` at 1.0 decompile l.19049,
> identical on the server build - so the tally below is now **8**. Evidence for this release: refcheck against the real
> 1.0 client Managed and dedicated-server Managed: `checked 3103 references, 8 Harmony targets (0 dynamic) - RESULT: OK`
> both sides; `tools/StatsExportFixtures` 20/20 pass; boot on the Linux dedicated server with Jotunn 2.30.0 **and
> Valkyrie's Cargo 0.1.1 loaded beside it** (profile `~/valheim-testbed/profiles/dvergr-vc-108`, port 2692):
> `BOOTED after 22s`, load order `Jotunn -> Valkyrie's Cargo -> DvergrAllies` (soft `BepInDependency`), log has
> `[Compat] Valkyrie's Cargo detected: its merchant Ingvar (ZDO key VCargo_ingvar) will never be tamed ...`,
> `[Patches] 8 applied, none failed.`, `Successfully injected Taming/Breeding components into 8 wild Dvergr prefabs!`,
> VC's `director up`, no exception from either plugin. Shipped DLL md5 `656c1ce88327d01cae5dfe851f24f5e4` (78336 bytes), identical in
> `bin/Release/net48/`, `HexiumDistrib/DvergrAllies/plugins/` and the boot profile. Everything below this banner
> describes 1.0.7 and its 1.0.12 re-check and is left as written; where it says "7 Harmony targets" read 8 for 1.0.8.

> **Verified 2026-09-11 on Valheim 1.0.12 (build 25253764 client / 25253791 server, network version 40) - no change
> needed, DvergrAllies stays at 1.0.7.** The 1.0.7 DLL (md5 `53ab198394ea8305f5e05d64382f640c`, identical in
> `bin/Release/net48/`, `HexiumDistrib/DvergrAllies/plugins/` and the boot profile) was re-checked against the 1.0.12
> engine without rebuilding. Not bumped, not rebuilt, csproj untouched.
>
> **What changed in the engine (1.0.7 -> 1.0.12, assembly_valheim only):** 37 types / 55 method bodies / 6 fields
> added / 1 removed / 1 const (`Version.c_networkVersion` 39 -> 40). `assembly_utils`, `assembly_guiutils`,
> `gui_framework`, `assembly_postprocessing`, `Splatform` are IL-identical. Ground truth:
> `MigrationStation/diffs/server_assembly_valheim_1.0.7_to_1.0.12.md` (+ `.ildiff.txt`) and the decompile diff
> `libs-Tools/GAME-SNAPSHOT-1.0.7-build25185596/DECOMPILED/` vs `libs-Tools/1.0/DECOMPILED/`.
>
> **Every touchpoint between this mod and that delta, with verdict:**
> - `Character.SetTamed(bool)` postfix x2 (`DvergrGenetics.cs:118`, `DvergrStatsExporter.cs:354`) - target not in
>   the changed-member list; 1.0.12 decompile l.4482 `SetTamed` -> `InvokeRPC("RPC_SetTamed")` as before. Unaffected.
> - `Character.OnDeath()` prefix (`DvergrStatsExporter.cs:390`) - target IS body-changed (asmdiff "100 %, 336 -> 336
>   tokens"); the IL hunk (`.ildiff.txt` l.127) is exactly one instruction, `ldsfld PlayerProfile::s_bypassCheatChecks`
>   -> `call PlayerProfile::get_s_bypassCheatChecks()`, and the decompiled 1.0.7 and 1.0.12 bodies are textually
>   identical (1.0.12 l.3075-3168). The prefix only reads `IsTamed()`, the ZNetView and the owner ZDO keys, so it
>   sees no difference. Unaffected.
> - `MonsterAI.UpdateAI(float)` prefix (`AllyPrefabManager.cs:498`), `ZNetScene.Awake()` postfix
>   (`AllyPrefabManager.cs:459`), `ZNet.Shutdown(bool)` prefix (`DvergrStatsExporter.cs:414`), `Trader.Start()`
>   postfix (`RecruiterManager.cs:117`) - none of the four types' touched members (ZNet's five are OpenServer /
>   SendPeerInfo / RPC_PeerInfo / ListContainsId / DelayThenRegisterCoroutine; MonsterAI, ZNetScene and Trader are not
>   in the changed-type list at all). Unaffected. No transpilers anywhere in the mod.
> - `Inventory.AddItem(ItemDrop.ItemData)` called directly on ally NPC inventories (`DvergrCombatAI.cs:295, 305, 337,
>   380`) - body-changed by the same single `ldsfld -> call get_` swap (`.ildiff.txt` l.318); decompiled bodies
>   identical. It calls the private `Inventory.Changed(bool, bool)`, which gained the `m_cheatedPopup` /
>   `$achievements_dropped_cheated_item` branch (1.0.12 l.68781-68801): that branch only arms when an item with
>   `m_cheated == true` enters the inventory, and the items the mod injects are `Clone()`s of prefab item data
>   (`m_cheated` false), so for these inventories it is dead code; on a dedicated server `Player.m_localPlayer` is
>   null anyway. `Inventory.AddItem(GameObject, int)` (`DvergrWeaponScaler.cs:105`) is not in the changed list.
>   Unaffected.
> - Game interface implemented: `DvergrTameable : Tameable, Hoverable` (`DvergrTameable.cs:5`) - `Hoverable` is
>   unchanged (1.0.12 decompile l.131062-131068: `GetHoverText / GetHoverName / GetHoverOffset`, same three members
>   as 1.0.7; the decompile diff contains no `interface` line at all); `Tameable` is not a changed type;
>   `Character.GetHoverOffset()` that the forwarder calls is unchanged. `SummonDvergrStatusEffect : StatusEffect`
>   (`RecruiterManager.cs:50`) - `StatusEffect` unchanged. Unaffected.
> - New non-defaulted parameters: none in 1.0.12 - the only signature reshape is the private
>   `ZDOMan.ConvertInventories(List<ZDO>, World)` -> `ConvertInventories(List<ZDOID>, World, int)`, which the mod
>   never calls. Constructor reshapes: none - only the bodies of `CookingStation..ctor`, `PresentManager..ctor`,
>   `PlayerProfile..cctor`, `Version..cctor` changed (field initialisers); `Inventory`, `Trader.TradeItem`,
>   `StatusEffect`, `ZPackage` ctors the mod uses are untouched. Unaffected.
> - Reflection over game types: `typeof(Trader.TradeItem).GetFields()` (`RecruiterManager.cs:142`) and
>   `typeof(ItemDrop.ItemData.SharedData).GetFields(...)` (`DvergrCombatAI.cs:230`) - neither type gained, lost or
>   retyped a field (the 6 added fields are on AchievementUnlockPopup, GrapplingPoint x2, Inventory, RuneStone,
>   CookingStation; the 1 removed is `PlayerProfile.s_bypassCheatChecks`). No `AccessTools.Method/Field/Property`,
>   `GetMethod`, `GetProperty` string lookups exist. Unaffected.
> - `PlayerProfile.s_bypassCheatChecks` field -> property: no source or IL reference (grep for
>   `s_bypassCheatChecks|m_cheated|Achievements|AnyCheatedItem|bypasscheatchecks` over the source root: 0 hits;
>   refcheck resolves all 2980 references). Unaffected.
> - Console commands: the mod registers none (`ConsoleCommand|Terminal|hideBehindDevCommands|isCheat`: 0 hits outside
>   `System.Console` in the off-game fixtures harness), so the 1.0.12 dev-command gating change has nothing to bite.
>   Unaffected.
> - Version / network version: the mod never prints or compares `Version.*`, `GetVersionString`, `c_networkVersion`
>   or a network version; its only version is its own `Plugin.PluginVersion` (Jotunn `NetworkCompatibility`,
>   `VersionStrictness.Minor`, compares mod versions between peers, not the game's). Unaffected.
> - Item data on ZDOs / item stands / containers (`ItemDrop.SaveToZDO` guard flip, `ZDOMan.ConvertContainers` /
>   `GetConvertHash`): the mod writes only its own creature-ZDO keys (`pregnant`, `dvergr_partner_prefab`,
>   `dvergr_partner_level`, `dvergr_gender`, `SoMStealthExempt`, `dvergr_owner_id`, `dvergr_owner_name`,
>   `dvergr_tame_counted`) and reads `ZDOVars.s_tamed` / `s_tameLastFeeding` / `GetPrefab()`; it never calls
>   `SaveToZDO`/`LoadFromZDO` and touches no item stand or container. `ConvertContainers` only runs for worlds older
>   than `Version.World.ChunkedSave` (1.0.12 l.76639) and only over ZDOs carrying the `stack` / `{i}_stack` int
>   hashes or the `s_items` string, which creature ZDOs do not. The census enumerates `ZDOMan.instance.m_objectsByID`
>   (field, unchanged). Unaffected.
> - Cooking / smelting / fermenting output, cheated-item flags, landing / `m_onLand` hooks, terrain ops
>   (`TerrainComp.PaintCleared`), admin / ban / whitelist (`ZNet.ListContainsId`), `Player.HaveRequirementItems`,
>   `Attack.DoMeleeAttack` snow-shovel gate: grep for each over the source root returns 0 hits. The ally creature
>   prefabs are Jotunn clones of the vanilla Dverger prefabs, so they inherit whatever `Character.UpdateGroundContact`
>   / `Attack.DoMeleeAttack` now do on the engine side (deep-snow landing VFX; melee no longer clears snow without a
>   shovel) exactly as the vanilla Dvergr do - the mod neither relies on nor overrides either. Unaffected.
> - Every other changed type (Terminal, Chat, Console, InventoryGui, Player.TryPlacePiece, CookingStation, Smelter,
>   Fermenter, Container, Destructible, DropOnDestroyed, MineRock, Piece, ItemSets, GrapplingPoint, RuneStone,
>   AchievementUnlockPopup, Achievements, PresentManager, FejdStartup, MasterClient, ServerListGui, ServerOptionsGUI,
>   ZPlayFab*, ZSteamMatchmaking, ZoneSystem.TestSpawnLocation, Character.ApplyDamage): not referenced by the mod
>   (grep over the source root, excluding bin/obj/tools: 0 hits). Unaffected.
>
> **Evidence:** refcheck (exact-signature references + Harmony-target resolution) on the staged DLL against the real
> 1.0.12 client Managed and the real 1.0.12 dedicated-server Managed (both md5-identical to `libs-Tools/1.0/{client,
> server}/assembly_valheim.dll`): `checked 2980 references, 7 Harmony targets (0 dynamic) - RESULT: OK`, exit 0,
> both sides (`scratchpad/refcheck-1012/final-DvergrAllies-DvergrAllies-{client,server}.txt`, re-run by this pass
> with the same result). Boot on the 1.0.12 Linux dedicated server, BepInEx 5.4.2350 + Jotunn 2.30.0, profile
> `~/valheim-testbed/profiles/final-DvergrAllies`: `BOOTED after 28s`
> (`scratchpad/boot-1012/final-DvergrAllies.txt`); `BepInEx/LogOutput.log` l.20-42 - `Loading [DvergrAllies 1.0.7]`,
> `[Patches] 7 applied, none failed.`, `[StatsExport] Armed.`, `DvergrAllies v1.0.7 has loaded!`, `Ally Dvergr
> prefabs created.`, `Adding 21 custom prefabs to the ZNetScene`, `Successfully injected Taming/Breeding components
> into 8 wild Dvergr prefabs!`, `Adding 10 custom items to the ObjectDB`, `Adding 1 custom status effects to the
> ObjectDB` - line-for-line the same as the preserved 1.0.7 run of the same profile
> (`~/valheim-testbed/profiles/final-DvergrAllies/logs-1.0.7-run/LogOutput.log` l.20-42); `server-console.log`
> l.140 `Valheim version: l-1.0.12 (network version 40)`. No Harmony / Missing* / TypeLoad line in either log; the
> only warnings are the same eight Jotunn `Ambiguous asset name for path` notes as on 1.0.7.
> Still open, unchanged from 2026-09-09: no client-attached playtest (taming, breeding, hover text, Haldor stock are
> client-driven); `tools/StatsExportFixtures/Program.cs` still labels its output `DvergrAllies 1.0.5`; the credential
> footnote below.


> **Applied 2026-09-09 - DvergrAllies 1.0.7.** Jotunn 2.30.0 (the Valheim 1.0.7 release) landed today, so the
> block in section 1 is lifted. Done: `JotunnLib` pinned to `[2.30.0]` (was `2.*`); `obj/project.assets.json`
> resolves `JotunnLib/2.30.0`; version bumped to 1.0.7 in the csproj, `Plugin.cs` and
> `HexiumDistrib/manifest.json` (manifest dependencies moved to `denikson-BepInExPack_Valheim-5.4.2350` and
> `ValheimModding-Jotunn-2.30.0`); CHANGELOG entry added; DLL staged by hand to
> `HexiumDistrib/DvergrAllies/plugins/` (the project has no post-build copy step).
>
> **Deviation from this report - one source change WAS required.** Section 1's "no source change is required"
> was wrong: the 1.0.7 `Hoverable` interface gained `float GetHoverOffset()` (decompile: `public interface
> Hoverable { string GetHoverText(); string GetHoverName(); float GetHoverOffset(); }` on both client and
> server; pre-1.0 had only the first two). `DvergrTameable : Tameable, Hoverable` therefore failed to compile
> (CS0535) and, as a shipped 1.0.6 DLL, would fail type load on 1.0.7 (`TypeLoadException: VTable setup of
> type ... failed` - the identical mechanism was observed live for MistsofAvalor's `AvalorPortalTrigger`, see
> that repo's MIGRATE-1.0.md). Fix in `DvergrTameable.cs`: `GetHoverOffset()` forwards to the `Character` on
> the same object (`Tameable` itself is not `Hoverable` in any build - the interface is declared on
> `DvergrTameable` so its own hover text is dispatched). Every vanilla implementer returns a serialized
> `m_hoverOffset` (default 0) that `Player.FindHoverObject` adds to `m_maxInteractDistance`, so forwarding
> preserves the exact vanilla reach. The audit missed it because it checked Harmony targets and engine
> members but not the game interfaces the mod's own types implement.
>
> Evidence: `dotnet build DvergrAllies.csproj -c Release` - 0 errors (3 pre-existing CS0436 publicizer
> warnings). refcheck against the real 1.0.7 client Managed folder: `checked 2980 references, 7 Harmony
> targets (0 dynamic) - RESULT: OK`; against the dedicated-server Managed folder: same, `RESULT: OK`.
> Boot-check on the 1.0.7 Linux dedicated server (profile `~/valheim-testbed/profiles/dvergr`, port 2632,
> BepInEx 5.4.2350 + Jotunn 2.30.0 + Newtonsoft.Json 13.0.3 beside the DLL): `BOOTED after 25s`; log has
> `Loading [Jotunn 2.30.0]`, `Loading [DvergrAllies 1.0.7]`, `[Patches] 7 applied, none failed.`,
> `[StatsExport] Armed.`, `DvergrAllies v1.0.7 has loaded!`, `Ally Dvergr prefabs created.`, Jotunn's
> `Adding 21 custom prefabs to the ZNetScene` / `Adding 10 custom items to the ObjectDB` / `Adding 1 custom
> status effects to the ObjectDB`, and `Successfully injected Taming/Breeding components into 8 wild Dvergr
> prefabs!`; no Harmony/Missing*/TypeLoad lines in either log. The only warnings are eight
> `Jotunn.Managers.AssetManager` "Ambiguous asset name for path" notes - Jotunn indexing vanilla's own
> duplicate prefab paths, identical in the other two Jotunn profiles of this lane, not attributable to this
> mod. Logs: `~/valheim-testbed/profiles/dvergr/BepInEx/LogOutput.log` and `.../server-console.log`.
> Off-game: `tools/StatsExportFixtures` (`dotnet run -c Release -- <scratch dir>`) - all 20 checks pass,
> exit 0 (run into a scratch directory; the checked-in `fixtures/` were not regenerated).
>
> Jotunn 2.30.0 caveat ("Piece categories are not updated yet"): not applicable - DvergrAllies registers no
> build pieces (no `PieceManager` / `CustomPiece` / `PieceConfig` / `PieceTable` anywhere in the source); it
> registers creature and item prefabs, items, one status effect and the Haldor trade entry.
> Open: not exercised with a client attached (taming, breeding, hover text, Haldor stock are client-driven);
> `tools/StatsExportFixtures/Program.cs` still labels its output `Source = "DvergrAllies 1.0.5"` (already
> out of step before this pass; left alone so the checked-in fixtures stay byte-stable); the credential
> footnote below still stands.

**Status: THIS MOD'S OWN CODE IS 1.0-CLEAN. It is BLOCKED on Jotunn, which is not yet 1.0-ready.**
Audited 2026-09-09 against `libs-Tools/1.0/DECOMPILED/` (client 1.0.7, build 25185596, network 39).

No source change is required in this repository. Every Harmony target resolves on 1.0.7 and every
signature it depends on is unchanged. The one action item is the dependency.

---

## 1. The blocker: Jotunn

`DvergrAllies.csproj` declares:

```xml
<PackageReference Include="JotunnLib" Version="2.*" />
```

This mod uses `Jotunn.Managers.PrefabManager`, `Jotunn.Logger`.

**Jotunn needs a 1.0 release before this mod can ship against 1.0.7.** Jotunn's `ExtEquipment` reads the
`VisEquipment` `m_*Item` fields, which 1.0 converted from `string` to `int` hashes — so Jotunn itself is
broken on 1.0 until upstream updates. Mists of Avalor is blocked on the same thing.

### The floating version is a hazard right now

`Version="2.*"` resolves to whatever the newest 2.x is **at restore time**. That cuts both ways:

- **Good:** when Jotunn ships a 1.0-compatible 2.x, a plain `dotnet restore` picks it up with no csproj edit.
- **Bad:** until then, a restore silently pulls a **pre-1.0** Jotunn, and the build succeeds. You get a
  DLL that looks fine and fails at runtime inside Jotunn, not inside your code — which is a slow thing
  to diagnose.

Until upstream updates, **pin the version explicitly** so the reference set is honest about what it is:

```xml
<PackageReference Include="JotunnLib" Version="[2.24.3]" />   <!-- replace with the version you actually validated -->
```

Then unpin (or move the pin) when the 1.0-compatible release lands. Also check
`libs-Tools/Jotunn.dll` — the copy there is dated **2026-03-06**, pre-1.0; other mods in the workspace
reference that file directly rather than the NuGet package, so it will need replacing too.

Track this as: **blocked on upstream, no local work outstanding.**

---

## 2. Verified safe — every target checked against the 1.0.7 decompile

- `Character.SetTamed(bool)`, `Character.OnDeath()` (`public virtual`)
- `MonsterAI.UpdateAI(float)` (`public override`)
- `ZNet.Shutdown(bool)`, `ZNetScene.Awake()`
- `Trader` (class-level patch) — type still present

**Not touched by this mod at all**, so the corresponding 1.0 breakages are irrelevant here: the sector
API (`FindSectorObjects`, `Vector2i` sectors, `ZoneSystem.m_activeArea`/`m_zones`), `ZDOExtraData`'s
per-type readers, `World.GetWorldSavePath`/`FileHelpers.FileSource`, the `Version` class rename,
`ZNet.PlayerInfo`, `ItemStand.m_visualHash`, and `Inventory.Load`'s new second overload.

**`StringExtensionMethods.GetStableHashCode(string)`** is unchanged on the release build — the two-argument
signature that appeared in the public-test build was reverted before ship. No bridge, and no
`Valheim10Compatibility` preloader dependency.

The `tools/StatsExportFixtures/` sub-project does not reference game assemblies and is unaffected.

---

## 3. When Jotunn updates

```bash
dotnet restore && dotnet build DvergrAllies.csproj -c Release
```

Game DLL `HintPath`s already resolve to `..\libs-Tools\*.dll`, which **is** the 1.0.7 client set as of
2026-09-09 — no csproj edit needed there. `libs-Tools/BepInEx.dll` was updated the same day to the
Valheim 1.0 pack (**5.4.2350**); `0Harmony.dll` is byte-identical to before, so nothing Harmony-side changes.

Re-verify against the new Jotunn API on the first build — Jotunn has broken `PrefabManager`/`ItemManager`
registration and config-attribute shapes across releases before. Then load on a 1.0.7 world and confirm
the mod's registered content actually appears.

---

## Footnote — plaintext credential in `origin`

Found 2026-09-09, unrelated to the 1.0 migration itself: this repo's `origin` remote embeds a live
GitHub fine-grained PAT directly in the URL (`https://RGlabs84:github_pat_...@github.com/...`),
readable in plaintext by `git remote -v` and `.git/config`. The same token is reused across
BlightedHeart, DvergrAllies, Fatty, and Njord. `gh` already ships a credential helper
(`gh auth setup-git`) that authenticates pushes without storing a token in the remote URL — switch to
it (or SSH), strip the token back out of `origin`, and rotate it, since it has already sat in
plaintext in git config.
