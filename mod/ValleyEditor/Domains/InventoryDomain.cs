using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using StardewValley;
using ValleyEditor.Server;

namespace ValleyEditor.Domains;

/// <summary>The main player's backpack.</summary>
internal sealed class InventoryDomain : Domain
{
    /// <summary>Backpack sizes come in rows of 12; 48 covers mods like Bigger Backpack.</summary>
    private const int RowSize = 12;
    private const int MaxSize = 48;

    public InventoryDomain(GameThreadDispatcher game, EditorState state)
        : base(game, state) { }

    public override void Register(Router router)
    {
        router.Get("/api/inventory", _ => this.Read(Snapshot));

        // add an item, into a given empty slot or wherever it fits
        router.Post("/api/inventory", request =>
        {
            JObject body = request.BodyObject;
            string id = body.Value<string>("qualifiedId") ?? throw new ApiException(400, "'qualifiedId' is required.");
            int stack = OptInt(body, "stack", 1, int.MaxValue) ?? 1;
            int quality = OptQuality(body) ?? 0;
            int? slot = OptInt(body, "slot", 0, MaxSize - 1);

            return this.Write(() =>
            {
                Farmer player = Game1.player;
                Item item = ItemRegistry.Create(id, 1, quality, allowNull: true)
                    ?? throw new ApiException(404, $"No item with ID '{id}'.");
                item.Stack = Math.Min(stack, item.maximumStackSize());
                if (!ItemJson.CanHaveQuality(item))
                    item.Quality = 0;

                if (slot.HasValue)
                {
                    CheckSlot(player, slot.Value);
                    if (player.Items[slot.Value] != null)
                        throw new ApiException(409, $"Slot {slot.Value} isn't empty.");
                    player.Items[slot.Value] = item;
                }
                else if (!player.addItemToInventoryBool(item))
                    throw new ApiException(409, "The backpack is full.");

                return Snapshot();
            });
        });

        router.Patch("/api/inventory/{slot}", request =>
        {
            int slot = ParseSlot(request);
            JObject body = request.BodyObject;
            int? stack = OptInt(body, "stack", 1, int.MaxValue);
            int? quality = OptQuality(body);

            return this.Write(() =>
            {
                Farmer player = Game1.player;
                CheckSlot(player, slot);
                Item item = player.Items[slot] ?? throw new ApiException(404, $"Slot {slot} is empty.");
                if (stack.HasValue)
                    item.Stack = Math.Min(stack.Value, item.maximumStackSize());
                if (quality.HasValue && ItemJson.CanHaveQuality(item))
                    item.Quality = quality.Value;
                return Snapshot();
            });
        });

        router.Delete("/api/inventory/{slot}", request =>
        {
            int slot = ParseSlot(request);
            return this.Write(() =>
            {
                Farmer player = Game1.player;
                CheckSlot(player, slot);
                player.Items[slot] = null;
                return Snapshot();
            });
        });

        router.Post("/api/inventory/swap", request =>
        {
            JObject body = request.BodyObject;
            int from = OptInt(body, "from", 0, MaxSize - 1) ?? throw new ApiException(400, "'from' is required.");
            int to = OptInt(body, "to", 0, MaxSize - 1) ?? throw new ApiException(400, "'to' is required.");

            return this.Write(() =>
            {
                Farmer player = Game1.player;
                CheckSlot(player, from);
                CheckSlot(player, to);
                (player.Items[from], player.Items[to]) = (player.Items[to], player.Items[from]);
                return Snapshot();
            });
        });

        router.Put("/api/inventory/size", request =>
        {
            int size = OptInt(request.BodyObject, "size", RowSize, MaxSize) ?? throw new ApiException(400, "'size' is required.");
            if (size % RowSize != 0)
                throw new ApiException(400, $"'size' must be a multiple of {RowSize}.");

            return this.Write(() =>
            {
                Farmer player = Game1.player;
                if (size < player.MaxItems)
                {
                    if (player.Items.Skip(size).Any(item => item != null))
                        throw new ApiException(409, $"Empty the slots after {size} before shrinking the backpack.");
                    while (player.Items.Count > size)
                        player.Items.RemoveAt(player.Items.Count - 1);
                    player.MaxItems = size;
                }
                else if (size > player.MaxItems)
                    player.increaseBackpackSize(size - player.MaxItems);
                return Snapshot();
            });
        });
    }

    private static object Snapshot()
    {
        Farmer player = Game1.player;
        return new { Size = player.MaxItems, Slots = ItemJson.Slots(player.Items, player.MaxItems) };
    }

    private static int? OptQuality(JObject body)
    {
        int? quality = OptInt(body, "quality", 0, 4);
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

    private static void CheckSlot(Farmer player, int slot)
    {
        if (slot >= player.MaxItems || slot >= player.Items.Count)
            throw new ApiException(400, $"Slot {slot} is outside the backpack ({player.MaxItems} slots).");
    }
}
