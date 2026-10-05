import { useEffect, useState } from 'preact/hooks';
import { api, type MachineInfo } from '../api';
import { FeedbackLine, ItemIcon, NumberField, QualitySelect, useAction } from '../components';
import { useI18n } from '../i18n';

/** Speed choices for a machine type: time multipliers (0 = instant), or null to follow the global rule. */
const SPEEDS: (number | null)[] = [null, 2, 1, 0.5, 0.25, 0.1, 0];

const formatMinutes = (minutes: number) => (minutes >= 60 ? `${Math.floor(minutes / 60)} h ${String(minutes % 60).padStart(2, '0')}` : `${minutes} min`);

/** A placed machine: what it's making, finish now, change the output, and the speed of every machine of its type. */
export function MachinePanel({ id, onChanged, onUpdated }: { id: string; onChanged: () => void; onUpdated: () => void }) {
  const { t } = useI18n();
  const { run, feedback, busy } = useAction(onChanged);
  const [machine, setMachine] = useState<MachineInfo | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [output, setOutput] = useState('');
  const [stack, setStack] = useState('1');
  const [quality, setQuality] = useState(0);
  const path = `/api/machines/${encodeURIComponent(id)}`;

  const load = (m: MachineInfo) => {
    setMachine(m);
    setOutput(m.output?.qualifiedId ?? '');
    setStack(String(m.output?.stack ?? 1));
    setQuality(m.output?.quality ?? 0);
  };
  useEffect(() => {
    api<MachineInfo>('GET', path).then(load, (e) => setError(e.message));
  }, [path]);

  if (!machine) return <p>{error ?? t('common.loading')}</p>;

  const apply = (call: () => Promise<MachineInfo>) =>
    run(call, (m) => {
      load(m);
      onUpdated();
    });
  const setSpeed = (multiplier: number | null) =>
    run(
      () => api<{ multiplier: number }>('PUT', `/api/machines/types/${encodeURIComponent(machine.qualifiedId)}/speed`, { multiplier }),
      () => api<MachineInfo>('GET', path).then(load),
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
        </p>
      )}
      {machine.working && (
        <button disabled={busy} onClick={() => apply(() => api<MachineInfo>('POST', `${path}/finish`))}>
          ⚡ {t('machine.finish')}
        </button>
      )}

      <form
        class="stack"
        onSubmit={(e) => {
          e.preventDefault();
          apply(() => api<MachineInfo>('PUT', `${path}/output`, { qualifiedId: output || null, stack: Number(stack), quality }));
        }}
      >
        <label>
          <span class="inline">
            {t('machine.setOutput')} {output && <ItemIcon qualifiedId={output} name={output} size={16} />}
          </span>
          <input type="text" value={output} placeholder="(O)348" onInput={(e) => setOutput((e.target as HTMLInputElement).value)} />
        </label>
        <div class="fields">
          <NumberField label={t('inv.stack')} value={stack} min={1} onInput={setStack} />
          <QualitySelect value={quality} onChange={setQuality} />
        </div>
        <div class="fields">
          <button type="submit" disabled={busy}>
            {t('common.apply')}
          </button>
          {machine.output && (
            <button type="button" class="secondary" disabled={busy} onClick={() => apply(() => api<MachineInfo>('PUT', `${path}/output`, { qualifiedId: null }))}>
              {t('machine.empty')}
            </button>
          )}
        </div>
      </form>

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
