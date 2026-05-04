using MelonLoader;
using HarmonyLib;
using UnityEngine;
using System.Collections;
using PoliceExpansionMod.Systems;
using PoliceExpansionMod.Config;
using PoliceExpansionMod.Patches;

[assembly: MelonInfo(typeof(PoliceExpansionMod.Core.PoliceExpansionMod), "Police Expansion Mod", "1.0.0", "YourName")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace PoliceExpansionMod.Core
{
    public class PoliceExpansionMod : MelonMod
    {
        public static PoliceExpansionMod Instance { get; private set; }

        // Core systems
        public HeatSystem HeatSystem { get; private set; }
        public PatrolSystem PatrolSystem { get; private set; }
        public EvidenceSystem EvidenceSystem { get; private set; }
        public InformantSystem InformantSystem { get; private set; }
        public BriberySystem BriberySystem { get; private set; }
        public RaidSystem RaidSystem { get; private set; }
        public UndercoverSystem UndercoverSystem { get; private set; }
        public EscalationSystem EscalationSystem { get; private set; }
        public DistrictSystem DistrictSystem { get; private set; }
        public ModConfig Config { get; private set; }

        private GameObject _modGameObject;

        public override void OnInitializeMelon()
        {
            Instance = this;
            LoggerInstance.Msg("Police Expansion Mod v1.0.0 initializing...");

            Config = ModConfig.Load();
            LoggerInstance.Msg("Config loaded.");

            // Register all Harmony patches against real game classes
            var harmony = new HarmonyLib.Harmony("com.policeexpansion.mod");
            harmony.PatchAll();
            LoggerInstance.Msg("Harmony patches applied.");
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            LoggerInstance.Msg($"Scene loaded: {sceneName}");

            // Only initialize in gameplay scenes
            if (sceneName == "Main" || sceneName == "GameScene" || sceneName.Contains("Game"))
            {
                MelonCoroutines.Start(InitializeSystems());
            }
        }

        private IEnumerator InitializeSystems()
        {
            yield return new WaitForSeconds(2f); // Wait for game to fully load

            _modGameObject = new GameObject("PoliceExpansionMod");
            UnityEngine.Object.DontDestroyOnLoad(_modGameObject);

            // Initialize systems in dependency order
            DistrictSystem = _modGameObject.AddComponent<DistrictSystem>();
            HeatSystem = _modGameObject.AddComponent<HeatSystem>();
            EscalationSystem = _modGameObject.AddComponent<EscalationSystem>();
            EvidenceSystem = _modGameObject.AddComponent<EvidenceSystem>();
            PatrolSystem = _modGameObject.AddComponent<PatrolSystem>();
            InformantSystem = _modGameObject.AddComponent<InformantSystem>();
            BriberySystem = _modGameObject.AddComponent<BriberySystem>();
            RaidSystem = _modGameObject.AddComponent<RaidSystem>();
            UndercoverSystem = _modGameObject.AddComponent<UndercoverSystem>();

            LoggerInstance.Msg("All Police Expansion systems initialized.");
        }

        public override void OnApplicationQuit()
        {
            SaveState.Save(this);
        }

        public void Log(string message)
        {
            LoggerInstance.Msg(message);
        }
    }
}
