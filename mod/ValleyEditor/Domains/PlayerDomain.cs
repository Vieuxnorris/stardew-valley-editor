using StardewValley;
using ValleyEditor.Server;

namespace ValleyEditor.Domains;

/// <summary>The main player's stats.</summary>
internal sealed class PlayerDomain : Domain
{
    public PlayerDomain(GameThreadDispatcher game, EditorState state)
        : base(game, state) { }

    public override void Register(Router router)
    {
        router.Get("/api/player", _ => this.Read(Snapshot));

        router.Patch("/api/player", request =>
        {
            int? money = OptInt(request.BodyObject, "money", 0, int.MaxValue);
            return this.Write(() =>
            {
                Farmer player = Game1.player;
                if (money.HasValue)
                    player.Money = money.Value;
                return Snapshot();
            });
        });
    }

    private static object Snapshot()
    {
        Farmer player = Game1.player;
        return new
        {
            Name = player.Name,
            FarmName = player.farmName.Value,
            Money = player.Money,
        };
    }
}
