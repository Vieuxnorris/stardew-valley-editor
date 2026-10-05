import { useEffect, useState } from 'preact/hooks';
import { api, buildingSpriteUrl, type BuildingInfo, type Farm, type FieldStats } from '../api';
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
  const { run, feedback, busy } = useAction(onChanged);
  const path = (b: BuildingInfo) => `/api/farm/buildings/${encodeURIComponent(b.id)}`;

  return (
    <section class="card">
      <h3>{t('farm.buildings')}</h3>
      <p class="muted">{t('farm.buildingsHint')}</p>
      <FeedbackLine feedback={feedback} />
      <ul class="building-grid">
        {farm.buildings.map((b) => {
          const building = b.daysOfConstructionLeft > 0 || b.daysUntilUpgrade > 0;
          return (
            <li key={b.id} class="building-card">
              <img src={buildingSpriteUrl(b.id, `${b.type}-${b.daysOfConstructionLeft}-${b.daysUntilUpgrade}`)} alt={b.name} loading="lazy" />
              <div>
                <strong>{b.name}</strong>
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
                <div class="fields">
                  {building && (
                    <button class="small" disabled={busy} onClick={() => run(() => api<Farm>('POST', `${path(b)}/finish`), setFarm)}>
                      {t('farm.finish')}
                    </button>
                  )}
                  {!building &&
                    b.upgrades.map((u) => (
                      <button key={u.type} class="small" disabled={busy} onClick={() => run(() => api<Farm>('POST', `${path(b)}/upgrade`, { type: u.type }), setFarm)}>
                        ⬆ {u.name}
                      </button>
                    ))}
                </div>
              </div>
            </li>
          );
        })}
      </ul>
    </section>
  );
}
