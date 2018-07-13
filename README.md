# SCP:SL Smod2 SF's Later Join
SCP:SL Smod2 Plugin which allows players to spawn after round started.  
Forked from storm37000/SCPSL_Smod_LaterJoin at commit f4bd2888faac9f1b3c5fb97b80ed089586efc189.

## Configuration
`sf_lj_time` is the only new config option.  
If player joins the game before `sf_lj_time` amount of seconds after round start, he will spawn into the game.

To spawn a player into game plugin will use vanilla `team_respawn_queue` config option.  
If `team_respawn_queue` is unset, it will use default `40143140314414041340`.

If smart class picker is activated, plugin will perform some magic to "smartly" pick class for player.
