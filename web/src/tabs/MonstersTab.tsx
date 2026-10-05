import { useEffect, useMemo, useState } from 'preact/hooks';
import { api, monsterSpriteUrl, type LootEntry, type Monster } from '../api';
import { FeedbackLine, ItemIcon, useAction } from '../components';
import { useI18n } from '../i18n';
import { LootTableEditor } from './LootTableEditor';

const toLoot = (m: Monster): LootEntry[] => m.drops.map((d) => ({ itemId: d.itemId, name: d.name, chance: d.chance, minStack: 1, maxStack: 1, quality: 0 }));

export function MonstersTab({ onChanged }: { onChanged: () => void }) {
  const { t } = useI18n();
  const [monsters, setMonsters] = useState<Monster[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState('');
  const [selected, setSelected] = useState<string | null>(null);

  useEffect(() => {
    api<Monster[]>('GET', '/api/monsters').then(setMonsters, (e) => setError(e.message));
  }, []);

  const visible = useMemo(() => monsters?.filter((m) => m.displayName.toLowerCase().includes(search.toLowerCase())) ?? [], [monsters, search]);
  if (!monsters) return <p>{error ?? t('common.loading')}</p>;
  const monster = monsters.find((m) => m.key === selected);

  return (
    <div class="stack">
      <section class="card">
        <p class="muted">{t('mon.hint')}</p>
        <input type="search" placeholder={t('mon.search')} value={search} onInput={(e) => setSearch((e.target as HTMLInputElement).value)} aria-label={t('mon.search')} />
      </section>
      <div class="monster-layout">
        <ul class="monster-grid">
          {visible.map((m) => (
            <li key={m.key}>
              <button class={`monster-tile ${m.key === selected ? 'active' : ''} ${m.edited ? 'edited' : ''}`} onClick={() => setSelected(m.key)} title={m.displayName} aria-label={m.displayName}>
                <MonsterIcon monster={m} />
              </button>
            </li>
          ))}
        </ul>
        {monster && <MonsterEditor key={monster.key} monster={monster} setMonsters={setMonsters} onChanged={onChanged} />}
      </div>
    </div>
  );
}

function MonsterEditor({ monster, setMonsters, onChanged }: { monster: Monster; setMonsters: (m: Monster[]) => void; onChanged: () => void }) {
  const { t } = useI18n();
  const { run, feedback, busy } = useAction(onChanged);
  const [drops, setDrops] = useState(() => toLoot(monster));
  useEffect(() => setDrops(toLoot(monster)), [monster]);
  const path = `/api/monsters/${encodeURIComponent(monster.key)}/drops`;

  return (
    <section class="card">
      <div class="card-header">
        <h3 class="inline">
          <MonsterIcon monster={monster} size={48} /> {monster.displayName}
          {monster.edited && <span class="badge">{t('mon.edited')}</span>}
        </h3>
        {monster.edited && (
          <button class="secondary" disabled={busy} onClick={() => run(() => api<Monster[]>('DELETE', path), setMonsters)}>
            {t('mon.reset')}
          </button>
        )}
      </div>
      {monster.edited && (
        <p class="muted">
          {t('fish.base')} :{' '}
          {monster.baseDrops.map((d) => (
            <span key={d.itemId} class="inline">
              <ItemIcon qualifiedId={d.itemId} name={d.name ?? d.itemId} size={16} /> {d.name} {Math.round(d.chance * 1000) / 10}%{' '}
            </span>
          ))}
        </p>
      )}
      <LootTableEditor entries={drops} onChange={setDrops} fixedType="(O)" />
      <button disabled={busy} onClick={() => run(() => api<Monster[]>('PUT', path, { drops }), setMonsters)}>
        {t('loot.save')}
      </button>
      <FeedbackLine feedback={feedback} />
    </section>
  );
}

/** A monster's sprite; falls back to its name when the game has no sprite under that name. */
function MonsterIcon({ monster, size = 40 }: { monster: Monster; size?: number }) {
  const [failed, setFailed] = useState(false);
  if (failed) return <span class="monster-fallback">{monster.displayName}</span>;
  return <img class="icon" src={monsterSpriteUrl(monster.key)} alt={monster.displayName} width={size} height={size} loading="lazy" onError={() => setFailed(true)} />;
}
