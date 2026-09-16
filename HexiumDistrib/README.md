<div align="center">

# ⚒️ Dvergr Allies

**Tame, breed, and lead an army of custom Dwarven warriors.**

[![Valheim](https://img.shields.io/badge/Valheim-Game-blue?style=flat-square)](https://www.valheimgame.com/)
[![BepInEx](https://img.shields.io/badge/BepInEx-Plugin-orange?style=flat-square)](https://github.com/BepInEx/BepInEx)
[![Jotunn](https://img.shields.io/badge/Jotunn-Library-green?style=flat-square)](https://github.com/Valheim-Modding/Jotunn)

</div>

> [!NOTE]
> Dvergr Allies is a massive overhaul to the Valheim taming system, allowing you to tame vanilla Dvergrs, breed them, and genetically evolve them into **four distinct custom combat classes**. Featuring dynamic weapon scaling, complex genetics, and custom-built Native Monster AI, your Dvergrs are no longer just passive NPCs—they are an elite fighting force.

---

## 🛡️ Features

> [!TIP]
> **Mercenary Contracts:** Don't want to tame? Buy **Dvergr Contracts** from Haldor the Trader! Consuming a contract will instantly summon a fully-tamed, randomized Dvergr class (including rare elites like Spellswords or Berserkers!).

- **Taming & Breeding:** Tame wild vanilla Dvergrs using `Coins`, `Cooked Meat`, `Sausages`, or `Yggdrasil Wood`.
- **Genetic Class System:** Breed your Dvergrs to discover specialized classes. Cross-breed existing classes to unlock powerful elite hybrids.
- **Dynamic Equipment Scaling:** Your allies don't just get more health when they level up. Their physical gear dynamically upgrades! A 0-star Warrior wields a Bronze Sword. A 1-star upgrades to Iron. A 2-star wields a devastating Blackmetal Battleaxe.
- **Native Combat AI:** Weapons and magic have been flawlessly integrated into the native Valheim monster physics engine, ensuring perfect hit registration, aggression, and particle effects.
- **Cleric AoE Healing:** Clerics pulse massive Area-of-Effect Heal-over-Time spells to keep your army alive during raids.
- **Discord Stats via BarrkBOT:** Your server tracks your empire and reports it to Discord — who commands the largest warband, whose bloodlines have taken the heaviest losses, and who got lucky pulling elites out of Haldor's contracts. Deaths and births are recorded as they happen and survive server restarts.

---

## 🧬 The Classes & Prefabs

| Class | Icon | Role | Abilities & Upgrades |
|:---|:---:|:---|:---|
| **Base Dvergr**<br>`AllyDvergr` | ⛏️ | **Foundation** | The genetic baseline. Wields simple weapons. |
| **The Warrior**<br>`AllyDvergrWarrior` | ⚔️ | **Tank / Aggro** | **0★:** Bronze Sword & Buckler<br>**1★:** Iron Sword & Silver Shield<br>**2★ (Berserker):** Blackmetal Battleaxe (+50% Slash Damage buff) |
| **The Cleric**<br>`AllyDvergrCleric` | 🌿 | **Support / Healer** | **Ability:** Pulses 15 HP/s AoE Heal-over-Time.<br>**Defense:** Wields an Iron Mace. |
| **The Spellsword**<br>`AllyDvergrSpellswordFire` / `Ice` | 🔥 / ❄️ | **Elite Hybrid** | **Ability:** Casts native Fireball or Icebolt, then rushes in for melee.<br>**Gear:** Bronze (0★) → Iron (1★) → Blackmetal (2★) |
| **The Elemental Mage**<br>`AllyDvergrMageElemental` | 🌌 | **Arcane Artillery** | **Ability:** Cycles elements every 10s (Fireball, Icebolt, Goblin Shaman Fire). |

---

## 🌳 The Breeding Tree

> [!IMPORTANT]
> You can't just find an elite unit in the wild—you have to breed one. When two Dvergrs mate, their offspring has a chance to mutate into a higher-tier class based on their parents' genetics.

### 🧬 Tier 1 Mutations (From Vanilla Tames)
*Combining two base Dvergr tames has a chance to produce a specialized unit.*

* ⛏️ `Rogue` + ⛏️ `Rogue` ➞ **⚔️ Warrior** *(Pure Melee)*
* ⛏️ `Support Mage` + ⛏️ `Support Mage` ➞ **🌿 Cleric** *(Pure Healing)*
* ⛏️ `Fire Mage` + ⛏️ `Ice Mage` ➞ **🌌 Elemental Mage** *(Dual Magic)*

### 🧬 Tier 2 Mutations (Hybrids & Elites)
*Combining different classes passes down their traits into an Elite Hybrid.*

* ⛏️ `Rogue` + ⛏️ `Fire Mage` ➞ **🔥 Fire Spellsword** *(Melee + Fire)*
* ⛏️ `Rogue` + ⛏️ `Ice Mage` ➞ **❄️ Ice Spellsword** *(Melee + Ice)*
* ⚔️ `Warrior` + ⚔️ `Warrior` ➞ **🪓 Berserker** *(Ultimate Melee)*

> [!NOTE]
> *(Note: In the code, offspring that don't trigger the "Special Combo Chance" simply inherit the exact class of one of their parents, maintaining your bloodlines!).*

---

## ⚙️ Configuration Options

> Dvergr Allies is highly customizable via the `BepInEx/config/DvergrAllies.cfg` file. You can adjust almost every aspect of the mod in real-time.

<details>
<summary><b>🥩 Taming</b></summary>

- **Taming Time:** Time required to tame a wild Dvergr. (Default: `1800s`)
- **Taming Items:** The prefabs they will eat. (Default: `CookedMeat`, `Coins`, `Sausages`, `YggdrasilWood`)
- **Fed Duration / Consume Range:** Fine-tune their hunger mechanics and eating distance.

</details>

<details>
<summary><b>🧬 Breeding</b></summary>

- **Pregnancy Duration:** How long it takes for offspring to spawn. (Default: `600s`)
- **Breeding Limit:** Max Dvergrs allowed in a `10m` radius before they stop breeding. (Default: `4`)
- **Pregnancy Chance:** Likelihood of successful mating per interval. (Default: `33%`)
- **Special Combo Chance:** The chance that a valid genetic pair produces the Hybrid Mutation instead of a normal child. (Default: `25%`)
- **Level Up Chance:** The chance an offspring is born with `+1` Star. (Default: `10%`)
- **Max Breeding Level:** Cap the maximum stars they can achieve. (Default: `2 Stars` / Level 3)

</details>

<details>
<summary><b>📈 Stats</b></summary>

- **Health Multiplier:** Global multiplier for your Dvergrs' base health pools.
- **Damage Multiplier:** Global multiplier applied directly to the physical slash/blunt/pierce damage of all custom Dvergr weapons, and the elemental damage of all Mage staves.

</details>

<details>
<summary><b>📊 BarrkBOT Export</b></summary>

- **Enable Stats Export:** Writes your Dvergr empire's stats to `BepInEx/config/DvergrAllies/` as JSON, so [BarrkBOT](https://discord.gg/GTTCnGS6Ym) can answer questions about them in Discord. (Default: `On`)
- **Export Interval:** Seconds between writes. (Default: `60s`) BarrkBOT only checks every 20 minutes, so lowering this buys nothing.
- **Force Census Unavailable:** *(Testing only — leave `Off`)* Makes the export deliberately report that it couldn't count your Dvergrs, so you can verify the failure path works without breaking anything. Counts are reported as "not measured" rather than zero, your lifetime history keeps recording, and the export says outright that it was forced.
- Server-side only — clients do no work and write no files.

</details>

<details>
<summary><b>🧠 AI & Economy</b></summary>

- **Max Follow Leash:** The maximum distance (in meters) a Dvergr will chase an enemy before dropping aggro and returning to the player they are following. (Default: `40m`)
- **Contract Cost:** Cost in coins to purchase a Mercenary Contract from Haldor. (Default: `999`)

</details>

> [!WARNING]
> **Debugging:** Toggle robust AI, Equipment, and Breeding logs to see exactly what your Dvergrs are thinking, who they are mating with, and what weapons they are equipping.

---

## 🤝 Compatibility

- **Valkyrie's Cargo** — Ingvar, the travelling merchant, is a vanilla Dverger under the hood. Dvergr Allies recognises him by that mod's own tag on his save data and strips every taming, breeding and genetics component from him the moment he appears, so **[E] trades with him** as intended - he can never be tamed, petted, renamed, commanded, bred, or counted among your Dvergr. Wild Dvergr are unaffected.
- **Shadows of Midgard** — your allies carry the `SoMStealthExempt` flag so SoM's stealth AI leaves them to their combat AI; wild, untamed Dvergr are explicitly *not* exempt, so sneaking past them still works.
- **BalrondIdleActors** — Balrond's actor scripts are stripped from the custom Ally prefabs so your allies fight and follow instead of idling, and the wild-Dvergr AI adjustments are switched off entirely when Balrond is detected so wild NPCs keep their idle behaviours.

---

## 🛠️ Installation

1. Install [BepInEx for Valheim](https://github.com/BepInEx/BepInEx).
2. Install [Jotunn (The Valheim Library)](https://github.com/Valheim-Modding/Jotunn).
3. Install **JsonDotNET** — required for the stats export.
4. Drop `DvergrAllies.dll` into your `BepInEx/plugins/` folder.
5. Tame a Dvergr and begin your empire!

---
join the Mists of Avalor Open BETA find it only on [Hexium ![Hexium Logo](data:image/png;base64,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)](https://valheim.hexium.gg/mods/Wubarrk/Mists_of_Avalor_BETA)

<br>
<small>

**License:** MIT — use, modify, and redistribute freely, including commercially; just keep the copyright notice. See `LICENSE.md` for the full text.

</small>
