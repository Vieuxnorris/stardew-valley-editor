import { useState } from 'preact/hooks';
import type { LootEntry } from '../api';
import { ItemIcon } from '../components';
import { useI18n } from '../i18n';
import { ItemCatalog } from './ItemCatalog';

type Props = {
  entries: LootEntry[];
  onChange: (entries: LootEntry[]) => void;
  /** Show stack range and quality columns (treasure chests); monster drops and fish spots only have a chance. */
  withStacks?: boolean;
  /** Restrict the item picker to one item type, e.g. '(O)'. */
  fixedType?: string;
};

/** Edit a list of loot entries: chance in percent, optional stack range and quality, add from the item catalog. */
export function LootTableEditor({ entries, onChange, withStacks = false, fixedType }: Props) {
  const { t } = useI18n();
  const [adding, setAdding] = useState(false);

  const update = (i: number, patch: Partial<LootEntry>) => onChange(entries.map((e, j) => (i === j ? { ...e, ...patch } : e)));
  const num = (e: Event) => Number((e.target as HTMLInputElement).value);

  return (
    <div class="loot-table">
      {entries.length === 0 ? (
        <p class="muted">{t('loot.empty')}</p>
      ) : (
        <table class="table">
          <thead>
            <tr>
              <th>{t('loot.item')}</th>
              <th>{t('loot.chance')}</th>
              {withStacks && (
                <>
                  <th>{t('loot.min')}</th>
                  <th>{t('loot.max')}</th>
                  <th>{t('inv.quality')}</th>
                </>
              )}
              <th />
            </tr>
          </thead>
          <tbody>
            {entries.map((entry, i) => (
              <tr key={`${i}-${entry.itemId}`}>
                <td class="inline">
                  <ItemIcon qualifiedId={entry.itemId} name={entry.name ?? entry.itemId} size={24} /> {entry.name ?? entry.itemId}
                </td>
                <td>
                  <input type="number" min={0} max={100} step={0.1} value={round(entry.chance * 100)} onInput={(e) => update(i, { chance: num(e) / 100 })} aria-label={t('loot.chance')} />
                </td>
                {withStacks && (
                  <>
                    <td>
                      <input type="number" min={1} max={999} value={entry.minStack} onInput={(e) => update(i, { minStack: num(e) })} aria-label={t('loot.min')} />
                    </td>
                    <td>
                      <input type="number" min={1} max={999} value={entry.maxStack} onInput={(e) => update(i, { maxStack: num(e) })} aria-label={t('loot.max')} />
                    </td>
                    <td>
                      <select value={entry.quality} onChange={(e) => update(i, { quality: num(e) })} aria-label={t('inv.quality')}>
                        {[0, 1, 2, 4].map((q) => (
                          <option key={q} value={q}>
                            {t(`quality.${q}`)}
                          </option>
                        ))}
                      </select>
                    </td>
                  </>
                )}
                <td>
                  <button type="button" class="secondary" onClick={() => onChange(entries.filter((_, j) => j !== i))} aria-label={`${t('prog.remove')} ${entry.name ?? entry.itemId}`}>
                    ✕
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {adding ? (
        <ItemCatalog
          picked={null}
          fixedType={fixedType}
          onPick={(item) => {
            onChange([...entries, { itemId: item.qualifiedId, name: item.name, chance: 1, minStack: 1, maxStack: 1, quality: 0 }]);
            setAdding(false);
          }}
        />
      ) : (
        <button type="button" class="secondary" onClick={() => setAdding(true)}>
          + {t('loot.add')}
        </button>
      )}
    </div>
  );
}

const round = (value: number) => Math.round(value * 1000) / 1000;
