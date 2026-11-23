<!--
  SOURCE of the store README (README schema v3.1). Edit THIS file, then forge the store page:
    python3 ~/WubarrkCODING/libs-Tools/readme-forge/forge_readme.py README.src.md HexiumDistrib/README.md \
        --manifest HexiumDistrib/manifest.json --code-root . [--check-badges]
  Never edit HexiumDistrib/README.md by hand. Numbers and settings come from readme_gen.py (read from the code).
-->
# ⚒️ Dvergr Allies

{{rune ᛒ | DVERGR ALLIES | TAME · BREED · COMMAND | 2f6f7e}}
{{badge VERSION | {{version}} | 8b1e1e}}
{{badge CLIENT + SERVER | EVERYONE MUST MATCH | 1f4e79}}

| ⚒️ **{{gen classes}}** | 🪙 **{{gen contract_cost}}** | ⭐ **{{gen star_cap}}★** | 🔒 **Synced** |
|:-:|:-:|:-:|:-:|
| custom classes to breed | coins for an instant ally | star cap from breeding | the server sets the rules |

> *The Dvergr never asked to be tamed. They asked for sausages. Somewhere between the two, an army happened.*

### Tame a Dvergr. Breed it. Watch it become something worse.

Feed the wild Dvergr of the Mistlands until they follow you, or buy a contract from Haldor for an ally on the spot. Pair them up and their children can mutate into elite classes with their own weapons, spells and tempers.

Your army grows through its bloodlines, its gear upgrades with its stars, and a Cleric keeps the line standing. A Dvergr you tame is on your side, and your own ballistas know it.

---

{{rune ᛞ | NEW IN {{version}} | HANDS OFF YOUR OWN | 8b1e1e}}

**A Dvergr you tamed from the wild can no longer be struck by you.**

| You | It |
|---|---|
| **swing at a Dvergr you tamed** | the hit does not land, as it never did on a hired or bred Ally |
| **strike a wild Dvergr beside your tame ones** | the wild camp is angered; your tame ones are not, and never turn on you |
| **tame a Dvergr while it is angry** | it calms for good, with no red **!** and no "Kill the Norseman!" |
| **run the server** | {{key Tamed Join Player Faction}} switches it off with the faction fix |

🔹 **Tested in game:** 60 checks on a real Valheim 1.0.21 client, including a real hit on a wild Dvergr standing beside a tame one, and confirmed by hand by a tester on Windows
🔹 **Update together:** the server and every client must run {{version}}. A mismatched client is turned away at the door.

---

{{rune ᚾ | BEFORE YOU INSTALL | READ THIS | b8860b}}

| | If… | Then… |
|---|---|---|
| 🤝 | you play on a **server** | put Dvergr Allies on the server **and** on every client, the exact same version. A client on another version is refused with a screen showing both. |
| 📦 | you install by **hand** | you need BepInEx, Jotunn and **JsonDotNET** (the stats export uses it). A mod manager fetches all three for you. |
| ⏳ | you want a **wild** Dvergr | feed it until it settles. It takes a long while on purpose: {{key Taming Time}} sets how long. |
| 🪙 | you want one **now** | buy a **Dvergr Contract** from Haldor ({{key Contract Cost}}) and use it. A tame ally appears beside you. |
| 🧭 | you run **Valkyrie's Cargo** | Ingvar is left alone. He can never be tamed, bred or counted among your Dvergr. |

---

{{rune ᚹ | I | TAME THEM | 2f6f7e}}

**Feed them until they follow. Or hire one.**

| You | It |
|---|---|
| **feed a wild Dvergr** `Coins`, `Cooked Meat`, `Sausages` or `Yggdrasil Wood` | it eats, settles, and after long enough is yours ({{key Taming Items}}) |
| **buy a Dvergr Contract from Haldor** | using it summons a tame Dvergr of a random class, elites included |
| **press [E] on a tame Dvergr** | order it: follow me, or stay |
| **press Shift+E** | give it a name |
| **let it go hungry** | the hover text says so. Keep it fed ({{key Fed Duration}}) |

<details>
<summary>📖 <b>Read the saga</b> — why they stand still while you feed them</summary>

