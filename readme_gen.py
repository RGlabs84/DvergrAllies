"""Generated blocks for README.src.md (called by libs-Tools/readme-forge/forge_readme.py).

Everything numeric on the store page is read from the code here, so the page cannot drift from the DLL:
  - the class count from AllyPrefabManager.CustomVariants,
  - every setting's section, name, default and description from ConfigManager.Init (the single place they are bound).
"""
import os
import re

HERE = os.path.dirname(os.path.abspath(__file__))

# What each rune seals on this page (the rune legend's last column).
RUNE_SEALS = {
    "ᛒ": "the name: birth, bloodlines, growth",
    "ᛞ": "what is new",
    "ᚾ": "what you need to know first",
    "ᚹ": "taming and contracts",
    "ᛈ": "breeding and mutations",
    "ᛏ": "the classes",
    "ᛉ": "your side: faction and ballistas",
    "ᚨ": "the warband report to Discord",
    "ᚠ": "the toolbox",
    "ᚷ": "multiplayer",
    "ᛗ": "other mods",
    "ᛚ": "the settings",
    "ᛟ": "the credits",
}


def _read(name):
    with open(os.path.join(HERE, name), encoding="utf-8-sig") as fh:
        return fh.read()


# Config.Bind("section", "name", default, SyncedConfig("description"))   <- server-synced, admin only
# Config.Bind("section", "name", default, "description")                  <- local
BIND = re.compile(
    r'config\.Bind\("([^"]+)",\s*"([^"]+)",\s*("[^"]*"|[^,]+?),\s*(SyncedConfig\()?"((?:[^"\\]|\\.)*)"')


def _settings():
    rows = BIND.findall(_read("ConfigManager.cs"))
    if len(rows) < 20:
        raise SystemExit(f"readme_gen: read only {len(rows)} settings from ConfigManager.cs")
    out = []
    for section, name, default, synced, desc in rows:
        out.append({"section": section, "name": name, "default": default, "synced": bool(synced), "desc": desc})
    return out


def _default(raw):
    raw = raw.strip()
    if raw in ("true", "false"):
        return "On" if raw == "true" else "Off"
    if raw.startswith('"'):
        return raw.strip('"')
    return re.sub(r"(?<=\d)f$", "", raw)


def _first_sentences(desc):
    """The first sentence, or the first two when the first is only a label ("TESTING ONLY.")."""
    parts = re.split(r"(?<=\.)\s+", desc.strip())
    text = parts[0]
    if len(text) < 24 and len(parts) > 1:
        text += " " + parts[1]
    return text.replace("|", "/")


def _table(rows):
    lines = ["| Section | Setting | Default | What it does |", "|---|---|---|---|"]
    for r in rows:
        section = re.sub(r"^[\d.]+ - ", "", r["section"])
        lines.append(f"| {section} | `{r['name']}` | `{_default(r['default'])}` | {_first_sentences(r['desc'])} |")
    return "\n".join(lines)


def _by_name(forge, name):
    for r in _settings():
        if r["name"] == name:
            return r
    raise SystemExit(f"readme_gen: no setting {name!r} in ConfigManager.cs")


def settings_server(forge):
    rows = [r for r in _settings() if r["synced"] and not r["section"].startswith("5 - Debug")]
    return _table(rows)


def settings_local(forge):
    rows = [r for r in _settings() if not r["synced"]]
    return _table(rows)


def contract_cost(forge):
    return _default(_by_name(forge, "Contract Cost")["default"])


def star_cap(forge):
    """Max Breeding Level is a level (3 = 2 stars)."""
    return str(int(_default(_by_name(forge, "Max Breeding Level")["default"])) - 1)


def classes(forge):
    n = len(re.findall(r"new CustomDvergrDef\s*\{", _read("AllyPrefabManager.cs")))
    if n < 1:
        raise SystemExit("readme_gen: found no CustomDvergrDef in AllyPrefabManager.cs")
    return str(n)
