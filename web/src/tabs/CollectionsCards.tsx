import { useEffect, useState } from 'preact/hooks';
import { api, powerSpriteUrl, type Collections } from '../api';
import { FeedbackLine, useAction } from '../components';
import { useI18n } from '../i18n';

/** The wallet's special items and powers, and the collections pages, sharing one snapshot. */
export function CollectionsCards({ onChanged }: { onChanged: () => void }) {
  const { t } = useI18n();
  const [data, setData] = useState<Collections | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    api<Collections>('GET', '/api/collections').then(setData, (e) => setError(e.message));
  }, []);

  if (!data) return <p>{error ?? t('common.loading')}</p>;
  return (
    <>
      <PowersCard data={data} setData={setData} onChanged={onChanged} />
      <CollectionsCard data={data} setData={setData} onChanged={onChanged} />
    </>
  );
}

type CardProps = { data: Collections; setData: (c: Collections) => void; onChanged: () => void };

function PowersCard({ data, setData, onChanged }: CardProps) {
  const { t } = useI18n();
  const { run, feedback, busy } = useAction(onChanged);
  const set = (id: string, value: boolean) => run(() => api<Collections>('PUT', `/api/collections/powers/${encodeURIComponent(id)}`, { value }), setData);
  const missing = data.powers.filter((p) => !p.unlocked && p.editable);

  const unlockAll = async () => {
    for (const p of missing) await api<Collections>('PUT', `/api/collections/powers/${encodeURIComponent(p.id)}`, { value: true });
    return api<Collections>('GET', '/api/collections');
  };

  return (
    <section class="card">
      <div class="card-header">
        <h3>
          {t('powers.title')}{' '}
          <span class="muted">
            {data.powers.filter((p) => p.unlocked).length}/{data.powers.length}
          </span>
        </h3>
        <button disabled={busy || missing.length === 0} onClick={() => run(unlockAll, setData)}>
          ✨ {t('powers.unlockAll')}
        </button>
      </div>
      <p class="muted">{t('powers.hint')}</p>
      <ul class="power-grid">
        {data.powers.map((p) => (
          <li key={p.id}>
            <button
              class={`power-tile ${p.unlocked ? 'unlocked' : ''}`}
              disabled={busy || !p.editable}
              onClick={() => set(p.id, !p.unlocked)}
              title={`${p.name}\n${p.description}${p.editable ? '' : `\n(${t('powers.notEditable')} : ${p.condition})`}`}
              aria-pressed={p.unlocked}
            >
              <img src={powerSpriteUrl(p.id)} alt="" width={32} height={32} />
              <span>{p.name}</span>
            </button>
          </li>
        ))}
      </ul>
      <FeedbackLine feedback={feedback} />
    </section>
  );
}

function CollectionsCard({ data, setData, onChanged }: CardProps) {
  const { t } = useI18n();
  const { run, feedback, busy, setFeedback } = useAction(onChanged);

  const complete = (ids: string[]) =>
    run(
      async () => {
        let added = 0;
        let last: Collections = data;
        for (const id of ids) {
          const r = await api<{ added: number; collections: Collections }>('POST', `/api/collections/${id}/complete`);
          added += r.added;
          last = r.collections;
        }
        return { added, last };
      },
      (r) => {
        setData(r.last);
        setTimeout(() => setFeedback({ ok: true, text: `${r.added} ${t('collections.added')}` }));
      },
    );

  const incomplete = data.categories.filter((c) => c.done < c.total).map((c) => c.id);

  return (
    <section class="card">
      <div class="card-header">
        <h3>{t('collections.title')}</h3>
        <button disabled={busy || incomplete.length === 0} onClick={() => complete(incomplete)}>
          🏆 {t('collections.completeAll')}
        </button>
      </div>
      <p class="muted">{t('collections.hint')}</p>
      <table class="table">
        <tbody>
          {data.categories.map((c) => (
            <tr key={c.id}>
              <th scope="row">{t(`collections.${c.id}`)}</th>
              <td>
                <progress max={c.total} value={c.done} /> {c.done}/{c.total}
              </td>
              <td>
                {c.done < c.total ? (
                  <button class="small secondary" disabled={busy} onClick={() => complete([c.id])}>
                    {t('collections.complete')}
                  </button>
                ) : (
                  <span class="badge">✓</span>
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
      <FeedbackLine feedback={feedback} />
    </section>
  );
}
