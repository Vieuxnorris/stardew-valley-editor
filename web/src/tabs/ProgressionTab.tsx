import { useEffect, useMemo, useState } from 'preact/hooks';
import { api, type Progression, type Quest, type SpecialOrders } from '../api';
import { FeedbackLine, NumberField, useAction } from '../components';
import { useI18n } from '../i18n';
import { CollectionsCards } from './CollectionsCards';

type Props = { onChanged: () => void };

export function ProgressionTab({ onChanged }: Props) {
  const { t } = useI18n();
  const [progress, setProgress] = useState<Progression | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    api<Progression>('GET', '/api/progression').then(setProgress, (e) => setError(e.message));
  }, []);

  if (!progress) return <p>{error ?? t('common.loading')}</p>;

  const shared = { progress, setProgress, onChanged };
  return (
    <div class="stack">
      <CollectionsCards onChanged={onChanged} />
      <UnlocksCard {...shared} />
      <MinesCard {...shared} />
      <CommunityCenterCard {...shared} />
      <MuseumCard {...shared} />
      <QuestsCard onChanged={onChanged} />
      <SpecialOrdersCard onChanged={onChanged} />
      <FlagsCard onChanged={onChanged} />
    </div>
  );
}

type CardProps = Props & { progress: Progression; setProgress: (p: Progression) => void };

function UnlocksCard({ progress, setProgress, onChanged }: CardProps) {
  const { t } = useI18n();
  const { run, feedback } = useAction(onChanged);
  return (
    <section class="card">
      <h3>{t('prog.unlocks')}</h3>
      <div class="check-grid">
        {progress.unlocks.map((u) => (
          <label key={u.id} class="check">
            <input
              type="checkbox"
              checked={u.value}
              onChange={(e) => run(() => api<Progression>('PUT', `/api/progression/unlocks/${u.id}`, { value: (e.target as HTMLInputElement).checked }), setProgress)}
            />{' '}
            {t(`unlock.${u.id}`)}
          </label>
        ))}
      </div>
      <FeedbackLine feedback={feedback} />
    </section>
  );
}

function MinesCard({ progress, setProgress, onChanged }: CardProps) {
  const { t } = useI18n();
  const { run, feedback, busy } = useAction(onChanged);
  const [mines, setMines] = useState(String(progress.mines.minesLevel));
  const [skull, setSkull] = useState(String(progress.mines.skullCavernLevel));
  useEffect(() => {
    setMines(String(progress.mines.minesLevel));
    setSkull(String(progress.mines.skullCavernLevel));
  }, [progress.mines]);

  return (
    <section class="card">
      <h3>{t('prog.mines')}</h3>
      <form
        class="fields"
        onSubmit={(e) => {
          e.preventDefault();
          run(() => api<Progression>('PATCH', '/api/progression/mines', { minesLevel: Number(mines), skullCavernLevel: Number(skull) }), setProgress);
        }}
      >
        <NumberField label={t('prog.minesLevel')} value={mines} max={120} onInput={setMines} />
        <NumberField label={t('prog.skullLevel')} value={skull} onInput={setSkull} />
        <button type="submit" disabled={busy}>
          {t('common.apply')}
        </button>
      </form>
      <FeedbackLine feedback={feedback} />
    </section>
  );
}

function CommunityCenterCard({ progress, setProgress, onChanged }: CardProps) {
  const { t } = useI18n();
  const { run, feedback, busy } = useAction(onChanged);
  return (
    <section class="card">
      <h3>{t('prog.cc')}</h3>
      <p class="muted">{t('prog.ccHint')}</p>
      <ul class="plain-list">
        {progress.communityCenter.map((area) => (
          <li key={area.area}>
            <span>{area.name}</span>
            {area.complete ? (
              <span class="ok">✓ {t('prog.done')}</span>
            ) : (
              <button disabled={busy} onClick={() => run(() => api<Progression>('POST', `/api/progression/community-center/${area.area}`), setProgress)}>
                {t('prog.complete')}
              </button>
            )}
          </li>
        ))}
      </ul>
      <FeedbackLine feedback={feedback} />
    </section>
  );
}

