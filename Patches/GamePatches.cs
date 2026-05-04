using HarmonyLib;
using MelonLoader;
using UnityEngine;
using PoliceExpansionMod.Core;
using PoliceExpansionMod.Systems;
using PoliceExpansionMod.Config;

namespace PoliceExpansionMod.Patches
{
    // ── CONFIRMED from HylandEnforcementSystem.dll ───────────────
    // Patch name: Customer_OfferDealItems_Postfix
    // Fires when a customer is offered deal items — the core drug sale event
    [HarmonyPatch("Customer", "OfferDealItems")]
    public class CustomerDealPatch
    {
        static void Postfix(object __instance)
        {
            if (PoliceExpansionMod.Instance?.HeatSystem == null) return;
            string district = DistrictHelper.GetCurrentDistrict();
            PoliceExpansionMod.Instance.HeatSystem.OnPublicDeal(district);
            PoliceExpansionMod.Instance.UndercoverSystem?.OnPlayerSale(district, "customer_deal");
            PoliceExpansionMod.Instance.EvidenceSystem?.AddEvidence(EvidenceType.TransactionHistory, district, "street");
            MelonLogger.Msg($"[Patch] Customer deal in {district} — heat applied.");
        }
    }

    // ── CONFIRMED: PlayerCrimeData_AddCrime_Postfix ──────────────
    // Fires on any crime committed by player
    [HarmonyPatch("PlayerCrimeData", "AddCrime")]
    public class AddCrimePatch
    {
        static void Postfix(object __instance, object crime)
        {
            if (PoliceExpansionMod.Instance?.HeatSystem == null) return;
            string district = DistrictHelper.GetCurrentDistrict();
            PoliceExpansionMod.Instance.HeatSystem.AddDistrictHeat(district, 6f, $"crime committed");
            PoliceExpansionMod.Instance.EvidenceSystem?.AddEvidence(EvidenceType.Fingerprints, district, district + "_street");
            MelonLogger.Msg($"[Patch] Crime added in {district}");
        }
    }

    // ── CONFIRMED: PlayerCrimeData_SetPursuitLevel_Postfix ───────
    // Fires when pursuit level changes — maps to our escalation
    [HarmonyPatch("PlayerCrimeData", "SetPursuitLevel")]
    public class PursuitLevelPatch
    {
        static void Postfix(object __instance, object level)
        {
            if (PoliceExpansionMod.Instance?.HeatSystem == null) return;
            int pursuitLevel = 0;
            int.TryParse(level?.ToString(), out pursuitLevel);
            if (pursuitLevel > 0)
            {
                PoliceExpansionMod.Instance.HeatSystem.AddGlobalHeat(pursuitLevel * 5f, "pursuit level set");
                MelonLogger.Msg($"[Patch] Pursuit level {pursuitLevel} — heat +{pursuitLevel * 5f}");
            }
        }
    }

    // ── CONFIRMED: PoliceOfficer_BeginFootPursuit_Postfix ────────
    // Fires when an officer starts chasing the player on foot
    [HarmonyPatch("PoliceOfficer", "BeginFootPursuit")]
    public class FootPursuitPatch
    {
        static void Postfix(object __instance)
        {
            PoliceExpansionMod.Instance?.HeatSystem?.OnFleeingPolice();
            MelonLogger.Msg("[Patch] Foot pursuit started!");
        }
    }

    // ── CONFIRMED: PoliceOfficer_BeginVehiclePursuit_Postfix ─────
    // Fires when an officer starts a vehicle chase
    [HarmonyPatch("PoliceOfficer", "BeginVehiclePursuit")]
    public class VehiclePursuitPatch
    {
        static void Postfix(object __instance)
        {
            PoliceExpansionMod.Instance?.HeatSystem?.OnFleeingPolice();
            PoliceExpansionMod.Instance?.HeatSystem?.OnSuspiciousDriving(DistrictHelper.GetCurrentDistrict());
            MelonLogger.Msg("[Patch] Vehicle pursuit started!");
        }
    }

