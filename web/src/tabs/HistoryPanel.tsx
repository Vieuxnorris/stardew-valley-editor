import type { HistoryEntry } from '../api';
import { useI18n } from '../i18n';

/** Which tab a change came from, by its API path. */
const AREAS: [string, string][] = [
  ['/api/player', 'player'],
  ['/api/inventory', 'inventory'],
  ['/api/chests', 'chests'],
  ['/api/npcs', 'npcs'],
  ['/api/world', 'world'],
  ['/api/farm', 'farm'],
  ['/api/animals', 'animals'],
  ['/api/pets', 'animals'],
  ['/api/progression', 'progression'],
  ['/api/collections', 'progression'],
  ['/api/quests', 'progression'],
  ['/api/special-orders', 'progression'],
  ['/api/fishing', 'fishing'],
  ['/api/monsters', 'monsters'],
  ['/api/rules', 'rules'],
];

type Props = { entries: HistoryEntry[]; busy: boolean; onUndo: () => void; onClose: () => void };

export function HistoryPanel({ entries, busy, onUndo, onClose }: Props) {
  const { t } = useI18n();
  const canUndo = entries.some((e) => e.undoable && !e.undone);
  const area = (path: string) => AREAS.find(([prefix]) => path.startsWith(prefix))?.[1];

  return (
    <aside class="history-panel card" aria-label={t('history.title')}>
      <div class="card-header">
        <h3>{t('history.title')}</h3>
        <button class="secondary small" onClick={onClose} aria-label={t('history.close')}>
          ✕
        </button>
      </div>
      <button disabled={busy || !canUndo} onClick={onUndo}>
        ↶ {t('history.undo')} <span class="muted">(Ctrl+Z)</span>
      </button>
      <p class="muted">{t('history.hint')}</p>
      {entries.length === 0 ? (
        <p class="muted">{t('history.empty')}</p>
      ) : (
        <ol class="history-list">
          {entries.map((e) => (
            <li key={e.id} class={e.undone ? 'undone' : ''}>
              <span class="muted">{e.time}</span> <strong>{area(e.path) ? t(`tab.${area(e.path)}`) : e.path}</strong>
              {e.undone ? <span class="badge">{t('history.undone')}</span> : !e.undoable && <span class="badge muted">{t('history.final')}</span>}
              <div class="history-detail">
                <code>
                  {e.method} {e.path}
                </code>
                {e.summary && <code class="muted"> {e.summary}</code>}
              </div>
            </li>
          ))}
        </ol>
      )}
    </aside>
  );
}
