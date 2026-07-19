using System;
using System.Collections;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace ErenshorSwingSync
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class SwingSyncPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.blackhorse311.erenshor.swingsync";
        public const string PluginName = "Erenshor SwingSync";
        public const string PluginVersion = "1.1.0";

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<float> SwingDelaySeconds;
        internal static ConfigEntry<bool> NpcEnabled;
        internal static ConfigEntry<float> NpcSwingDelaySeconds;

        internal static bool Replaying;
        internal static Animator SuppressAnim;
        internal static AccessTools.FieldRef<PlayerCombat, Character> MyselfRef;

        private static Action<PlayerCombat, Character, int, bool> _performAttacks;
        private static Action<NPC, int, bool> _performMeleeHit;
        private static AccessTools.FieldRef<NPC, Stats> _npcStatsRef;
        private static AccessTools.FieldRef<Stats, float> _mhAtkDelayRef;
        private static AccessTools.FieldRef<Stats, float> _ohAtkDelayRef;

        internal static bool PlayerReady => _performAttacks != null;
        internal static bool NpcReady => _performMeleeHit != null;

        private void Awake()
        {
            Log = Logger;

            Enabled = Config.Bind("General", "Enabled", true,
                "Master toggle for player swings. When on, your melee swing animation starts immediately " +
                "and the damage (numbers, combat log, sounds, target flinch) lands after the windup delay, " +
                "so the hit connects when the blade does instead of half a second before it. " +
                "Turn off to restore the vanilla 1999-authentic feel.");
            SwingDelaySeconds = Config.Bind("General", "SwingDelaySeconds", 0.45f,
                new ConfigDescription(
                    "Seconds between your swing starting and the damage landing. " +
                    "Tune until impacts line up with your weapon animation. Applies live.",
                    new AcceptableValueRange<float>(0f, 1.5f)));
            NpcEnabled = Config.Bind("NPCs", "Enabled", true,
                "Sync NPC and SimPlayer melee swings the same way: their damage lands when their " +
                "animation connects instead of at the start of the windup. Attack round cadence " +
                "(their DPS) is unchanged.");
            NpcSwingDelaySeconds = Config.Bind("NPCs", "SwingDelaySeconds", 0.45f,
                new ConfigDescription(
                    "Seconds between an NPC/SimPlayer swing starting and their damage landing. Applies live.",
                    new AcceptableValueRange<float>(0f, 1.5f)));

            // RE findings from Erenshor Steam build (Unity 2021.3.45f2), decompiled 2026-07-19.
            MethodInfo performAttacks = AccessTools.Method(typeof(PlayerCombat), "PerformAttacks",
                new[] { typeof(Character), typeof(int), typeof(bool) });
            FieldInfo myselfField = AccessTools.Field(typeof(PlayerCombat), "Myself");
            if (performAttacks != null && myselfField != null)
            {
                _performAttacks = AccessTools.MethodDelegate<Action<PlayerCombat, Character, int, bool>>(
                    performAttacks, virtualCall: false);
                MyselfRef = AccessTools.FieldRefAccess<PlayerCombat, Character>(myselfField);
            }
            else
            {
                Log.LogError(
                    $"Could not resolve PlayerCombat members (PerformAttacks found: {performAttacks != null}, " +
                    $"Myself found: {myselfField != null}). Probably a game update - check for a mod update. " +
                    "Player swing sync disabled, game untouched.");
            }

            MethodInfo performMeleeHit = AccessTools.Method(typeof(NPC), "PerformMeleeHit",
                new[] { typeof(int), typeof(bool) });
            FieldInfo npcStatsField = AccessTools.Field(typeof(NPC), "MyStats");
            FieldInfo mhDelayField = AccessTools.Field(typeof(Stats), "ActualMHAtkDelay");
            FieldInfo ohDelayField = AccessTools.Field(typeof(Stats), "ActualOHAtkDelay");
            if (performMeleeHit != null && npcStatsField != null && mhDelayField != null && ohDelayField != null)
            {
                _performMeleeHit = AccessTools.MethodDelegate<Action<NPC, int, bool>>(
                    performMeleeHit, virtualCall: false);
                _npcStatsRef = AccessTools.FieldRefAccess<NPC, Stats>(npcStatsField);
                _mhAtkDelayRef = AccessTools.FieldRefAccess<Stats, float>(mhDelayField);
                _ohAtkDelayRef = AccessTools.FieldRefAccess<Stats, float>(ohDelayField);
            }
            else
            {
                Log.LogError(
                    $"Could not resolve NPC/Stats members (PerformMeleeHit found: {performMeleeHit != null}, " +
                    $"NPC.MyStats found: {npcStatsField != null}, ActualMHAtkDelay found: {mhDelayField != null}, " +
                    $"ActualOHAtkDelay found: {ohDelayField != null}). " +
                    "Probably a game update - check for a mod update. NPC swing sync disabled, game untouched.");
            }

            new Harmony(PluginGuid).PatchAll(typeof(SwingSyncPlugin).Assembly);
            Log.LogInfo($"{PluginName} {PluginVersion} loaded. Player windup: {SwingDelaySeconds.Value:F2}s, " +
                        $"NPC windup: {NpcSwingDelaySeconds.Value:F2}s");
        }

        // ---- Player ----

        internal static void ScheduleImpact(PlayerCombat pc, Character target, int attackCount, bool isMainHand, Animator anim)
        {
            // Host the coroutine on PlayerCombat itself: it is alive whenever a swing fires,
            // and if the player zones mid-swing the pending impact correctly dies with it.
            // (The BepInEx plugin object is NOT a safe host here - the game destroyed it
            // mid-session in testing, NRE'ing every swing. Erenshor build 2026-07.)
            pc.StartCoroutine(Impact(pc, target, attackCount, isMainHand, anim));
        }

        private static IEnumerator Impact(PlayerCombat pc, Character target, int attackCount, bool isMainHand, Animator anim)
        {
            yield return new WaitForSeconds(SwingDelaySeconds.Value);
            if (pc == null || target == null || !target.Alive)
            {
                yield break; // target died or despawned mid-swing; the swing whiffs
            }
            try
            {
                Replaying = true;
                SuppressAnim = anim;
                // Re-enters the patched method; the prefix passes through when Replaying is set,
                // so other mods' patches on PerformAttacks still run at impact time.
                _performAttacks(pc, target, attackCount, isMainHand);
            }
            catch (Exception ex)
            {
                Log.LogError($"Delayed player attack resolution failed: {ex}");
            }
            finally
            {
                Replaying = false;
                SuppressAnim = null;
            }
        }

        // ---- NPCs / SimPlayers ----

        internal static void ScheduleNpcImpact(NPC npc, int baseDamage, bool isOffhand)
        {
            npc.StartCoroutine(NpcImpact(npc, baseDamage, isOffhand));
        }

        private static IEnumerator NpcImpact(NPC npc, int baseDamage, bool isOffhand)
        {
            float delaySeconds = NpcSwingDelaySeconds.Value;
            yield return new WaitForSeconds(delaySeconds);
            if (npc == null)
            {
                yield break; // attacker despawned mid-swing
            }
            Stats stats = _npcStatsRef(npc);
            if (stats == null || stats.Myself == null || !stats.Myself.Alive)
            {
                yield break; // attacker died mid-swing
            }
            if (npc.CurrentAggroTarget == null || !npc.CurrentAggroTarget.Alive)
            {
                yield break; // target died or despawned mid-swing; the swing whiffs
            }
            float mhBefore = _mhAtkDelayRef(stats);
            float ohBefore = _ohAtkDelayRef(stats);
            try
            {
                Replaying = true;
                _performMeleeHit(npc, baseDamage, isOffhand);
            }
            catch (Exception ex)
            {
                Log.LogError($"Delayed NPC attack resolution failed: {ex}");
            }
            finally
            {
                Replaying = false;
            }
            // The original resets the attack-round timers at impact time, but vanilla reset them at
            // swing time. Backdate whichever timers this call reset so NPC attack cadence (and DPS)
            // stays exactly vanilla. Timers are in 60ths of a second.
            float ticks = delaySeconds * 60f;
            if (!Mathf.Approximately(_mhAtkDelayRef(stats), mhBefore))
            {
                _mhAtkDelayRef(stats) = Mathf.Max(_mhAtkDelayRef(stats) - ticks, 0f);
            }
            if (!Mathf.Approximately(_ohAtkDelayRef(stats), ohBefore))
            {
                _ohAtkDelayRef(stats) = Mathf.Max(_ohAtkDelayRef(stats) - ticks, 0f);
            }
        }
    }

    [HarmonyPatch(typeof(PlayerCombat), "PerformAttacks")]
    internal static class PlayerCombat_PerformAttacks_Patch
    {
        // Prefix-skip justification: deferring the original body until the swing animation
        // connects IS the feature. The deferred call runs the full original (other mods'
        // patches included). Config-toggleable via [General] Enabled.
        private static bool Prefix(PlayerCombat __instance, Character target, int attackCount, bool isMainHand)
        {
            if (!SwingSyncPlugin.PlayerReady || !SwingSyncPlugin.Enabled.Value || SwingSyncPlugin.Replaying)
            {
                return true;
            }
            try
            {
                // Mirror the original's branch selection: only pure melee-in-range attacks are
                // deferred. Wand/bow (projectile travel already syncs impact) and out-of-range
                // attempts fall through to vanilla.
                bool isWand = isMainHand
                    ? GameData.PlayerInv?.MH?.MyItem?.IsWand == true
                    : GameData.PlayerInv?.OH?.MyItem?.IsWand == true;
                bool isBow = isMainHand
                    ? GameData.PlayerInv?.MH?.MyItem?.IsBow == true
                    : GameData.PlayerInv?.OH?.MyItem?.IsBow == true;
                if (isWand || isBow || target == null)
                {
                    return true;
                }
                if (__instance.MyMeleeRange == null || !__instance.MyMeleeRange.GetNPCsInRange().Contains(target))
                {
                    return true;
                }
                Character self = SwingSyncPlugin.MyselfRef(__instance);
                if (self == null)
                {
                    return true;
                }
                Animator anim = self.GetMyAnim();
                if (anim == null)
                {
                    return true;
                }

                // Swing now (mirrors the original's i==0/1/2 animator calls)...
                anim.SetTrigger(isMainHand ? "MeleeSwing" : "DualWield");
                if (attackCount >= 1)
                {
                    anim.SetBool(isMainHand ? "DoubleAttack" : "OHDoubleAttack", value: true);
                }
                if (attackCount >= 2)
                {
                    anim.SetTrigger("MeleeSwing");
                }

                // ...damage lands when the blade does.
                SwingSyncPlugin.ScheduleImpact(__instance, target, attackCount, isMainHand, anim);
                return false;
            }
            catch (Exception ex)
            {
                SwingSyncPlugin.Log.LogError($"SwingSync player prefix failed, vanilla behavior this swing: {ex}");
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(NPC), "PerformMeleeHit")]
    internal static class NPC_PerformMeleeHit_Patch
    {
        // Prefix-skip justification: same as the player patch - the deferral IS the feature,
        // and the deferred call runs the full original. Config-toggleable via [NPCs] Enabled.
        // No animation work needed here: NPC.Combat() fires the swing triggers itself before
        // calling PerformMeleeHit, so deferring only this method produces the sync.
        private static bool Prefix(NPC __instance, int baseDamage, bool isOffhand)
        {
            if (!SwingSyncPlugin.NpcReady || !SwingSyncPlugin.NpcEnabled.Value || SwingSyncPlugin.Replaying)
            {
                return true;
            }
            try
            {
                if (__instance.CurrentAggroTarget == null)
                {
                    return true;
                }
                SwingSyncPlugin.ScheduleNpcImpact(__instance, baseDamage, isOffhand);
                return false;
            }
            catch (Exception ex)
            {
                SwingSyncPlugin.Log.LogError($"SwingSync NPC prefix failed, vanilla behavior this swing: {ex}");
                return true;
            }
        }
    }

    // The deferred player attack still fires its own animator calls; these two patches swallow
    // exactly those (player's animator, only while the deferred call is on the stack) so the
    // swing doesn't play twice. NPC replays never set SuppressAnim - their triggers live in
    // NPC.Combat(), which is not deferred.
    [HarmonyPatch(typeof(Animator), nameof(Animator.SetTrigger), typeof(string))]
    internal static class Animator_SetTrigger_Patch
    {
        private static bool Prefix(Animator __instance)
        {
            return !SwingSyncPlugin.Replaying || __instance != SwingSyncPlugin.SuppressAnim;
        }
    }

    [HarmonyPatch(typeof(Animator), nameof(Animator.SetBool), typeof(string), typeof(bool))]
    internal static class Animator_SetBool_Patch
    {
        private static bool Prefix(Animator __instance)
        {
            return !SwingSyncPlugin.Replaying || __instance != SwingSyncPlugin.SuppressAnim;
        }
    }
}
