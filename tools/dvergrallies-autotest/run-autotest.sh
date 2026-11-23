#!/bin/bash
# run-autotest.sh [outdir] : DvergrAllies's unattended in-game test (DvergrAlliesAutoTest, src/Phases.cs).
# Ported from ThunderFeet's tools/thunderfeet-autotest/run-autotest.sh (Runic's, Better Continents' and DwarvenDefender's before it):
# the Steam CLIENT with BepInEx 5.4.2360 injected by doorstop environment variables from a profile of its own, a fresh
# throwaway HOME (its own saves and prefs: the user's are never touched), windowed 1280 x 720, ONE client at a time.
# The profile holds DvergrAllies.dll, the Newtonsoft.Json.dll its stats export needs, and Jotunn (the hard dependency, the same
# 2.30.0 the mod was built against), so the mod loads exactly as a player's install does.
#
# ALWAYS run it through the machine's RAM gate, from the mod folder:
#     ~/valheim-testbed/heavy.sh 10G dvergrallies-client tools/dvergrallies-autotest/run-autotest.sh
#
# Environment: DA_AUTOTEST_TIMEOUT (seconds, default 900: the plugin quits by itself then, this script kills the game
# 180 s later), DA_AUTOTEST_DISPLAY (default: $DISPLAY, else the Steam process's), DA_AUTOTEST_ONLY (phase letters, e.g. TB),
# DA_AUTOTEST_PROFILE (a mod-manager profile's BepInEx folder to run beside), DA_DRY=1 (print the plan, start nothing),
# DA_AUTOTEST_ZIP (a packed release zip: its plugins/ folder is installed instead of bin/'s DLL, so the test runs the shipped file).
# Results: ~/valheim-testbed/autotest-dvergrallies/results-<stamp>/ (or [outdir]); .../latest points at it.
set -u
HERE="$(cd "$(dirname "$(readlink -f "$0")")" && pwd)"
MOD="$(cd "$HERE/../.." && pwd)"
TB="$HOME/valheim-testbed"
BASE="$TB/autotest-dvergrallies"
LT="${LIBS_TOOLS:-$HOME/WubarrkCODING/libs-Tools}"          # the LOCAL mirror, never ~/ArchShare (the sandbox blocks it)
PACKZIP="$LT/denikson-BepInExPack_Valheim-5.4.2360.zip"
FAKEHOME_SH="$LT/DEDICATED-SERVER-TESTBED/fake-home.sh"
GAME="$HOME/.local/share/Steam/steamapps/common/Valheim"
MOD_DLL="$MOD/bin/Release/net48/DvergrAllies.dll"
DRIVER_DLL="$HERE/bin/DvergrAlliesAutoTest.dll"
JOTUNN_DLL="${DA_JOTUNN_DLL:-$MOD/bin/Release/net48/Jotunn.dll}"        # the 2.30.0 the mod compiled against
NEWTONSOFT_DLL="${DA_NEWTONSOFT_DLL:-$MOD/bin/Release/net48/Newtonsoft.Json.dll}"
PKGZIP="${DA_AUTOTEST_ZIP:-}"     # a packed release zip: its plugins/ folder is installed into the profile instead of bin/'s DLL (what a mod manager does)
EXPECT_VERSION="$(python3 -c 'import json,sys; print(json.load(open(sys.argv[1], encoding="utf-8-sig"))["version_number"])' "$MOD/HexiumDistrib/manifest.json" 2>/dev/null)"
LIMIT="${DA_AUTOTEST_TIMEOUT:-900}"
STAMP="$(date +%Y%m%d-%H%M%S)"
OUT="${1:-$BASE/results-$STAMP}"
OUT="$(realpath -m "$OUT")"
PROFILE="$BASE/profile"
FAKEHOME="$BASE/home"
PACK="$BASE/pack"

# Live clients only: Gale leaves valheim.x86_64 <defunct> zombies behind (FACTS, DDPlaytest).
live_clients() { for p in $(pgrep -x valheim.x86_64); do [ "$(awk '{print $3}' "/proc/$p/stat" 2>/dev/null)" != Z ] && echo "$p"; done; }

