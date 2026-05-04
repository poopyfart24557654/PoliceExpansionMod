using System;
using System.Collections.Generic;
using UnityEngine;
using MelonLoader;
using PoliceExpansionMod.Core;
using PoliceExpansionMod.Config;

namespace PoliceExpansionMod.Systems
{
    public enum OfficerPersonality { Honest, Neutral, Corrupt, Greedy }
    public enum BribeTarget { PatrolOfficer, CheckpointGuard, Investigator, EvidenceHandler, CityOfficial, LegalContact }

    public class BribeAttempt
    {
        public BribeTarget       Target      { get; set; }
        public float             Amount      { get; set; }
        public bool              Succeeded   { get; set; }
        public OfficerPersonality Personality { get; set; }
        public string            District    { get; set; }
        public DateTime          Timestamp   { get; set; } = DateTime.Now;
    }

    public class BriberySystem : MonoBehaviour
    {
        private List<BribeAttempt>         _history         = new List<BribeAttempt>();
        private Dictionary<string,int>     _failedBribes    = new Dictionary<string,int>(); // district→count
        private Dictionary<BribeTarget,float> _suspicionLevel = new Dictionary<BribeTarget,float>();

        public event Action<BribeAttempt> OnBribeSucceeded;
        public event Action<BribeAttempt> OnBribeFailed;

        private ModConfig _cfg;

        private void Start()
        {
            _cfg = PoliceExpansionMod.Instance.Config;
            MelonLogger.Msg("[BriberySystem] Online.");
        }

        // ── Public API ───────────────────────────────────────────

        /// <summary>Attempt a bribe. Returns true if successful.</summary>
        public bool AttemptBribe(BribeTarget target, float amount, string district)
        {
            var personality = RollOfficerPersonality(district);
            float successChance = CalculateSuccessChance(target, amount, district, personality);

            bool success = UnityEngine.Random.value < successChance;

            var attempt = new BribeAttempt {
                Target = target, Amount = amount, Succeeded = success,
                Personality = personality, District = district
            };
            _history.Add(attempt);

            if (success)
                HandleBribeSuccess(attempt);
            else
                HandleBribeFailure(attempt);

            return success;
        }

        // ── Success / Failure ────────────────────────────────────

        private void HandleBribeSuccess(BribeAttempt attempt)
        {
            MelonLogger.Msg($"[Bribery] Bribe SUCCESS — ${attempt.Amount} to {attempt.Target} in {attempt.District}");
            PoliceExpansionMod.Instance.HeatSystem.OnBribeSuccess();

            switch (attempt.Target)
            {
                case BribeTarget.PatrolOfficer:
                    PoliceExpansionMod.Instance.HeatSystem.ReduceDistrictHeat(attempt.District, 10f, "officer bribed");
                    break;
                case BribeTarget.CheckpointGuard:
                    MelonLogger.Msg("[Bribery] Checkpoint cleared.");
                    break;
                case BribeTarget.Investigator:
                    PoliceExpansionMod.Instance.HeatSystem.ReduceGlobalHeat(15f, "investigator bribed");
                    break;
                case BribeTarget.EvidenceHandler:
                    // Destroy one piece of evidence
                    MelonLogger.Msg("[Bribery] Evidence handler lost a file...");
                    break;
                case BribeTarget.CityOfficial:
                    PoliceExpansionMod.Instance.HeatSystem.ReduceGlobalHeat(20f, "official bribed");
                    break;
            }
            OnBribeSucceeded?.Invoke(attempt);
        }

        private void HandleBribeFailure(BribeAttempt attempt)
        {
            MelonLogger.Msg($"[Bribery] Bribe FAILED — {attempt.Target} in {attempt.District} (personality: {attempt.Personality})");

            // Track failures in this district
            if (!_failedBribes.ContainsKey(attempt.District)) _failedBribes[attempt.District] = 0;
            _failedBribes[attempt.District]++;

            float heatPenalty = 10f + _failedBribes[attempt.District] * 5f;

            switch (attempt.Personality)
            {
                case OfficerPersonality.Honest:
                    // Honest cop reports it — big heat spike
                    heatPenalty = 25f;
                    PoliceExpansionMod.Instance.HeatSystem
                        .AddDistrictHeat(attempt.District, heatPenalty, "bribe reported by honest officer");
                    // Could trigger sting
                    if (UnityEngine.Random.value < 0.4f)
                        PoliceExpansionMod.Instance.UndercoverSystem?.TriggerSting(attempt.District);
                    break;

                case OfficerPersonality.Neutral:
                    PoliceExpansionMod.Instance.HeatSystem
                        .AddDistrictHeat(attempt.District, heatPenalty, "bribe failed");
                    break;

                case OfficerPersonality.Greedy:
                    // Wants more money — just adds heat
                    PoliceExpansionMod.Instance.HeatSystem
                        .AddDistrictHeat(attempt.District, heatPenalty * 0.5f, "greedy officer wants more");
                    MelonLogger.Msg("[Bribery] Officer wants more money. Try a larger bribe.");
                    break;
            }
            OnBribeFailed?.Invoke(attempt);
        }

        // ── Success Chance Calculation ───────────────────────────

        private float CalculateSuccessChance(BribeTarget target, float amount, string district, OfficerPersonality personality)
        {
            float base_chance = personality switch {
                OfficerPersonality.Corrupt  => 0.75f,
                OfficerPersonality.Greedy   => 0.50f,
                OfficerPersonality.Neutral  => 0.30f,
                OfficerPersonality.Honest   => 0.02f,
                _                           => 0.25f
            };

            // District corruption bonus
            var districtData = DistrictSystem.Instance?.GetDistrict(district);
            if (districtData != null)
                base_chance += districtData.CorruptionLevel * 0.3f;

            // Amount scaling (diminishing returns above ~$5000)
            float amountMult = Mathf.Log10(Mathf.Max(1, amount) / 100f + 1f);
            base_chance += amountMult * 0.1f;

            // Target difficulty
            float targetMult = target switch {
                BribeTarget.PatrolOfficer    => 1.2f,
                BribeTarget.CheckpointGuard  => 1.0f,
                BribeTarget.Investigator     => 0.6f,
                BribeTarget.EvidenceHandler  => 0.7f,
                BribeTarget.CityOfficial     => 0.4f,
                BribeTarget.LegalContact     => 0.9f,
                _                            => 1.0f
            };

            // Repeat failure penalty
            int failures = _failedBribes.TryGetValue(district, out var f) ? f : 0;
            float failPenalty = failures * 0.1f;

            float final = (base_chance * targetMult - failPenalty) * _cfg.BriberySuccessMultiplier;
            return Mathf.Clamp01(final);
        }

        private OfficerPersonality RollOfficerPersonality(string district)
        {
            var d = DistrictSystem.Instance?.GetDistrict(district);
            float corruption = d?.CorruptionLevel ?? 0.3f;
            float roll = UnityEngine.Random.value;

            if (roll < corruption * 0.5f)        return OfficerPersonality.Corrupt;
            if (roll < corruption * 0.5f + 0.2f) return OfficerPersonality.Greedy;
            if (roll < 0.7f)                     return OfficerPersonality.Neutral;
            return OfficerPersonality.Honest;
        }

        public List<BribeAttempt> GetHistory() => _history;
    }
}
