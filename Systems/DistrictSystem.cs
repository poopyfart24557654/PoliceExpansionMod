using System.Collections.Generic;
using UnityEngine;
using MelonLoader;

namespace PoliceExpansionMod.Systems
{
    public enum DistrictType
    {
        RichArea,
        Slums,
        IndustrialZone,
        Docks,
        Suburbs,
        Downtown,
        Unknown
    }

    public class District
    {
        public string       Name              { get; set; }
        public DistrictType Type              { get; set; }
        public float        BasePatrolDensity { get; set; }  // 0–2
        public float        CivilianReportRate { get; set; } // 0–1: how fast civs call police
        public float        CorruptionLevel   { get; set; }  // 0–1: easier bribery if high
        public float        GangPresence      { get; set; }  // 0–1
        public bool         HasCheckpoints    { get; set; }
        public bool         IsSmuggleFriendly { get; set; }
    }

    public class DistrictSystem : MonoBehaviour
    {
        public static readonly string[] AllDistricts = {
            "Downtown", "Slums", "Industrial", "Docks", "Suburbs", "RichArea"
        };

        private Dictionary<string, District> _districts;

        public static DistrictSystem Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            BuildDistricts();
        }

        private void BuildDistricts()
        {
            _districts = new Dictionary<string, District>
            {
                ["Downtown"] = new District {
                    Name = "Downtown", Type = DistrictType.Downtown,
                    BasePatrolDensity = 1.4f, CivilianReportRate = 0.7f,
                    CorruptionLevel = 0.3f,   GangPresence = 0.2f,
                    HasCheckpoints = true,    IsSmuggleFriendly = false
                },
                ["Slums"] = new District {
                    Name = "Slums", Type = DistrictType.Slums,
                    BasePatrolDensity = 0.6f, CivilianReportRate = 0.2f,
                    CorruptionLevel = 0.7f,   GangPresence = 0.8f,
                    HasCheckpoints = false,   IsSmuggleFriendly = false
                },
                ["Industrial"] = new District {
                    Name = "Industrial", Type = DistrictType.IndustrialZone,
                    BasePatrolDensity = 0.9f, CivilianReportRate = 0.4f,
                    CorruptionLevel = 0.5f,   GangPresence = 0.3f,
                    HasCheckpoints = false,   IsSmuggleFriendly = true
                },
                ["Docks"] = new District {
                    Name = "Docks", Type = DistrictType.Docks,
                    BasePatrolDensity = 1.1f, CivilianReportRate = 0.3f,
                    CorruptionLevel = 0.6f,   GangPresence = 0.4f,
                    HasCheckpoints = true,    IsSmuggleFriendly = true
                },
                ["Suburbs"] = new District {
                    Name = "Suburbs", Type = DistrictType.Suburbs,
                    BasePatrolDensity = 0.8f, CivilianReportRate = 0.9f,
                    CorruptionLevel = 0.1f,   GangPresence = 0.0f,
                    HasCheckpoints = false,   IsSmuggleFriendly = false
                },
                ["RichArea"] = new District {
                    Name = "RichArea", Type = DistrictType.RichArea,
                    BasePatrolDensity = 1.8f, CivilianReportRate = 0.95f,
                    CorruptionLevel = 0.05f,  GangPresence = 0.0f,
                    HasCheckpoints = true,    IsSmuggleFriendly = false
                }
            };

            MelonLogger.Msg("[Districts] All districts initialized.");
        }

        public District GetDistrict(string name) =>
            _districts.TryGetValue(name, out var d) ? d : new District {
                Name = name, Type = DistrictType.Unknown,
                BasePatrolDensity = 1f, CivilianReportRate = 0.5f,
                CorruptionLevel = 0.3f, GangPresence = 0.2f
            };

        public float GetEffectivePatrolDensity(string districtName, float globalHeat, float cfg)
        {
            var d = GetDistrict(districtName);
            float heatMult = 1f + (globalHeat / 100f) * 1.5f;
            return d.BasePatrolDensity * heatMult * cfg;
        }

        public bool IsSafeForOperation(string districtName)
        {
            var d = GetDistrict(districtName);
            float heat = PoliceExpansionMod.Core.PoliceExpansionMod.Instance
                             .HeatSystem.GetDistrictHeat(districtName);
            return heat < 60f && d.BasePatrolDensity < 1.5f;
        }
    }
}