case "$OUT" in "$GAME"|"$GAME"/*|"$HOME/.config/unity3d"*|*com.kesomannen.gale*) echo "refusing an output folder in $OUT"; exit 2 ;; esac

# The display: never Wayland (Unity 6 picks its Wayland backend and dies before BepInEx, FACTS 2026-11-16).
DISP="${DA_AUTOTEST_DISPLAY:-${DISPLAY:-}}"
XAUTH="${XAUTHORITY:-}"
SPID="$(pgrep -x steam | head -1)"
if [ -n "$SPID" ] && { [ -z "$DISP" ] || [ -z "$XAUTH" ]; }; then
  [ -z "$DISP" ] && DISP="$(tr '\0' '\n' < "/proc/$SPID/environ" 2>/dev/null | sed -n 's/^DISPLAY=//p' | head -1)"
  [ -z "$XAUTH" ] && XAUTH="$(tr '\0' '\n' < "/proc/$SPID/environ" 2>/dev/null | sed -n 's/^XAUTHORITY=//p' | head -1)"
fi

echo "run-autotest.sh: DvergrAllies $(md5sum "$MOD_DLL" 2>/dev/null | cut -c1-8) ($MOD_DLL), driver $(md5sum "$DRIVER_DLL" 2>/dev/null | cut -c1-8) ($DRIVER_DLL)"
echo "  out $OUT; profile $PROFILE; fake HOME $FAKEHOME (fresh); display ${DISP:-none}; time limit $LIMIT s"
if [ "${DA_DRY:-}" = 1 ]; then
  [ -f "$MOD_DLL" ] && echo "  DvergrAllies.dll ok" || echo "  MISSING $MOD_DLL (build DvergrAllies.csproj -c Release)"
  [ -f "$DRIVER_DLL" ] && echo "  driver ok" || echo "  MISSING $DRIVER_DLL (build tools/dvergrallies-autotest/DvergrAlliesAutoTest.csproj)"
  [ -f "$JOTUNN_DLL" ] && echo "  Jotunn ok" || echo "  MISSING $JOTUNN_DLL (build DvergrAllies.csproj -c Release)"
  [ -f "$NEWTONSOFT_DLL" ] && echo "  Newtonsoft.Json ok" || echo "  MISSING $NEWTONSOFT_DLL"
  echo "  manifest version: ${EXPECT_VERSION:-UNREADABLE}"
  [ -f "$PACKZIP" ] && echo "  BepInEx pack ok" || echo "  MISSING $PACKZIP"
  [ -f "$FAKEHOME_SH" ] && echo "  fake-home.sh ok" || echo "  MISSING $FAKEHOME_SH"
  [ -x "$GAME/valheim.x86_64" ] && echo "  game ok" || echo "  MISSING $GAME/valheim.x86_64"
  [ -n "$SPID" ] && echo "  steam is running (pid $SPID)" || echo "  the Steam client is NOT running: it would refuse"
  [ -n "$(live_clients)" ] && echo "  a Valheim client is running: it would refuse" || echo "  no Valheim client is running"
  echo "(DA_DRY: nothing started)"; exit 0
fi

[ -n "$(live_clients)" ] && { echo "a Valheim client is already running (pid $(live_clients | tr '\n' ' ')): not starting another"; exit 3; }
[ -n "$SPID" ] || { echo "the Steam client is not running: start Steam first (the game logs in through it)"; exit 3; }
[ -n "$DISP" ] || { echo "no X display (set DA_AUTOTEST_DISPLAY)"; exit 3; }
for f in "$MOD_DLL" "$DRIVER_DLL" "$JOTUNN_DLL" "$NEWTONSOFT_DLL" "$PACKZIP" "$FAKEHOME_SH" "$GAME/valheim.x86_64"; do [ -e "$f" ] || { echo "missing $f"; exit 2; }; done
mkdir -p "$OUT" "$BASE"
ln -sfn "$OUT" "$BASE/latest"

# ---- the BepInEx pack (unpacked once), a fresh profile, a fresh HOME
if [ ! -f "$PACK/BepInExPack_Valheim/BepInEx/core/BepInEx.Preloader.dll" ]; then
  rm -rf "$PACK"; mkdir -p "$PACK"
  (cd "$PACK" && unzip -q -o "$PACKZIP") || { echo "cannot unpack $PACKZIP"; exit 2; }
fi
DOORSTOP="$PACK/BepInExPack_Valheim/doorstop_libs"
rm -rf "${PROFILE:?}" "${FAKEHOME:?}"
mkdir -p "$PROFILE/BepInEx/plugins/Wubarrk-DvergrAllies" "$PROFILE/BepInEx/plugins/ValheimModding-Jotunn" "$PROFILE/BepInEx/plugins/DvergrAlliesAutoTest" "$PROFILE/BepInEx/patchers" "$PROFILE/BepInEx/config"
cp -r "$PACK/BepInExPack_Valheim/BepInEx/core" "$PROFILE/BepInEx/"
cp "$PACK/BepInExPack_Valheim/BepInEx/config/BepInEx.cfg" "$PROFILE/BepInEx/config/"
if [ -n "$PKGZIP" ]; then
  [ -f "$PKGZIP" ] || { echo "DA_AUTOTEST_ZIP: no such file $PKGZIP"; exit 2; }
  unzip -q -o "$PKGZIP" 'plugins/*' -d "$OUT/pkg" || { echo "cannot unpack $PKGZIP"; exit 2; }
  cp "$OUT/pkg/plugins/"* "$PROFILE/BepInEx/plugins/Wubarrk-DvergrAllies/"
  { echo "package: $PKGZIP"; md5sum "$PKGZIP"; ls -l "$OUT/pkg/plugins"; } > "$OUT/package.txt"
  cat "$OUT/package.txt"
else
  cp "$MOD_DLL" "$PROFILE/BepInEx/plugins/Wubarrk-DvergrAllies/"
fi
cp "$NEWTONSOFT_DLL" "$PROFILE/BepInEx/plugins/Wubarrk-DvergrAllies/"
cp "$JOTUNN_DLL" "$PROFILE/BepInEx/plugins/ValheimModding-Jotunn/"
cp "$DRIVER_DLL" "$PROFILE/BepInEx/plugins/DvergrAlliesAutoTest/"
# Compat runs: DA_AUTOTEST_PROFILE=<a mod-manager profile's BepInEx folder> COPIES its plugins (all but its own DvergrAllies),
# patchers and configs into the throwaway profile, so the test runs beside the same mods and settings a player has.
# The source profile is only read. (Runic's RUNIC_AUTOTEST_EXTRA / _CONFIG, made one variable so a path with spaces works.)
if [ -n "${DA_AUTOTEST_PROFILE:-}" ]; then
  SRC="$DA_AUTOTEST_PROFILE"
  [ -d "$SRC/plugins" ] || { echo "DA_AUTOTEST_PROFILE: no plugins folder in $SRC"; exit 2; }
  find "$SRC/plugins" -mindepth 1 -maxdepth 1 ! -iname '*DvergrAllies*' ! -iname '*Jotunn*' -exec cp -r {} "$PROFILE/BepInEx/plugins/" \;
  [ -d "$SRC/patchers" ] && cp -r "$SRC/patchers/." "$PROFILE/BepInEx/patchers/"
  [ -d "$SRC/config" ] && cp -r "$SRC/config/." "$PROFILE/BepInEx/config/"
  echo "compat profile: $SRC ($(find "$SRC/plugins" -mindepth 1 -maxdepth 1 ! -iname '*DvergrAllies*' ! -iname '*Jotunn*' | wc -l) plugin folders + patchers + config)" | tee "$OUT/extra.txt"
fi
md5sum "$PROFILE/BepInEx/plugins/ValheimModding-Jotunn/Jotunn.dll" "$PROFILE/BepInEx/plugins/Wubarrk-DvergrAllies/DvergrAllies.dll" "$PROFILE/BepInEx/plugins/DvergrAlliesAutoTest/DvergrAlliesAutoTest.dll" | tee "$OUT/dlls.md5"
# shellcheck source=/dev/null
source "$FAKEHOME_SH"
fake_home "$FAKEHOME"
SAVEDATA="$FAKEHOME/.config/unity3d/IronGate/Valheim"

# ---- the game
rm -f "$OUT/done.txt"   # a stale flag would end the wait at once (DDPlaytest)
export DA_AUTOTEST_OUT="$OUT" DA_AUTOTEST_TIMEOUT="$LIMIT" DA_EXPECT_VERSION="$EXPECT_VERSION"
(
  cd "$GAME" || exit 1
  unset WAYLAND_DISPLAY
  export DISPLAY="$DISP"
  [ -n "$XAUTH" ] && export XAUTHORITY="$XAUTH"
  export DOORSTOP_ENABLED=1 DOORSTOP_TARGET_ASSEMBLY="$PROFILE/BepInEx/core/BepInEx.Preloader.dll"
  export LD_LIBRARY_PATH="$DOORSTOP:${LD_LIBRARY_PATH:-}" LD_PRELOAD="libdoorstop_x64.so${LD_PRELOAD:+:$LD_PRELOAD}" SteamAppId=892970
  fake_home_env
  exec ./valheim.x86_64 -screen-fullscreen 0 -screen-width 1280 -screen-height 720 > "$OUT/game.out" 2>&1 < /dev/null
) &
PID=$!
echo "$PID" > "$OUT/pid"
echo "game started (pid $PID); waiting for $OUT/done.txt (up to $((LIMIT + 180)) s)"

END=$((SECONDS + LIMIT + 180))
DONE_AT=""
echo "t_s,rss_kb" > "$OUT/mem.csv"
while kill -0 "$PID" 2>/dev/null && [ $SECONDS -lt $END ]; do
  GP="$(live_clients | head -1)"
  [ -n "$GP" ] && echo "$SECONDS,$(awk '/^VmRSS:/{print $2}' "/proc/$GP/status" 2>/dev/null)" >> "$OUT/mem.csv"
  if [ -f "$OUT/done.txt" ] && [ -z "$DONE_AT" ]; then DONE_AT=$SECONDS; echo "results written after ${SECONDS} s; letting the game log out and quit"; fi
  # After done.txt the plugin logs out, quits, and kills itself 30 s after Quit: give it 120 s in all.
  if [ -n "$DONE_AT" ] && [ $((SECONDS - DONE_AT)) -gt 120 ]; then echo "the game did not quit 120 s after its results: stopping it"; break; fi
  sleep 2
done
if kill -0 "$PID" 2>/dev/null; then
  [ -z "$DONE_AT" ] && echo "no results after $((LIMIT + 180)) s: stopping the game"
  kill -TERM "$PID" 2>/dev/null
  sleep 10
  kill -9 "$PID" 2>/dev/null
fi
wait "$PID" 2>/dev/null
for _ in 1 2 3 4 5 6; do [ -z "$(live_clients)" ] && break; sleep 5; done

# ---- collect and report
cp -p "$PROFILE/BepInEx/LogOutput.log" "$OUT/" 2>/dev/null
cp -p "$SAVEDATA/Player.log" "$OUT/" 2>/dev/null
fake_home_verdict | tee "$OUT/prefs-check.txt"
echo
echo "==== $(cat "$OUT/done.txt" 2>/dev/null || echo "NO done.txt: the run did not finish (see $OUT/driver.log, game.out)")"
echo "==== results ($OUT/results.txt)"
if [ -f "$OUT/results.txt" ]; then cat "$OUT/results.txt"; else echo "(none)"; fi
echo
echo "==== error lines in LogOutput.log"
grep -a -n -E "\[(Error|Fatal)|Exception|Feature Lost" "$OUT/LogOutput.log" 2>/dev/null | grep -v "RESULT" | head -60 || true
echo
echo "screenshots: $(ls "$OUT/img" 2>/dev/null | tr '\n' ' ')"
echo "all output: $OUT (latest: $BASE/latest)"

# ---- the verdict: PASS, FAIL or NOT ASSESSED, never a silent pass (studio rule, 2026-11-17: "a check that cannot report
# that it did not run is not a check"). Ranked FAIL > NOT ASSESSED > PASS. Exit 0 PASS, 1 FAIL, 4 NOT ASSESSED.
NPASS=$(grep -c '^PASS' "$OUT/results.txt" 2>/dev/null); NFAIL=$(grep -c '^FAIL' "$OUT/results.txt" 2>/dev/null); NSKIP=$(grep -c '^SKIP' "$OUT/results.txt" 2>/dev/null)
NERR=$(grep -a -E "\[(Error|Fatal)|Exception|Feature Lost" "$OUT/LogOutput.log" 2>/dev/null | grep -v -c "RESULT")   # the driver's own RESULT lines (e.g. "no Feature Lost") are not errors
WHY=""
[ -f "$OUT/done.txt" ] || WHY="no done.txt: the run never finished"
grep -q "overall timeout" "$OUT/done.txt" 2>/dev/null && WHY="the run hit its overall timeout"
[ -z "$WHY" ] && [ $(( ${NPASS:-0} + ${NFAIL:-0} )) -eq 0 ] && WHY="0 tests reported a result"
[ -z "$WHY" ] && [ "${NSKIP:-0}" -gt 0 ] && WHY="${NSKIP} check(s) were skipped (SKIP lines in results.txt)"
NEWER=$(find "$MOD" -maxdepth 1 -name '*.cs' -newer "$MOD_DLL" 2>/dev/null | head -3 | tr '\n' ' ')
echo
if [ "${NFAIL:-0}" -gt 0 ]; then V=FAIL; elif [ -n "$WHY" ]; then V="NOT ASSESSED"; else V=PASS; fi
echo "==== VERDICT: $V  (${NPASS:-0} pass, ${NFAIL:-0} fail, ${NSKIP:-0} skipped of $(( ${NPASS:-0} + ${NFAIL:-0} + ${NSKIP:-0} )) checks; ${NERR:-0} error lines in LogOutput.log)"
[ -n "$WHY" ] && echo "     not assessed because: $WHY"
[ -n "$NEWER" ] && echo "     WARNING: source is newer than the tested DvergrAllies.dll (e.g. $NEWER): this run did not test the current code"
[ "$MOD_DLL" -nt "$DRIVER_DLL" ] && echo "     WARNING: the driver is older than the mod DLL: rebuild tools/dvergrallies-autotest too"
echo "VERDICT: $V" >> "$OUT/results.txt"
case "$V" in PASS) exit 0 ;; FAIL) exit 1 ;; *) exit 4 ;; esac
