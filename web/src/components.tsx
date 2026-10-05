import { useState } from 'preact/hooks';
import { spriteUrl } from './api';
import { useI18n } from './i18n';

export type Feedback = { ok: boolean; text: string } | null;

/**
 * Run an API change, report success or the error, and tell the app the game changed
 * (so the "unsaved changes" banner refreshes).
 */
export function useAction(onChanged: () => void) {
  const { t } = useI18n();
  const [feedback, setFeedback] = useState<Feedback>(null);
  const [busy, setBusy] = useState(false);

  const run = async <T,>(call: () => Promise<T>, apply?: (result: T) => void) => {
    setBusy(true);
    setFeedback(null);
    try {
      const result = await call();
      apply?.(result);
      setFeedback({ ok: true, text: t('common.saved') });
      onChanged();
    } catch (err) {
      setFeedback({ ok: false, text: `${t('common.error')} : ${(err as Error).message}` });
    } finally {
      setBusy(false);
    }
  };

  return { run, feedback, busy, setFeedback };
}

export function FeedbackLine({ feedback }: { feedback: Feedback }) {
  if (!feedback) return null;
  return (
    <p class={feedback.ok ? 'ok' : 'error'} role="status">
      {feedback.text}
    </p>
  );
}

export function NumberField(props: { label: string; value: string; onInput: (value: string) => void; min?: number; max?: number }) {
  return (
    <label>
      {props.label}
      <input
        type="number"
        step={1}
        min={props.min ?? 0}
        max={props.max}
        value={props.value}
        onInput={(e) => props.onInput((e.target as HTMLInputElement).value)}
      />
    </label>
  );
}

export function ItemIcon({ qualifiedId, name, size = 32 }: { qualifiedId: string; name: string; size?: number }) {
  return <img class="icon" src={spriteUrl(qualifiedId)} alt={name} title={name} width={size} height={size} loading="lazy" />;
}

export function QualitySelect({ value, onChange }: { value: number; onChange: (quality: number) => void }) {
  const { t } = useI18n();
  return (
    <label>
      {t('inv.quality')}
      <select value={value} onChange={(e) => onChange(Number((e.target as HTMLSelectElement).value))}>
        {[0, 1, 2, 4].map((q) => (
          <option key={q} value={q}>
            {t(`quality.${q}`)}
          </option>
        ))}
      </select>
    </label>
  );
}
