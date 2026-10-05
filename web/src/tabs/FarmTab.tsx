import { useEffect, useState } from 'preact/hooks';
import { api, buildingSpriteUrl, farmMapUrl, type Animal, type BuildingInfo, type Farm, type FieldStats, type LocationView } from '../api';
import { AnimalCard } from './AnimalsTab';
import { ChestEditor } from './ChestsTab';
import { MachinePanel } from './MachinePanel';
import { FeedbackLine, useAction } from '../components';
import { useI18n } from '../i18n';

type CropAction = 'water' | 'ripen' | 'clearDead' | 'trees';
const CROP_ACTIONS: CropAction[] = ['water', 'ripen', 'clearDead', 'trees'];

export function FarmTab({ onChanged }: { onChanged: () => void }) {
  const { t } = useI18n();
  const [farm, setFarm] = useState<Farm | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    api<Farm>('GET', '/api/farm').then(setFarm, (e) => setError(e.message));
  }, []);

  if (!farm) return <p>{error ?? t('common.loading')}</p>;
  const shared = { farm, setFarm, onChanged };

  return (
    <div class="stack">
      <FieldsCard {...shared} />
      <HouseCard {...shared} />
      <BuildingsCard {...shared} />
    </div>
  );
}

type CardProps = { farm: Farm; setFarm: (farm: Farm) => void; onChanged: () => void };

