using StardewModdingAPI;
using StardewValley;
using ValleyEditor.Server;

namespace ValleyEditor.Domains;

/// <summary>Whether a save is loaded, and versions. Works on the title screen too.</summary>
internal sealed class StatusDomain : Domain
{
    private readonly IManifest manifest;

    public StatusDomain(GameThreadDispatcher game, EditorState state, IManifest manifest)
        : base(game, state)
    {
        this.manifest = manifest;
    }

    public override void Register(Router router)
    {
        router.Get("/api/status", _ => this.Run(() => new
        {
            WorldReady = Context.IsWorldReady,
            SaveName = Context.IsWorldReady ? Constants.SaveFolderName : null,
            FarmName = Context.IsWorldReady ? Game1.player.farmName.Value : null,
            this.State.UnsavedChanges,
            GameVersion = Game1.version,
            ModVersion = this.manifest.Version.ToString(),
        }));
    }
}
