# Changelog

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
