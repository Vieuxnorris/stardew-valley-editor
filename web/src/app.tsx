import { useCallback, useEffect, useState } from 'preact/hooks';
import { api, ApiError, hasToken, type Status } from './api';
import { useI18n } from './i18n';
import { InventoryTab } from './tabs/InventoryTab';
import { NpcsTab } from './tabs/NpcsTab';
import { PlayerTab } from './tabs/PlayerTab';
import { ProgressionTab } from './tabs/ProgressionTab';
import { RulesTab } from './tabs/RulesTab';
import { WorldTab } from './tabs/WorldTab';

const STATUS_POLL_MS = 2000;

type Connection = { kind: 'loading' } | { kind: 'offline' } | { kind: 'unauthorized' } | { kind: 'ok'; status: Status };

const TABS = ['player', 'inventory', 'npcs', 'world', 'progression', 'rules'] as const;
type Tab = (typeof TABS)[number];

export function App() {
  const { t, lang, setLang } = useI18n();
  const [connection, setConnection] = useState<Connection>({ kind: 'loading' });
  const [tab, setTab] = useState<Tab>('player');

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

  return (
    <div class="app">
      <header class="topbar">
        <h1>{t('app.title')}</h1>
        {status?.worldReady && <span class="farm">{status.farmName}</span>}
        <div class="spacer" />
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
          <main key={status.saveName}>
            {tab === 'player' && <PlayerTab onChanged={refreshStatus} />}
            {tab === 'inventory' && <InventoryTab onChanged={refreshStatus} />}
            {tab === 'npcs' && <NpcsTab onChanged={refreshStatus} />}
            {tab === 'world' && <WorldTab onChanged={refreshStatus} />}
            {tab === 'progression' && <ProgressionTab onChanged={refreshStatus} />}
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
