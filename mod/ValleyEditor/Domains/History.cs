using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using StardewValley;
using StardewValley.SaveSerialization;
using ValleyEditor.Server;

namespace ValleyEditor.Domains;

/// <summary>One change made through the editor.</summary>
internal sealed class HistoryEntry
{
    public int Id { get; init; }
    public DateTime Time { get; init; }
    public string Method { get; init; } = "";
    public string Path { get; init; } = "";

    /// <summary>A short excerpt of the request body, to tell similar changes apart.</summary>
    public string? Summary { get; init; }

    /// <summary>Restores the state from before the change, if this kind of change can be undone.</summary>
    public Action? Undo { get; init; }

    public bool Undone { get; set; }
}

/// <summary>The API request being handled; flows from the HTTP handler into the game-thread work it queues.</summary>
internal static class RequestContext
{
    public static readonly AsyncLocal<ApiRequest?> Current = new();
}

/// <summary>
/// Lets a change register how to revert itself. Call <see cref="Remember"/> on the game thread, before changing anything,
/// inside a domain write; the first call wins so it captures the state from before the whole request.
/// </summary>
internal static class UndoCapture
{
    [ThreadStatic]
    private static Action? pending;

    public static void Remember(Action undo) => pending ??= undo;

    public static Action? Take()
    {
        Action? undo = pending;
        pending = null;
        return undo;
    }
}

/// <summary>Deep copies of items through the game's own save serializer, so stats, enchantments and attachments come along.</summary>
internal static class ItemCloner
{
    public static Item Clone(Item item)
    {
        try
        {
            var serializer = SaveSerializer.GetSerializer(typeof(Item));
            using var stream = new MemoryStream();
            serializer.SerializeFast(stream, item);
            stream.Position = 0;
            return (Item)serializer.DeserializeFast(stream);
        }
        catch
        {
            // a modded item type the serializer doesn't know: a shallow copy is better than nothing
            Item copy = item.getOne();
            copy.Stack = item.Stack;
            return copy;
        }
    }

    /// <summary>An undo that puts back copies of every item a list holds now.</summary>
    public static Action SnapshotList(IList<Item> items)
    {
        List<Item?> saved = items.Select(i => i is null ? null : Clone(i)).ToList();
        return () =>
        {
            for (int i = 0; i < saved.Count; i++)
            {
                if (i < items.Count)
                    items[i] = saved[i]!;
                else
                    items.Add(saved[i]!);
            }
            while (items.Count > saved.Count)
                items.RemoveAt(items.Count - 1);
        };
    }
}
