import { useEffect, useMemo, useState } from 'preact/hooks';
import { api, type ChestInfo, type Inventory } from '../api';
import { ItemIcon } from '../components';
import { useI18n } from '../i18n';
import { AddItemCard, ItemGrid } from './InventoryTab';

export function ChestsTab({ onChanged }: { onChanged: () => void }) {
  const { t } = useI18n();
  const [chests, setChests] = useState<ChestInfo[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [selected, setSelected] = useState<string | null>(null);
  const [filter, setFilter] = useState('');

  const reload = () => api<ChestInfo[]>('GET', '/api/chests').then(setChests, (e) => setError(e.message));
  useEffect(() => {
    reload();
  }, []);

  const visible = useMemo(() => {
    const q = filter.toLowerCase();
    return chests?.filter((c) => `${c.name} ${c.locationName}`.toLowerCase().includes(q)) ?? [];
  }, [chests, filter]);

  if (!chests) return <p>{error ?? t('common.loading')}</p>;
  const chest = chests.find((c) => c.id === selected);

  return (
    <div class="stack">
      <section class="card">
        <div class="card-header">
          <h3>{t('chests.title')}</h3>
          <button class="secondary" onClick={reload}>
            {t('common.reload')}
          </button>
        </div>
        <p class="muted">{t('chests.hint')}</p>
        <input type="search" placeholder={t('chests.filter')} value={filter} onInput={(e) => setFilter((e.target as HTMLInputElement).value)} aria-label={t('chests.filter')} />
        {chests.length === 0 ? (
          <p class="muted">{t('chests.none')}</p>
        ) : (
          <ul class="chest-list">
            {visible.map((c) => (
              <li key={c.id}>
                <button class={`chest-tile ${c.id === selected ? 'active' : ''}`} onClick={() => setSelected(c.id)} style={c.color ? { borderLeftColor: c.color } : undefined}>
                  <ItemIcon qualifiedId={c.qualifiedId} name={c.name} size={32} />
                  <span>
                    <strong>{c.isFridge ? t('chests.fridge') : c.name}</strong>
                    <span class="muted">
                      {c.locationName}
                      {c.x !== null && ` (${c.x}, ${c.y})`}
                    </span>
                  </span>
                  <span class="badge">
                    {c.used}/{c.capacity}
                  </span>
                </button>
              </li>
            ))}
          </ul>
        )}
      </section>
      {chest && <ChestEditor key={chest.id} chest={chest} onChanged={onChanged} onContentChanged={reload} />}
    </div>
  );
}

export function ChestEditor({ chest, version = 0, onChanged, onContentChanged }: { chest: Pick<ChestInfo, 'id' | 'name' | 'isFridge' | 'locationName'>; version?: number; onChanged: () => void; onContentChanged: () => void }) {
  const { t } = useI18n();
  const [content, setContent] = useState<Inventory | null>(null);
  const [error, setError] = useState<string | null>(null);
  const basePath = `/api/chests/${encodeURIComponent(chest.id)}`;

  useEffect(() => {
    api<Inventory>('GET', basePath).then(setContent, (e) => setError(e.message));
  }, [basePath, version]);

  if (!content) return <p>{error ?? t('common.loading')}</p>;

  // keep the list's fill counts in step with edits
  const update = (inv: Inventory) => {
    setContent(inv);
    onContentChanged();
  };
  const title = `${chest.isFridge ? t('chests.fridge') : chest.name} — ${chest.locationName}`;

  return (
    <>
      <ItemGrid basePath={basePath} container={content} setContainer={update} onChanged={onChanged} title={title} />
      <AddItemCard basePath={basePath} onChanged={onChanged} setContainer={update} title={t('chests.add')} />
    </>
  );
}
