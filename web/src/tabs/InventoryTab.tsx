import { useEffect, useState } from 'preact/hooks';
import { api, type CatalogEntry, type Inventory, type ItemStack } from '../api';
import { FeedbackLine, ItemIcon, NumberField, QualitySelect, useAction } from '../components';
import { useI18n } from '../i18n';
import { ItemCatalog } from './ItemCatalog';

const QUALITY_CLASS: Record<number, string> = { 1: 'silver', 2: 'gold', 4: 'iridium' };

export function InventoryTab({ onChanged }: { onChanged: () => void }) {
  const { t } = useI18n();
  const [inventory, setInventory] = useState<Inventory | null>(null);
  const [selected, setSelected] = useState<number | null>(null);
  const [dragFrom, setDragFrom] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);
  const { run, feedback } = useAction(onChanged);

  useEffect(() => {
    api<Inventory>('GET', '/api/inventory').then(setInventory, (e) => setError(e.message));
  }, []);

  if (!inventory) return <p>{error ?? t('common.loading')}</p>;

  const swap = (from: number, to: number) => from !== to && run(() => api<Inventory>('POST', '/api/inventory/swap', { from, to }), setInventory);
  const selectedItem = selected !== null ? inventory.slots[selected] : null;

  return (
    <div class="stack">
      <section class="card">
        <div class="card-header">
          <h3>{t('inv.backpack')}</h3>
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
        </div>
        <p class="muted">{t('inv.dragHint')}</p>
        <div class="slots">
          {inventory.slots.map((item, i) => (
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
        {selectedItem && selected !== null && <SlotEditor key={`${selected}-${selectedItem.qualifiedId}`} slot={selected} item={selectedItem} onChanged={onChanged} setInventory={setInventory} />}
      </section>

      <AddItemCard onChanged={onChanged} setInventory={setInventory} />
    </div>
  );
}

function SlotEditor({ slot, item, onChanged, setInventory }: { slot: number; item: ItemStack; onChanged: () => void; setInventory: (inv: Inventory) => void }) {
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
          run(() => api<Inventory>('PATCH', `/api/inventory/${slot}`, { stack: Number(stack), ...(item.canHaveQuality ? { quality } : {}) }), setInventory);
        }}
      >
        {item.maxStack > 1 && <NumberField label={`${t('inv.stack')} (max ${item.maxStack})`} value={stack} min={1} max={item.maxStack} onInput={setStack} />}
        {item.canHaveQuality && <QualitySelect value={quality} onChange={setQuality} />}
        <button type="submit" disabled={busy}>
          {t('common.apply')}
        </button>
        <button type="button" class="danger" disabled={busy} onClick={() => run(() => api<Inventory>('DELETE', `/api/inventory/${slot}`), setInventory)}>
          {t('inv.delete')}
        </button>
      </form>
      <FeedbackLine feedback={feedback} />
    </div>
  );
}

function AddItemCard({ onChanged, setInventory }: { onChanged: () => void; setInventory: (inv: Inventory) => void }) {
  const { t } = useI18n();
  const { run, feedback, busy } = useAction(onChanged);
  const [picked, setPicked] = useState<CatalogEntry | null>(null);
  const [stack, setStack] = useState('1');
  const [quality, setQuality] = useState(0);

  return (
    <section class="card">
      <h3>{t('inv.add')}</h3>
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
                  run(() => api<Inventory>('POST', '/api/inventory', { qualifiedId: picked.qualifiedId, stack: Number(stack), quality: picked.type === '(O)' ? quality : 0 }), setInventory);
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
