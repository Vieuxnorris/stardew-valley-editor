import { useEffect, useState } from 'preact/hooks';
import { api, type MachineInfo, type OutputRule } from '../api';
import { FeedbackLine, ItemIcon, useAction } from '../components';
import { useI18n } from '../i18n';
import { ItemCatalog } from './ItemCatalog';

/** Speed choices for a machine type: time multipliers (0 = instant), or null to follow the global rule. */
const SPEEDS: (number | null)[] = [null, 2, 1, 0.5, 0.25, 0.1, 0];
const QUALITIES = [0, 1, 2, 4];

/** Where an output change applies: the batch in the machine now, every batch of this machine, or of every machine of its type. */
type Scope = 'now' | 'machine' | 'type';

const formatMinutes = (minutes: number) => (minutes >= 60 ? `${Math.floor(minutes / 60)} h ${String(minutes % 60).padStart(2, '0')}` : `${minutes} min`);

/** A placed machine: what it's making, finish now, change the output (once or for every batch), and its type's speed. */
export function MachinePanel({ id, version = 0, onChanged, onUpdated }: { id: string; version?: number; onChanged: () => void; onUpdated: () => void }) {
  const { t } = useI18n();
  const { run, feedback, busy } = useAction(onChanged);
  const [machine, setMachine] = useState<MachineInfo | null>(null);
  const [error, setError] = useState<string | null>(null);
  const path = `/api/machines/${encodeURIComponent(id)}`;

  useEffect(() => {
    api<MachineInfo>('GET', path).then(setMachine, (e) => setError(e.message));
  }, [path, version]);

  if (!machine) return <p>{error ?? t('common.loading')}</p>;

  const apply = (call: () => Promise<MachineInfo>) =>
    run(call, (m) => {
      setMachine(m);
      onUpdated();
    });
  const setSpeed = (multiplier: number | null) =>
    run(
      () => api<{ multiplier: number }>('PUT', `/api/machines/types/${encodeURIComponent(machine.qualifiedId)}/speed`, { multiplier }),
      () => api<MachineInfo>('GET', path).then(setMachine),
    );

  return (
    <div class="building-editor stack">
      <h4 class="inline">
        <ItemIcon qualifiedId={machine.qualifiedId} name={machine.name} size={32} /> {machine.name}
      </h4>
      <p>
        {machine.ready ? (
          <span class="badge">{t('machine.ready')}</span>
        ) : machine.working ? (
          <span class="badge">
            {t('machine.working')} : {formatMinutes(machine.minutesUntilReady)}
          </span>
        ) : (
          <span class="muted">{t('machine.idle')}</span>
        )}
      </p>
      {machine.input && (
        <p class="inline muted">
          {t('machine.input')} : <ItemIcon qualifiedId={machine.input.qualifiedId} name={machine.input.name} size={16} /> {machine.input.name}
        </p>
      )}
      {machine.output && (
        <p class="inline">
          {t('machine.output')} : <ItemIcon qualifiedId={machine.output.qualifiedId} name={machine.output.name} size={24} /> {machine.output.name} ×{machine.output.stack}
          {machine.output.quality > 0 && <span class="badge">{t(`quality.${machine.output.quality}`)}</span>}
        </p>
      )}
      {machine.working && (
        <button disabled={busy} onClick={() => apply(() => api<MachineInfo>('POST', `${path}/finish`))}>
          ⚡ {t('machine.finish')}
        </button>
      )}

      <RuleLine label={t('machine.ruleMachine')} rule={machine.ownRule} busy={busy} onClear={() => apply(() => api<MachineInfo>('PUT', `${path}/output-rule`, { scope: 'machine', clear: true }))} />
      <RuleLine label={t('machine.ruleType')} rule={machine.typeRule} busy={busy} onClear={() => apply(() => api<MachineInfo>('PUT', `${path}/output-rule`, { scope: 'type', clear: true }))} />

      <OutputForm key={machine.qualifiedId} machine={machine} path={path} busy={busy} apply={apply} />

      <label>
        {t('machine.speed')}
        <select value={machine.customSpeed ? String(machine.speed) : ''} disabled={busy} onChange={(e) => setSpeed((e.target as HTMLSelectElement).value === '' ? null : Number((e.target as HTMLSelectElement).value))}>
          {SPEEDS.map((s) => (
            <option key={String(s)} value={s === null ? '' : String(s)}>
              {s === null ? `${t('machine.speedGlobal')} (×${machine.globalSpeed})` : s === 0 ? t('machine.instant') : `×${s}`}
            </option>
          ))}
        </select>
      </label>
      <p class="muted">{t('machine.speedHint')}</p>
      <FeedbackLine feedback={feedback} />
    </div>
  );
}

