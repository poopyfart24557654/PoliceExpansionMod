using System;
using System.Collections.Generic;
using UnityEngine;
using MelonLoader;
using PoliceExpansionMod.Core;
using PoliceExpansionMod.Config;

namespace PoliceExpansionMod.Systems
{
    public enum EvidenceType
    {
        Cash, Product, Weapon, Ledger, MarkedBills,
        HiddenCamera, EmployeeTestimony, Fingerprints,
        TransactionHistory, Photos
    }

    public class EvidenceItem
    {
        public string       Id          { get; set; } = Guid.NewGuid().ToString("N").Substring(0, 8);
        public EvidenceType Type        { get; set; }
        public string       PropertyId  { get; set; }
        public string       District    { get; set; }
        public float        Weight      { get; set; }  // how much it moves an investigation
        public DateTime     CollectedAt { get; set; }
        public bool         Destroyed   { get; set; }
    }

    public class EvidenceSystem : MonoBehaviour
    {
        private List<EvidenceItem>     _evidence     = new List<EvidenceItem>();
        private Dictionary<string,float> _caseStrength = new Dictionary<string,float>(); // per-property

        public event Action<string, float> OnCaseStrengthChanged;  // propertyId, strength
        public event Action<string>        OnWarrantIssued;         // propertyId

        private const float WarrantThreshold = 75f;
        private ModConfig   _cfg;

        private void Start()
        {
            _cfg = PoliceExpansionMod.Instance.Config;
            MelonLogger.Msg("[EvidenceSystem] Online.");
        }

        // ── Public API ───────────────────────────────────────────

        public void AddEvidence(EvidenceType type, string district, string propertyId)
        {
            float weight = GetEvidenceWeight(type) * _cfg.EvidenceGrowthRate;

            var item = new EvidenceItem {
                Type       = type,
                District   = district,
                PropertyId = propertyId,
                Weight     = weight,
                CollectedAt = DateTime.Now
            };
            _evidence.Add(item);

            // Increase case strength against this property
            if (!_caseStrength.ContainsKey(propertyId)) _caseStrength[propertyId] = 0f;
            _caseStrength[propertyId] = Mathf.Clamp(_caseStrength[propertyId] + weight, 0, 100f);

            MelonLogger.Msg($"[Evidence] {type} collected at {propertyId} " +
                            $"(weight: {weight:F1}, case: {_caseStrength[propertyId]:F1})");

            OnCaseStrengthChanged?.Invoke(propertyId, _caseStrength[propertyId]);

            // Also raises property heat
            PoliceExpansionMod.Instance.HeatSystem
                .AddPropertyHeat(propertyId, weight * 0.5f, $"evidence: {type}");

            // Check for warrant
            if (_caseStrength[propertyId] >= WarrantThreshold)
                TriggerWarrant(propertyId);
        }

        public bool DestroyEvidence(string propertyId, EvidenceType? specificType = null)
        {
            var targets = specificType.HasValue
                ? _evidence.FindAll(e => e.PropertyId == propertyId && e.Type == specificType.Value && !e.Destroyed)
                : _evidence.FindAll(e => e.PropertyId == propertyId && !e.Destroyed);

            if (targets.Count == 0)
            {
                MelonLogger.Msg($"[Evidence] No evidence to destroy at {propertyId}.");
                return false;
            }

            float totalDestroyed = 0f;
            foreach (var e in targets) { e.Destroyed = true; totalDestroyed += e.Weight; }

            if (_caseStrength.ContainsKey(propertyId))
            {
                _caseStrength[propertyId] = Mathf.Max(0, _caseStrength[propertyId] - totalDestroyed * 0.7f);
                OnCaseStrengthChanged?.Invoke(propertyId, _caseStrength[propertyId]);
            }

            PoliceExpansionMod.Instance.HeatSystem.OnEvidenceDestroyed(propertyId);
            MelonLogger.Msg($"[Evidence] Destroyed {targets.Count} items at {propertyId} " +
                            $"(-{totalDestroyed:F1} case strength)");
            return true;
        }

        public float GetCaseStrength(string propertyId) =>
            _caseStrength.TryGetValue(propertyId, out var s) ? s : 0f;

        public bool IsUnderInvestigation(string propertyId) =>
            GetCaseStrength(propertyId) >= 30f;

        public bool HasWarrant(string propertyId) =>
            GetCaseStrength(propertyId) >= WarrantThreshold;

        public void CleanStashHouse(string propertyId)
        {
            DestroyEvidence(propertyId);
            PoliceExpansionMod.Instance.HeatSystem
                .ReducePropertyHeat(propertyId, 20f, "stash cleaned");
            MelonLogger.Msg($"[Evidence] Stash house {propertyId} cleaned.");
        }

        // ── Employee Testimony (from informants) ─────────────────
        public void AddEmployeeTestimony(string propertyId)
        {
            AddEvidence(EvidenceType.EmployeeTestimony, "Unknown", propertyId);
            PoliceExpansionMod.Instance.HeatSystem.OnSnitch();
        }

        // ── Warrant ──────────────────────────────────────────────
        private void TriggerWarrant(string propertyId)
        {
            MelonLogger.Msg($"[Evidence] WARRANT ISSUED for {propertyId}!");
            OnWarrantIssued?.Invoke(propertyId);
            PoliceExpansionMod.Instance.RaidSystem?.ScheduleRaid(propertyId, delay: 30f);
        }

        // ── Evidence Weights ─────────────────────────────────────
        private static float GetEvidenceWeight(EvidenceType type) => type switch
        {
            EvidenceType.MarkedBills        => 20f,
            EvidenceType.EmployeeTestimony  => 18f,
            EvidenceType.Ledger             => 16f,
            EvidenceType.HiddenCamera       => 15f,
            EvidenceType.TransactionHistory => 14f,
            EvidenceType.Photos             => 12f,
            EvidenceType.Product            => 10f,
            EvidenceType.Weapon             =>  8f,
            EvidenceType.Fingerprints       =>  6f,
            EvidenceType.Cash               =>  4f,
            _                               =>  5f
        };
    }
}
