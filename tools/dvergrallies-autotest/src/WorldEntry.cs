// Into a single-player world without the menus, a flat clear strip to build the scenes on, and the player helpers every phase uses.
// Ported from ThunderFeet's tools/thunderfeet-autotest/src/WorldEntry.cs (itself Runic's: EnsureCharacter, EnterWorld, Face,
// PlacePiece). FindCorridor finds the strip: DvergrAllies spawns its Dvergr, ballistas and targets along it.
using System;
using System.Collections;
using Splatform;
using UnityEngine;

namespace DvergrAlliesAutoTest
{
    public partial class AutoTest
    {
        /// <summary>Start of the corridor, its direction, and its length.</summary>
        static Vector3 Origin, Fwd;
        static float CorridorLength;

        static PlayerProfile EnsureCharacter()
        {
            var profile = new PlayerProfile(CharacterFile, FileHelpers.FileSource.Local);
            if (!profile.Load())
            {
                profile = new PlayerProfile(CharacterFile, FileHelpers.FileSource.Local);
                profile.SetName("Dvergtester");
            }
            profile.m_firstSpawn = false;
            profile.Save();
            return profile;
        }

        IEnumerator EnterWorld()
        {
            inWorld = false;
            var profile = EnsureCharacter();
            Game.SetProfile(profile.GetFilename(), profile.m_fileSource);
            SaveSystem.ClearWorldListCache(true);

            global::World world;
            if (SaveSystem.TryGetSaveByName(WorldName, SaveDataType.World, out var save) && !save.IsDeleted)
            {
                world = global::World.LoadWorld(save);
                if (world.m_dataError != global::World.SaveDataError.None)
                {
                    Result("FAIL", "load world", $"{WorldName}: data error {world.m_dataError}");
                    yield break;
                }
            }
            else
            {
                world = new global::World(WorldName, WorldSeed) { m_fileSource = FileHelpers.FileSource.Local };
                SaveSystem.SetSaveNumber(0u);
                world.SaveWorldFWLData(DateTime.Now);
                Log($"world {WorldName} created (seed {WorldSeed})");
            }

            FejdStartup.instance.m_world = world;
            ZNet.m_onlineBackend = OnlineBackendType.Steamworks;
            ZNet.SetServer(true, false, false, world.m_name, "", world);
            ZNet.ResetServerHost();
            FejdStartup.instance.LoadMainScene();

            float t0 = Time.realtimeSinceStartup;
            yield return WaitFor(() => Player.m_localPlayer != null && ZoneSystem.instance != null && ZoneSystem.instance.IsActiveAreaLoaded()
                                       && !Player.m_localPlayer.IsTeleporting(), 900f);
            if (!waitOk)
            {
                Result("FAIL", "spawn", "no player in a loaded world within 900 s");
                yield break;
            }
            Result("PASS", "spawn", $"player in the world after {Time.realtimeSinceStartup - t0:0} s");
            yield return Sec(8f);

            Player p = Player.m_localPlayer;
            p.SetGodMode(true);
            p.SetGhostMode(true);
            yield return FindCorridor();
            inWorld = Player.m_localPlayer != null;
        }

        /// <summary>A straight run of 30 m near the spawn: dry, no step over 1 m between samples 2 m apart, nothing solid
        /// in a 1.2 m sphere at chest height along it. The player then stands at its start, facing along it.</summary>
        IEnumerator FindCorridor()
        {
            Player p = Player.m_localPlayer;
            Vector3 start = p.transform.position;
            float water = ZoneSystem.instance.m_waterLevel;
            int solid = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "vehicle");
            const float len = 30f;
            bool found = false;
            Vector3 bestStart = start, bestDir = Vector3.forward;
            foreach (float r in new[] { 10f, 25f, 40f, 60f, 90f })
            {
                for (int k = 0; k < 12 && !found; k++)
                {
                    float a = k * Mathf.PI / 6f;
                    Vector3 c = start + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                    for (int d = 0; d < 8 && !found; d++)
                    {
                        float b = d * Mathf.PI / 4f;
                        var dir = new Vector3(Mathf.Cos(b), 0f, Mathf.Sin(b));
                        bool ok = true;
                        float prev = Ground(c);
                        for (float s = 0f; s <= len && ok; s += 2f)
                        {
                            Vector3 q = c + dir * s;
                            float h = Ground(q);
                            if (h < water + 1f || Mathf.Abs(h - prev) > 1f) ok = false;
                            else if (Physics.CheckSphere(new Vector3(q.x, h + 1.3f, q.z), 1.2f, solid, QueryTriggerInteraction.Ignore)) ok = false;
                            prev = h;
                        }
                        if (ok) { found = true; bestStart = c; bestDir = dir; }
                    }
                }
                if (found) break;
            }
            if (!found) Result("WARN", "corridor", $"no clear 30 m run within 90 m of {V(start)}: testing from the spawn");

