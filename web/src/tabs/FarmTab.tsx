import { useEffect, useState } from 'preact/hooks';
import { api, buildingSpriteUrl, farmMapUrl, type BuildingInfo, type Farm, type FieldStats } from '../api';
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

  const buttons = (location?: string) =>
    CROP_ACTIONS.map((a) => (
      <button key={a} class="secondary small" disabled={busy} onClick={() => act(a, location)}>
        {t(`farm.action.${a}`)}
      </button>
    ));

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

function BuildingsCard({ farm, setFarm, onChanged }: CardProps) {
  const { t } = useI18n();
  const [locationId, setLocationId] = useState(farm.buildingLocations[0]?.location ?? '');
  const [selected, setSelected] = useState<string | null>(null);
  const [hover, setHover] = useState<string | null>(null);
  // bump after a change so the rendered map reloads
  const [version, setVersion] = useState(0);

  const location = farm.buildingLocations.find((l) => l.location === locationId) ?? farm.buildingLocations[0];
  const buildings = farm.buildings.filter((b) => b.location === location?.location);
  const building = farm.buildings.find((b) => b.id === selected);
  const update = (f: Farm) => {
    setFarm(f);
    setVersion((v) => v + 1);
  };

  if (!location) return null;
  const pct = (value: number, total: number) => `${(value / total) * 100}%`;
  const box = (r: Rect) => ({ left: pct(r.x, location.width), top: pct(r.y, location.height), width: pct(r.width, location.width), height: pct(r.height, location.height) });

  return (
    <section class="card">
      <div class="card-header">
        <h3>{t('farm.buildings')}</h3>
        {farm.buildingLocations.length > 1 && (
          <div class="tabs compact">
            {farm.buildingLocations.map((l) => (
              <button key={l.location} class={l.location === location.location ? 'active' : ''} onClick={() => setLocationId(l.location)}>
                {l.displayName}
              </button>
            ))}
          </div>
        )}
      </div>
      <p class="muted">{t('farm.buildingsHint')}</p>
      <div class="farm-layout">
        <div class="world-map farm-map" style={{ aspectRatio: `${location.width} / ${location.height}` }}>
          <img src={`${farmMapUrl(location.location)}&v=${version}`} alt={location.displayName} />
          {/* lower buildings last, so they sit on top where sprites overlap */}
          {[...buildings]
            .sort((a, b) => a.sprite.y + a.sprite.height - (b.sprite.y + b.sprite.height))
            .map((b) => {
              const underConstruction = b.daysOfConstructionLeft > 0;
              return (
                <button
                  key={b.id}
                  class={`map-area building-area ${b.id === selected ? 'current' : ''} ${underConstruction ? 'construction' : ''}`}
                  style={box(underConstruction ? b.footprint : b.sprite)}
                  title={b.name}
                  aria-label={b.name}
                  onMouseEnter={() => setHover(b.name)}
                  onMouseLeave={() => setHover(null)}
                  onClick={() => setSelected(b.id)}
                />
              );
            })}
          {hover && <span class="map-label">{hover}</span>}
        </div>
        {building ? (
          <BuildingEditor key={building.id} building={building} update={update} onChanged={onChanged} />
        ) : (
          <p class="muted">{t('farm.pickBuilding')}</p>
        )}
      </div>
    </section>
  );
}

type Rect = { x: number; y: number; width: number; height: number };

function BuildingEditor({ building: b, update, onChanged }: { building: BuildingInfo; update: (f: Farm) => void; onChanged: () => void }) {
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
