using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using StardewValley;
using ValleyEditor.Server;

namespace ValleyEditor.Domains;

/// <summary>A slot grid the editor can change: the backpack or a chest.</summary>
internal sealed class ItemContainer
{
    public ItemContainer(IList<Item> items, int capacity, Func<Item, bool> add)
    {
        this.Items = items;
        this.Capacity = capacity;
        this.Add = add;
    }

    /// <summary>The underlying list. Chests only hold as many entries as they've ever used, so it may be shorter than <see cref="Capacity"/>.</summary>
    public IList<Item> Items { get; }

    public int Capacity { get; }

    /// <summary>Add an item wherever it fits (merging stacks like the game does); false if it doesn't fit.</summary>
    public Func<Item, bool> Add { get; }

    public Item? Get(int slot) => slot < this.Items.Count ? this.Items[slot] : null;

    public void Set(int slot, Item? item)
    {
        while (this.Items.Count <= slot)
            this.Items.Add(null!);
        this.Items[slot] = item!;
    }

    public void CheckSlot(int slot)
    {
        if (slot >= this.Capacity)
            throw new ApiException(400, $"Slot {slot} is outside this container ({this.Capacity} slots).");
    }

    public object?[] Slots() => ItemJson.Slots(this.Items!, this.Capacity);
}

/// <summary>The slot-editing routes shared by the backpack and chests: add, change, delete and swap items.</summary>
internal static class SlotRoutes
{
    /// <summary>Register the routes under <paramref name="prefix"/> (which may contain route parameters).</summary>
    /// <param name="resolve">Find the container for a request; runs on the game thread.</param>
    /// <param name="snapshot">The JSON returned after a change; runs on the game thread.</param>
    public static void Register(
        Router router,
        string prefix,
        Func<Func<object?>, System.Threading.Tasks.Task<object?>> write,
        Func<ApiRequest, ItemContainer> resolve,
        Func<ApiRequest, object> snapshot)
    {
        // add an item, into a given empty slot or wherever it fits
        router.Post(prefix, request =>
        {
            JObject body = request.BodyObject;
            string id = body.Value<string>("qualifiedId") ?? throw new ApiException(400, "'qualifiedId' is required.");
            int stack = Domain.OptInt(body, "stack", 1, int.MaxValue) ?? 1;
            int quality = OptQuality(body) ?? 0;
            int? slot = Domain.OptInt(body, "slot", 0, 1000);

            return write(() =>
            {
                ItemContainer container = resolve(request);
                Item item = ItemRegistry.Create(id, 1, quality, allowNull: true)
                    ?? throw new ApiException(404, $"No item with ID '{id}'.");
                item.Stack = Math.Min(stack, item.maximumStackSize());
                if (!ItemJson.CanHaveQuality(item))
                    item.Quality = 0;

                if (slot.HasValue)
                {
                    container.CheckSlot(slot.Value);
                    if (container.Get(slot.Value) != null)
                        throw new ApiException(409, $"Slot {slot.Value} isn't empty.");
                    container.Set(slot.Value, item);
                }
                else if (!container.Add(item))
                    throw new ApiException(409, "There's no room left.");

                return snapshot(request);
            });
        });

        router.Patch(prefix + "/{slot}", request =>
        {
            int slot = ParseSlot(request);
            JObject body = request.BodyObject;
            int? stack = Domain.OptInt(body, "stack", 1, int.MaxValue);
            int? quality = OptQuality(body);

            return write(() =>
            {
                ItemContainer container = resolve(request);
                container.CheckSlot(slot);
                Item item = container.Get(slot) ?? throw new ApiException(404, $"Slot {slot} is empty.");
                if (stack.HasValue)
                    item.Stack = Math.Min(stack.Value, item.maximumStackSize());
                if (quality.HasValue && ItemJson.CanHaveQuality(item))
                    item.Quality = quality.Value;
                return snapshot(request);
            });
        });

        router.Delete(prefix + "/{slot}", request =>
        {
            int slot = ParseSlot(request);
            return write(() =>
            {
                ItemContainer container = resolve(request);
                container.CheckSlot(slot);
                container.Set(slot, null);
                return snapshot(request);
            });
        });

        router.Post(prefix + "/swap", request =>
        {
            JObject body = request.BodyObject;
            int from = Domain.OptInt(body, "from", 0, 1000) ?? throw new ApiException(400, "'from' is required.");
            int to = Domain.OptInt(body, "to", 0, 1000) ?? throw new ApiException(400, "'to' is required.");

            return write(() =>
            {
                ItemContainer container = resolve(request);
                container.CheckSlot(from);
                container.CheckSlot(to);
                Item? a = container.Get(from), b = container.Get(to);
                container.Set(from, b);
                container.Set(to, a);
                return snapshot(request);
            });
        });
    }

    private static int? OptQuality(JObject body)
    {
        int? quality = Domain.OptInt(body, "quality", 0, 4);
        if (quality.HasValue && !ItemJson.Qualities.Contains(quality.Value))
            throw new ApiException(400, "'quality' must be 0 (normal), 1 (silver), 2 (gold) or 4 (iridium).");
        return quality;
    }

    private static int ParseSlot(ApiRequest request)
    {
        return int.TryParse(request.Params["slot"], out int slot) && slot >= 0
            ? slot
            : throw new ApiException(400, "The slot must be a non-negative integer.");
    }
}
