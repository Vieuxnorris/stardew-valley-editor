import { useCallback, useEffect, useState } from 'preact/hooks';
import { api, ApiError, hasToken, type HistoryEntry, type Status } from './api';
import { useI18n } from './i18n';
import { AnimalsTab } from './tabs/AnimalsTab';
import { ChestsTab } from './tabs/ChestsTab';
import { FarmTab } from './tabs/FarmTab';
import { FishingTab } from './tabs/FishingTab';
import { HistoryPanel } from './tabs/HistoryPanel';
import { InventoryTab } from './tabs/InventoryTab';
import { MonstersTab } from './tabs/MonstersTab';
import { NpcsTab } from './tabs/NpcsTab';
import { PlayerTab } from './tabs/PlayerTab';
import { ProgressionTab } from './tabs/ProgressionTab';
import { RulesTab } from './tabs/RulesTab';
import { WorldTab } from './tabs/WorldTab';

const STATUS_POLL_MS = 2000;

type Connection = { kind: 'loading' } | { kind: 'offline' } | { kind: 'unauthorized' } | { kind: 'ok'; status: Status };

const TABS = ['player', 'inventory', 'chests', 'npcs', 'world', 'farm', 'animals', 'progression', 'fishing', 'monsters', 'rules'] as const;
type Tab = (typeof TABS)[number];

export function App() {
  const { t, lang, setLang } = useI18n();
  const [connection, setConnection] = useState<Connection>({ kind: 'loading' });
  // ?tab=farm opens a tab directly
  const [tab, setTab] = useState<Tab>(() => {
    const fromUrl = new URLSearchParams(location.search).get('tab');
    return (TABS as readonly string[]).includes(fromUrl ?? '') ? (fromUrl as Tab) : 'player';
  });
  const [historyOpen, setHistoryOpen] = useState(false);
  const [history, setHistory] = useState<HistoryEntry[]>([]);
  const [undoing, setUndoing] = useState(false);
  const [undoMessage, setUndoMessage] = useState<string | null>(null);
  // bumped after an undo, to reload the open tab from the game
  const [reloadKey, setReloadKey] = useState(0);

  const refreshStatus = useCallback(async () => {
    try {
      setConnection({ kind: 'ok', status: await api<Status>('GET', '/api/status') });
    } catch (e) {
      setConnection({ kind: e instanceof ApiError && e.status === 401 ? 'unauthorized' : 'offline' });
    }
  }, []);

  useEffect(() => {
    refreshStatus();
    const timer = setInterval(refreshStatus, STATUS_POLL_MS);
    return () => clearInterval(timer);
  }, [refreshStatus]);

  const status = connection.kind === 'ok' ? connection.status : null;

  const loadHistory = useCallback(() => api<HistoryEntry[]>('GET', '/api/history').then(setHistory, () => {}), []);
  useEffect(() => {
    if (!historyOpen) return;
    loadHistory();
    const timer = setInterval(loadHistory, STATUS_POLL_MS);
    return () => clearInterval(timer);
  }, [historyOpen, loadHistory]);

  const undo = useCallback(async () => {
    setUndoing(true);
    try {
      setHistory(await api<HistoryEntry[]>('POST', '/api/history/undo'));
      setUndoMessage(t('history.undoneMessage'));
      setReloadKey((k) => k + 1);
      refreshStatus();
    } catch (e) {
      setUndoMessage(`${t('common.error')} : ${(e as Error).message}`);
    } finally {
      setUndoing(false);
      setTimeout(() => setUndoMessage(null), 3000);
    }
  }, [refreshStatus, t]);

  // Ctrl+Z undoes, except while typing in a field (where it undoes the typing)
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      const target = e.target as HTMLElement;
      if (!(e.ctrlKey || e.metaKey) || e.key.toLowerCase() !== 'z' || e.shiftKey) return;
      if (target.closest('input, textarea, select, [contenteditable]')) return;
      e.preventDefault();
      undo();
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [undo]);

  return (
    <div class="app">
      <header class="topbar">
        <h1>{t('app.title')}</h1>
        {status?.worldReady && <span class="farm">{status.farmName}</span>}
        <div class="spacer" />
        {status?.worldReady && (
          <button class="secondary small" onClick={() => setHistoryOpen(!historyOpen)} aria-pressed={historyOpen}>
            🕘 {t('history.title')}
          </button>
        )}
        <select value={lang} onChange={(e) => setLang((e.target as HTMLSelectElement).value as 'fr' | 'en')} aria-label="Language">
          <option value="fr">FR</option>
          <option value="en">EN</option>
        </select>
      </header>

      {!hasToken || connection.kind === 'unauthorized' ? (
        <p class="banner error">{t('status.noToken')}</p>
      ) : connection.kind === 'offline' ? (
        <p class="banner error">{t('status.offline')}</p>
      ) : connection.kind === 'loading' ? (
        <p class="banner">{t('common.loading')}</p>
      ) : !status?.worldReady ? (
        <p class="banner">{t('status.noSave')}</p>
      ) : (
        <>
          {status.unsavedChanges && <p class="banner warn">{t('status.unsaved')}</p>}
          <nav class="tabs">
            {TABS.map((id) => (
              <button key={id} class={id === tab ? 'active' : ''} onClick={() => setTab(id)}>
                {t(`tab.${id}`)}
              </button>
            ))}
          </nav>
          {undoMessage && <p class="banner">{undoMessage}</p>}
          {historyOpen && <HistoryPanel entries={history} busy={undoing} onUndo={undo} onClose={() => setHistoryOpen(false)} />}
          <main key={`${status.saveName}-${reloadKey}`}>
            {tab === 'player' && <PlayerTab onChanged={refreshStatus} />}
            {tab === 'inventory' && <InventoryTab onChanged={refreshStatus} />}
            {tab === 'chests' && <ChestsTab onChanged={refreshStatus} />}
            {tab === 'npcs' && <NpcsTab onChanged={refreshStatus} />}
            {tab === 'world' && <WorldTab onChanged={refreshStatus} />}
            {tab === 'farm' && <FarmTab onChanged={refreshStatus} />}
            {tab === 'animals' && <AnimalsTab onChanged={refreshStatus} />}
            {tab === 'progression' && <ProgressionTab onChanged={refreshStatus} />}
            {tab === 'fishing' && <FishingTab onChanged={refreshStatus} />}
            {tab === 'monsters' && <MonstersTab onChanged={refreshStatus} />}
            {tab === 'rules' && <RulesTab onChanged={refreshStatus} />}
          </main>
        </>
      )}

      {status && (
        <footer>
          Stardew Valley {status.gameVersion} · Valley Editor {status.modVersion}
        </footer>
      )}
    </div>
  );
}
