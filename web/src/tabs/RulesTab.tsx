import { useEffect, useMemo, useState } from 'preact/hooks';
import { api, type CropRule, type MineBand, type Rules, type RulesSnapshot } from '../api';
import { FeedbackLine, ItemIcon, useAction } from '../components';
import { useI18n } from '../i18n';

type Props = { onChanged: () => void };
type CardProps = Props & { snapshot: RulesSnapshot; setSnapshot: (s: RulesSnapshot) => void };

const patchRules = (body: object) => api<RulesSnapshot>('PATCH', '/api/rules', body);

export function RulesTab({ onChanged }: Props) {
  const { t } = useI18n();
  const [snapshot, setSnapshot] = useState<RulesSnapshot | null>(null);
  const [error, setError] = useState<string | null>(null);
  const { run, feedback, busy } = useAction(onChanged);

  useEffect(() => {
    api<RulesSnapshot>('GET', '/api/rules').then(setSnapshot, (e) => setError(e.message));
  }, []);

  if (!snapshot) return <p>{error ?? t('common.loading')}</p>;

  const shared = { snapshot, setSnapshot, onChanged };
  return (
    <div class="stack">
      <section class="card">
        <div class="card-header">
          <p class="muted">{t('rules.intro')}</p>
          <button class="secondary" disabled={busy} onClick={() => run(() => api<RulesSnapshot>('POST', '/api/rules/reset'), setSnapshot)}>
            {t('rules.reset')}
          </button>
        </div>
        <FeedbackLine feedback={feedback} />
      </section>
      <OpCard {...shared} />
      <CropsCard {...shared} />
      <TreesCard {...shared} />
      <MinesCard {...shared} />
    </div>
  );
}

/** A multiplier input; shows when the value differs from vanilla (1, or another default). */
function RatioField(props: { label: string; value: string; onInput: (v: string) => void; min: number; max: number; step?: number; vanilla?: number }) {
  const { t } = useI18n();
  const vanilla = props.vanilla ?? 1;
  const changed = props.value !== '' && Number(props.value) !== vanilla;
  return (
    <label>
      <span>
        {props.label} {changed && <span class="badge">{t('rules.vanilla')} : {vanilla}</span>}
      </span>
      <input type="number" min={props.min} max={props.max} step={props.step ?? 0.05} value={props.value} onInput={(e) => props.onInput((e.target as HTMLInputElement).value)} />
    </label>
  );
}

/** Form state for a set of numeric rule fields. */
function useRuleFields<K extends keyof Rules>(rules: Rules, keys: K[]) {
  const initial = () => Object.fromEntries(keys.map((k) => [k, String(rules[k])])) as Record<K, string>;
  const [values, setValues] = useState(initial);
  useEffect(() => setValues(initial()), [rules]);
  const set = (key: K) => (value: string) => setValues({ ...values, [key]: value });
  const body = () => Object.fromEntries(keys.map((k) => [k, Number(values[k])]));
  return { values, set, body };
}

