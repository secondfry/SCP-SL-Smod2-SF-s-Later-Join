using Smod2.Commands;

namespace SF_s_Later_Join.Commands {
  class DisableCommand : ICommandHandler {
    private LaterJoin plugin;

    public DisableCommand(LaterJoin plugin) {
      this.plugin = plugin;
    }

    public string GetCommandDescription() {
      return "Disables SF's Later Join immediatelly";
    }

    public string GetUsage() {
      return "sf_lj_disable";
    }

    public string[] OnCall(ICommandSender sender, string[] args) {
      this.plugin.SetIsDisabled(true);
      this.plugin.GetLJEventHandler().SetIsPluginDisabledThisRound(true);

      return new string[] { "SF's Later Join is disabled." };
    }
  }
}