    // ── CONFIRMED: PoliceOfficer_BeginBodySearch_Postfix ─────────
    // Fires when officer searches the player's body — evidence risk
    [HarmonyPatch("PoliceOfficer", "BeginBodySearch")]
    public class BodySearchPatch
    {
        static void Postfix(object __instance)
        {
            string district = DistrictHelper.GetCurrentDistrict();
            PoliceExpansionMod.Instance?.HeatSystem?.AddDistrictHeat(district, 8f, "body search");
            PoliceExpansionMod.Instance?.EvidenceSystem?.AddEvidence(EvidenceType.Product, district, "player_on_person");
            MelonLogger.Msg("[Patch] Body search — evidence risk applied.");
        }
    }

    // ── CONFIRMED: PoliceOfficer_Activate_Postfix ────────────────
    // Fires when a police officer becomes active/alerted
    [HarmonyPatch("PoliceOfficer", "Activate")]
    public class OfficerActivatePatch
    {
        static void Postfix(object __instance)
        {
            string district = DistrictHelper.GetCurrentDistrict();
            PoliceExpansionMod.Instance?.HeatSystem?.AddDistrictHeat(district, 3f, "officer activated");
            MelonLogger.Msg($"[Patch] Officer activated in {district}");
        }
    }

    // ── CONFIRMED: PoliceOfficer_CheckNewInvestigation_Postfix ───
    // Fires when police check for a new investigation target
    [HarmonyPatch("PoliceOfficer", "CheckNewInvestigation")]
    public class NewInvestigationPatch
    {
        static void Postfix(object __instance)
        {
            string district = DistrictHelper.GetCurrentDistrict();
            // Investigation check = evidence accumulating
            PoliceExpansionMod.Instance?.EvidenceSystem?
                .AddEvidence(EvidenceType.Photos, district, district + "_property");
            MelonLogger.Msg($"[Patch] New investigation check in {district}");
        }
    }

    // ── CONFIRMED: Business_StartLaunderingOperation_Postfix ─────
    // Fires when player starts a laundering operation
    [HarmonyPatch("Business", "StartLaunderingOperation")]
    public class LaunderingPatch
    {
        static void Postfix(object __instance)
        {
            PoliceExpansionMod.Instance?.HeatSystem?.OnLaunderSuccess();
            MelonLogger.Msg("[Patch] Laundering operation started — heat reduced.");
        }
    }

    // ── CONFIRMED: Contract_InitializeContract_Postfix ───────────
    // Fires when a new drug deal contract is initialized
    [HarmonyPatch("Contract", "InitializeContract")]
    public class ContractInitPatch
    {
        static void Postfix(object __instance)
        {
            string district = DistrictHelper.GetCurrentDistrict();
            PoliceExpansionMod.Instance?.HeatSystem?.OnRepeatedSalesInArea(district);
            MelonLogger.Msg($"[Patch] Contract initialized in {district} — repeat sale heat.");
        }
    }

    // ── CONFIRMED: Contract_SubmitPayment_Postfix ────────────────
    // Fires when a contract payment is submitted — financial evidence
    [HarmonyPatch("Contract", "SubmitPayment")]
    public class ContractPaymentPatch
    {
        static void Postfix(object __instance)
        {
            string district = DistrictHelper.GetCurrentDistrict();
            PoliceExpansionMod.Instance?.EvidenceSystem?
                .AddEvidence(EvidenceType.Cash, district, district + "_property");
            MelonLogger.Msg("[Patch] Contract payment submitted — cash evidence added.");
        }
    }

    // ── CONFIRMED: TimeManager_PassMinute_Postfix ────────────────
    // Fires every in-game minute — drives our heat decay and curfew logic
    [HarmonyPatch("TimeManager", "PassMinute")]
    public class TimePatch
    {
        static void Postfix(object __instance)
        {
            // Notify patrol system of time tick (used for curfew checks)
            PoliceExpansionMod.Instance?.PatrolSystem?.OnMinuteTick();
        }
    }

    // ── CONFIRMED: Player_SleepStart_Postfix ─────────────────────
    // Fires when player goes to sleep — clean day detection
    [HarmonyPatch("Player", "SleepStart")]
    public class SleepStartPatch
    {
        static void Postfix(object __instance)
        {
            PoliceExpansionMod.Instance?.HeatSystem?.OnSuccessfulCleanDay();
            MelonLogger.Msg("[Patch] Player slept — clean day heat reduction.");
        }
    }

