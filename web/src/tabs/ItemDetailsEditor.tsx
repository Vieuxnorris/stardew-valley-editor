import { useEffect, useState } from 'preact/hooks';
import { api, type Inventory, type ItemDetails, type WeaponStats } from '../api';
import { FeedbackLine, ItemIcon, useAction } from '../components';
import { useI18n } from '../i18n';

const WEAPON_FIELDS: { key: keyof WeaponStats; step: number }[] = [
  { key: 'minDamage', step: 1 },
  { key: 'maxDamage', step: 1 },
  { key: 'speed', step: 1 },
  { key: 'precision', step: 1 },
  { key: 'defense', step: 1 },
  { key: 'areaOfEffect', step: 1 },
  { key: 'knockback', step: 0.01 },
  { key: 'critChance', step: 0.01 },
  { key: 'critMultiplier', step: 0.01 },
];

type Props = { basePath: string; slot: number; onChanged: () => void; setContainer: (inv: Inventory) => void };

/** The type-specific part of the item editor (weapon stats, enchantments, tool level, rings...). */
export function ItemDetailsEditor({ basePath, slot, onChanged, setContainer }: Props) {
  const { t } = useI18n();
  const { run, feedback, busy } = useAction(onChanged);
  const [details, setDetails] = useState<ItemDetails | null>(null);
  const [form, setForm] = useState<Record<string, string>>({});
  const [weapon, setWeapon] = useState<Record<string, string>>({});
  const [enchants, setEnchants] = useState<Record<string, number>>({});
  const path = `${basePath}/${slot}/details`;

  const load = (d: ItemDetails) => {
    setDetails(d);
    setForm({
      price: String(d.object?.price ?? ''),
      edibility: String(d.object?.edibility ?? ''),
      ingredient: d.object?.ingredient ?? '',
      toolLevel: d.tool?.current ?? '',
      defense: String(d.boots?.defense ?? ''),
      immunity: String(d.boots?.immunity ?? ''),
      color: d.clothing?.color ?? '#ffffff',
      combineWith: '',
    });
    setWeapon(d.weapon ? Object.fromEntries(WEAPON_FIELDS.map((f) => [f.key, String(d.weapon![f.key])])) : {});
    setEnchants(Object.fromEntries((d.enchantments ?? []).map((e) => [e.id, e.level])));
  };

  useEffect(() => {
    api<ItemDetails>('GET', path).then(load, () => setDetails(null));
  }, [path]);

  if (!details || details.kind === 'other') return null;

  const set = (key: string) => (e: Event) => setForm({ ...form, [key]: (e.target as HTMLInputElement).value });
  const save = (body: object) =>
    run(
      () => api<{ details: ItemDetails; container: Inventory }>('PATCH', path, body),
      (r) => {
        load(r.details);
        setContainer(r.container);
      },
    );

  const submit = (e: Event) => {
    e.preventDefault();
    const body: Record<string, unknown> = {};
    if (details.object) {
      body.price = Number(form.price);
      body.edibility = Number(form.edibility);
      if (details.object.preserve && form.ingredient && form.ingredient !== details.object.ingredient) body.ingredient = form.ingredient;
    }
    if (details.weapon) body.weapon = Object.fromEntries(WEAPON_FIELDS.map((f) => [f.key, Number(weapon[f.key])]));
    if (details.enchantments) body.enchantments = enchants;
    if (details.tool?.levels && form.toolLevel !== details.tool.current) body.toolLevel = form.toolLevel;
    if (details.boots) {
      body.defense = Number(form.defense);
      body.immunity = Number(form.immunity);
    }
    if (details.clothing) body.color = form.color;
    save(body);
  };

  const base = (value: number | null | undefined, current: string) =>
    value !== null && value !== undefined && Number(current) !== value ? (
      <span class="badge">
        {t('rules.vanilla')} : {value}
      </span>
    ) : null;

  return (
    <form class="item-details stack" onSubmit={submit}>
      {details.object && (
        <fieldset>
          <legend>{t('item.object')}</legend>
          <div class="fields">
            <label>
              <span>
                {t('item.price')} {base(details.object.basePrice, form.price)}
              </span>
              <input type="number" min={0} value={form.price} onInput={set('price')} />
            </label>
            <label>
              <span>
                {t('item.edibility')} {base(details.object.baseEdibility, form.edibility)}
              </span>
              <input type="number" min={-300} value={form.edibility} onInput={set('edibility')} />
            </label>
            {details.object.preserve && (
              <label>
                <span>
                  {t('item.ingredient')} {form.ingredient && <ItemIcon qualifiedId={form.ingredient} name={form.ingredient} size={16} />}
                </span>
                <input type="text" value={form.ingredient} onInput={set('ingredient')} placeholder="(O)613" />
              </label>
            )}
          </div>
        </fieldset>
      )}

      {details.weapon && (
        <fieldset>
          <legend>{t('item.weapon')}</legend>
          <div class="fields">
            {WEAPON_FIELDS.map((f) => (
              <label key={f.key}>
                <span>
                  {t(`item.w.${f.key}`)} {base(details.weapon!.base?.[f.key], weapon[f.key])}
                </span>
                <input type="number" step={f.step} value={weapon[f.key]} onInput={(e) => setWeapon({ ...weapon, [f.key]: (e.target as HTMLInputElement).value })} />
              </label>
            ))}
          </div>
          <p class="muted">{t('item.weaponHint')}</p>
        </fieldset>
      )}

      {details.enchantments && details.enchantments.length > 0 && (
        <fieldset>
          <legend>{t('item.enchantments')}</legend>
          {(['forge', 'innate', 'primary'] as const).map((group) => {
            const list = details.enchantments!.filter((e) => e.group === group);
            if (list.length === 0) return null;
            return (
              <div key={group}>
                <h5>{t(`item.ench.${group}`)}</h5>
                <div class="enchant-grid">
                  {list.map((e) =>
                    e.leveled ? (
                      <label key={e.id} class="inline">
                        <input type="number" min={0} max={99} value={enchants[e.id] ?? 0} onInput={(ev) => setEnchants({ ...enchants, [e.id]: Number((ev.target as HTMLInputElement).value) })} />
                        {e.name}
                        {e.gameMaxLevel > 0 && <span class="muted"> (max {e.gameMaxLevel})</span>}
                      </label>
                    ) : (
                      <label key={e.id} class="check">
                        <input type="checkbox" checked={(enchants[e.id] ?? 0) > 0} onChange={(ev) => setEnchants({ ...enchants, [e.id]: (ev.target as HTMLInputElement).checked ? 1 : 0 })} /> {e.name}
                      </label>
                    ),
                  )}
                </div>
              </div>
            );
          })}
          <p class="muted">{t('item.enchantHint')}</p>
        </fieldset>
      )}

      {details.tool && (details.tool.levels || details.tool.waterMax !== null) && (
        <fieldset>
          <legend>{t('item.tool')}</legend>
          <div class="fields">
            {details.tool.levels && details.tool.levels.length > 1 && (
              <label>
                {t('item.toolLevel')}
                <select value={form.toolLevel} onChange={set('toolLevel')}>
                  {details.tool.levels.map((l) => (
                    <option key={l.id} value={l.id}>
                      {l.name}
                    </option>
                  ))}
                </select>
              </label>
            )}
            {details.tool.waterMax !== null && (
              <button type="button" class="secondary" disabled={busy} onClick={() => save({ fillWater: true })}>
                💧 {t('item.fillWater')} ({details.tool.waterLeft}/{details.tool.waterMax})
              </button>
            )}
          </div>
        </fieldset>
      )}

      {details.ring && (
        <fieldset>
          <legend>{t('item.ring')}</legend>
          {details.ring.combined.length > 0 && (
            <p class="inline">
              {t('item.combined')} :{' '}
              {details.ring.combined.map((id) => (
                <ItemIcon key={id} qualifiedId={id} name={id} size={24} />
              ))}
            </p>
          )}
          <div class="fields">
            <select value={form.combineWith} onChange={set('combineWith')} aria-label={t('item.combineWith')}>
              <option value="">{t('item.combineWith')}…</option>
              {details.ring.rings.map((r) => (
                <option key={r.id} value={r.id}>
                  {r.name}
                </option>
              ))}
            </select>
            <button type="button" class="secondary" disabled={busy || !form.combineWith} onClick={() => save({ combineWith: form.combineWith })}>
              💍 {t('item.combine')}
            </button>
          </div>
        </fieldset>
      )}

      {details.boots && (
        <fieldset>
          <legend>{t('item.boots')}</legend>
          <div class="fields">
            <label>
              {t('item.w.defense')}
              <input type="number" min={0} value={form.defense} onInput={set('defense')} />
            </label>
            <label>
              {t('item.immunity')}
              <input type="number" min={0} value={form.immunity} onInput={set('immunity')} />
            </label>
          </div>
        </fieldset>
      )}

      {details.clothing && (
        <fieldset>
          <legend>{t('item.clothing')}</legend>
          <label class="inline">
            {t('item.color')} <input type="color" value={form.color} onInput={set('color')} />
          </label>
        </fieldset>
      )}

      <div class="fields">
        <button type="submit" disabled={busy}>
          {t('item.applyDetails')}
        </button>
      </div>
      <FeedbackLine feedback={feedback} />
    </form>
  );
}