/** An active "every batch" rule, with a button to remove it. */
function RuleLine({ label, rule, busy, onClear }: { label: string; rule: OutputRule | null; busy: boolean; onClear: () => void }) {
  const { t } = useI18n();
  if (!rule) return null;
  return (
    <p class="rule-line inline">
      <span class="badge">{label}</span>
      {rule.itemId && <ItemIcon qualifiedId={rule.itemId} name={rule.itemId} size={20} />}
      {rule.stack !== null && <span>×{rule.stack}</span>}
      {rule.quality !== null && <span>{t(`quality.${rule.quality}`)}</span>}
      <button class="secondary small" disabled={busy} onClick={onClear}>
        ✕ {t('machine.ruleClear')}
      </button>
    </p>
  );
}

function OutputForm({ machine, path, busy, apply }: { machine: MachineInfo; path: string; busy: boolean; apply: (call: () => Promise<MachineInfo>) => void }) {
  const { t } = useI18n();
  const initial = machine.ownRule ?? machine.typeRule;
  const [itemId, setItemId] = useState<string>(initial?.itemId ?? '');
  const [stack, setStack] = useState(initial?.stack?.toString() ?? '');
  const [quality, setQuality] = useState(initial?.quality?.toString() ?? '');
  const [scope, setScope] = useState<Scope>('machine');
  const [browse, setBrowse] = useState(false);

  const submit = (e: Event) => {
    e.preventDefault();
    if (scope === 'now') {
      // the batch in the machine now: a concrete item is needed
      const id = itemId || machine.output?.qualifiedId;
      apply(() => api<MachineInfo>('PUT', `${path}/output`, { qualifiedId: id ?? null, stack: stack === '' ? (machine.output?.stack ?? 1) : Number(stack), quality: quality === '' ? (machine.output?.quality ?? 0) : Number(quality) }));
    } else {
      apply(() =>
        api<MachineInfo>('PUT', `${path}/output-rule`, {
          scope,
          itemId: itemId || null,
          stack: stack === '' ? null : Number(stack),
          quality: quality === '' ? null : Number(quality),
        }),
      );
    }
  };

  return (
    <form class="stack output-form" onSubmit={submit}>
      <h5>{t('machine.setOutput')}</h5>
      <div class="output-grid">
        <button type="button" class={`output-choice ${itemId === '' ? 'active' : ''}`} onClick={() => setItemId('')} title={t('machine.keepItem')}>
          <span class="muted">{t('machine.keepItemShort')}</span>
        </button>
        {machine.possibleOutputs.map((o) => (
          <button type="button" key={o.id} class={`output-choice ${itemId === o.id ? 'active' : ''}`} onClick={() => setItemId(o.id)} title={o.name}>
            <ItemIcon qualifiedId={o.id} name={o.name} size={32} />
          </button>
        ))}
        {itemId && !machine.possibleOutputs.some((o) => o.id === itemId) && (
          <button type="button" class="output-choice active" title={itemId}>
            <ItemIcon qualifiedId={itemId} name={itemId} size={32} />
          </button>
        )}
      </div>
      <button type="button" class="secondary small" onClick={() => setBrowse(!browse)}>
        🔎 {browse ? t('machine.hideCatalog') : t('machine.otherItem')}
      </button>
      {browse && (
        <div class="output-catalog">
          <ItemCatalog
            picked={null}
            onPick={(entry) => {
              setItemId(entry.qualifiedId);
              setBrowse(false);
            }}
            fixedType="(O)"
          />
        </div>
      )}
      <div class="fields">
        <label>
          {t('inv.stack')}
          <input type="number" min={1} value={stack} placeholder={t('machine.gameValue')} onInput={(e) => setStack((e.target as HTMLInputElement).value)} />
        </label>
        <label>
          {t('inv.quality')}
          <select value={quality} onChange={(e) => setQuality((e.target as HTMLSelectElement).value)}>
            <option value="">{t('machine.gameValue')}</option>
            {QUALITIES.map((q) => (
              <option key={q} value={String(q)}>
                {t(`quality.${q}`)}
              </option>
            ))}
          </select>
        </label>
      </div>
      <fieldset class="scope">
        <legend>{t('machine.scope')}</legend>
        {(['now', 'machine', 'type'] as Scope[]).map((s) => (
          <label key={s} class="check">
            <input type="radio" name="scope" checked={scope === s} onChange={() => setScope(s)} /> {t(`machine.scope.${s}`)}
          </label>
        ))}
      </fieldset>
      <div class="fields">
        <button type="submit" disabled={busy || (scope === 'now' && !itemId && !machine.output)}>
          {t('common.apply')}
        </button>
        {machine.output && (
          <button type="button" class="secondary" disabled={busy} onClick={() => apply(() => api<MachineInfo>('PUT', `${path}/output`, { qualifiedId: null }))}>
            {t('machine.empty')}
          </button>
        )}
      </div>
    </form>
  );
}