function MuseumCard({ progress, setProgress, onChanged }: CardProps) {
  const { t } = useI18n();
  const { run, feedback, busy, setFeedback } = useAction(onChanged);
  const { donated, donatable } = progress.museum;
  return (
    <section class="card">
      <h3>{t('prog.museum')}</h3>
      <p>
        {donated} {t('prog.donated')} {donatable}
      </p>
      <button
        disabled={busy || donated >= donatable}
        onClick={() =>
          run(
            () => api<{ added: number; progress: Progression }>('POST', '/api/progression/museum/donate-all'),
            (result) => {
              setProgress(result.progress);
              setTimeout(() => setFeedback({ ok: true, text: `${result.added} ${t('prog.donatedNow')}` }));
            },
          )
        }
      >
        {t('prog.donateAll')}
      </button>
      <FeedbackLine feedback={feedback} />
    </section>
  );
}

function QuestsCard({ onChanged }: Props) {
  const { t } = useI18n();
  const { run, feedback, busy } = useAction(onChanged);
  const [quests, setQuests] = useState<Quest[] | null>(null);
  const [catalog, setCatalog] = useState<{ id: string; title: string }[]>([]);
  const [toAdd, setToAdd] = useState('');

  useEffect(() => {
    api<Quest[]>('GET', '/api/quests').then(setQuests);
    api<{ id: string; title: string }[]>('GET', '/api/quests/catalog').then((list) => {
      setCatalog(list);
      setToAdd(list[0]?.id ?? '');
    });
  }, []);

  return (
    <section class="card">
      <h3>{t('prog.quests')}</h3>
      {quests?.length === 0 && <p class="muted">{t('prog.noQuests')}</p>}
      <ul class="plain-list">
        {quests?.map((q) => (
          <li key={`${q.index}-${q.id}`}>
            <span>
              {q.completed ? '✓ ' : ''}
              {q.name}
              {q.daysLeft !== null && (
                <span class="muted">
                  {' '}
                  · {q.daysLeft} {t('prog.daysLeft')}
                </span>
              )}
            </span>
            <span class="inline">
              {!q.completed && (
                <button disabled={busy} onClick={() => run(() => api<Quest[]>('POST', `/api/quests/${q.index}/complete`), setQuests)}>
                  {t('prog.complete')}
                </button>
              )}
              <button class="secondary" disabled={busy} onClick={() => run(() => api<Quest[]>('DELETE', `/api/quests/${q.index}`), setQuests)}>
                {t('prog.remove')}
              </button>
            </span>
          </li>
        ))}
      </ul>
      <form
        class="fields"
        onSubmit={(e) => {
          e.preventDefault();
          run(() => api<Quest[]>('POST', '/api/quests', { id: toAdd }), setQuests);
        }}
      >
        <select value={toAdd} onChange={(e) => setToAdd((e.target as HTMLSelectElement).value)} aria-label={t('prog.addQuest')}>
          {catalog.map((q) => (
            <option key={q.id} value={q.id}>
              {q.title} ({q.id})
            </option>
          ))}
        </select>
        <button type="submit" disabled={busy || !toAdd}>
          {t('prog.addQuest')}
        </button>
      </form>
      <FeedbackLine feedback={feedback} />
    </section>
  );
}

