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
<summary><b>🧠 AI & Economy</b></summary>

- **Max Follow Leash:** The maximum distance (in meters) a Dvergr will chase an enemy before dropping aggro and returning to the player they are following. (Default: `40m`)
- **Contract Cost:** Cost in coins to purchase a Mercenary Contract from Haldor. (Default: `999`)

</details>

> [!WARNING]
> **Debugging:** Toggle robust AI, Equipment, and Breeding logs to see exactly what your Dvergrs are thinking, who they are mating with, and what weapons they are equipping.

---

## 🛠️ Installation

1. Install [BepInEx for Valheim](https://github.com/BepInEx/BepInEx).
2. Install [Jotunn (The Valheim Library)](https://github.com/Valheim-Modding/Jotunn).
3. Drop `DvergrAllies.dll` into your `BepInEx/plugins/` folder.
4. Tame a Dvergr and begin your empire!
