using System;
using System.Collections.Generic;
using System.Linq;
using StardewValley;
using ValleyEditor.Rules;
using ValleyEditor.Server;

namespace ValleyEditor.Domains;

/// <summary>Per-monster drop tables (Data/Monsters, field 6).</summary>
internal sealed class MonstersDomain : Domain
{
    private const int DropsField = 6;
    private const int DisplayNameField = 14;

    private readonly RulesService rules;

    public MonstersDomain(GameThreadDispatcher game, EditorState state, RulesService rules)
        : base(game, state)
    {
        this.rules = rules;
    }

    public override void Register(Router router)
    {
        router.Get("/api/monsters", _ => this.Read(this.Snapshot));

        router.Put("/api/monsters/{name}/drops", request => this.Write(() =>
        {
            string name = this.RequireMonster(request.Params["name"]);
            List<LootEntry> drops = LootJson.Parse(request.BodyObject["drops"], "drops", objectsOnly: true);
            this.rules.Update(r => r.MonsterDrops[name] = drops);
            return this.Snapshot();
        }));

        router.Delete("/api/monsters/{name}/drops", request => this.Write(() =>
        {
            string name = this.RequireMonster(request.Params["name"]);
            this.rules.Update(r => r.MonsterDrops.Remove(name));
            return this.Snapshot();
        }));
    }

    private string RequireMonster(string name)
    {
        DataLoader.Monsters(Game1.content); // make sure the baselines are captured
        return this.rules.MonsterBaselines.Keys.FirstOrDefault(n => n.Equals(name, StringComparison.OrdinalIgnoreCase))
            ?? throw new ApiException(404, $"No monster named '{name}'.");
    }

    private object Snapshot()
    {
        Dictionary<string, string> current = DataLoader.Monsters(Game1.content);
        RulesData r = RulesService.Current;

        return this.rules.MonsterBaselines
            .Select(pair =>
            {
                string[] baseFields = pair.Value.Split('/');
                string[] currentFields = current.TryGetValue(pair.Key, out string? raw) ? raw.Split('/') : baseFields;
                return new
                {
                    pair.Key,
                    DisplayName = baseFields.ElementAtOrDefault(DisplayNameField) is { Length: > 0 } display ? display : pair.Key,
                    Edited = r.MonsterDrops.ContainsKey(pair.Key),
                    BaseDrops = Drops(baseFields),
                    Drops = Drops(currentFields),
                };
            })
            .OrderBy(m => m.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private static object[] Drops(string[] fields)
    {
        return RuleMath.ParseMonsterDrops(fields.ElementAtOrDefault(DropsField) ?? "")
            .Select(d =>
            {
                string qualified = ItemRegistry.QualifyItemId(d.ItemId) ?? d.ItemId;
                return (object)new { ItemId = qualified, Name = LootJson.ItemName(qualified), d.Chance };
            })
            .ToArray();
    }
}
