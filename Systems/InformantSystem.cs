using System;
using System.Collections.Generic;
using UnityEngine;
using MelonLoader;
using PoliceExpansionMod.Core;
using PoliceExpansionMod.Config;

namespace PoliceExpansionMod.Systems
{
    public enum NPCRole { Customer, Worker, Dealer, Rival, Lieutenant }

    public class TrackedNPC
    {
        public string   Id              { get; set; } = Guid.NewGuid().ToString("N").Substring(0, 8);
        public string   Name            { get; set; }
        public NPCRole  Role            { get; set; }
        public string   PropertyId      { get; set; }
        public float    Loyalty         { get; set; } = 100f;   // 0–100
        public float    BetrayalChance  { get; set; }           // computed
        public bool     IsInformant     { get; set; }
        public bool     IsArrested      { get; set; }
        public int      MissedPayments  { get; set; }
        public float    FearLevel       { get; set; }           // 0–100
        public float    DebtLevel       { get; set; }           // $ owed to player
    }

    public class InformantSystem : MonoBehaviour
    {
        private List<TrackedNPC> _npcs = new List<TrackedNPC>();
        private float _checkInterval = 120f;  // check every 2 min game time
        private float _checkTimer;

        private ModConfig _cfg;

        public event Action<TrackedNPC>  OnNPCBecameInformant;
        public event Action<string>      OnBetrayalClueSpotted;  // NPC id

        private void Start()
        {
            _cfg = PoliceExpansionMod.Instance.Config;
            MelonLogger.Msg("[InformantSystem] Online.");
        }

        private void Update()
        {
            _checkTimer += Time.deltaTime;
            if (_checkTimer >= _checkInterval)
            {
                _checkTimer = 0f;
                EvaluateAllNPCs();
            }
        }

        // ── NPC Registration ─────────────────────────────────────

        public TrackedNPC RegisterNPC(string name, NPCRole role, string propertyId)
        {
            var npc = new TrackedNPC { Name = name, Role = role, PropertyId = propertyId };
            _npcs.Add(npc);
            return npc;
        }

        // ── Loyalty Modifiers ────────────────────────────────────

        public void PayLoyaltyBonus(string npcId, float amount)
        {
            var npc = Find(npcId);
            if (npc == null) return;
            npc.Loyalty = Mathf.Clamp(npc.Loyalty + amount * 0.5f, 0, 100);
            npc.DebtLevel = Mathf.Max(0, npc.DebtLevel - amount);
            MelonLogger.Msg($"[Informant] {npc.Name} loyalty → {npc.Loyalty:F0}");
        }

        public void OnNPCArrested(string npcId)
        {
            var npc = Find(npcId);
            if (npc == null) return;
            npc.IsArrested = true;
            npc.FearLevel += 40f;
            npc.Loyalty   -= 30f;
            MelonLogger.Msg($"[Informant] {npc.Name} arrested — loyalty ↓, fear ↑");
            EvaluateNPC(npc); // Immediately check if they flip
        }

        public void OnNPCMissedPayment(string npcId)
        {
            var npc = Find(npcId);
            if (npc == null) return;
            npc.MissedPayments++;
            npc.Loyalty   -= 15f * npc.MissedPayments;
            npc.DebtLevel += 500f;
            MelonLogger.Msg($"[Informant] {npc.Name} missed payment #{npc.MissedPayments}");
        }

        public void RotateStaff(string propertyId)
        {
            int removed = _npcs.RemoveAll(n => n.PropertyId == propertyId && n.Loyalty < 40f);
            MelonLogger.Msg($"[Informant] Rotated {removed} risky staff from {propertyId}");
        }

        public void CutTies(string npcId)
        {
            var npc = Find(npcId);
            if (npc == null) return;
            _npcs.Remove(npc);
            MelonLogger.Msg($"[Informant] Cut ties with {npc.Name}");
        }

        public void PromoteToLieutenant(string npcId)
        {
            var npc = Find(npcId);
            if (npc == null) return;
            npc.Role    = NPCRole.Lieutenant;
            npc.Loyalty = Mathf.Min(100, npc.Loyalty + 20f);
            MelonLogger.Msg($"[Informant] {npc.Name} promoted to Lieutenant");
        }

        // ── Evaluation ───────────────────────────────────────────

        private void EvaluateAllNPCs()
        {
            foreach (var npc in _npcs) EvaluateNPC(npc);
        }

        private void EvaluateNPC(TrackedNPC npc)
        {
            if (npc.IsInformant) return;

            // Compute betrayal chance
            float chance  = 0f;
            chance += (100f - npc.Loyalty)    * 0.003f;
            chance += npc.FearLevel            * 0.002f;
            chance += npc.MissedPayments       * 0.05f;
            chance += npc.IsArrested ? 0.25f : 0f;
            chance += npc.DebtLevel > 2000f ? 0.1f : 0f;
            chance *= _cfg.InformantChance * 10f;

            npc.BetrayalChance = Mathf.Clamp01(chance);

            // Drop a clue if suspiciously risky
            if (npc.BetrayalChance > 0.3f && UnityEngine.Random.value < 0.4f)
                OnBetrayalClueSpotted?.Invoke(npc.Id);

            // Roll betrayal
            if (UnityEngine.Random.value < npc.BetrayalChance)
                FlipNPC(npc);
        }

        private void FlipNPC(TrackedNPC npc)
        {
            npc.IsInformant = true;
            MelonLogger.Msg($"[Informant] *** {npc.Name} ({npc.Role}) became an informant! ***");
            OnNPCBecameInformant?.Invoke(npc);

            // Tell evidence system
            PoliceExpansionMod.Instance.EvidenceSystem
                .AddEmployeeTestimony(npc.PropertyId);
        }

        private TrackedNPC Find(string id) => _npcs.Find(n => n.Id == id);

        public List<TrackedNPC> GetAllNPCs() => _npcs;
        public float GetLowestLoyalty() =>
            _npcs.Count == 0 ? 100f : _npcs[0].Loyalty; // simplified
    }
}
