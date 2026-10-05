using System.Linq;
using ValleyEditor.Server;

namespace ValleyEditor.Domains;

/// <summary>The journal of the editor's changes, and undo.</summary>
internal sealed class HistoryDomain : Domain
{
    public HistoryDomain(GameThreadDispatcher game, EditorState state)
        : base(game, state) { }

    public override void Register(Router router)
    {
        router.Get("/api/history", _ => this.Run(this.Snapshot));

        // undo the latest change that can be undone and isn't yet
        router.Post("/api/history/undo", _ => this.Run(() =>
        {
            HistoryEntry entry = this.State.History.LastOrDefault(e => e.Undo != null && !e.Undone)
                ?? throw new ApiException(409, "Nothing to undo.");
            entry.Undo!();
            entry.Undone = true;
            this.State.UnsavedChanges = true;
            this.State.LastWriteTick = this.State.Tick;
            return this.Snapshot();
        }));
    }

    private object Snapshot()
    {
        return this.State.History
            .AsEnumerable()
            .Reverse()
            .Select(e => new { e.Id, Time = e.Time.ToString("HH:mm:ss"), e.Method, e.Path, e.Summary, Undoable = e.Undo != null, e.Undone })
            .ToArray();
    }
}
