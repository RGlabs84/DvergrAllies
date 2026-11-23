#!/usr/bin/env python3
"""fold-package.py: forge the store README, check everything a release needs, and cut the ONE zip that goes to Hexium AND Thunderstore.

    python3 -I tools/fold-package.py [--check-badges]

It refuses (exit 1, "FOLD FAILED") when any check fails, and says which. It never uploads anything.
Checks (each PASS or FAIL, never skipped silently):
  - the manifest version equals DvergrAllies.csproj <Version> and Plugin.cs PluginVersion, and CHANGELOG.md's first heading;
  - name letters/digits/_ only, description <= 250 characters, a 256x256 PNG icon, dependencies listed;
  - the README forges under schema v3.1 (one NEW IN, every setting name found in the code) and starts with both markers;
  - no source file is newer than bin/Release/net48/DvergrAllies.dll (test the build you ship), and the DLL is byte-identical to the
    staged HexiumDistrib/DvergrAllies/plugins/DvergrAllies.dll after it is copied there;
  - the PDB matches the DLL and holds no home-folder path (libs-Tools/pdb_match.py);
  - the zip holds no Jotunn or Newtonsoft (they are dependencies, not part of the package).
"""
import hashlib
import json
import os
import re
import shutil
import struct
import subprocess
import sys
import zipfile

HERE = os.path.dirname(os.path.abspath(__file__))
MOD = os.path.dirname(HERE)
DIST = os.path.join(MOD, "HexiumDistrib")
LIBS = os.environ.get("LIBS_TOOLS", os.path.expanduser("~/WubarrkCODING/libs-Tools"))
BIN = os.path.join(MOD, "bin", "Release", "net48")

problems = []


def check(ok, what, detail=""):
    print(f"{'PASS' if ok else 'FAIL'}  {what}" + (f": {detail}" if detail else ""))
    if not ok:
        problems.append(what)


def read(path):
    with open(path, encoding="utf-8-sig") as fh:
        return fh.read()


def main():
    manifest_path = os.path.join(DIST, "manifest.json")
    manifest = json.loads(read(manifest_path))
    ver = manifest["version_number"]

    # --- versions
    csproj = re.search(r"<Version>([^<]+)</Version>", read(os.path.join(MOD, "DvergrAllies.csproj"))).group(1)
    plugin = re.search(r'PluginVersion\s*=\s*"([^"]+)"', read(os.path.join(MOD, "Plugin.cs"))).group(1)
    first = re.search(r"^## (\S+)", read(os.path.join(DIST, "CHANGELOG.md")), re.M).group(1)
    check(ver == csproj == plugin == first, "one version everywhere", f"manifest {ver}, csproj {csproj}, Plugin.cs {plugin}, CHANGELOG {first}")

    # --- manifest rules both stores share
    check(re.fullmatch(r"[A-Za-z0-9_]+", manifest["name"]) is not None, "package name is letters, digits and _", manifest["name"])
    check(len(manifest["description"]) <= 250, "description is at most 250 characters", str(len(manifest["description"])))
    check(len(manifest.get("dependencies", [])) > 0, "dependencies listed", ", ".join(manifest.get("dependencies", [])))
    with open(os.path.join(DIST, "icon.png"), "rb") as fh:
        head = fh.read(24)
    size = struct.unpack(">II", head[16:24]) if head[:8] == b"\x89PNG\r\n\x1a\n" else None
    check(size == (256, 256), "icon is a 256x256 PNG", str(size))

    # --- the README, forged from its source
    cmd = [sys.executable, "-I", "-B", os.path.join(LIBS, "readme-forge", "forge_readme.py"), os.path.join(MOD, "README.src.md"),
           os.path.join(DIST, "README.md"), "--manifest", manifest_path, "--code-root", MOD]
    if "--check-badges" in sys.argv:
        cmd.append("--check-badges")
    env = dict(os.environ, PYTHONDONTWRITEBYTECODE="1")
    r = subprocess.run(cmd, capture_output=True, text=True, cwd=MOD, env=env)
    check(r.returncode == 0, "README forged (schema v3.1)", (r.stdout + r.stderr).strip().replace("\n", " | "))
    readme = read(os.path.join(DIST, "README.md"))
    check(readme.startswith("<!-- README schema v3 -->\n<!-- README schema v3.1 -->\n"), "README carries both schema markers")
    check(ver in readme, "README names this version")

    # --- the build under test
    dll = os.path.join(BIN, "DvergrAllies.dll")
    pdb = os.path.join(BIN, "DvergrAllies.pdb")
    check(os.path.isfile(dll) and os.path.isfile(pdb), "built DLL and PDB exist", BIN)
    if os.path.isfile(dll):
        newer = [f for f in os.listdir(MOD) if f.endswith(".cs") and os.path.getmtime(os.path.join(MOD, f)) > os.path.getmtime(dll)]
        check(not newer, "no source is newer than the DLL (the DLL is the current code)", ", ".join(newer))
        m = subprocess.run([sys.executable, "-I", os.path.join(LIBS, "pdb_match.py"), dll], capture_output=True, text=True)
        check(m.returncode == 0 and m.stdout.startswith("match"), "PDB matches the DLL, no home path", (m.stdout + m.stderr).strip()[:200])

    if problems:
        print(f"\nFOLD FAILED: {len(problems)} check(s) failed: " + "; ".join(problems))
        return 1

    # --- stage the plugin folder and cut the zip
    stage = os.path.join(DIST, "DvergrAllies", "plugins")
    os.makedirs(stage, exist_ok=True)
    shutil.copy2(dll, os.path.join(stage, "DvergrAllies.dll"))
    shutil.copy2(pdb, os.path.join(stage, "DvergrAllies.pdb"))
    out = os.path.join(DIST, f"DvergrAllies-v{ver}.zip")
    if os.path.exists(out):
        print(f"\nFOLD FAILED: {out} already exists. A released zip is frozen: never rebuilt. Delete it by hand only if it was never uploaded.")
        return 1
    members = [("manifest.json", manifest_path), ("README.md", os.path.join(DIST, "README.md")),
               ("CHANGELOG.md", os.path.join(DIST, "CHANGELOG.md")), ("LICENSE.md", os.path.join(DIST, "LICENSE.md")),
               ("icon.png", os.path.join(DIST, "icon.png")),
               ("plugins/DvergrAllies.dll", os.path.join(stage, "DvergrAllies.dll")),
               ("plugins/DvergrAllies.pdb", os.path.join(stage, "DvergrAllies.pdb"))]
    with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as z:
        for arc, src in members:
            z.write(src, arc)
    with zipfile.ZipFile(out) as z:
        names = z.namelist()
        bad = [n for n in names if re.search(r"jotunn|newtonsoft", n, re.I)]
        check(not bad, "the zip holds no Jotunn or Newtonsoft", ", ".join(bad))
        check(z.read("plugins/DvergrAllies.dll") == open(dll, "rb").read(), "the zip's DLL is byte-identical to the tested build")
        check(z.testzip() is None, "the zip is intact")
    if problems:
        os.remove(out)
        print(f"\nFOLD FAILED: {len(problems)} check(s) failed: " + "; ".join(problems))
        return 1
    with open(out, "rb") as fh:
        digest = hashlib.sha256(fh.read()).hexdigest()
    print(f"\nFOLD OK: {out}\n  {len(names)} files, {os.path.getsize(out):,} bytes, sha256 {digest}")
    for n in names:
        print("   ", n)
    return 0


if __name__ == "__main__":
    sys.exit(main())
