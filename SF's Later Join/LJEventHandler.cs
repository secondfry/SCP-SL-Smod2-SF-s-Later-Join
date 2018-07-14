using Smod2;
using Smod2.API;
using Smod2.EventHandlers;
using Smod2.Events;
using System;
using System.Collections.Generic;
using System.Timers;

namespace SF_s_Later_Join {
    class LJEventHandler : IEventHandlerPlayerJoin, IEventHandlerRoundStart, IEventHandlerRoundEnd {
        private LaterJoin plugin;
        private bool isSpawnAllowed = false;
        private List<string> playersSpawned = new List<string>();
        private List<int> teamsSpawned = new List<int>();
        private List<int> scpsToSpawn = new List<int>();
        private Timer spawnTimer = new Timer();
        private static readonly Random fakeRandom = new Random();

        public LJEventHandler(LaterJoin plugin) {
            this.plugin = plugin;
        }

        public void OnRoundStart(RoundStartEvent ev) {
            // OnRoundEnd bugged
            this.ResetPlayersSpawned();
            this.ResetTeamsSpawned();
            this.DisableTimer();

            this.isSpawnAllowed = true;
            this.PopulateSCPsToSpawn();
            this.IterateCurrentPlayers(ev);
            this.CheckStartTimer();
        }

        private void PopulateSCPsToSpawn() {
            List<int> enabledSCPs = this.plugin.getEnabledSCPs();
            this.scpsToSpawn = new List<int>(enabledSCPs);
        }

        private void IterateCurrentPlayers(RoundStartEvent ev) {
            foreach (Player player in ev.Server.GetPlayers()) {
                // Check if player actually spawned
                if ((int) player.TeamRole.Role == -1) {
                    continue;
                }

                // Add each player to list of already spawned players
                this.playersSpawned.Add(player.SteamId);
                // Add each player team to list of teams in game
                this.teamsSpawned.Add((int) player.TeamRole.Team);
                // Remove each player role from list of SCPs to spawn
                this.scpsToSpawn.Remove((int) player.TeamRole.Role);
            }
        }

        private void CheckStartTimer() {
            int seconds = ConfigManager.Manager.Config.GetIntValue("sf_lj_time", 120);
            if (seconds < 0) {
                return;
            }

            this.InitTimer(seconds);
        }

        private void InitTimer(int seconds) {
            this.spawnTimer = new Timer {
                Interval = seconds * 1000,
                AutoReset = false,
                Enabled = true
            };
            this.spawnTimer.Elapsed += delegate {
                this.isSpawnAllowed = false;
                this.spawnTimer.Enabled = false;
                this.plugin.Debug("Timer elapsed.");
            };
        }

        public void OnPlayerJoin(PlayerJoinEvent ev) {
            Player player = ev.Player;

            if (!this.isSpawnAllowed) {
                this.plugin.Debug("[StID " + player.SteamId + "] " + player.Name + " – spawn is no longer allowed");
                return;
            }

            if (this.playersSpawned.Contains(player.SteamId)) {
                this.plugin.Debug("[StID " + player.SteamId + "] " + player.Name + " has already spawned this round");
                return;
            }

            int teamID = this.RollTeam();
            int roleID = LJEventHandler.GetClassID(teamID, this.scpsToSpawn);
            int i = 0;
            while (roleID == (int) Role.UNASSIGNED && i < 5) {
                teamID = this.RollTeam();
                roleID = LJEventHandler.GetClassID(teamID, this.scpsToSpawn);
                i++;
            }

            if (roleID == (int) Role.UNASSIGNED) {
                // Unlucky
                this.plugin.Info("[StID " + player.SteamId + "] " + player.Name + " is unlucky");
                return;
            }

            this.plugin.Info("[StID " + player.SteamId + "] " + player.Name + " => [R " + roleID + "]");
            player.ChangeRole((Role) roleID, true, true);

            this.playersSpawned.Add(player.SteamId);
            this.teamsSpawned.Add((int) player.TeamRole.Team);
            this.scpsToSpawn.Remove((int) player.TeamRole.Role);
        }

        private int RollTeam() {
            IConfigFile config = ConfigManager.Manager.Config;
            List<int> respawnQueue = this.plugin.getRespawnQueue();
            int teamID;

            bool isUsingSmartPicker = config.GetBoolValue("smart_class_picker", false);
            if (isUsingSmartPicker) {
                // Smart picker is enabled
                // Using fake smart picker
                teamID = this.RollTeamSmart();
                return teamID;
            }

            if (this.teamsSpawned.Count >= respawnQueue.Count) {
                // Overflow
                // Using filler picker
                teamID = this.RollTeamFiller();
                return teamID;
            }

            // Picking next team from queue
            teamID = respawnQueue[this.teamsSpawned.Count];
            return teamID;
        }

        private int RollTeamFiller() {
            IConfigFile config = ConfigManager.Manager.Config;
            int teamID = config.GetIntValue("filler_team_id", (int) Team.CLASSD);
            return teamID;
        }

        private int RollTeamSmart() {
            // This is fake smart
            List<int> smartQueue = new List<int>(this.plugin.getRespawnQueue());
            foreach (int spawnedTeamID in this.teamsSpawned) {
                smartQueue.Remove(spawnedTeamID);
            }

            // Actually really dumb
            smartQueue.Add((int) Team.SCP);
            smartQueue.Add((int) Team.CHAOS_INSURGENCY);
            smartQueue.Add((int) Team.CLASSD);
            smartQueue.Add((int) Team.CLASSD);
            smartQueue.Add((int) Team.SCIENTISTS);
            smartQueue.Add((int) Team.NINETAILFOX);
            
            int teamID = smartQueue[fakeRandom.Next(0, smartQueue.Count - 1)];
            return teamID;
        }

        private static int GetClassID(int teamID, List<int> enabledSCPs = null) {
            if (enabledSCPs == null) {
                enabledSCPs = new List<int> {
                    (int) Role.SCP_049,
                    (int) Role.SCP_096,
                    (int) Role.SCP_106,
                    (int) Role.SCP_173,
                    (int) Role.SCP_939_53,
                    (int) Role.SCP_939_89
                };
            }

            switch (teamID) {
                case (int) Team.SCP:
                    if (enabledSCPs.Count == 0) {
                        break;
                    }

                    Random fakeRandom = new Random();
                    return enabledSCPs[fakeRandom.Next(0, enabledSCPs.Count - 1)];
                case (int) Team.NINETAILFOX:
                    return (int) Role.FACILITY_GUARD;
                case (int) Team.CHAOS_INSURGENCY:
                    return (int) Role.CHAOS_INSUGENCY;
                case (int) Team.SCIENTISTS:
                    return (int) Role.SCIENTIST;
                case (int) Team.CLASSD:
                    return (int) Role.CLASSD;
                case (int) Team.SPECTATOR:
                    return (int) Role.SPECTATOR;
                case (int) Team.TUTORIAL:
                    return (int) Role.TUTORIAL;
            }

            return (int) Role.UNASSIGNED;
        }

        public void OnRoundEnd(RoundEndEvent ev) {
            // OnRoundEnd bugged
            if (ev.Round.Duration < 10) {
                return;
            }

            this.isSpawnAllowed = false;
            this.ResetPlayersSpawned();
            this.ResetTeamsSpawned();
            this.DisableTimer();
        }

        private void ResetPlayersSpawned() {
            this.playersSpawned = new List<string>();
        }

        private void ResetTeamsSpawned() {
            this.teamsSpawned = new List<int>();
        }

        private void DisableTimer() {
            this.spawnTimer.Enabled = false;
        }
    }
}
