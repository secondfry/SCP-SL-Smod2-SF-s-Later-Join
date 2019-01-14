using Smod2.Commands;

namespace SF_s_Later_Join.Commands {
  class EnableCommand : ICommandHandler {
    private LaterJoin plugin;

    public EnableCommand(LaterJoin plugin) {
      this.plugin = plugin;
    }

    public string GetCommandDescription() {
      return "Enables SF's Later Join after this round end";
    }

    public string GetUsage() {
      return "sf_lf_enable";
    }

    public string[] OnCall(ICommandSender sender, string[] args) {
      this.plugin.SetIsDisabled(false);

      return new string[] { "SF's Later Join will be enabled from next round." };
    }
  }
}
