using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MelonLoader;
using PoliceExpansionMod.Core;

namespace PoliceExpansionMod.Systems
{
    public enum RaidPhase { None, Suspicion, Surveillance, WarrantBuild, Active, Completed }
    public enum RaidOutcome { Failed, PartialSeizure, FullSeizure, PlayerEscaped, Bribed }

    public class ActiveRaid
    {
        public string     PropertyId    { get; set; }
        public RaidPhase  Phase         { get; set; } = RaidPhase.Suspicion;
        public float      WarrantStrength { get; set; }
        public bool       PlayerWarned  { get; set; }
        public DateTime   ScheduledTime { get; set; }
        public RaidOutcome Outcome      { get; set; }
    }

    public class RaidSystem : MonoBehaviour
    {
        private List<ActiveRaid>   _raids       = new List<ActiveRaid>();
        private float              _raidReadiness;
        private float              _updateTimer;
        private const float        UpdateInterval = 30f;

        private ModConfig _cfg;

        public event Action<ActiveRaid>          OnRaidWarning;    // player gets a heads-up
        public event Action<ActiveRaid>          OnRaidActive;     // raid begins
        public event Action<ActiveRaid, RaidOutcome> OnRaidResolved;

        private void Start()
        {
            _cfg = PoliceExpansionMod.Instance.Config;

            // Hook evidence → warrant → raid pipeline
            PoliceExpansionMod.Instance.EvidenceSystem.OnWarrantIssued += OnWarrantIssued;
            MelonLogger.Msg("[RaidSystem] Online.");
        }

        private void Update()
        {
            _updateTimer += Time.deltaTime;
            if (_updateTimer >= UpdateInterval)
            {
                _updateTimer = 0f;
                UpdateRaids();
            }
        }

        // ── Public API ───────────────────────────────────────────

        public void SetRaidReadiness(float readiness) => _raidReadiness = readiness;

        public void ScheduleRaid(string propertyId, float delay = 60f)
        {
            // Don't double-schedule
            if (_raids.Exists(r => r.PropertyId == propertyId && r.Phase != RaidPhase.Completed))
            {
                MelonLogger.Msg($"[Raid] {propertyId} already has a pending raid.");
                return;
            }

            var raid = new ActiveRaid {
                PropertyId    = propertyId,
                Phase         = RaidPhase.WarrantBuild,
                ScheduledTime = DateTime.Now.AddSeconds(delay * (1f / _cfg.RaidFrequency))
            };
            _raids.Add(raid);
            MelonLogger.Msg($"[Raid] Raid scheduled for {propertyId} in {delay:F0}s.");
        }

        // ── Internal Update ──────────────────────────────────────

        private void UpdateRaids()
        {
            foreach (var raid in _raids.ToArray())
            {
                if (raid.Phase == RaidPhase.Completed) continue;

                switch (raid.Phase)
                {
                    case RaidPhase.Suspicion:
                        AdvanceSuspicion(raid); break;
                    case RaidPhase.Surveillance:
                        AdvanceSurveillance(raid); break;
                    case RaidPhase.WarrantBuild:
                        AdvanceWarrant(raid); break;
                    case RaidPhase.Active:
                        // Active raid handled on trigger
                        break;
                }
            }
        }

        private void AdvanceSuspicion(ActiveRaid raid)
        {
            float heat = PoliceExpansionMod.Instance.HeatSystem.GetPropertyHeat(raid.PropertyId);
            if (heat > 30f)
            {
                raid.Phase = RaidPhase.Surveillance;
                MelonLogger.Msg($"[Raid] {raid.PropertyId}: Suspicion → Surveillance");
            }
        }

        private void AdvanceSurveillance(ActiveRaid raid)
        {
            float caseStrength = PoliceExpansionMod.Instance.EvidenceSystem.GetCaseStrength(raid.PropertyId);
            raid.WarrantStrength = caseStrength;
            if (caseStrength >= 50f)
            {
                raid.Phase = RaidPhase.WarrantBuild;
                MelonLogger.Msg($"[Raid] {raid.PropertyId}: Surveillance → Warrant Build");
            }
        }

