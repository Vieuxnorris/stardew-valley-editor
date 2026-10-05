using System;
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
    protected Task<object?> Write(Func<object?> write) => this.game.Run(() =>
    {
        RequireWorld();
        object? result = write();
        this.State.UnsavedChanges = true;
        return result;
    });

    /// <summary>Run on the game thread without requiring a loaded save.</summary>
    protected Task<object?> Run(Func<object?> func) => this.game.Run(func);

    private static void RequireWorld()
    {
        if (!Context.IsWorldReady)
            throw new ApiException(409, "No save is loaded. Load a save in the game first.");
    }

    /// <summary>Read an optional integer field from a JSON body, enforcing a range.</summary>
    protected static int? OptInt(JObject body, string name, int min, int max)
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
