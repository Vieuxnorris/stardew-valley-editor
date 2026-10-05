import { useEffect, useMemo, useState } from 'preact/hooks';
import { api, type FishingSnapshot, type FishTable, type LootEntry } from '../api';
import { FeedbackLine, ItemIcon, NumberField, useAction } from '../components';
import { useI18n } from '../i18n';
import { LootTableEditor } from './LootTableEditor';

type Props = { onChanged: () => void };
type CardProps = Props & { snapshot: FishingSnapshot; setSnapshot: (s: FishingSnapshot) => void };

export function FishingTab({ onChanged }: Props) {
  const { t } = useI18n();
  const [snapshot, setSnapshot] = useState<FishingSnapshot | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    api<FishingSnapshot>('GET', '/api/fishing').then(setSnapshot, (e) => setError(e.message));
  }, []);

  if (!snapshot) return <p>{error ?? t('common.loading')}</p>;

  const shared = { snapshot, setSnapshot, onChanged };
  return (
    <div class="stack">
      <CatchCard {...shared} />
      <TreasureCard {...shared} />
      <TablesCard onChanged={onChanged} />
      <CollectionCard {...shared} />
    </div>
  );
}

function CatchCard({ snapshot, setSnapshot, onChanged }: CardProps) {
  const { t } = useI18n();
  const { run, feedback, busy } = useAction(onChanged);
  const s = snapshot.settings;
  const [perCatch, setPerCatch] = useState(String(s.fishPerCatch));
  useEffect(() => setPerCatch(String(s.fishPerCatch)), [s.fishPerCatch]);
  const save = (body: object) => run(() => api<FishingSnapshot>('PATCH', '/api/fishing', body), setSnapshot);

  return (
    <section class="card">
      <h3>{t('fish.catch')}</h3>
      <form
        class="fields"
        onSubmit={(e) => {
          e.preventDefault();
          save({ fishPerCatch: Number(perCatch) });
        }}
      >
        <NumberField label={t('fish.perCatch')} value={perCatch} min={1} max={999} onInput={setPerCatch} />
        <button type="submit" disabled={busy}>
          {t('common.apply')}
        </button>
        <label>
          {t('fish.quality')}
          <select value={s.fishQuality} disabled={busy} onChange={(e) => save({ fishQuality: Number((e.target as HTMLSelectElement).value) })}>
            <option value={-1}>{t('fish.qualityGame')}</option>
            {[0, 1, 2, 4].map((q) => (
              <option key={q} value={q}>
                {t(`quality.${q}`)}
              </option>
            ))}
          </select>
        </label>
        <label>
          {t('fish.forced')}
          <select value={s.forcedFishId ?? ''} disabled={busy} onChange={(e) => save({ forcedFishId: (e.target as HTMLSelectElement).value })}>
            <option value="">{t('fish.forcedNone')}</option>
            {snapshot.fish
              .filter((f) => !f.crabPot)
              .map((f) => (
                <option key={f.id} value={f.id}>
                  {f.name}
                </option>
              ))}
          </select>
        </label>
      </form>
      <label class="check">
        <input type="checkbox" checked={s.fishMaxSize} disabled={busy} onChange={(e) => save({ fishMaxSize: (e.target as HTMLInputElement).checked })} /> {t('fish.maxSize')}
      </label>
      <p class="muted">{t('fish.forcedHint')}</p>
      <FeedbackLine feedback={feedback} />
    </section>
  );
}

