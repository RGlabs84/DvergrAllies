using System;
using BepInEx;
using BepInEx.Bootstrap;

namespace DvergrAllies
{
    // FIND ANOTHER PLUGIN BY GUID, WITHOUT CARING ABOUT CASE.
    //
    // Chainloader.PluginInfos is a plain Dictionary<string, PluginInfo> built with the DEFAULT comparer -
    // ordinal and CASE-SENSITIVE. So ContainsKey("some.guid") returns false against a plugin that registered
    // itself as "Some.Guid", and a mod author owns their own GUID: re-casing it between releases does not read
    // as a breaking change from where they are standing. Shadows of Midgard did exactly that between 1.0.0 and
    // 2.0.0, and it made a sibling mod log "not installed" in a session where SoM was loaded and patching.
    //
    // STANDING RULE across every project in this root: never compare a plugin GUID with ==, ContainsKey or
    // TryGetValue. Soft-dependency probes go through here.
    // PluginInfo is written BepInEx.PluginInfo throughout, never bare: BepInEx.PluginInfoProps generates a
    // STATIC PluginInfo class into this project'''s own root namespace, which wins the bare name and cannot be
    // used as a parameter type. Keep it qualified so this file stays copy-pasteable into any project here.
    public static class PluginLookup
    {
        public static bool TryFind(string guid, out BepInEx.PluginInfo info)
        {
            info = null;
            if (string.IsNullOrEmpty(guid)) return false;

            // Fast path: an exact hit is the common case and costs one hash.
            if (Chainloader.PluginInfos.TryGetValue(guid, out info) && info != null) return true;

            foreach (var kv in Chainloader.PluginInfos)
            {
                if (!string.Equals(kv.Key, guid, StringComparison.OrdinalIgnoreCase)) continue;
                info = kv.Value;
                return info != null;
            }

            info = null;
            return false;
        }

        public static bool IsLoaded(string guid)
        {
            BepInEx.PluginInfo unused;
            return TryFind(guid, out unused);
        }
    }
}