function FieldsCard({ farm, setFarm, onChanged }: CardProps) {
  const { t } = useI18n();
  const { run, feedback, busy, setFeedback } = useAction(onChanged);

  const act = (action: CropAction, location?: string) =>
    run(
      () => api<{ changed: number; farm: Farm }>('POST', '/api/farm/crops', { action, location }),
      (r) => {
        setFarm(r.farm);
        setTimeout(() => setFeedback({ ok: true, text: `${r.changed} ${t('farm.changed')}` }));
      },
    );

  const finishMachines = (location?: string) =>
    run(
      () => api<{ finished: number }>('POST', '/api/machines/finish-all', { location }),
      (r) => setTimeout(() => setFeedback({ ok: true, text: `${r.finished} ${t('machine.finishedCount')}` })),
    );

  const buttons = (location?: string) => [
    ...CROP_ACTIONS.map((a) => (
      <button key={a} class="secondary small" disabled={busy} onClick={() => act(a, location)}>
        {t(`farm.action.${a}`)}
      </button>
    )),
    <button key="machines" class="secondary small" disabled={busy} onClick={() => finishMachines(location)}>
      ⚙ {t('machine.finishAll')}
    </button>,
  ];

  const totals = farm.fields.reduce(
    (sum, f) => ({ crops: sum.crops + f.crops, dry: sum.dry + f.dry, ready: sum.ready + f.ready, dead: sum.dead + f.dead, youngTrees: sum.youngTrees + f.youngTrees }),
    { crops: 0, dry: 0, ready: 0, dead: 0, youngTrees: 0 },
  );

  return (
    <section class="card">
      <h3>{t('farm.fields')}</h3>
      <p class="muted">{t('farm.fieldsHint')}</p>
      <div class="fields">
        <strong>{t('farm.everywhere')}</strong>
        {buttons()}
      </div>
      <FeedbackLine feedback={feedback} />
      {farm.fields.length === 0 ? (
        <p class="muted">{t('farm.noFields')}</p>
      ) : (
        <div class="table-scroll">
          <table class="table">
            <thead>
              <tr>
                <th>{t('farm.location')}</th>
                <th>{t('farm.crops')}</th>
                <th>{t('farm.dry')}</th>
                <th>{t('farm.ready')}</th>
                <th>{t('farm.dead')}</th>
                <th>{t('farm.youngTrees')}</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {farm.fields.map((f: FieldStats) => (
                <tr key={f.location}>
                  <th scope="row">{f.displayName}</th>
                  <td>{f.crops}</td>
                  <td class={f.dry ? 'warn-text' : ''}>{f.dry}</td>
                  <td>{f.ready}</td>
                  <td class={f.dead ? 'warn-text' : ''}>{f.dead}</td>
                  <td>{f.youngTrees}</td>
                  <td class="row-actions">{buttons(f.location)}</td>
                </tr>
              ))}
              <tr class="total">
                <th scope="row">{t('farm.total')}</th>
                <td>{totals.crops}</td>
                <td>{totals.dry}</td>
                <td>{totals.ready}</td>
                <td>{totals.dead}</td>
                <td>{totals.youngTrees}</td>
                <td />
              </tr>
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}

function HouseCard({ farm, setFarm, onChanged }: CardProps) {
  const { t } = useI18n();
  const { run, feedback, busy } = useAction(onChanged);
  const { level, maxLevel, daysUntilUpgrade } = farm.house;
  const pending = daysUntilUpgrade > 0;
  const act = (action: 'upgrade' | 'cancel') => run(() => api<Farm>('POST', '/api/farm/house', { action }), setFarm);

  return (
    <section class="card">
      <h3>{t('farm.house')}</h3>
      <p>
        {t('farm.houseLevel')} : <strong>{t(`farm.houseLevel.${level}`)}</strong> ({level}/{maxLevel})
        {pending && (
          <span class="badge">
            {t('farm.houseUpgrading')} {daysUntilUpgrade} {t('farm.days')}
          </span>
        )}
      </p>
      <div class="fields">
        {!pending && level < maxLevel && (
          <button disabled={busy} onClick={() => act('upgrade')}>
            {t('farm.houseUpgrade')} → {t(`farm.houseLevel.${level + 1}`)}
          </button>
        )}
        {pending && daysUntilUpgrade > 1 && (
          <button disabled={busy} onClick={() => act('upgrade')}>
            {t('farm.houseTomorrow')}
          </button>
        )}
        {pending && (
          <button class="secondary" disabled={busy} onClick={() => act('cancel')}>
            {t('farm.houseCancel')}
          </button>
        )}
      </div>
      <p class="muted">{t('farm.houseHint')}</p>
      <FeedbackLine feedback={feedback} />
    </section>
  );
}

const AUTO_REFRESH_MS = 10_000;

function BuildingsCard({ farm, setFarm, onChanged }: CardProps) {
  const { t } = useI18n();
  // the levels opened so far: a root location, then building interiors
  const [path, setPath] = useState<string[]>(() => [farm.buildingLocations[0]?.location ?? 'Farm']);
  const [view, setView] = useState<LocationView | null>(null);
  const [selected, setSelected] = useState<Selection | null>(null);
  const [hover, setHover] = useState<string | null>(null);
  const [version, setVersion] = useState(0);
  const [auto, setAuto] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const current = path[path.length - 1];

  useEffect(() => {
    api<LocationView>('GET', `/api/farm/view/${encodeURIComponent(current)}`).then(
      (v) => {
        setView(v);
        setError(null);
      },
      (e) => setError(e.message),
    );
  }, [current, version]);

  const refresh = () => {
    api<Farm>('GET', '/api/farm').then(setFarm, () => {});
    setVersion((v) => v + 1);
  };

  useEffect(() => {
    if (!auto) return;
    const timer = setInterval(() => !document.hidden && refresh(), AUTO_REFRESH_MS);
    return () => clearInterval(timer);
  }, [auto]);

  const update = (f: Farm) => {
    setFarm(f);
    setVersion((v) => v + 1);
  };
  const go = (newPath: string[]) => {
    setPath(newPath);
    setSelected(null);
    setView(null);
  };

  const buildings = farm.buildings.filter((b) => b.location === current);
  const building = selected?.kind === 'building' ? farm.buildings.find((b) => b.id === selected.id) : undefined;
  const chest = selected?.kind === 'chest' ? view?.chests.find((c) => c.id === selected.id) : undefined;

  const pct = (value: number, total: number) => `${(value / total) * 100}%`;
  const box = (r: Rect) => (view ? { left: pct(r.x, view.width), top: pct(r.y, view.height), width: pct(r.width, view.width), height: pct(r.height, view.height) } : {});
  const spot = (key: string, kind: Selection['kind'], id: string, label: string, rect: Rect, extra = '', onDoubleClick?: () => void) => (
    <button
      key={key}
      class={`map-area ${kind}-area ${selected?.kind === kind && selected.id === id ? 'current' : ''} ${extra}`}
      style={box(rect)}
      title={label}
      aria-label={label}
      onMouseEnter={() => setHover(label)}
      onMouseLeave={() => setHover(null)}
      onClick={() => setSelected({ kind, id })}
      onDblClick={onDoubleClick}
    />
  );

  return (
    <section class="card">
      <div class="card-header">
        <h3>{t('farm.buildings')}</h3>
        {farm.buildingLocations.length > 1 && (
          <div class="tabs compact">
            {farm.buildingLocations.map((l) => (
              <button key={l.location} class={l.location === path[0] ? 'active' : ''} onClick={() => go([l.location])}>
                {l.displayName}
              </button>
            ))}
          </div>
        )}
      </div>
      <p class="muted">{t('farm.buildingsHint')}</p>
      <div class="fields map-toolbar">
        <nav class="breadcrumb" aria-label={t('farm.levels')}>
          {path.map((loc, i) => (
            <span key={loc}>
              {i > 0 && ' › '}
              {i < path.length - 1 ? (
                <button class="link" onClick={() => go(path.slice(0, i + 1))}>
                  {i === 0 ? (farm.buildingLocations.find((l) => l.location === loc)?.displayName ?? loc) : (farm.buildings.find((b) => b.interior === loc)?.name ?? loc)}
                </button>
              ) : (
                <strong>{view?.displayName ?? loc}</strong>
              )}
            </span>
          ))}
        </nav>
        <span class="spacer" />
        <button class="secondary small" onClick={refresh}>
          ↻ {t('common.reload')}
        </button>
        <label class="check">
          <input type="checkbox" checked={auto} onChange={(e) => setAuto((e.target as HTMLInputElement).checked)} /> {t('farm.autoRefresh')}
        </label>
      </div>
      {error && <p class="error">{error}</p>}
      <div class="farm-layout">
        {view ? (
          <div class="world-map farm-map" style={{ aspectRatio: `${view.width} / ${view.height}` }}>
            <img src={`${farmMapUrl(current)}&v=${version}`} alt={view.displayName} />
            {/* lower buildings last, so they sit on top where sprites overlap */}
            {[...buildings]
              .sort((a, b) => a.sprite.y + a.sprite.height - (b.sprite.y + b.sprite.height))
              .map((b) =>
                spot(`b-${b.id}`, 'building', b.id, b.name, b.daysOfConstructionLeft > 0 ? b.footprint : b.sprite, b.daysOfConstructionLeft > 0 ? 'construction' : '', b.interior ? () => go([...path, b.interior!]) : undefined),
              )}
            {view.chests.map((c) => spot(`c-${c.id}`, 'chest', c.id, c.name ?? t('chests.fridge'), c))}
            {view.machines.map((m) => spot(`m-${m.id}`, 'machine', m.id, `${m.name}${m.ready ? ` — ${t('machine.ready')}` : m.working ? ` — ${t('machine.working')}` : ''}`, m, m.ready ? 'ready' : m.working ? 'working' : ''))}
            {view.animals.map((a) => spot(`a-${a.id}`, 'animal', a.id, a.name, a))}
            {hover && <span class="map-label">{hover}</span>}
          </div>
        ) : (
          <p>{t('common.loading')}</p>
        )}
        <div class="map-panel">
          {view?.parent && path.length === 1 && (
            <p class="muted">
              {t('farm.insideOf')} {view.parent.displayName}
            </p>
          )}
          {building ? (
            <BuildingEditor key={building.id} building={building} update={update} onChanged={onChanged} onEnter={building.interior ? () => go([...path, building.interior!]) : undefined} />
          ) : chest ? (
            <ChestEditor
              key={chest.id}
              chest={{ id: chest.id, name: chest.name ?? t('chests.fridge'), isFridge: chest.name === null, locationName: view?.displayName ?? '' }}
              onChanged={onChanged}
              onContentChanged={() => {}}
            />
          ) : selected?.kind === 'machine' ? (
            <MachinePanel key={selected.id} id={selected.id} onChanged={onChanged} onUpdated={() => setVersion((v) => v + 1)} />
          ) : selected?.kind === 'animal' ? (
            <AnimalPanel key={selected.id} id={selected.id} onChanged={onChanged} />
          ) : (
            <p class="muted">{t('farm.pickBuilding')}</p>
          )}
        </div>
      </div>
    </section>
  );
}

type Selection = { kind: 'building' | 'chest' | 'animal' | 'machine'; id: string };

/** One animal's card, loaded on its own for the map's side panel. */
function AnimalPanel({ id, onChanged }: { id: string; onChanged: () => void }) {
  const { t } = useI18n();
  const [animals, setAnimals] = useState<Animal[] | null>(null);
  useEffect(() => {
    api<Animal[]>('GET', '/api/animals').then(setAnimals, () => setAnimals([]));
  }, [id]);
  const animal = animals?.find((a) => a.id === id);
  if (!animals) return <p>{t('common.loading')}</p>;
  if (!animal) return <p class="muted">{t('animals.none')}</p>;
  return (
    <ul class="animal-grid single">
      <AnimalCard animal={animal} setAnimals={setAnimals} onChanged={onChanged} />
    </ul>
  );
}

type Rect = { x: number; y: number; width: number; height: number };

function BuildingEditor({ building: b, update, onChanged, onEnter }: { building: BuildingInfo; update: (f: Farm) => void; onChanged: () => void; onEnter?: () => void }) {
  const { t } = useI18n();
  const { run, feedback, busy } = useAction(onChanged);
  const path = `/api/farm/buildings/${encodeURIComponent(b.id)}`;
  const inProgress = b.daysOfConstructionLeft > 0 || b.daysUntilUpgrade > 0;

  return (
    <div class="building-editor">
      <img src={buildingSpriteUrl(b.id, `${b.type}-${b.skinId}-${b.daysOfConstructionLeft}-${b.daysUntilUpgrade}`)} alt={b.name} />
      <h4>{b.name}</h4>
      <p class="muted">
        {b.locationName} ({b.x}, {b.y})
        {b.animals !== null && ` · ${b.animals}/${b.animalLimit} ${t('farm.animals')}`}
      </p>
      {b.daysOfConstructionLeft > 0 && (
        <p class="badge">
          {t('farm.underConstruction')} : {b.daysOfConstructionLeft} {t('farm.days')}
        </p>
      )}
      {b.daysUntilUpgrade > 0 && (
        <p class="badge">
          {t('farm.upgrading')} {b.upgradeName} : {b.daysUntilUpgrade} {t('farm.days')}
        </p>
      )}
      <div class="stack">
        {onEnter && !b.daysOfConstructionLeft && (
          <button class="secondary" onClick={onEnter}>
            🚪 {t('farm.enter')}
          </button>
        )}
        {inProgress && (
          <button disabled={busy} onClick={() => run(() => api<Farm>('POST', `${path}/finish`), update)}>
            {t('farm.finish')}
          </button>
        )}
        {!inProgress &&
          b.upgrades.map((u) => (
            <button key={u.type} disabled={busy} onClick={() => run(() => api<Farm>('POST', `${path}/upgrade`, { type: u.type }), update)}>
              ⬆ {t('farm.upgradeTo')} {u.name}
            </button>
          ))}
        {b.skins.length > 0 && (
          <label>
            {t('farm.skin')}
            <select value={b.skinId ?? ''} disabled={busy} onChange={(e) => run(() => api<Farm>('PUT', `${path}/skin`, { skin: (e.target as HTMLSelectElement).value || null }), update)}>
              <option value="">{t('farm.skinDefault')}</option>
              {b.skins.map((s) => (
                <option key={s.id} value={s.id}>
                  {s.name}
                </option>
              ))}
            </select>
          </label>
        )}
        {b.hasAnimalDoor && (
          <label class="check">
            <input type="checkbox" checked={b.animalDoorOpen} disabled={busy} onChange={(e) => run(() => api<Farm>('POST', `${path}/animal-door`, { open: (e.target as HTMLInputElement).checked }), update)} />{' '}
            {t('farm.animalDoor')}
          </label>
        )}
      </div>
      <FeedbackLine feedback={feedback} />
    </div>
  );
}