function TreasureCard({ snapshot, setSnapshot, onChanged }: CardProps) {
  const { t } = useI18n();
  const { run, feedback, busy } = useAction(onChanged);
  const s = snapshot.settings;
  const [multiplier, setMultiplier] = useState(String(s.treasureMultiplier));
  const [rolls, setRolls] = useState(String(s.treasureRolls));
  const [loot, setLoot] = useState<LootEntry[]>(s.treasureLoot);
  useEffect(() => {
    setMultiplier(String(s.treasureMultiplier));
    setRolls(String(s.treasureRolls));
    setLoot(s.treasureLoot);
  }, [snapshot]);

  return (
    <section class="card">
      <h3>{t('fish.treasure')}</h3>
      <form
        class="fields"
        onSubmit={(e) => {
          e.preventDefault();
          run(() => api<FishingSnapshot>('PATCH', '/api/fishing', { treasureMultiplier: Number(multiplier), treasureRolls: Number(rolls) }), setSnapshot);
        }}
      >
        <NumberField label={t('fish.treasureRolls')} value={rolls} min={1} max={20} onInput={setRolls} />
        <NumberField label={t('fish.treasureMultiplier')} value={multiplier} min={1} max={999} onInput={setMultiplier} />
        <button type="submit" disabled={busy}>
          {t('common.apply')}
        </button>
      </form>
      <label class="check">
        <input
          type="checkbox"
          checked={s.treasureReplaceVanilla}
          disabled={busy}
          onChange={(e) => run(() => api<FishingSnapshot>('PATCH', '/api/fishing', { treasureReplaceVanilla: (e.target as HTMLInputElement).checked }), setSnapshot)}
        />{' '}
        {t('fish.treasureReplace')}
      </label>
      <p class="muted">{t('fish.treasureRollsHint')}</p>
      <p class="muted">{t('fish.treasureHint')}</p>
      <LootTableEditor entries={loot} onChange={setLoot} withStacks />
      <button disabled={busy} onClick={() => run(() => api<FishingSnapshot>('PUT', '/api/fishing/treasure', { entries: loot }), setSnapshot)}>
        {t('loot.save')}
      </button>
      <FeedbackLine feedback={feedback} />
    </section>
  );
}

function TablesCard({ onChanged }: Props) {
  const { t } = useI18n();
  const { run, feedback, busy } = useAction(onChanged);
  const [tables, setTables] = useState<FishTable[] | null>(null);
  const [location, setLocation] = useState('');

  useEffect(() => {
    api<FishTable[]>('GET', '/api/fishing/tables').then((list) => {
      setTables(list);
      setLocation(list.find((l) => l.location === 'Beach')?.location ?? list[0]?.location ?? '');
    });
  }, []);

  const table = tables?.find((l) => l.location === location);

  return (
    <section class="card">
      <h3>{t('fish.tables')}</h3>
      <p class="muted">{t('fish.tablesHint')}</p>
      {tables && (
        <label class="inline">
          {t('fish.location')}
          <select value={location} onChange={(e) => setLocation((e.target as HTMLSelectElement).value)}>
            {tables.map((l) => (
              <option key={l.location} value={l.location}>
                {l.displayName}
                {l.displayName !== l.location ? ` (${l.location})` : ''}
              </option>
            ))}
          </select>
        </label>
      )}
      {table && <TableEditor key={table.location} table={table} busy={busy} save={(body) => run(() => api<FishTable[]>('PUT', `/api/fishing/tables/${encodeURIComponent(table.location)}`, body), setTables)} />}
      <FeedbackLine feedback={feedback} />
    </section>
  );
}

