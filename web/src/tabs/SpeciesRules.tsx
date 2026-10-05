import { useEffect, useState } from 'preact/hooks';
import { api, type AnimalSpecies } from '../api';
import { FeedbackLine, useAction } from '../components';
import { useI18n } from '../i18n';

/** Settings shared by every animal of a species: days between produce, days to grow up, always deluxe. */
export function SpeciesRules({ type, onChanged }: { type: string; onChanged: () => void }) {
  const { t } = useI18n();
  const { run, feedback, busy } = useAction(onChanged);
  const [species, setSpecies] = useState<AnimalSpecies | null>(null);
  const [produce, setProduce] = useState('');
  const [mature, setMature] = useState('');
  const [deluxe, setDeluxe] = useState(false);
  const path = `/api/animals/types/${encodeURIComponent(type)}`;

  const load = (s: AnimalSpecies) => {
    setSpecies(s);
    setProduce(s.rule?.daysToProduce?.toString() ?? '');
    setMature(s.rule?.daysToMature?.toString() ?? '');
    setDeluxe(s.rule?.alwaysDeluxe ?? false);
  };
  useEffect(() => {
    api<AnimalSpecies>('GET', path).then(load, () => setSpecies(null));
  }, [path]);

  if (!species) return null;
  const num = (v: string) => (v === '' ? null : Number(v));

  return (
    <details class="species-rules">
      <summary>
        {t('species.title')} : {species.name}
        {species.rule && <span class="badge">{t('mon.edited')}</span>}
      </summary>
      <form
        class="stack"
        onSubmit={(e) => {
          e.preventDefault();
          run(() => api<AnimalSpecies>('PUT', path, { daysToProduce: num(produce), daysToMature: num(mature), alwaysDeluxe: deluxe }), load);
        }}
      >
        <label>
          <span>
            {t('species.daysToProduce')} <span class="muted">({t('rules.vanilla')} : {species.baseDaysToProduce})</span>
          </span>
          <input type="number" min={1} value={produce} placeholder={String(species.baseDaysToProduce)} onInput={(e) => setProduce((e.target as HTMLInputElement).value)} />
        </label>
        <label>
          <span>
            {t('species.daysToMature')} <span class="muted">({t('rules.vanilla')} : {species.baseDaysToMature})</span>
          </span>
          <input type="number" min={0} value={mature} placeholder={String(species.baseDaysToMature)} onInput={(e) => setMature((e.target as HTMLInputElement).value)} />
        </label>
        {species.hasDeluxe && (
          <label class="check">
            <input type="checkbox" checked={deluxe} onChange={(e) => setDeluxe((e.target as HTMLInputElement).checked)} /> {t('species.alwaysDeluxe')}
          </label>
        )}
        <div class="fields">
          <button type="submit" disabled={busy}>
            {t('common.apply')}
          </button>
          {species.rule && (
            <button type="button" class="secondary" disabled={busy} onClick={() => run(() => api<AnimalSpecies>('PUT', path, {}), load)}>
              {t('mon.reset')}
            </button>
          )}
        </div>
        <p class="muted">{t('species.hint')}</p>
      </form>
      <FeedbackLine feedback={feedback} />
    </details>
  );
}
