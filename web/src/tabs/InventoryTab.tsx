import type { ComponentChildren } from 'preact';
import { useEffect, useState } from 'preact/hooks';
import { api, type CatalogEntry, type Inventory, type ItemStack } from '../api';
import { FeedbackLine, ItemIcon, NumberField, QualitySelect, useAction } from '../components';
import { useI18n } from '../i18n';
import { ItemCatalog } from './ItemCatalog';
import { ItemDetailsEditor } from './ItemDetailsEditor';

const QUALITY_CLASS: Record<number, string> = { 1: 'silver', 2: 'gold', 4: 'iridium' };

export function InventoryTab({ onChanged }: { onChanged: () => void }) {
  const { t } = useI18n();
  const [inventory, setInventory] = useState<Inventory | null>(null);
  const [error, setError] = useState<string | null>(null);
  const { run, feedback } = useAction(onChanged);

  useEffect(() => {
    api<Inventory>('GET', '/api/inventory').then(setInventory, (e) => setError(e.message));
  }, []);

  if (!inventory) return <p>{error ?? t('common.loading')}</p>;

  return (
    <div class="stack">
      <ItemGrid
        basePath="/api/inventory"
        container={inventory}
        setContainer={setInventory}
        onChanged={onChanged}
        title={t('inv.backpack')}
        actions={
          <label class="inline">
            {t('inv.size')}
            <select value={inventory.size} onChange={(e) => run(() => api<Inventory>('PUT', '/api/inventory/size', { size: Number((e.target as HTMLSelectElement).value) }), setInventory)}>
              {[12, 24, 36, 48].map((size) => (
                <option key={size} value={size}>
                  {size} {t('inv.slots')}
                </option>
              ))}
            </select>
          </label>
        }
      />
      <FeedbackLine feedback={feedback} />
      <AddItemCard basePath="/api/inventory" onChanged={onChanged} setContainer={setInventory} />
    </div>
  );
}

type GridProps = {
  /** The slot routes: GET/POST at the path, PATCH/DELETE at path/slot, POST path/swap. */
  basePath: string;
  container: Inventory;
  setContainer: (container: Inventory) => void;
  onChanged: () => void;
  title: ComponentChildren;
  actions?: ComponentChildren;
};

/** A slot grid (backpack or chest): drag to move, click to edit a stack. */
export function ItemGrid({ basePath, container, setContainer, onChanged, title, actions }: GridProps) {
  const { t } = useI18n();
  const [selected, setSelected] = useState<number | null>(null);
  const [dragFrom, setDragFrom] = useState<number | null>(null);
  const { run, feedback } = useAction(onChanged);

  const swap = (from: number, to: number) => from !== to && run(() => api<Inventory>('POST', `${basePath}/swap`, { from, to }), setContainer);
  const selectedItem = selected !== null ? container.slots[selected] : null;

  return (
    <section class="card">
      <div class="card-header">
        <h3 class="inline">{title}</h3>
        {actions}
      </div>
      <p class="muted">{t('inv.dragHint')}</p>
      <div class="slots">
        {container.slots.map((item, i) => (
          <button
            key={i}
            class={`slot ${selected === i ? 'active' : ''}`}
            draggable={item !== null}
            onClick={() => setSelected(i)}
            onDragStart={() => setDragFrom(i)}
            onDragOver={(e) => e.preventDefault()}
            onDrop={(e) => {
              e.preventDefault();
              if (dragFrom !== null) swap(dragFrom, i);
              setDragFrom(null);
            }}
            aria-label={item ? `${item.name} ×${item.stack}` : t('inv.empty')}
          >
            {item && (
              <>
                <ItemIcon qualifiedId={item.qualifiedId} name={item.name} />
                {item.stack > 1 && <span class="stack-count">{item.stack}</span>}
                {QUALITY_CLASS[item.quality] && <span class={`star ${QUALITY_CLASS[item.quality]}`}>★</span>}
              </>
            )}
          </button>
        ))}
      </div>
      <FeedbackLine feedback={feedback} />
      {selectedItem && selected !== null && (
        <SlotEditor key={`${basePath}-${selected}-${selectedItem.qualifiedId}`} basePath={basePath} slot={selected} item={selectedItem} onChanged={onChanged} setContainer={setContainer} />
      )}
    </section>
  );
}

function SlotEditor({ basePath, slot, item, onChanged, setContainer }: { basePath: string; slot: number; item: ItemStack; onChanged: () => void; setContainer: (inv: Inventory) => void }) {
  const { t } = useI18n();
  const { run, feedback, busy } = useAction(onChanged);
  const [stack, setStack] = useState(String(item.stack));
  const [quality, setQuality] = useState(item.quality);

  return (
    <div class="slot-editor">
      <h4>
        <ItemIcon qualifiedId={item.qualifiedId} name={item.name} /> {item.name} <span class="muted">{item.qualifiedId}</span>
      </h4>
      <form
        class="fields"
        onSubmit={(e) => {
          e.preventDefault();
          run(() => api<Inventory>('PATCH', `${basePath}/${slot}`, { stack: Number(stack), ...(item.canHaveQuality ? { quality } : {}) }), setContainer);
        }}
      >
        {item.maxStack > 1 && <NumberField label={`${t('inv.stack')} (max ${item.maxStack})`} value={stack} min={1} max={item.maxStack} onInput={setStack} />}
        {item.canHaveQuality && <QualitySelect value={quality} onChange={setQuality} />}
        <button type="submit" disabled={busy}>
          {t('common.apply')}
        </button>
        <button type="button" class="danger" disabled={busy} onClick={() => run(() => api<Inventory>('DELETE', `${basePath}/${slot}`), setContainer)}>
          {t('inv.delete')}
        </button>
      </form>
      <FeedbackLine feedback={feedback} />
      <ItemDetailsEditor basePath={basePath} slot={slot} onChanged={onChanged} setContainer={setContainer} />
    </div>
  );
}

export function AddItemCard({ basePath, onChanged, setContainer, title }: { basePath: string; onChanged: () => void; setContainer: (inv: Inventory) => void; title?: string }) {
  const { t } = useI18n();
  const { run, feedback, busy } = useAction(onChanged);
  const [picked, setPicked] = useState<CatalogEntry | null>(null);
  const [stack, setStack] = useState('1');
  const [quality, setQuality] = useState(0);

  return (
    <section class="card">
      <h3>{title ?? t('inv.add')}</h3>
      <div class="add-item">
        <ItemCatalog picked={picked} onPick={setPicked} />
        <div class="add-form">
          {picked ? (
            <>
              <h4>
                <ItemIcon qualifiedId={picked.qualifiedId} name={picked.name} size={48} /> {picked.name}
              </h4>
              <p class="muted">
                {picked.qualifiedId}
                {picked.categoryName && ` · ${picked.categoryName}`}
                {picked.modName && ` · ${picked.modName}`}
              </p>
              <form
                class="fields"
                onSubmit={(e) => {
                  e.preventDefault();
                  run(() => api<Inventory>('POST', basePath, { qualifiedId: picked.qualifiedId, stack: Number(stack), quality: picked.type === '(O)' ? quality : 0 }), setContainer);
                }}
              >
                <NumberField label={t('inv.stack')} value={stack} min={1} onInput={setStack} />
                {picked.type === '(O)' && <QualitySelect value={quality} onChange={setQuality} />}
                <button type="submit" disabled={busy}>
                  {t('inv.addTo')}
                </button>
              </form>
              <FeedbackLine feedback={feedback} />
            </>
          ) : (
            <p class="muted">{t('catalog.pick')}</p>
          )}
        </div>
      </div>
    </section>
  );
}
