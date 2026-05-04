using System;
using System.Collections.Generic;
using UnityEngine;
using MelonLoader;
using PoliceExpansionMod.Core;

namespace PoliceExpansionMod.Systems
{
    public enum UndercoverRole { Buyer, LoiteringNPC, RepeatCustomer, FakeDealer, StreetWatcher }

    public class UndercoverOfficer
    {
        public string          Id              { get; set; } = Guid.NewGuid().ToString("N").Substring(0, 8);
        public UndercoverRole  Role            { get; set; }
        public string          District        { get; set; }
        public bool            HasTestBought   { get; set; }
        public int             TimesSpotted    { get; set; }  // by player
        public float           EvidenceGathered { get; set; }
        public bool            Blown           { get; set; }  // player identified them
        public int             VisitCount      { get; set; }
    }

    public class UndercoverSystem : MonoBehaviour
    {
        private List<UndercoverOfficer> _officers = new List<UndercoverOfficer>();
        private float _activityLevel;  // 0–1, set by escalation
        private float _spawnTimer;
        private const float SpawnInterval = 90f;

        private ModConfig _cfg;

        public event Action<UndercoverOfficer> OnStingTriggered;
        public event Action<string>            OnSuspiciousCustomerWarning;  // clue hint to player

        private void Start()
        {
            _cfg = PoliceExpansionMod.Instance.Config;
            MelonLogger.Msg("[UndercoverSystem] Online.");
        }

        private void Update()
        {
            _spawnTimer += Time.deltaTime;
            if (_spawnTimer >= SpawnInterval)
            {
                _spawnTimer = 0f;
                TrySpawnUndercover();
            }
        }

        // ── Public API ───────────────────────────────────────────

        public void SetUndercoverActivity(float level) => _activityLevel = level;

        public void TriggerSting(string district)
        {
            var uc = SpawnOfficer(district, UndercoverRole.Buyer);
            uc.HasTestBought = true;
            MelonLogger.Msg($"[Undercover] Sting operation launched in {district}!");
            OnStingTriggered?.Invoke(uc);
        }

        // ── Player Interactions ──────────────────────────────────

        /// <summary>Call when player sells to a customer NPC. Returns true if it was an undercover buy.</summary>
        public bool OnPlayerSale(string district, string customerId)
        {
            if (UnityEngine.Random.value > _cfg.UndercoverChance * _activityLevel * 3f)
                return false;

            // Check if this customer is an undercover officer
            var uc = _officers.Find(o => o.District == district && !o.Blown && !o.HasTestBought);
            if (uc == null) return false;

            uc.HasTestBought     = true;
            uc.EvidenceGathered += 25f;

            MelonLogger.Msg($"[Undercover] TEST BUY executed in {district}! Role: {uc.Role}");
            PoliceExpansionMod.Instance.EvidenceSystem
                .AddEvidence(EvidenceType.MarkedBills, district, "unknown_property");
            PoliceExpansionMod.Instance.HeatSystem
                .AddDistrictHeat(district, 12f, "undercover test buy");

            // Enough evidence → escalate
            if (uc.EvidenceGathered >= 50f)
                EscalateFromUndercover(uc);

            return true;
        }

        /// <summary>Player notices something off about a customer.</summary>
        public bool PlayerInspectCustomer(string district)
        {
            var uc = _officers.Find(o => o.District == district && !o.Blown);
            if (uc == null) return false;

            uc.TimesSpotted++;
            float detectChance = 0.3f + uc.TimesSpotted * 0.15f + uc.VisitCount * 0.1f;

            if (UnityEngine.Random.value < detectChance)
            {
                uc.Blown = true;
                MelonLogger.Msg($"[Undercover] Player identified undercover officer in {district}! Cover blown.");
                PoliceExpansionMod.Instance.HeatSystem.ReduceDistrictHeat(district, 5f, "undercover blown");
                return true;
            }

            // Warn player something is off
            OnSuspiciousCustomerWarning?.Invoke(uc.Id);
            MelonLogger.Msg($"[Undercover] Suspicious behavior detected — pay attention.");
            return false;
        }

        // ── Spawn Logic ──────────────────────────────────────────

        private void TrySpawnUndercover()
        {
            if (_activityLevel <= 0f) return;
            if (UnityEngine.Random.value > _activityLevel * _cfg.UndercoverChance * 5f) return;

            // Pick hottest district
            string hotDistrict = GetHottestDistrict();
            var    role        = RollRole();
            SpawnOfficer(hotDistrict, role);
        }

        private UndercoverOfficer SpawnOfficer(string district, UndercoverRole role)
        {
            var uc = new UndercoverOfficer { Role = role, District = district };
            _officers.Add(uc);
            MelonLogger.Msg($"[Undercover] Officer deployed as {role} in {district}");
            return uc;
        }

        private UndercoverRole RollRole()
        {
            float r = UnityEngine.Random.value;
            if (r < 0.35f) return UndercoverRole.Buyer;
            if (r < 0.55f) return UndercoverRole.LoiteringNPC;
            if (r < 0.70f) return UndercoverRole.RepeatCustomer;
            if (r < 0.85f) return UndercoverRole.StreetWatcher;
            return UndercoverRole.FakeDealer;
        }

        private void EscalateFromUndercover(UndercoverOfficer uc)
        {
            MelonLogger.Msg($"[Undercover] Sufficient evidence gathered — escalating investigation.");
            PoliceExpansionMod.Instance.HeatSystem.AddGlobalHeat(20f, "undercover investigation");
            PoliceExpansionMod.Instance.EvidenceSystem
                .AddEvidence(EvidenceType.Photos, uc.District, "unknown_property");
        }

        private string GetHottestDistrict()
        {
            string hottest  = DistrictSystem.AllDistricts[0];
            float  maxHeat  = 0f;
            foreach (var d in DistrictSystem.AllDistricts)
            {
                float h = PoliceExpansionMod.Instance.HeatSystem.GetDistrictHeat(d);
                if (h > maxHeat) { maxHeat = h; hottest = d; }
            }
            return hottest;
        }

        public int ActiveOfficerCount => _officers.FindAll(o => !o.Blown).Count;
    }
}