**Wild Dvergr are made calm.** The Mistlands Dvergr is a touchy neutral: stand near one too long and it turns on you. While untamed, Dvergr Allies switches off that temper, their startled flight, their panic near a campfire and their urge to attack your taming pens, so you can stand beside one and feed it. A wild Dvergr is still a wild Dvergr: it is no ally until it is tame.

**The contract is a gamble.** Every contract draws its class at random from the whole roster, so the elite classes can come straight from Haldor's counter.

</details>

---

{{rune ᛈ | II | BREED THEM | 2f6f7e}}

**Pair two tame Dvergr and a child follows. Sometimes it is something new.**

| Parents | Child | |
|---|---|---|
| ⛏️ **Rogue** + ⛏️ **Rogue** | ⚔️ **Warrior** | pure melee |
| ⛏️ **Support Mage** + ⛏️ **Support Mage** | 🌿 **Cleric** | pure healing |
| ⛏️ **Fire Mage** + ⛏️ **Ice Mage** | 🌌 **Elemental Mage** | dual magic |
| ⛏️ **Rogue** + ⛏️ **Fire Mage** | 🔥 **Fire Spellsword** | melee and fire |
| ⛏️ **Rogue** + ⛏️ **Ice Mage** | ❄️ **Ice Spellsword** | melee and ice |
| ⚔️ **Warrior** + ⚔️ **Warrior** | 🪓 **Berserker** | the ultimate melee |

When the mutation does not come, the child simply has the class of one of its parents, so your bloodlines hold.

<details>
<summary>📖 <b>Read the saga</b> — how long it takes, and how many is too many</summary>

**A pair needs time and space.** A pregnancy lasts {{key Pregnancy Duration}}; a Dvergr looks for a partner within {{key Partner Check Range}}, every {{key Update Interval}}, and each match has a {{key Pregnancy Chance}} chance to take. Once {{key Breeding Limit}} tame Dvergr stand within 10 m, they stop breeding.

**Luck sets the pace.** {{key Special Combo Chance}} is how often a matching pair mutates; {{key Level Up Chance}} is how often a child is born a star above its best parent, up to {{key Max Breeding Level}}.

**Every Dvergr has a gender.** It is rolled once, shown on the hover text, and a pair needs one of each.

</details>

---

{{rune ᛏ | III | THE CLASSES | 2f6f7e}}

**Each class has its own weapons, and its gear grows with its stars.**

| Class | Role | What it does |
|:---|:---|:---|
| ⛏️ **Base Dvergr** | foundation | the genetic baseline, with simple weapons |
| ⚔️ **Warrior** | tank | **0★** Bronze Sword and Buckler · **1★** Iron Sword and Silver Shield · **2★** Blackmetal Battleaxe, with +50% slash damage |
| 🌿 **Cleric** | healer | pulses an area heal-over-time (15 HP/s) and fights with an Iron Mace |
| 🔥 ❄️ **Spellsword** | elite hybrid | casts Fireball or Icebolt, then rushes in for melee. Bronze, then Iron, then Blackmetal as it gains stars |
| 🌌 **Elemental Mage** | artillery | cycles its element every 10 seconds: Fireball, Icebolt, Goblin Shaman fire |
| 🪓 **Berserker** | ultimate melee | goes into a rage in the fight |

They fight through the game's own monster AI, so hits, aggro and particle effects behave like any other creature's. {{key Health Multiplier}} and {{key Damage Multiplier}} scale the whole roster at once.

---

{{rune ᛉ | IV | YOUR SIDE | 2f6f7e}}

**Tame means yours, to the game and to the ballista.**

| The rule | What it means |
|---|---|
| 🤝 **Tame joins the Players faction** | every Dvergr you tame, fed or hired or bred, counts as one of your own. Wild ones stay Dverger. |
| 🛡️ **You cannot strike your own** | a tame Dvergr takes no hit from you and is not angered when you strike a wild one nearby. A wild one can still be struck to anger its camp. |
| 🏹 **Your ballistas pass them over** | a ballista **you built** aims at the next real enemy instead. Wild Dvergr are still targets. |
| 🏰 **Outposts stay hostile** | the ballistas that belong to a Dvergr outpost are not yours and are not changed |
| 🔒 **The server holds the switches** | {{key Tamed Join Player Faction}} and {{key Spare Allies From Ballistas}}, both on by default |

---

{{rune ᚨ | V | THE WARBAND REPORT | 2f6f7e}}

