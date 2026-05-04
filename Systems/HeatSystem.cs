using System;
using System.Collections.Generic;
using UnityEngine;
using MelonLoader;
using PoliceExpansionMod.Core;
using PoliceExpansionMod.Config;

namespace PoliceExpansionMod.Systems
{
    /// <summary>
    /// Manages Global, District, and Property heat levels.
    /// Heat rises from criminal activity and decays over time.
    /// </summary>
    public class HeatSystem : MonoBehaviour
    {
        // ── Constants ────────────────────────────────────────────
        public const float MaxHeat        = 100f;
        public const float DecayInterval  = 60f;   // seconds between decay ticks

        // ── Global Heat ──────────────────────────────────────────
        private float _globalHeat;
        public  float GlobalHeat => _globalHeat;

        // ── District Heat ────────────────────────────────────────
        private Dictionary<string, float> _districtHeat = new Dictionary<string, float>();

        // ── Property Heat ────────────────────────────────────────
        private Dictionary<string, float> _propertyHeat = new Dictionary<string, float>();

        private float _decayTimer;
        private ModConfig _cfg;

        // ── Events ───────────────────────────────────────────────
        public event Action<float>          OnGlobalHeatChanged;
        public event Action<string, float>  OnDistrictHeatChanged;
        public event Action<string, float>  OnPropertyHeatChanged;
        public event Action<int>            OnEscalationTierChanged;

        private void Start()
        {
            _cfg = PoliceExpansionMod.Instance.Config;
            InitializeDistricts();
            PoliceExpansionMod.Instance.Log("HeatSystem online.");
        }

        private void InitializeDistricts()
        {
            foreach (var district in DistrictSystem.AllDistricts)
                _districtHeat[district] = 0f;
        }

        private void Update()
        {
            _decayTimer += Time.deltaTime;
            if (_decayTimer >= DecayInterval)
            {
                _decayTimer = 0f;
                TickDecay();
            }
        }

        // ── Public API ───────────────────────────────────────────

        public void AddGlobalHeat(float amount, string reason = "")
        {
            float prev = _globalHeat;
            _globalHeat = Mathf.Clamp(_globalHeat + amount, 0, MaxHeat);
            if (!Mathf.Approximately(prev, _globalHeat))
            {
                PoliceExpansionMod.Instance.Log($"[Heat] Global +{amount:F1} → {_globalHeat:F1} ({reason})");
                OnGlobalHeatChanged?.Invoke(_globalHeat);
                CheckEscalation();
            }
        }

        public void AddDistrictHeat(string district, float amount, string reason = "")
        {
            if (!_districtHeat.ContainsKey(district)) _districtHeat[district] = 0f;
            float prev = _districtHeat[district];
            _districtHeat[district] = Mathf.Clamp(prev + amount, 0, MaxHeat);
            if (!Mathf.Approximately(prev, _districtHeat[district]))
            {
                PoliceExpansionMod.Instance.Log($"[Heat] {district} +{amount:F1} → {_districtHeat[district]:F1} ({reason})");
                OnDistrictHeatChanged?.Invoke(district, _districtHeat[district]);
            }
            // District heat also bleeds into global
            AddGlobalHeat(amount * 0.2f, "district bleed");
        }

        public void AddPropertyHeat(string propertyId, float amount, string reason = "")
        {
            if (!_propertyHeat.ContainsKey(propertyId)) _propertyHeat[propertyId] = 0f;
            float prev = _propertyHeat[propertyId];
            _propertyHeat[propertyId] = Mathf.Clamp(prev + amount, 0, MaxHeat);
            if (!Mathf.Approximately(prev, _propertyHeat[propertyId]))
            {
                OnPropertyHeatChanged?.Invoke(propertyId, _propertyHeat[propertyId]);
            }
        }

        public void ReduceGlobalHeat(float amount, string reason = "")  => AddGlobalHeat(-amount, reason);
        public void ReduceDistrictHeat(string d, float amount, string r) => AddDistrictHeat(d, -amount, r);
        public void ReducePropertyHeat(string p, float amount, string r) => AddPropertyHeat(p, -amount, r);

        public float GetDistrictHeat(string district) =>
            _districtHeat.TryGetValue(district, out var h) ? h : 0f;

        public float GetPropertyHeat(string propertyId) =>
            _propertyHeat.TryGetValue(propertyId, out var h) ? h : 0f;

        // ── Heat-Generating Events ───────────────────────────────

        public void OnPublicDeal(string district)
        {
            AddDistrictHeat(district, 5f, "public deal");
            AddGlobalHeat(2f, "public deal");
        }

        public void OnRepeatedSalesInArea(string district)    => AddDistrictHeat(district, 8f, "repeated sales");
        public void OnSuspiciousDriving(string district)      => AddDistrictHeat(district, 3f, "suspicious driving");
        public void OnCurfewViolation(string district)        => AddDistrictHeat(district, 6f, "curfew violation");
        public void OnFleeingPolice()                         => AddGlobalHeat(15f, "fleeing police");
        public void OnVisibleWeapon(string district)          => AddDistrictHeat(district, 10f, "visible weapon");
        public void OnEmployeeArrested()                      => AddGlobalHeat(12f, "employee arrested");
        public void OnCustomerArrested()                      => AddGlobalHeat(6f, "customer arrested");
        public void OnFailedCheckpoint(string district)       => AddDistrictHeat(district, 10f, "failed checkpoint");
        public void OnOfficerViolence()                       => AddGlobalHeat(20f, "officer violence");
        public void OnSnitch()                                => AddGlobalHeat(18f, "informant");

        // ── Heat-Reducing Events ─────────────────────────────────
        public void OnSuccessfulCleanDay()                    => AddGlobalHeat(-5f, "clean day");
        public void OnOperationZoneChanged(string oldZone)    => ReduceDistrictHeat(oldZone, 15f, "zone change");
        public void OnEvidenceDestroyed(string propertyId)    => ReducePropertyHeat(propertyId, 10f, "evidence destroyed");
        public void OnBribeSuccess()                          => AddGlobalHeat(-8f, "bribe success");
        public void OnLaunderSuccess()                        => AddGlobalHeat(-4f, "launder success");

        // ── Decay ─────────────────────────────────────────────────
        private void TickDecay()
        {
            float decayRate = 2f * _cfg.HeatDecaySpeed;

            _globalHeat = Mathf.Max(0, _globalHeat - decayRate * 0.5f);

            var districts = new List<string>(_districtHeat.Keys);
            foreach (var d in districts)
                _districtHeat[d] = Mathf.Max(0, _districtHeat[d] - decayRate);

            var properties = new List<string>(_propertyHeat.Keys);
            foreach (var p in properties)
                _propertyHeat[p] = Mathf.Max(0, _propertyHeat[p] - decayRate * 1.5f);

            OnGlobalHeatChanged?.Invoke(_globalHeat);
        }

        // ── Escalation Check ────────────────────────────────────
        private int _lastTier = 0;
        private void CheckEscalation()
        {
            int tier = GetEscalationTier(_globalHeat);
            if (tier != _lastTier)
            {
                _lastTier = tier;
                OnEscalationTierChanged?.Invoke(tier);
                PoliceExpansionMod.Instance.Log($"[Heat] Escalation tier → {tier}");
            }
        }

        public static int GetEscalationTier(float heat)
        {
            if (heat < 15)  return 0;
            if (heat < 30)  return 1;
            if (heat < 45)  return 2;
            if (heat < 60)  return 3;
            if (heat < 75)  return 4;
            if (heat < 90)  return 5;
            return 6;
        }
    }
}
