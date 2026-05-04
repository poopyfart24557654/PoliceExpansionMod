using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MelonLoader;
using PoliceExpansionMod.Core;
using PoliceExpansionMod.Config;

namespace PoliceExpansionMod.Systems
{
    public class PatrolOfficer
    {
        public string   Id            { get; set; }
        public string   District      { get; set; }
        public Vector3  Position      { get; set; }
        public bool     IsUndercover  { get; set; }
        public bool     IsInPursuit   { get; set; }
        public float    Suspicion     { get; set; }  // 0–100
        public PatrolMode Mode        { get; set; }
    }

    public class PatrolSystem : MonoBehaviour
    {
        private PatrolMode _currentMode = PatrolMode.Normal;
        private bool _checkpointsEnabled;
        private List<PatrolOfficer> _activeOfficers = new List<PatrolOfficer>();
        private float _patrolUpdateInterval = 15f;
        private float _patrolTimer;

        private HeatSystem     _heat;
        private DistrictSystem _districts;
        private ModConfig      _cfg;

        // Curfew: 11pm – 5am game time
        private const float CurfewStart = 23f;
        private const float CurfewEnd   =  5f;

        private void Start()
        {
            _heat      = PoliceExpansionMod.Instance.HeatSystem;
            _districts = DistrictSystem.Instance;
            _cfg       = PoliceExpansionMod.Instance.Config;
            MelonLogger.Msg("[PatrolSystem] Online.");
        }

        private void Update()
        {
            _patrolTimer += Time.deltaTime;
            if (_patrolTimer >= _patrolUpdateInterval)
            {
                _patrolTimer = 0f;
                UpdatePatrolBehavior();
            }
        }

        // ── Public API ───────────────────────────────────────────

        public void SetPatrolMode(PatrolMode mode)
        {
            _currentMode = mode;
            foreach (var o in _activeOfficers) o.Mode = mode;
            _patrolUpdateInterval = mode switch {
                PatrolMode.Normal     => 20f,
                PatrolMode.Heightened => 12f,
                PatrolMode.Aggressive =>  8f,
                PatrolMode.Lockdown   =>  4f,
                _                     => 15f
            };
            MelonLogger.Msg($"[Patrol] Mode → {mode}");
        }

        public void EnableCheckpoints(bool enabled)
        {
            _checkpointsEnabled = enabled;
            MelonLogger.Msg($"[Patrol] Checkpoints {(enabled ? "ENABLED" : "DISABLED")}");
        }

        // ── Patrol Logic ─────────────────────────────────────────

        private void UpdatePatrolBehavior()
        {
            bool isCurfew = IsCurfewActive();

            foreach (var district in DistrictSystem.AllDistricts)
            {
                float districtHeat = _heat.GetDistrictHeat(district);
                float density      = _districts.GetEffectivePatrolDensity(
                                         district, _heat.GlobalHeat, _cfg.PatrolDensity);

                int desiredOfficers = Mathf.RoundToInt(density * 2f * _cfg.PatrolDensity);

                // Curfew doubles patrols
                if (isCurfew && _cfg.EnableCurfewPatrols)
                    desiredOfficers = Mathf.RoundToInt(desiredOfficers * 1.8f);

                // High district heat: route officers there
                if (districtHeat > 50f)
                    desiredOfficers = Mathf.RoundToInt(desiredOfficers * 1.5f);

                AdjustOfficersInDistrict(district, desiredOfficers);
            }

            // Dispatch backup to hottest district
            if (_currentMode >= PatrolMode.Aggressive)
                DispatchBackupToHotspot();

            if (_checkpointsEnabled)
                ManageCheckpoints();
        }

        private void AdjustOfficersInDistrict(string district, int target)
        {
            int current = CountOfficersInDistrict(district);
            int delta   = target - current;

            if (delta > 0)
                for (int i = 0; i < delta; i++) SpawnOfficer(district);
            else if (delta < 0)
                for (int i = 0; i < -delta; i++) DespawnOfficer(district);
        }

        private void SpawnOfficer(string district)
        {
            var officer = new PatrolOfficer {
                Id       = System.Guid.NewGuid().ToString("N").Substring(0, 8),
                District = district,
                Mode     = _currentMode,
                Suspicion = 0f
            };
            _activeOfficers.Add(officer);
            // TODO: Hook into game's NPC spawning API when available
            MelonLogger.Msg($"[Patrol] Officer spawned in {district} (mode: {_currentMode})");
        }

        private void DespawnOfficer(string district)
        {
            var idx = _activeOfficers.FindIndex(o => o.District == district && !o.IsInPursuit);
            if (idx >= 0) _activeOfficers.RemoveAt(idx);
        }

        private int CountOfficersInDistrict(string district) =>
            _activeOfficers.FindAll(o => o.District == district).Count;

        private void DispatchBackupToHotspot()
        {
            string hottest    = "";
            float  maxHeat    = 0f;
            foreach (var d in DistrictSystem.AllDistricts)
            {
                float h = _heat.GetDistrictHeat(d);
                if (h > maxHeat) { maxHeat = h; hottest = d; }
            }
            if (!string.IsNullOrEmpty(hottest) && maxHeat > 40f)
                SpawnOfficer(hottest);
        }

        private void ManageCheckpoints()
        {
            // Checkpoints appear in high-heat districts
            foreach (var d in DistrictSystem.AllDistricts)
            {
                float dHeat = _heat.GetDistrictHeat(d);
                var   dist  = _districts.GetDistrict(d);
                if (dist.HasCheckpoints && dHeat > 45f)
                {
                    MelonLogger.Msg($"[Patrol] Checkpoint active in {d} (heat: {dHeat:F0})");
                    // TODO: Hook into game checkpoint placement
                }
            }
        }

        // ── Pursuit ──────────────────────────────────────────────

        public void TriggerPursuit(string district)
        {
            _heat.OnFleeingPolice();
            var officer = _activeOfficers.Find(o => o.District == district);
            if (officer != null)
            {
                officer.IsInPursuit = true;
                MelonLogger.Msg($"[Patrol] Pursuit started in {district}!");
            }
        }

        public void EndPursuit(string officerId, bool playerEscaped)
        {
            var o = _activeOfficers.Find(x => x.Id == officerId);
            if (o != null) o.IsInPursuit = false;

            if (playerEscaped)
            {
                _heat.AddGlobalHeat(8f, "escaped pursuit");
                MelonLogger.Msg("[Patrol] Player escaped pursuit — heat +8");
            }
        }

        // ── Curfew ───────────────────────────────────────────────

        private bool IsCurfewActive()
        {
            // Hook into game's time system when available
            float hour = System.DateTime.Now.Hour; // placeholder
            return hour >= CurfewStart || hour < CurfewEnd;
        }

        public int ActiveOfficerCount => _activeOfficers.Count;

        /// <summary>Called every in-game minute via TimeManager patch.</summary>
        public void OnMinuteTick()
        {
            UpdatePatrolBehavior();
        }
    }
}
