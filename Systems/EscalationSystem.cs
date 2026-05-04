using UnityEngine;
using MelonLoader;
using PoliceExpansionMod.Core;

namespace PoliceExpansionMod.Systems
{
    /// <summary>
    /// Six-tier escalation system. Each tier unlocks new enforcement behaviors.
    /// </summary>
    public class EscalationSystem : MonoBehaviour
    {
        public int CurrentTier { get; private set; } = 0;

        private HeatSystem _heat;

        private void Start()
        {
            _heat = PoliceExpansionMod.Instance.HeatSystem;
            _heat.OnEscalationTierChanged += HandleTierChange;
        }

        private void HandleTierChange(int tier)
        {
            CurrentTier = tier;
            var desc = GetTierDescription(tier);
            PoliceExpansionMod.Instance.Log($"[Escalation] → Tier {tier}: {desc}");
            ApplyTierEffects(tier);
        }

        private void ApplyTierEffects(int tier)
        {
            var patrol  = PoliceExpansionMod.Instance.PatrolSystem;
            var raid    = PoliceExpansionMod.Instance.RaidSystem;
            var uc      = PoliceExpansionMod.Instance.UndercoverSystem;

            switch (tier)
            {
                case 0:
                    patrol?.SetPatrolMode(PatrolMode.Normal);
                    raid?.SetRaidReadiness(0f);
                    uc?.SetUndercoverActivity(0f);
                    break;
                case 1: // Local patrol attention
                    patrol?.SetPatrolMode(PatrolMode.Heightened);
                    break;
                case 2: // Aggressive stops & searches
                    patrol?.SetPatrolMode(PatrolMode.Aggressive);
                    uc?.SetUndercoverActivity(0.2f);
                    break;
                case 3: // Checkpoints & surveillance
                    patrol?.SetPatrolMode(PatrolMode.Aggressive);
                    patrol?.EnableCheckpoints(true);
                    uc?.SetUndercoverActivity(0.4f);
                    break;
                case 4: // Investigators & raids
                    patrol?.SetPatrolMode(PatrolMode.Lockdown);
                    raid?.SetRaidReadiness(0.5f);
                    uc?.SetUndercoverActivity(0.6f);
                    break;
                case 5: // Major task force
                    patrol?.SetPatrolMode(PatrolMode.Lockdown);
                    raid?.SetRaidReadiness(0.8f);
                    uc?.SetUndercoverActivity(0.8f);
                    break;
                case 6: // Full city crackdown
                    patrol?.SetPatrolMode(PatrolMode.Lockdown);
                    raid?.SetRaidReadiness(1.0f);
                    uc?.SetUndercoverActivity(1.0f);
                    break;
            }
        }

        public static string GetTierDescription(int tier) => tier switch
        {
            0 => "No Special Attention",
            1 => "Local Patrol Attention",
            2 => "Aggressive Stops & Searches",
            3 => "Checkpoints & Surveillance",
            4 => "Investigators & Property Raids",
            5 => "Major Task Force Response",
            6 => "Full City Crackdown",
            _ => "Unknown"
        };

        /// <summary>True if police are actively watching at this tier.</summary>
        public bool IsActiveEnforcement() => CurrentTier >= 2;
        public bool IsRaidPossible()       => CurrentTier >= 4;
        public bool IsTaskForce()          => CurrentTier >= 5;
    }

    public enum PatrolMode { Normal, Heightened, Aggressive, Lockdown }
}
