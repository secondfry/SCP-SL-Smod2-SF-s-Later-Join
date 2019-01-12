using Smod2;
using Smod2.API;
using Smod2.EventHandlers;
using Smod2.Events;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Timers;

namespace SF_s_Later_Join {
  public class LJEventHandler : IEventHandlerPlayerJoin,
                                IEventHandlerWaitingForPlayers,
                                IEventHandlerPreRoundStart,
                                IEventHandlerPlayerPickupItem,
                                IEventHandlerRoundStart,
                                IEventHandlerRoundEnd,
                                IEventHandlerSetRole,
                                IEventHandlerLCZDecontaminate,
                                IEventHandlerWarheadDetonate {
    private LaterJoin plugin;
    private bool isSpawnAllowed = false;
    private List<string> playersSpawned = new List<string>();
    private List<int> teamsSpawned = new List<int>();
    private List<Role> scpsToSpawn = new List<Role>();
    private Timer delayedSpawnTimer = new Timer();
    private static readonly Random fakeRandom = new Random();
    private Stopwatch roundWatch = new Stopwatch();
    private bool isLCZDecontaminated = false;
    private bool isWarheadDetonated = false;
    private bool isWaitingForActualRoundToStart = false;
    private bool isExploringEnabled = false;

    public LJEventHandler(LaterJoin plugin) {
      this.plugin = plugin;
      this.PopulateSCPsToSpawn();
    }

    public void OnWaitingForPlayers(WaitingForPlayersEvent ev) {
      // Initially disallow pickups
      // and don't remember spawned players
      // until actual round starts
      this.isWaitingForActualRoundToStart = true;
      // ...and lock SCP-173 door so he can't cheese it on pre-round
      this.LockDoor173();

      // There are two mechanics in play for each joined player
      // OnPlayerJoin event will AttemptSpawnPlayer, so...
      // ...let's allow spawns for the round!
      this.isSpawnAllowed = true;
      // OnSetRole event will happen until round starts with forced Spectrator role, so...
      // ...let's check if exploring before round started is enabled
      this.isExploringEnabled = ConfigManager.Manager.Config.GetBoolValue("sf_lj_explore", false);

      // Reset map state
      this.isLCZDecontaminated = false;
      this.isWarheadDetonated = false;

      // Populate SCPs to spawn in the round
      this.PopulateSCPsToSpawn();

      // Reset round duration watch
      this.roundWatch.Reset();

      // Overkill clean-up (probably already happened in previous round OnRoundEnd)
      this.ResetPlayersSpawned();
      this.ResetTeamsSpawned();

      // Overkill timer stop (probably already happened in previous round OnRoundEnd)
      this.StopDelayedSpawnTimer();
    }

    public void LockDoor173() {
      Door door173 = UnityEngine.GameObject.Find("MeshDoor173").GetComponentInChildren<Door>();
      door173.ForceCooldown((float)86400);
    }

    public void OnPreRoundStart(PreRoundStartEvent ev) {
      this.isWaitingForActualRoundToStart = false;
    }

    public void OnPlayerPickupItem(PlayerPickupItemEvent ev) {
      ev.Allow = this.isWaitingForActualRoundToStart;
    }

    public void OnRoundStart(RoundStartEvent ev) {
      // Start round duration watch
      this.roundWatch.Start();

      // Start delayed spawn timer
      this.StartDelayedSpawnTimer();
    }

    private void PopulateSCPsToSpawn() {
      List<Role> enabledSCPs = this.plugin.GetEnabledSCPs();
      this.scpsToSpawn = new List<Role>(enabledSCPs);
    }

    public void OnSetRole(PlayerSetRoleEvent ev) {
      if (ev.Role == Role.SPECTATOR) {
        // Stop respawning players after 10 seconds into the round
        // ...and don't spawn them before round starts if exploring is disabled
        if (this.roundWatch.ElapsedMilliseconds > 10000) {
          return;
        } else if (!this.isExploringEnabled) {
          return;
        }

        ev.Role = (Role)this.SelectRole();

        if (ev.Role == Role.UNASSIGNED) {
          ev.Role = Role.SPECTATOR;
        }
      }

      if (!this.isWaitingForActualRoundToStart) {
        return;
      }

      // Add each player to list of already spawned players
      this.playersSpawned.Add(ev.Player.SteamId);
      // Add each player team to list of teams in game
      this.teamsSpawned.Add((int)ev.TeamRole.Team);
      // Remove each player role from list of SCPs to spawn
      this.scpsToSpawn.Remove(ev.TeamRole.Role);
      this.scpsToSpawn.Remove(ev.Role);
    }

    private void StartDelayedSpawnTimer() {
      int seconds = ConfigManager.Manager.Config.GetIntValue("sf_lj_time", 120);
      if (seconds < 0) {
        return;
      }

      this.delayedSpawnTimer = new Timer {
        Interval = seconds * 1000,
        AutoReset = false,
        Enabled = true
      };
      this.delayedSpawnTimer.Elapsed += delegate {
        this.isSpawnAllowed = false;
        this.delayedSpawnTimer.Enabled = false;
        this.plugin.Debug("Timer elapsed.");
      };
    }

    public void OnPlayerJoin(PlayerJoinEvent ev) {
      // Don't spawn players before actual round if exploring is disabled
      if (this.isWaitingForActualRoundToStart && !this.isExploringEnabled) {
        return;
      }

      Player player = ev.Player;
      this.AttemptSpawnPlayer(player);
    }

    public void AttemptSpawnPlayer(Player player) {
      if (!this.isSpawnAllowed) {
        player.ChangeRole(Role.SPECTATOR);
        this.plugin.Debug("[StID " + player.SteamId + "] " + player.Name + " – spawn is no longer allowed");
        return;
      }

      if (this.playersSpawned.Contains(player.SteamId)) {
        player.ChangeRole(Role.SPECTATOR);
        this.plugin.Debug("[StID " + player.SteamId + "] " + player.Name + " has already spawned this round");
        return;
      }

      this.SpawnPlayer(player);
    }

    public void SpawnPlayer(Player player) {
      Role role = this.SelectRole();

      if (role == Role.UNASSIGNED) {
        // Unlucky
        player.ChangeRole(Role.SPECTATOR);
        this.plugin.Info("[StID " + player.SteamId + "] " + player.Name + " is unlucky");
        return;
      }

      this.plugin.Info("[StID " + player.SteamId + "] " + player.Name + " => [R " + role + "]");
      player.ChangeRole(role);
    }

    public Role SelectRole() {
      Smod2.API.Team team = this.RollTeam();
      Role role = LJEventHandler.GetClassID(team, this.scpsToSpawn);
      role = this.MutateRoleByAvailability(role);
      int i = 0;
      while (role == Role.UNASSIGNED && i < 5) {
        team = this.RollTeam();
        role = LJEventHandler.GetClassID(team, this.scpsToSpawn);
        role = this.MutateRoleByAvailability(role);
        i++;
      }

      return role;
    }

    private Smod2.API.Team RollTeam() {
      IConfigFile config = ConfigManager.Manager.Config;
      List<int> respawnQueue = this.plugin.GetRespawnQueue();
      int teamID;

      bool isUsingSmartPicker = config.GetBoolValue("smart_class_picker", false);
      if (isUsingSmartPicker) {
        // Smart picker is enabled
        // Using fake smart picker
        teamID = this.RollTeamSmart();
        return (Smod2.API.Team)teamID;
      }

      if (this.teamsSpawned.Count >= respawnQueue.Count) {
        // Overflow
        // Using filler picker
        teamID = this.RollTeamFiller();
        return (Smod2.API.Team)teamID;
      }

      // Picking next team from queue
      teamID = respawnQueue[this.teamsSpawned.Count];
      return (Smod2.API.Team)teamID;
    }

    private int RollTeamFiller() {
      IConfigFile config = ConfigManager.Manager.Config;
      int teamID = config.GetIntValue("filler_team_id", (int)Smod2.API.Team.CLASSD);
      return teamID;
    }

    private int RollTeamSmart() {
      // This is fake smart
      List<int> smartQueue = new List<int>(this.plugin.GetRespawnQueue());
      foreach (int spawnedTeamID in this.teamsSpawned) {
        smartQueue.Remove(spawnedTeamID);
      }

      // Actually really dumb
      smartQueue.Add((int)Smod2.API.Team.SCP);
      smartQueue.Add((int)Smod2.API.Team.CHAOS_INSURGENCY);
      smartQueue.Add((int)Smod2.API.Team.CLASSD);
      smartQueue.Add((int)Smod2.API.Team.CLASSD);
      smartQueue.Add((int)Smod2.API.Team.SCIENTIST);
      smartQueue.Add((int)Smod2.API.Team.NINETAILFOX);

      int teamID = smartQueue[fakeRandom.Next(0, smartQueue.Count)];
      return teamID;
    }

    private static Role GetClassID(Smod2.API.Team team, List<Role> enabledSCPs = null) {
      if (enabledSCPs == null) {
        enabledSCPs = new List<Role> {
          Role.SCP_049,
          Role.SCP_079,
          Role.SCP_096,
          Role.SCP_106,
          Role.SCP_173,
          Role.SCP_939_53,
          Role.SCP_939_89
        };
      }

      switch (team) {
        case Smod2.API.Team.SCP:
          if (enabledSCPs.Count == 0) {
            break;
          }

          Random fakeRandom = new Random();
          return enabledSCPs[fakeRandom.Next(0, enabledSCPs.Count)];
        case Smod2.API.Team.NINETAILFOX:
          return Role.FACILITY_GUARD;
        case Smod2.API.Team.CHAOS_INSURGENCY:
          return Role.CHAOS_INSURGENCY;
        case Smod2.API.Team.SCIENTIST:
          return Role.SCIENTIST;
        case Smod2.API.Team.CLASSD:
          return Role.CLASSD;
        case Smod2.API.Team.SPECTATOR:
          return Role.SPECTATOR;
        case Smod2.API.Team.TUTORIAL:
          return Role.TUTORIAL;
      }

      return Role.UNASSIGNED;
    }

    // SCPs should be handled by OnDecontaminate and OnDetonate events themselves
    private Role MutateRoleByAvailability(Role role) {
      if (this.isWarheadDetonated) {
        switch (role) {
          case Role.FACILITY_GUARD:
            return Role.NTF_CADET;
          case Role.SCIENTIST:
            return Role.NTF_SCIENTIST;
          case Role.CLASSD:
            return Role.CHAOS_INSURGENCY;
          case Role.TUTORIAL:
          case Role.SCP_049:
          case Role.SCP_049_2:
          case Role.SCP_079:
          case Role.SCP_096:
          case Role.SCP_106:
          case Role.SCP_173:
          case Role.SCP_939_53:
          case Role.SCP_939_89:
            break;
          default:
            return role;
        }

        return Role.UNASSIGNED;
      }

      if (this.isLCZDecontaminated) {
        switch (role) {
          case Role.SCIENTIST:
            return Role.NTF_SCIENTIST;
          case Role.CLASSD:
            return Role.CHAOS_INSURGENCY;
          case Role.TUTORIAL:
          case Role.SCP_173:
            break;
          default:
            return role;
        }

        return Role.UNASSIGNED;
      }

      return role;
    }

    public void OnDecontaminate() {
      this.scpsToSpawn.Remove(Role.SCP_173);
      this.isLCZDecontaminated = true;
    }

    public void OnDetonate() {
      this.scpsToSpawn.Clear();
      this.isWarheadDetonated = true;
    }

    public void OnRoundEnd(RoundEndEvent ev) {
      if (this.roundWatch.ElapsedMilliseconds < 10000) {
        return;
      }

      // Disable spawning after round have ended
      this.isSpawnAllowed = false;

      // Stop the timer for disabling spawns
      this.StopDelayedSpawnTimer();

      // Clear spawned tables
      this.ResetPlayersSpawned();
      this.ResetTeamsSpawned();

      // Stop round watch
      this.roundWatch.Stop();
    }

    private void ResetPlayersSpawned() {
      this.playersSpawned.Clear();
    }

    private void ResetTeamsSpawned() {
      this.teamsSpawned.Clear();
    }

    private void StopDelayedSpawnTimer() {
      this.delayedSpawnTimer.Enabled = false;
    }
  }
}
