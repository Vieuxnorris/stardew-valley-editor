import { useEffect, useMemo, useState } from 'preact/hooks';
import { api, portraitUrl, type Villager } from '../api';
import { FeedbackLine, useAction } from '../components';
import { useI18n } from '../i18n';

export function NpcsTab({ onChanged }: { onChanged: () => void }) {
  const { t } = useI18n();
  const [villagers, setVillagers] = useState<Villager[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState('');
  const { run, feedback, busy } = useAction(onChanged);

  useEffect(() => {
    api<Villager[]>('GET', '/api/npcs').then(setVillagers, (e) => setError(e.message));
  }, []);

  const visible = useMemo(() => villagers?.filter((v) => v.displayName.toLowerCase().includes(search.toLowerCase())) ?? [], [villagers, search]);

  if (!villagers) return <p>{error ?? t('common.loading')}</p>;

  const patch = (name: string, body: object) => run(() => api<Villager[]>('PATCH', `/api/npcs/${encodeURIComponent(name)}`, body), setVillagers);
  const bulk = (action: string) => run(() => api<Villager[]>('POST', '/api/npcs/bulk', { action }), setVillagers);

  return (
    <div class="stack">
      <section class="card">
        <div class="fields">
          <input type="search" placeholder={t('npc.search')} value={search} onInput={(e) => setSearch((e.target as HTMLInputElement).value)} aria-label={t('npc.search')} />
          <button disabled={busy} onClick={() => bulk('maxHearts')}>
            ♥ {t('npc.maxHearts')}
          </button>
          <button class="secondary" disabled={busy} onClick={() => bulk('resetGifts')}>
            {t('npc.resetGifts')}
          </button>
          <button class="secondary" disabled={busy} onClick={() => bulk('talkToAll')}>
            {t('npc.talkToAll')}
          </button>
        </div>
        <p class="muted">{t('npc.hint')}</p>
        <FeedbackLine feedback={feedback} />
      </section>

      <div class="npc-grid">
        {visible.map((v) => (
          <VillagerCard key={v.name} villager={v} busy={busy} patch={(body) => patch(v.name, body)} />
        ))}
      </div>
    </div>
  );
}

function VillagerCard({ villager: v, busy, patch }: { villager: Villager; busy: boolean; patch: (body: object) => void }) {
  const { t } = useI18n();
  const [points, setPoints] = useState(String(v.points));
  useEffect(() => setPoints(String(v.points)), [v.points]);
  const fixedStatus = v.status === 'Engaged' || v.status === 'Married' || v.status === 'Divorced';

  return (
    <section class="card npc-card">
      <div class="npc-head">
        <img class="portrait" src={portraitUrl(v.name)} alt="" width={64} height={64} loading="lazy" />
        <div>
          <h3>{v.displayName}</h3>
          <p class="muted">
            {!v.met && `${t('npc.notMet')} · `}
            {fixedStatus && `${t(`npc.status.${v.status}`)} · `}
            {v.birthSeason && v.birthDay && `🎂 ${v.birthDay} ${t(`season.${v.birthSeason}`)}`}
            {v.location && ` · ${v.location}`}
          </p>
        </div>
      </div>

      <div class="hearts" role="group" aria-label={t('npc.hearts')}>
        {Array.from({ length: v.maxHearts }, (_, i) => (
          <button
            key={i}
            class={`heart ${i < v.hearts ? 'full' : ''}`}
            disabled={busy}
            title={`${i + 1} ${t('npc.hearts')}`}
            aria-label={`${i + 1} ${t('npc.hearts')}`}
            // clicking the last full heart empties it, like a rating widget
            onClick={() => patch({ hearts: i + 1 === v.hearts ? i : i + 1 })}
          >
            ♥
          </button>
        ))}
      </div>

      <div class="fields">
        <form
          class="inline"
          onSubmit={(e) => {
            e.preventDefault();
            patch({ points: Number(points) });
          }}
        >
          <label class="inline">
            {t('npc.points')}
            <input type="number" min={0} max={v.maxPoints} step={1} value={points} onInput={(e) => setPoints((e.target as HTMLInputElement).value)} />
          </label>
          <span class="muted">/ {v.maxPoints}</span>
          <button type="submit" disabled={busy || points === String(v.points)}>
            {t('common.apply')}
          </button>
        </form>
      </div>

      <div class="fields">
        <label class="inline">
          {t('npc.giftsWeek')}
          <select value={v.giftsThisWeek} disabled={busy} onChange={(e) => patch({ giftsThisWeek: Number((e.target as HTMLSelectElement).value) })}>
            {[0, 1, 2].map((n) => (
              <option key={n} value={n}>
                {n}
              </option>
            ))}
          </select>
        </label>
        <label class="check">
          <input type="checkbox" checked={v.giftsToday > 0} disabled={busy} onChange={(e) => patch({ giftsToday: (e.target as HTMLInputElement).checked ? 1 : 0 })} /> {t('npc.giftToday')}
        </label>
        <label class="check">
          <input type="checkbox" checked={v.talkedToToday} disabled={busy} onChange={(e) => patch({ talkedToToday: (e.target as HTMLInputElement).checked })} /> {t('npc.talked')}
        </label>
        {v.datable && !fixedStatus && (
          <label class="check">
            <input type="checkbox" checked={v.status === 'Dating'} disabled={busy} onChange={(e) => patch({ dating: (e.target as HTMLInputElement).checked })} /> {t('npc.dating')}
          </label>
        )}
      </div>
    </section>
  );
}