        private void AdvanceWarrant(ActiveRaid raid)
        {
            if (_raidReadiness < 0.3f) return;  // Not enough escalation

            bool timeReached = DateTime.Now >= raid.ScheduledTime;
            if (timeReached || raid.WarrantStrength >= 75f)
            {
                // Warn player BEFORE active raid
                if (!raid.PlayerWarned)
                {
                    raid.PlayerWarned = true;
                    MelonLogger.Msg($"[Raid] *** WARNING: Raid incoming on {raid.PropertyId}! " +
                                    $"You have {_cfg.RaidWarnTime}s! ***");
                    OnRaidWarning?.Invoke(raid);
                    MelonCoroutines.Start(DelayedRaidActivation(raid, _cfg.RaidWarnTime));
                }
            }
        }

        private IEnumerator DelayedRaidActivation(ActiveRaid raid, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (raid.Phase != RaidPhase.Completed)
                ActivateRaid(raid);
        }

        private void ActivateRaid(ActiveRaid raid)
        {
            raid.Phase = RaidPhase.Active;
            MelonLogger.Msg($"[Raid] *** RAID ACTIVE on {raid.PropertyId}! ***");
            OnRaidActive?.Invoke(raid);

            // Add significant heat
            PoliceExpansionMod.Instance.HeatSystem.AddGlobalHeat(20f, "active raid");
            PoliceExpansionMod.Instance.HeatSystem.AddPropertyHeat(raid.PropertyId, 40f, "active raid");
        }

        // ── Player Responses During Raid ─────────────────────────

        public void MoveProduct(string propertyId)
        {
            var raid = GetActiveRaid(propertyId);
            if (raid == null) return;
            MelonLogger.Msg($"[Raid] Product moved from {propertyId}.");
            PoliceExpansionMod.Instance.HeatSystem.ReducePropertyHeat(propertyId, 15f, "product moved");
        }

        public void UseEmergencyCleanup(string propertyId)
        {
            var raid = GetActiveRaid(propertyId);
            if (raid == null) return;
            PoliceExpansionMod.Instance.EvidenceSystem.CleanStashHouse(propertyId);
            MelonLogger.Msg($"[Raid] Emergency cleanup at {propertyId}.");
        }

        public void UseFakePaperwork(string propertyId)
        {
            var raid = GetActiveRaid(propertyId);
            if (raid == null) return;
            raid.WarrantStrength -= 20f;
            MelonLogger.Msg($"[Raid] Fake paperwork reduced warrant strength.");
        }

        public void AttemptFlee(string propertyId)
        {
            var raid = GetActiveRaid(propertyId);
            if (raid == null) return;
            bool escaped = UnityEngine.Random.value < 0.5f;
            if (escaped)
            {
                ResolveRaid(raid, RaidOutcome.PlayerEscaped);
                PoliceExpansionMod.Instance.HeatSystem.OnFleeingPolice();
            }
            else
            {
                MelonLogger.Msg("[Raid] Failed to flee — officers blocked exit!");
                ResolveRaid(raid, RaidOutcome.FullSeizure);
            }
        }

        public void ResolveRaid(ActiveRaid raid, RaidOutcome outcome)
        {
            raid.Phase   = RaidPhase.Completed;
            raid.Outcome = outcome;
            MelonLogger.Msg($"[Raid] Raid on {raid.PropertyId} resolved: {outcome}");
            OnRaidResolved?.Invoke(raid, outcome);

            switch (outcome)
            {
                case RaidOutcome.FullSeizure:
                    PoliceExpansionMod.Instance.HeatSystem.AddGlobalHeat(15f, "full seizure");
                    break;
                case RaidOutcome.PlayerEscaped:
                    PoliceExpansionMod.Instance.HeatSystem.AddGlobalHeat(25f, "escaped raid");
                    break;
                case RaidOutcome.Bribed:
                    PoliceExpansionMod.Instance.HeatSystem.OnBribeSuccess();
                    break;
            }
        }

        private void OnWarrantIssued(string propertyId) => ScheduleRaid(propertyId, 30f);

        private ActiveRaid GetActiveRaid(string propertyId) =>
            _raids.Find(r => r.PropertyId == propertyId && r.Phase == RaidPhase.Active);
    }
}