**Your server tells Discord how your empire is doing.**

Through [BarrkBOT](https://discord.gg/GTTCnGS6Ym), who commands the largest warband, whose bloodlines have taken the heaviest losses, and who got lucky pulling elites out of Haldor's contracts. Deaths and births are recorded as they happen and survive server restarts.

The server writes the stats as JSON to `BepInEx/config/DvergrAllies/`. Clients do no work and write no files. {{key Enable Stats Export}} turns it off.

---

{{rune ᚠ | THE TOOLBOX | QUICK START · HOW-TO | 1b1b1b | 8b1e1e | e8c35a}}

**First ten minutes:** install on the server and every player → buy a contract from Haldor and use it → press **E** on your new Dvergr and tell it to follow → feed a wild one while you wait.

<details>
<summary>🛠️ <b>Make taming faster or slower</b></summary>

{{key Taming Time}} is how long a wild Dvergr takes to settle. {{key Taming Items}} is the list of food it will eat. {{key Consume Range}} is how close it must be to eat; raise it if Dvergr get stuck pushing each other.

</details>

<details>
<summary>🛠️ <b>Keep the army on a leash</b></summary>

{{key Max Follow Leash}} is how far a Dvergr chases an enemy before it drops the fight and comes back to you.

</details>

<details>
<summary>🛠️ <b>Make contracts cheaper or dearer</b></summary>

{{key Contract Cost}} is the price in coins at Haldor's counter.

</details>

<details>
<summary>🛠️ <b>I do not want the faction or ballista fix</b></summary>

Set {{key Tamed Join Player Faction}} or {{key Spare Allies From Ballistas}} to `Off` on the server. Each fix is independent.

</details>

<details>
<summary>🛠️ <b>See what they are thinking</b></summary>

The **5 - Debug** section of the config turns on logs for AI, breeding and equipment, so you can see who is mating with whom and what each Dvergr equipped.

</details>

---

{{rune ᚷ | MULTIPLAYER | WHO NEEDS WHAT | 1f4e79}}

| 🏰 The server | 🤝 Every client | 🚫 A different version |
|---|---|---|
| holds the settings, runs the breeding and writes the Discord stats | needs the mod too, to see and command its Dvergr | is refused at the door, with a screen naming both versions |

**Versions:** the server and every client must run the exact same Dvergr Allies version. A new release means everyone updates together.

---

{{rune ᛗ | COMPATIBILITY | OTHER MODS | 2e5e3a}}

| Mod | How they get on |
|---|---|
| ⚠️ **BalrondIdleActors** | its actor scripts are stripped from the custom Ally prefabs so your allies fight and follow instead of idling. The changes to wild Dvergr are switched off entirely when it is detected, so wild NPCs keep their idle behaviour. |
| 🧭 **Valkyrie's Cargo** | Ingvar the merchant is a vanilla Dverger underneath. Dvergr Allies recognises him by that mod's own tag and strips every component of its own from him, so **E** trades with him. He can never be tamed, petted, renamed, commanded, bred or counted, and he stays in the Dverger faction. |
| 🥷 **Shadows of Midgard** | your allies carry the `SoMStealthExempt` flag so its stealth AI leaves them to their combat AI. Wild Dvergr are not exempt, so sneaking past them still works. |

---

{{rune ᛚ | SETTINGS | CONFIGURATION MANAGER · F1 | b8860b}}

<details>
<summary>🔒 <b>Server-synced</b> — set on the server, locked to admins</summary>

{{gen settings_server}}

The **5 - Debug** switches are server-synced as well.

</details>

<details>
<summary>🖥️ <b>Local</b> — your own, never synced</summary>

{{gen settings_local}}

</details>

---

{{rune ᛟ | CREDITS | THANK YOU | 1b1b1b | c9a227 | 1b1b1b}}

**Needs:** BepInExPack for Valheim, Jotunn and JsonDotNET · **Stats** read by BarrkBOT · *Made by Wubarrk.* · **License:** MIT, see `LICENSE.md`

Join the Mists of Avalor Open BETA, found only on [Hexium](https://valheim.hexium.gg/mods/Wubarrk/Mists_of_Avalor_BETA).

<details>
<summary>ᛟ <b>The runes on this page</b> — every banner is sealed with one</summary>

{{runes}}

</details>
