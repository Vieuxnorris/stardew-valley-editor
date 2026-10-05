using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using ValleyEditor.Domains;
using ValleyEditor.Server;
using ValleyEditor.Sprites;

namespace ValleyEditor;

internal sealed class ModConfig
{
    /// <summary>The localhost port the editor listens on.</summary>
    public int Port { get; set; } = 47800;

    /// <summary>Whether to open the editor in the browser when a save is loaded.</summary>
    public bool OpenBrowserOnSaveLoaded { get; set; } = false;
}

internal sealed class ModEntry : Mod
{
    private readonly GameThreadDispatcher dispatcher = new();
    private readonly EditorState state = new();
    private ModConfig config = new();
    private WebServer? server;

    public override void Entry(IModHelper helper)
    {
        this.config = helper.ReadConfig<ModConfig>();

        var router = new Router();
        var sprites = new ItemSprites(this.dispatcher);
        var items = new ItemsDomain(this.dispatcher, this.state, helper.ModRegistry, sprites);
        Domain[] domains =
        {
            new StatusDomain(this.dispatcher, this.state, this.ModManifest),
            new PlayerDomain(this.dispatcher, this.state),
            new InventoryDomain(this.dispatcher, this.state),
            items,
            new WorldDomain(this.dispatcher, this.state),
            new ProgressionDomain(this.dispatcher, this.state),
            new QuestsDomain(this.dispatcher, this.state),
            new NpcsDomain(this.dispatcher, this.state, sprites),
        };
        foreach (Domain domain in domains)
            domain.Register(router);

        this.server = new WebServer(this.config.Port, Path.Combine(helper.DirectoryPath, "wwwroot"), router, this.Monitor);
        try
        {
            this.server.Start();
            this.Monitor.Log($"Editor running at {this.server.Url} (type 'editor' in this console to open it).", LogLevel.Info);
        }
        catch (HttpListenerException ex)
        {
            this.Monitor.Log($"Couldn't start the editor on port {this.config.Port}: {ex.Message}. Change 'Port' in config.json.", LogLevel.Error);
            this.server = null;
        }

        helper.Events.GameLoop.UpdateTicked += (_, _) => this.dispatcher.Drain();
        helper.Events.GameLoop.Saved += (_, _) => this.state.UnsavedChanges = false;
        helper.Events.GameLoop.ReturnedToTitle += (_, _) => this.state.UnsavedChanges = false;
        helper.Events.GameLoop.SaveLoaded += (_, _) =>
        {
            this.state.UnsavedChanges = false;
            items.Invalidate();
            if (this.config.OpenBrowserOnSaveLoaded)
                this.OpenBrowser();
        };

        helper.ConsoleCommands.Add("editor", "Opens Valley Editor in your browser.", (_, _) => this.OpenBrowser());
    }

    private void OpenBrowser()
    {
        if (this.server is null)
        {
            this.Monitor.Log("The editor server isn't running; see the error above.", LogLevel.Error);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(this.server.Url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            this.Monitor.Log($"Couldn't open the browser ({ex.Message}). Open this URL yourself: {this.server.Url}", LogLevel.Warn);
        }
    }
}
