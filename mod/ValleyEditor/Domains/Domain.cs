using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using StardewModdingAPI;
using ValleyEditor.Server;

namespace ValleyEditor.Domains;

/// <summary>State shared by all domains.</summary>
internal sealed class EditorState
{
    /// <summary>Whether the editor changed the game since it last saved (changes only persist when the game saves overnight).</summary>
    public bool UnsavedChanges { get; set; }

    /// <summary>Game ticks counted by the mod (incremented on UpdateTicked).</summary>
    public int Tick { get; set; }

    /// <summary>The tick of the editor's last change, so rules reacting to inventory changes can ignore the editor's own.</summary>
    public int LastWriteTick { get; set; } = int.MinValue / 2;

    private const int MaxHistory = 200;
    private int nextHistoryId = 1;

    /// <summary>The editor's changes since the save was loaded, oldest first. Game thread only.</summary>
    public List<HistoryEntry> History { get; } = new();

    public void Record(ApiRequest? request, Action? undo)
    {
        string? summary = request?.Body?.ToString(Newtonsoft.Json.Formatting.None);
        if (summary is { Length: > 160 })
            summary = summary[..157] + "...";
        this.History.Add(new HistoryEntry
        {
            Id = this.nextHistoryId++,
            Time = DateTime.Now,
            Method = request?.Method ?? "?",
            Path = request?.Path ?? "?",
            Summary = summary,
            Undo = undo,
        });
        if (this.History.Count > MaxHistory)
            this.History.RemoveAt(0);
    }
}

/// <summary>A group of API routes for one area of the game (player, inventory, NPCs...).</summary>
internal abstract class Domain
{
    private readonly GameThreadDispatcher game;

    protected EditorState State { get; }

    protected Domain(GameThreadDispatcher game, EditorState state)
    {
        this.game = game;
        this.State = state;
    }

    public abstract void Register(Router router);

    /// <summary>Read game state on the game thread. Fails with 409 if no save is loaded.</summary>
    protected Task<object?> Read(Func<object?> read) => this.game.Run(() =>
    {
        RequireWorld();
        return read();
    });

    /// <summary>Change game state on the game thread and flag unsaved changes. Fails with 409 if no save is loaded.</summary>
    protected Task<object?> Write(Func<object?> write)
    {
        ApiRequest? request = RequestContext.Current.Value; // captured here, on the HTTP side
        return this.game.Run(() =>
        {
            RequireWorld();
            UndoCapture.Take(); // drop anything left over from a failed request
            object? result;
            try
            {
                result = write();
            }
            catch
            {
                UndoCapture.Take();
                throw;
            }
            this.State.Record(request, UndoCapture.Take());
            this.State.UnsavedChanges = true;
            this.State.LastWriteTick = this.State.Tick;
            return result;
        });
    }

    /// <summary>Run on the game thread without requiring a loaded save.</summary>
    protected Task<object?> Run(Func<object?> func) => this.game.Run(func);

    private static void RequireWorld()
    {
        if (!Context.IsWorldReady)
            throw new ApiException(409, "No save is loaded. Load a save in the game first.");
    }

    /// <summary>Read an optional integer field from a JSON body, enforcing a range.</summary>
    internal static int? OptInt(JObject body, string name, int min, int max)
    {
        JToken? token = body[name];
        if (token is null || token.Type == JTokenType.Null)
            return null;
        if (token.Type != JTokenType.Integer)
            throw new ApiException(400, $"'{name}' must be an integer.");

        long value = token.Value<long>();
        if (value < min || value > max)
            throw new ApiException(400, $"'{name}' must be between {min} and {max}.");
        return (int)value;
    }
}