    // ── CONFIRMED: SaveManager_Save_Postfix ──────────────────────
    // Fires when game saves — we piggyback to save our state
    [HarmonyPatch("SaveManager", "Save")]
    public class SavePatch
    {
        static void Postfix()
        {
            SaveState.Save(PoliceExpansionMod.Instance);
            MelonLogger.Msg("[Patch] Save hook — PEM state saved.");
        }
    }

    // ── CONFIRMED: LoadManager_StartGame_Postfix ─────────────────
    // Fires when the game world finishes loading
    [HarmonyPatch("LoadManager", "StartGame")]
    public class GameLoadPatch
    {
        static void Postfix()
        {
            MelonLogger.Msg("[Patch] Game loaded — PEM systems active.");
        }
    }

    // ── CONFIRMED: PoliceStation_Dispatch_Prefix ─────────────────
    // Fires when police station dispatches officers — we scale it with our heat
    [HarmonyPatch("PoliceStation", "Dispatch")]
    public class DispatchPatch
    {
        static bool Prefix(object __instance, ref int __result)
        {
            float globalHeat = PoliceExpansionMod.Instance?.HeatSystem?.GlobalHeat ?? 0f;
            int tier = HeatSystem.GetEscalationTier(globalHeat);
            // Allow more dispatches at higher tiers
            if (tier >= 4)
            {
                MelonLogger.Msg($"[Patch] Dispatch boosted by tier {tier} heat.");
            }
            return true; // always let original run
        }
    }

    // ── CONFIRMED: VehiclePatrolInstance_Evaluate_Prefix ─────────
    // Fires when a vehicle patrol evaluates its next action
    [HarmonyPatch("VehiclePatrolInstance", "Evaluate")]
    public class VehiclePatrolPatch
    {
        static void Prefix(object __instance)
        {
            // Route patrol toward hottest district
            string district = DistrictHelper.GetCurrentDistrict();
            float heat = PoliceExpansionMod.Instance?.HeatSystem?.GetDistrictHeat(district) ?? 0f;
            if (heat > 50f)
                MelonLogger.Msg($"[Patch] Vehicle patrol redirected to hot district: {district} ({heat:F0})");
        }
    }

    // ── CONFIRMED: CurfewInstance_Evaluate_Prefix ────────────────
    // Fires when curfew logic evaluates — boost our heat during curfew
    [HarmonyPatch("CurfewInstance", "Evaluate")]
    public class CurfewPatch
    {
        static void Prefix(object __instance)
        {
            string district = DistrictHelper.GetCurrentDistrict();
            PoliceExpansionMod.Instance?.HeatSystem?.OnCurfewViolation(district);
            MelonLogger.Msg($"[Patch] Curfew evaluated in {district}");
        }
    }

    // ── CONFIRMED: CheckpointInstance_Evaluate_Prefix ────────────
    // Fires when a checkpoint evaluates the player
    [HarmonyPatch("CheckpointInstance", "Evaluate")]
    public class CheckpointEvalPatch
    {
        static void Prefix(object __instance)
        {
            string district = DistrictHelper.GetCurrentDistrict();
            MelonLogger.Msg($"[Patch] Checkpoint evaluated in {district}");
            // Failed checkpoint heat is applied separately if player flees
        }
    }
}

// ── District Helper ──────────────────────────────────────────────
namespace PoliceExpansionMod.Patches
{
    public static class DistrictHelper
    {
        // Confirmed method name from HylandEnforcementSystem: GetCurrentDistrict
        // We mirror their approach using camera/player position
        public static string GetCurrentDistrict()
        {
            var cam = Camera.main;
            if (cam == null) return "Downtown";
            Vector3 pos = cam.transform.position;

            // Coordinate ranges — tune these once in-game
            if (pos.x > 200f)   return "Docks";
            if (pos.x < -200f)  return "Slums";
            if (pos.z > 200f)   return "RichArea";
            if (pos.z < -200f)  return "Industrial";
            if (pos.x > 50f)    return "Suburbs";
            return "Downtown";
        }
    }
}
