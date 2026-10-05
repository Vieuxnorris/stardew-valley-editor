using System.Linq;
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

        SlotRoutes.Register(router, "/api/inventory", this.Read, this.Write, _ => Backpack(), _ => Snapshot());

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

    private static ItemContainer Backpack()
    {
        Farmer player = Game1.player;
        return new ItemContainer(player.Items, player.MaxItems, item => player.addItemToInventoryBool(item));
    }

    private static object Snapshot() => new { Size = Game1.player.MaxItems, Slots = Backpack().Slots() };
}
