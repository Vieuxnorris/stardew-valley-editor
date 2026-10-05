import { useEffect, useState } from 'preact/hooks';
import { api, type CatalogEntry, type CatalogPage, type Facets } from '../api';
import { ItemIcon } from '../components';
import { useI18n } from '../i18n';

const PAGE_SIZE = 60;
const SEARCH_DELAY_MS = 250;

/** Searchable list of every vanilla and modded item; calls onPick with the chosen one. */
export function ItemCatalog({ picked, onPick, fixedType }: { picked: CatalogEntry | null; onPick: (entry: CatalogEntry) => void; fixedType?: string }) {
  const { t } = useI18n();
  const [query, setQuery] = useState('');
  const [type, setType] = useState(fixedType ?? '');
  const [mod, setMod] = useState('');
  const [facets, setFacets] = useState<Facets | null>(null);
  const [page, setPage] = useState<CatalogPage | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    api<Facets>('GET', '/api/items/facets')
      .then(setFacets)
      .catch((e) => setError(e.message));
  }, []);

  const search = async (offset: number) => {
    const params = new URLSearchParams({ q: query, type, mod, offset: String(offset), limit: String(PAGE_SIZE) });
    try {
      const result = await api<CatalogPage>('GET', `/api/items?${params}`);
      setPage((previous) => (offset === 0 || !previous ? result : { total: result.total, items: [...previous.items, ...result.items] }));
      setError(null);
    } catch (e) {
      setError((e as Error).message);
    }
  };

  useEffect(() => {
    const timer = setTimeout(() => search(0), SEARCH_DELAY_MS);
    return () => clearTimeout(timer);
  }, [query, type, mod]);

  return (
    <div class="catalog">
      <div class="fields">
        <input type="search" placeholder={t('catalog.search')} value={query} onInput={(e) => setQuery((e.target as HTMLInputElement).value)} aria-label={t('catalog.search')} />
        {!fixedType && (
          <select value={type} onChange={(e) => setType((e.target as HTMLSelectElement).value)} aria-label={t('catalog.allTypes')}>
            <option value="">{t('catalog.allTypes')}</option>
            {facets?.types.map((f) => (
              <option key={f.id} value={f.id}>
                {t(`type.${f.id}`)} ({f.count})
              </option>
            ))}
          </select>
        )}
        <select value={mod} onChange={(e) => setMod((e.target as HTMLSelectElement).value)} aria-label={t('catalog.allMods')}>
          <option value="">{t('catalog.allMods')}</option>
          {facets?.mods.map((f) => (
            <option key={f.id} value={f.id}>
              {f.name ?? t('catalog.vanilla')} ({f.count})
            </option>
          ))}
        </select>
      </div>

      {error && <p class="error">{error}</p>}
      {page && (
        <>
          <p class="muted">
            {page.total} {t('catalog.results')}
          </p>
          <ul class="catalog-list">
            {page.items.map((entry) => (
              <li key={entry.qualifiedId}>
                <button class={`catalog-item ${picked?.qualifiedId === entry.qualifiedId ? 'active' : ''}`} onClick={() => onPick(entry)} title={`${entry.qualifiedId}${entry.modName ? ` · ${entry.modName}` : ''}`}>
                  <ItemIcon qualifiedId={entry.qualifiedId} name={entry.name} />
                  <span>{entry.name}</span>
                </button>
              </li>
            ))}
          </ul>
          {page.items.length < page.total && (
            <button class="secondary" onClick={() => search(page.items.length)}>
              {t('catalog.more')}
            </button>
          )}
        </>
      )}
    </div>
  );
}
