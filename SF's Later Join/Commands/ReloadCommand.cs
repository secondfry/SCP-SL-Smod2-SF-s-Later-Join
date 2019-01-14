using Smod2.Commands;

namespace SF_s_Later_Join.Commands {
  class ReloadCommand : ICommandHandler {
    private LaterJoin plugin;

    public ReloadCommand(LaterJoin plugin) {
      this.plugin = plugin;
    }

    public string GetCommandDescription() {
      return "Reloads SF's Later Join state from config (disable effective immediatelly and enable effective after this round end)";
    }

    public string GetUsage() {
      return "sf_lf_reload";
    }

    public string[] OnCall(ICommandSender sender, string[] args) {
      this.plugin.CheckIfDisabled();

      string state = "";
      if (this.plugin.GetIsDisabled()) {
        this.plugin.GetLJEventHandler().SetIsPluginDisabledThisRound(true);

        state = "disabled immediatelly";
      } else {
        state = "enabled after this round end";
      }

      return new string[] { "SF's Later Join state has been reloaded. It is now: " + state + "." };
    }
  }
}