function TableEditor({ table, busy, save }: { table: FishTable; busy: boolean; save: (body: object) => void }) {
  const { t } = useI18n();
  const [chances, setChances] = useState(() => Object.fromEntries(table.entries.filter((e) => e.id).map((e) => [e.id!, e.chance])));
  const idSet = (pick: (e: FishTable['entries'][number]) => boolean) => new Set(table.entries.filter((e) => e.id && pick(e)).map((e) => e.id!));
  const [removed, setRemoved] = useState(() => idSet((e) => e.removed));
  const [unrestricted, setUnrestricted] = useState(() => idSet((e) => e.unrestricted));
  const [priority, setPriority] = useState(() => idSet((e) => e.priority));
  const [seasons, setSeasons] = useState(() => Object.fromEntries(table.entries.filter((e) => e.id && e.season).map((e) => [e.id!, e.season!])) as Record<string, string>);
  const [added, setAdded] = useState<LootEntry[]>(table.added);

  const submit = () => {
    // only send chances that differ from vanilla
    const changed = Object.fromEntries(table.entries.filter((e) => e.id && chances[e.id] !== e.baseChance).map((e) => [e.id!, chances[e.id!]]));
    save({ chances: changed, removed: [...removed], unrestricted: [...unrestricted], priority: [...priority], seasons, added });
  };

  return (
    <div class="stack">
      <div class="table-scroll">
        <table class="table">
          <thead>
            <tr>
              <th>{t('loot.item')}</th>
              <th>{t('world.season')}</th>
              <th>{t('fish.base')}</th>
              <th>{t('loot.chance')}</th>
              <th title={t('fish.unrestrictedHint')}>{t('fish.unrestricted')}</th>
              <th title={t('fish.priorityHint')}>{t('fish.priority')}</th>
              <th>{t('fish.removed')}</th>
            </tr>
          </thead>
          <tbody>
            {table.entries.map((entry, i) => (
              <tr key={entry.id ?? i} class={entry.id && removed.has(entry.id) ? 'struck' : ''}>
                <td class="inline" title={entry.condition ?? undefined}>
                  {entry.itemId?.startsWith('(') && <ItemIcon qualifiedId={entry.itemId} name={entry.name} size={24} />} {entry.name}
                  {entry.isBossFish && <span class="badge">{t('fish.boss')}</span>}
                </td>
                <td>
                  {entry.id ? (
                    <select
                      value={seasons[entry.id] ?? ''}
                      onChange={(e) => {
                        const value = (e.target as HTMLSelectElement).value;
                        const next = { ...seasons };
                        if (value) next[entry.id!] = value;
                        else delete next[entry.id!];
                        setSeasons(next);
                      }}
                      aria-label={`${entry.name} ${t('world.season')}`}
                    >
                      <option value="">
                        {t('fish.vanillaSeason')} ({entry.baseSeason ? t(`season.${entry.baseSeason}`) : t('fish.anySeason')})
                      </option>
                      <option value="any">{t('fish.anySeason')}</option>
                      {(['spring', 'summer', 'fall', 'winter'] as const).map((s) => (
                        <option key={s} value={s}>
                          {t(`season.${s}`)}
                        </option>
                      ))}
                    </select>
                  ) : (
                    '—'
                  )}
                </td>
                <td>{Math.round(entry.baseChance * 1000) / 10}</td>
                <td>
                  {entry.id ? (
                    <input
                      type="number"
                      min={0}
                      max={100}
                      step={0.1}
                      value={Math.round(chances[entry.id] * 1000) / 10}
                      onInput={(e) => setChances({ ...chances, [entry.id!]: Number((e.target as HTMLInputElement).value) / 100 })}
                      aria-label={`${entry.name} ${t('loot.chance')}`}
                    />
                  ) : (
                    '—'
                  )}
                </td>
                <td>{entry.id && <SetToggle set={unrestricted} setSet={setUnrestricted} id={entry.id} label={`${t('fish.unrestricted')} ${entry.name}`} />}</td>
                <td>{entry.id && <SetToggle set={priority} setSet={setPriority} id={entry.id} label={`${t('fish.priority')} ${entry.name}`} />}</td>
                <td>
                  {entry.id && (
                    <input
                      type="checkbox"
                      checked={removed.has(entry.id)}
                      onChange={(e) => {
                        const next = new Set(removed);
                        if ((e.target as HTMLInputElement).checked) next.add(entry.id!);
                        else next.delete(entry.id!);
                        setRemoved(next);
                      }}
                      aria-label={`${t('fish.removed')} ${entry.name}`}
                    />
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <h4>{t('fish.added')}</h4>
      <LootTableEditor entries={added} onChange={setAdded} fixedType="(O)" />
      <div>
        <button disabled={busy} onClick={submit}>
          {t('loot.save')}
        </button>
      </div>
    </div>
  );
}

function CollectionCard({ snapshot, setSnapshot, onChanged }: CardProps) {
  const { t } = useI18n();
  const { run, feedback, busy } = useAction(onChanged);
  const [filter, setFilter] = useState('');
  const fish = useMemo(() => snapshot.fish.filter((f) => f.name.toLowerCase().includes(filter.toLowerCase())), [snapshot.fish, filter]);
  const caughtCount = snapshot.fish.filter((f) => f.caught > 0).length;

  return (
    <section class="card">
      <div class="card-header">
        <h3>
          {t('fish.collection')} <span class="muted">({caughtCount} / {snapshot.fish.length})</span>
        </h3>
        <button disabled={busy} onClick={() => run(() => api<FishingSnapshot>('POST', '/api/fishing/collection/catch-all'), setSnapshot)}>
          {t('fish.catchAll')}
        </button>
      </div>
      <p class="muted">{t('fish.collectionHint')}</p>
      <input type="search" placeholder={t('prog.filter')} value={filter} onInput={(e) => setFilter((e.target as HTMLInputElement).value)} aria-label={t('prog.filter')} />
      <div class="table-scroll">
        <table class="table">
          <thead>
            <tr>
              <th>{t('loot.item')}</th>
              <th>{t('fish.caught')}</th>
              <th>{t('fish.record')}</th>
            </tr>
          </thead>
          <tbody>
            {fish.map((f) => (
              <CollectionRow key={f.id} fish={f} busy={busy} save={(count, maxSize) => run(() => api<FishingSnapshot>('PUT', `/api/fishing/collection/${encodeURIComponent(f.id)}`, { count, maxSize }), setSnapshot)} />
            ))}
          </tbody>
        </table>
      </div>
      <FeedbackLine feedback={feedback} />
    </section>
  );
}

function CollectionRow({ fish, busy, save }: { fish: FishingSnapshot['fish'][number]; busy: boolean; save: (count: number, maxSize: number) => void }) {
  const { t } = useI18n();
  const [count, setCount] = useState(String(fish.caught));
  const [size, setSize] = useState(String(fish.recordSize));
  useEffect(() => {
    setCount(String(fish.caught));
    setSize(String(fish.recordSize));
  }, [fish.caught, fish.recordSize]);
  const commit = () => (count !== String(fish.caught) || size !== String(fish.recordSize)) && save(Number(count), Number(size));

  return (
    <tr>
      <td class="inline">
        <ItemIcon qualifiedId={fish.id} name={fish.name} size={24} /> {fish.name}
        {fish.crabPot && <span class="badge">{t('fish.crabPot')}</span>}
      </td>
      <td>
        <input type="number" min={0} value={count} disabled={busy} onInput={(e) => setCount((e.target as HTMLInputElement).value)} onBlur={commit} aria-label={`${fish.name} ${t('fish.caught')}`} />
      </td>
      <td>
        <input type="number" min={0} value={size} disabled={busy} onInput={(e) => setSize((e.target as HTMLInputElement).value)} onBlur={commit} aria-label={`${fish.name} ${t('fish.record')}`} />
        {fish.maxSize !== null && <span class="muted"> / {fish.maxSize}</span>}
      </td>
    </tr>
  );
}

/** A checkbox that adds or removes an ID from a set. */
function SetToggle({ set, setSet, id, label }: { set: Set<string>; setSet: (s: Set<string>) => void; id: string; label: string }) {
  return (
    <input
      type="checkbox"
      checked={set.has(id)}
      onChange={(e) => {
        const next = new Set(set);
        if ((e.target as HTMLInputElement).checked) next.add(id);
        else next.delete(id);
        setSet(next);
      }}
      aria-label={label}
    />
  );
}