function CropsCard({ snapshot, setSnapshot, onChanged }: CardProps) {
  const { t } = useI18n();
  const { run, feedback, busy, setFeedback } = useAction(onChanged);
  const { values, set, body } = useRuleFields(snapshot.rules, ['cropGrowth']);
  const [filter, setFilter] = useState('');
  const crops = useMemo(() => snapshot.crops.filter((c) => c.name.toLowerCase().includes(filter.toLowerCase())), [snapshot.crops, filter]);

  return (
    <section class="card">
      <h3>{t('rules.crops')}</h3>
      <form
        class="fields"
        onSubmit={(e) => {
          e.preventDefault();
          run(() => patchRules(body()), setSnapshot);
        }}
      >
        <RatioField label={t('rules.cropGrowth')} value={values.cropGrowth} onInput={set('cropGrowth')} min={0.05} max={10} />
        <button type="submit" disabled={busy}>
          {t('common.apply')}
        </button>
        <button
          type="button"
          class="secondary"
          disabled={busy}
          onClick={() =>
            run(
              () => api<{ updated: number }>('POST', '/api/rules/apply-to-planted-crops'),
              (r) => setTimeout(() => setFeedback({ ok: true, text: `${r.updated} ${t('rules.updatedCrops')}` })),
            )
          }
        >
          {t('rules.applyPlanted')}
        </button>
      </form>
      <p class="muted">{t('rules.cropHint')}</p>
      <FeedbackLine feedback={feedback} />

      <h4>{t('rules.cropTable')}</h4>
      <input type="search" placeholder={t('rules.filterCrops')} value={filter} onInput={(e) => setFilter((e.target as HTMLInputElement).value)} aria-label={t('rules.filterCrops')} />
      <div class="table-scroll">
        <table class="table">
          <thead>
            <tr>
              <th>{t('rules.crop')}</th>
              <th>{t('rules.days')}</th>
              <th>{t('rules.regrow')}</th>
              <th>
                {t('rules.override')} <span class="muted">({t('rules.overrideHint')})</span>
              </th>
            </tr>
          </thead>
          <tbody>
            {crops.map((crop) => (
              <CropRow key={crop.seedId} crop={crop} busy={busy} save={(value) => run(() => patchRules({ cropGrowthOverrides: { [crop.seedId]: value } }), setSnapshot)} />
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}

function CropRow({ crop, busy, save }: { crop: CropRule; busy: boolean; save: (value: number | null) => void }) {
  const [value, setValue] = useState(crop.override?.toString() ?? '');
  useEffect(() => setValue(crop.override?.toString() ?? ''), [crop.override]);
  const current = crop.override?.toString() ?? '';
  const commit = () => value !== current && save(value === '' ? null : Number(value));

  const days = (base: number | null, now: number) => (base !== null && base !== now ? `${base} → ${now}` : String(now));
  return (
    <tr>
      <td class="inline">
        {crop.harvestItemId && <ItemIcon qualifiedId={crop.harvestItemId} name={crop.name} size={24} />} {crop.name}
      </td>
      <td>{days(crop.baseDays, crop.days)}</td>
      <td>{crop.regrowDays > 0 ? days(crop.baseRegrowDays, crop.regrowDays) : '—'}</td>
      <td>
        <input
          type="number"
          min={0.05}
          max={10}
          step={0.05}
          value={value}
          disabled={busy}
          onInput={(e) => setValue((e.target as HTMLInputElement).value)}
          onBlur={commit}
          onKeyDown={(e) => e.key === 'Enter' && commit()}
          aria-label={crop.name}
        />
      </td>
    </tr>
  );
}

function TreesCard({ snapshot, setSnapshot, onChanged }: CardProps) {
  const { t } = useI18n();
  const { run, feedback, busy } = useAction(onChanged);
  const { values, set, body } = useRuleFields(snapshot.rules, ['fruitTreeSpeed', 'wildTreeGrowth', 'machineTime']);

  return (
    <section class="card">
      <h3>{t('rules.trees')}</h3>
      <form
        class="fields"
        onSubmit={(e) => {
          e.preventDefault();
          run(() => patchRules(body()), setSnapshot);
        }}
      >
        <RatioField label={t('rules.fruitTreeSpeed')} value={values.fruitTreeSpeed} onInput={set('fruitTreeSpeed')} min={1} max={28} step={1} />
        <RatioField label={t('rules.wildTreeGrowth')} value={values.wildTreeGrowth} onInput={set('wildTreeGrowth')} min={0} max={20} />
        <RatioField label={t('rules.machineTime')} value={values.machineTime} onInput={set('machineTime')} min={0.01} max={10} />
        <button type="submit" disabled={busy}>
          {t('common.apply')}
        </button>
      </form>
      <p class="muted">{t('rules.machineHint')}</p>
      <FeedbackLine feedback={feedback} />
    </section>
  );
}

function MinesCard({ snapshot, setSnapshot, onChanged }: CardProps) {
  const { t } = useI18n();
  const { run, feedback, busy } = useAction(onChanged);
  const { values, set, body } = useRuleFields(snapshot.rules, ['mineStones', 'mineMonsters', 'mineGems']);
  const oreInitial = () => Object.fromEntries(snapshot.mineBands.map((b) => [b, String(snapshot.rules.mineOre[b] ?? 1)])) as Record<MineBand, string>;
  const [ore, setOre] = useState(oreInitial);
  useEffect(() => setOre(oreInitial()), [snapshot.rules]);

  return (
    <section class="card">
      <h3>{t('rules.mines')}</h3>
      <form
        class="stack"
        onSubmit={(e) => {
          e.preventDefault();
          run(() => patchRules({ ...body(), mineOre: Object.fromEntries(snapshot.mineBands.map((b) => [b, Number(ore[b])])) }), setSnapshot);
        }}
      >
        <fieldset>
          <legend>{t('rules.ore')}</legend>
          <div class="fields">
            {snapshot.mineBands.map((band) => (
              <RatioField key={band} label={t(`band.${band}`)} value={ore[band]} onInput={(v) => setOre({ ...ore, [band]: v })} min={0} max={50} step={0.5} />
            ))}
          </div>
        </fieldset>
        <div class="fields">
          <RatioField label={t('rules.mineStones')} value={values.mineStones} onInput={set('mineStones')} min={0} max={5} step={0.1} />
          <RatioField label={t('rules.mineMonsters')} value={values.mineMonsters} onInput={set('mineMonsters')} min={0} max={10} step={0.1} />
          <RatioField label={t('rules.mineGems')} value={values.mineGems} onInput={set('mineGems')} min={0} max={20} step={0.5} />
          <button type="submit" disabled={busy}>
            {t('common.apply')}
          </button>
        </div>
      </form>
      <RuleSwitch rules={snapshot.rules} field="mineAlwaysLadder" label={t('rules.mineAlwaysLadder')} busy={busy} save={(body) => run(() => patchRules(body), setSnapshot)} />
      <p class="muted">{t('rules.minesHint')}</p>
      <FeedbackLine feedback={feedback} />
    </section>
  );
}

type BoolRule = { [K in keyof Rules]: Rules[K] extends boolean ? K : never }[keyof Rules];

/** A rule checkbox that applies as soon as it's clicked. */
function RuleSwitch({ rules, field, label, busy, save }: { rules: Rules; field: BoolRule; label: string; busy: boolean; save: (body: object) => void }) {
  return (
    <label class="check">
      <input type="checkbox" checked={rules[field]} disabled={busy} onChange={(e) => save({ [field]: (e.target as HTMLInputElement).checked })} /> {label}
    </label>
  );
}

function OpCard({ snapshot, setSnapshot, onChanged }: CardProps) {
  const { t } = useI18n();
  const { run, feedback, busy } = useAction(onChanged);
  const save = (body: object) => run(() => patchRules(body), setSnapshot);
  const { values, set, body } = useRuleFields(snapshot.rules, ['pickupMultiplier', 'monsterLootRolls', 'sellPrice', 'speedBonus', 'magnetRadiusBonus', 'luckBonus']);
  const rules = snapshot.rules;
  const toggle = (field: BoolRule) => <RuleSwitch rules={rules} field={field} label={t(`op.${field}`)} busy={busy} save={save} />;

  return (
    <section class="card op-card">
      <h3>⚡ {t('op.title')}</h3>
      <p class="muted">{t('op.intro')}</p>

      <div class="op-grid">
        <fieldset>
          <legend>{t('op.fishing')}</legend>
          {toggle('instantFishing')}
          {toggle('perfectCatch')}
          {toggle('alwaysTreasure')}
        </fieldset>
        <fieldset>
          <legend>{t('op.player')}</legend>
          {toggle('infiniteHealth')}
          {toggle('infiniteStamina')}
          {toggle('freezeTime')}
          {toggle('maxDailyLuck')}
        </fieldset>
      </div>

      <form
        class="stack"
        onSubmit={(e) => {
          e.preventDefault();
          save(body());
        }}
      >
        <fieldset>
          <legend>{t('op.loot')}</legend>
          <div class="fields">
            <RatioField label={t('op.pickupMultiplier')} value={values.pickupMultiplier} onInput={set('pickupMultiplier')} min={1} max={100} step={1} />
            <label>
              {t('op.minQuality')}
              <select value={rules.minQuality} disabled={busy} onChange={(e) => save({ minQuality: Number((e.target as HTMLSelectElement).value) })}>
                {[0, 1, 2, 4].map((q) => (
                  <option key={q} value={q}>
                    {t(`quality.${q}`)}
                  </option>
                ))}
              </select>
            </label>
            <RatioField label={t('op.monsterLootRolls')} value={values.monsterLootRolls} onInput={set('monsterLootRolls')} min={1} max={20} step={1} />
            <RatioField label={t('op.sellPrice')} value={values.sellPrice} onInput={set('sellPrice')} min={0.01} max={100} step={0.5} />
          </div>
          <p class="muted">{t('op.pickupHint')}</p>
        </fieldset>
        <div class="fields">
          <RatioField label={t('op.speedBonus')} value={values.speedBonus} onInput={set('speedBonus')} min={0} max={20} step={1} vanilla={0} />
          <RatioField label={t('op.magnetRadiusBonus')} value={values.magnetRadiusBonus} onInput={set('magnetRadiusBonus')} min={0} max={2000} step={64} vanilla={0} />
          <RatioField label={t('op.luckBonus')} value={values.luckBonus} onInput={set('luckBonus')} min={0} max={20} step={1} vanilla={0} />
          <button type="submit" disabled={busy}>
            {t('common.apply')}
          </button>
        </div>
      </form>
      <FeedbackLine feedback={feedback} />
    </section>
  );
}