function FlagsCard({ onChanged }: Props) {
  const { t } = useI18n();
  const { run, feedback, busy } = useAction(onChanged);
  const [kind, setKind] = useState<'mail' | 'events'>('mail');
  const [flags, setFlags] = useState<string[]>([]);
  const [filter, setFilter] = useState('');
  const [newFlag, setNewFlag] = useState('');

  useEffect(() => {
    api<string[]>('GET', `/api/progression/flags/${kind}`).then(setFlags);
  }, [kind]);

  const visible = useMemo(() => flags.filter((f) => f.toLowerCase().includes(filter.toLowerCase())), [flags, filter]);
  const setFlag = (id: string, value: boolean) => run(() => api<string[]>('PUT', `/api/progression/flags/${kind}`, { id, value }), setFlags);

  return (
    <section class="card">
      <div class="card-header">
        <h3>{t('prog.flags')}</h3>
        <select value={kind} onChange={(e) => setKind((e.target as HTMLSelectElement).value as 'mail' | 'events')} aria-label={t('prog.flags')}>
          <option value="mail">{t('prog.flagMail')}</option>
          <option value="events">{t('prog.flagEvents')}</option>
        </select>
      </div>
      <p class="muted">{t('prog.flagsHint')}</p>
      <form
        class="fields"
        onSubmit={(e) => {
          e.preventDefault();
          if (newFlag.trim()) setFlag(newFlag.trim(), true);
          setNewFlag('');
        }}
      >
        <input type="search" placeholder={t('prog.filter')} value={filter} onInput={(e) => setFilter((e.target as HTMLInputElement).value)} aria-label={t('prog.filter')} />
        <input type="text" placeholder="ID" value={newFlag} onInput={(e) => setNewFlag((e.target as HTMLInputElement).value)} aria-label="ID" />
        <button type="submit" disabled={busy || !newFlag.trim()}>
          {t('prog.addFlag')}
        </button>
      </form>
      <p class="muted">
        {visible.length} / {flags.length}
      </p>
      <ul class="flag-list">
        {visible.map((f) => (
          <li key={f}>
            <code>{f}</code>
            <button class="secondary" disabled={busy} onClick={() => setFlag(f, false)} aria-label={`${t('prog.remove')} ${f}`}>
              ✕
            </button>
          </li>
        ))}
      </ul>
      <FeedbackLine feedback={feedback} />
    </section>
  );
}

function SpecialOrdersCard({ onChanged }: Props) {
  const { t } = useI18n();
  const { run, feedback, busy } = useAction(onChanged);
  const [orders, setOrders] = useState<SpecialOrders | null>(null);

  useEffect(() => {
    api<SpecialOrders>('GET', '/api/quests/special-orders').then(setOrders);
  }, []);

  if (!orders) return null;
  return (
    <section class="card">
      <h3>{t('so.title')}</h3>
      <h4>{t('so.active')}</h4>
      {orders.active.length === 0 && <p class="muted">{t('so.none')}</p>}
      <ul class="plain-list">
        {orders.active.map((o) => (
          <li key={o.index}>
            <span>
              {o.name} <span class="muted">· {o.state}</span>
              {o.objectives.map((obj, i) => (
                <span key={i} class="muted">
                  <br />
                  {obj.description} ({obj.current}/{obj.max})
                </span>
              ))}
            </span>
            {o.state === 'InProgress' && (
              <button disabled={busy} onClick={() => run(() => api<SpecialOrders>('POST', `/api/quests/special-orders/${o.index}/complete`), setOrders)}>
                {t('so.complete')}
              </button>
            )}
          </li>
        ))}
      </ul>
      <h4>{t('so.catalog')}</h4>
      <div class="check-grid">
        {orders.catalog.map((o) => (
          <label key={o.key} class="check" title={o.key}>
            <input
              type="checkbox"
              checked={o.completed}
              disabled={busy}
              onChange={(e) => run(() => api<SpecialOrders>('PUT', '/api/quests/special-orders/completed', { key: o.key, value: (e.target as HTMLInputElement).checked }), setOrders)}
            />{' '}
            {o.name} <span class="muted">({o.requester})</span>
          </label>
        ))}
      </div>
      <FeedbackLine feedback={feedback} />
    </section>
  );
}
