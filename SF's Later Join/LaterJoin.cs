using Smod2;
using Smod2.API;
using Smod2.Attributes;
using Smod2.EventHandlers;
using System;
using System.Collections.Generic;

namespace SF_s_Later_Join {
    [PluginDetails(
        author = "ShingekiNoRex, storm37000, @Second_Fry",
        name = "SF's Later Join",
        description = "Allow those who join just after round start to spawn",
        id = "sf.later.join",
        version = "1.2.0",
        SmodMajor = 3,
        SmodMinor = 1,
        SmodRevision = 7
        )]
    public class LaterJoin : Plugin {
        private List<int> enabledSCPs = new List<int>();
        private List<int> respawnQueue = new List<int>();

        public override void OnDisable() {
            this.ResetEnabledSCPs();
            this.ResetRespawnQueue();
            this.Info(this.Details.name + " has been disabled.");
        }

        public override void OnEnable() {
            this.PopulateEnabledSCPs();
            this.PopulateRespawnQueue();
            this.Info(this.Details.name + " has been enabled.");
        }

        public override void Register() {
            this.AddEventHandlers(new LJEventHandler(this));
            AddConfig(new Smod2.Config.ConfigSetting("sf_lj_time", 120, Smod2.Config.SettingType.NUMERIC, true, "Amount of time for player to join and still spawn after round start"));
        }

        private void PopulateEnabledSCPs() {
            string[] prefixes = {
                "scp049",
                "scp096",
                "scp106",
                "scp173",
                "scp939_53",
                "scp939_89"
            };
            IConfigFile config = ConfigManager.Manager.Config;

            foreach (string prefix in prefixes) {
                bool isDisabled = config.GetBoolValue(prefix + "_disable", false);
                if (isDisabled) {
                    continue;
                }

                int roleID = LaterJoin.ConvertSCPPrefixToRoleID(prefix);
                if (roleID == -1) {
                    this.Error("Trying to convert unknown prefix: " + prefix);
                    continue;
                }

                int amount = config.GetIntValue(prefix + "_amount", 1);
                while (amount > 0) {
                    this.enabledSCPs.Add(roleID);
                    amount--;
                }
            }
        }

        private void ResetEnabledSCPs() {
            this.enabledSCPs = new List<int>();
        }

        private void PopulateRespawnQueue() {
            IConfigFile config = ConfigManager.Manager.Config;
            string teamIDs = config.GetStringValue("team_respawn_queue", "4014314031441404134040143");
            foreach (char teamID in teamIDs) {
                this.respawnQueue.Add((int) Char.GetNumericValue(teamID));
            }
        }

        private void ResetRespawnQueue() {
            this.respawnQueue = new List<int>();
        }

        private static int ConvertSCPPrefixToRoleID(string prefix) {
            switch (prefix) {
                case "scp049":
                    return (int) Role.SCP_049;
                case "scp096":
                    return (int) Role.SCP_096;
                case "scp106":
                    return (int) Role.SCP_106;
                case "scp173":
                    return (int) Role.SCP_173;
                case "scp939_53":
                    return (int) Role.SCP_939_53;
                case "scp939_89":
                    return (int) Role.SCP_939_89;
            }

            return -1;
        }

        public List<int> getEnabledSCPs() {
            return this.enabledSCPs;
        }

        public List<int> getRespawnQueue() {
            return this.respawnQueue;
        }
    }
}