            Fwd = bestDir;
            CorridorLength = found ? len : 10f;
            Vector3 at = OnGround(bestStart) + Vector3.up * 0.3f;
            if (found && Vector3.Distance(start, at) > 2f)
            {
                if (!p.TeleportTo(at, Quaternion.LookRotation(Fwd), false))
                {
                    yield return Sec(2.5f);
                    p.TeleportTo(at, Quaternion.LookRotation(Fwd), false);
                }
                yield return null;
                yield return WaitFor(() => !p.IsTeleporting() && ZoneSystem.instance.IsActiveAreaLoaded(), 120f);
                yield return Sec(3f);
            }
            Origin = p.transform.position;
            Face(Fwd);
            Result(found ? "PASS" : "WARN", "corridor", $"start {V(Origin)}, facing {V(Fwd)}, {CorridorLength:0} m, biome {Heightmap.FindBiome(Origin)}");
        }

        // ------------------------------------------------------------------------------------------ helpers

        internal static string V(Vector3 v) => $"({v.x.ToString("0.0", IC)}, {v.y.ToString("0.0", IC)}, {v.z.ToString("0.0", IC)})";

        static float Ground(Vector3 p) => ZoneSystem.instance.GetGroundHeight(p);

        static Vector3 OnGround(Vector3 p) => new Vector3(p.x, Ground(p), p.z);

        static void Face(Vector3 dir)
        {
            Player p = Player.m_localPlayer;
            if (p == null || dir.sqrMagnitude < 1e-6f) return;
            var flat = new Vector3(dir.x, 0f, dir.z).normalized;
            p.m_lookYaw = Quaternion.LookRotation(flat);
            p.m_lookPitch = 0f;
            p.SetLookDir(flat);
            p.transform.rotation = Quaternion.LookRotation(flat);
        }

        /// <summary>Puts the player at a point standing still, facing a direction (no teleport cooldown: same zone).</summary>
        static IEnumerator PlaceAt(Vector3 at, Vector3 facing)
        {
            Player p = Player.m_localPlayer;
            // stand ON whatever is there (a floor laid over the terrain), not inside it
            int solid = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain", "vehicle");
            at = Physics.Raycast(new Vector3(at.x, Ground(at) + 4f, at.z), Vector3.down, out RaycastHit hit, 8f, solid, QueryTriggerInteraction.Ignore)
                ? hit.point + Vector3.up * 0.2f
                : OnGround(at) + Vector3.up * 0.2f;
            p.transform.position = at;
            p.m_body.position = at;
            p.m_body.linearVelocity = Vector3.zero;
            p.m_currentVel = Vector3.zero;
            Face(facing);
            yield return Sec(0.6f);
            yield return WaitFor(() => p.IsOnGround(), 3f);
            Face(facing);
        }

        static GameObject Spawn(string prefabName, Vector3 pos, Quaternion rot)
        {
            GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(prefabName) : null;
            if (prefab == null) return null;
            return UnityEngine.Object.Instantiate(prefab, pos, rot);
        }

        static GameObject PlacePiece(string prefabName, Vector3 pos, Quaternion rot)
        {
            GameObject go = Spawn(prefabName, pos, rot);
            if (go == null) return null;
            Piece piece = go.GetComponent<Piece>();
            Player p = Player.m_localPlayer;
            try
            {
                if (piece != null && p != null)
                    piece.SetCreator(p.GetPlayerID(), PlatformManager.DistributionPlatform.LocalUser.PlatformUserID);
                go.GetComponent<WearNTear>()?.OnPlaced();
            }
            catch (Exception e) { Log("SetCreator: " + e.Message); }
            return go;
        }
    }
}
