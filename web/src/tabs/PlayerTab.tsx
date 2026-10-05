import { useEffect, useState } from 'preact/hooks';
import { api, type Player, type Skill } from '../api';
import { FeedbackLine, NumberField, useAction } from '../components';
import { useI18n } from '../i18n';

type Props = { onChanged: () => void };

export function PlayerTab({ onChanged }: Props) {
  const { t } = useI18n();
  const [player, setPlayer] = useState<Player | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    api<Player>('GET', '/api/player')
      .then(setPlayer)
      .catch((e) => setError(e.message));
  }, []);

  if (!player) return <p>{error ?? t('common.loading')}</p>;

  const shared = { player, setPlayer, onChanged };
  return (
    <div class="stack">
      <h2 class="page-title">
        {player.name} — {player.farmName}
      </h2>
      <StatsCard {...shared} title={t('player.resources')} fields={['money', 'qiGems', 'goldenWalnuts']} />
      <StatsCard {...shared} title={t('player.vitals')} fields={['health', 'maxHealth', 'stamina', 'maxStamina']} />
      <SkillsCard {...shared} />
      <ProfessionsCard {...shared} />
      <StatsCard {...shared} title={t('player.mastery')} fields={['masteryExp']} extra={`${t('player.masteryLevel')} : ${player.masteryLevel}`} />
    </div>
  );
}

type CardProps = Props & { player: Player; setPlayer: (player: Player) => void };
type StatField = 'money' | 'qiGems' | 'goldenWalnuts' | 'health' | 'maxHealth' | 'stamina' | 'maxStamina' | 'masteryExp';

/** A form of plain numeric player fields, sent together with PATCH /api/player. */
function StatsCard({ player, setPlayer, onChanged, title, fields, extra }: CardProps & { title: string; fields: StatField[]; extra?: string }) {
  const { t } = useI18n();
  const { run, feedback, busy } = useAction(onChanged);
  const initial = () => Object.fromEntries(fields.map((f) => [f, String(player[f])]));
  const [values, setValues] = useState<Record<string, string>>(initial);

  // refresh the inputs when another card changed the player (e.g. Defender raises max health)
  useEffect(() => setValues(initial()), [player]);

  const submit = (e: Event) => {
    e.preventDefault();
    const changed = Object.fromEntries(fields.filter((f) => values[f] !== String(player[f])).map((f) => [f, Number(values[f])]));
    run(() => api<Player>('PATCH', '/api/player', changed), setPlayer);
  };

  return (
    <section class="card">
      <h3>{title}</h3>
      <form class="fields" onSubmit={submit}>
        {fields.map((f) => (
          <NumberField key={f} label={t(`player.${f}`)} value={values[f]} onInput={(v) => setValues({ ...values, [f]: v })} />
        ))}
        <button type="submit" disabled={busy}>
          {t('common.apply')}
        </button>
      </form>
      {extra && <p class="muted">{extra}</p>}
      <FeedbackLine feedback={feedback} />
    </section>
  );
}

function SkillsCard({ player, setPlayer, onChanged }: CardProps) {
  const { t } = useI18n();
  const { run, feedback } = useAction(onChanged);
  const update = (skill: Skill, body: { level: number } | { xp: number }) => run(() => api<Player>('PUT', `/api/player/skills/${skill.key}`, body), setPlayer);

  return (
    <section class="card">
      <h3>{t('player.skills')}</h3>
      <p class="muted">{t('player.skillsHint')}</p>
      <table class="table">
        <thead>
          <tr>
            <th />
            <th>{t('player.level')}</th>
            <th>{t('player.xp')}</th>
          </tr>
        </thead>
        <tbody>
          {player.skills.map((skill) => (
            <SkillRow key={skill.key} skill={skill} onLevel={(level) => update(skill, { level })} onXp={(xp) => update(skill, { xp })} />
          ))}
        </tbody>
      </table>
      <FeedbackLine feedback={feedback} />
    </section>
  );
}

function SkillRow({ skill, onLevel, onXp }: { skill: Skill; onLevel: (level: number) => void; onXp: (xp: number) => void }) {
  const { t } = useI18n();
  const [xp, setXp] = useState(String(skill.xp));
  useEffect(() => setXp(String(skill.xp)), [skill.xp]);

  return (
    <tr>
      <th scope="row">
        {skill.name}
        {skill.pendingLevelUps > 0 && (
          <span class="badge" title={t('player.pending')}>
            +{skill.pendingLevelUps} {t('player.pending')}
          </span>
        )}
      </th>
      <td>
        <select value={skill.level} onChange={(e) => onLevel(Number((e.target as HTMLSelectElement).value))} aria-label={`${skill.name} ${t('player.level')}`}>
          {Array.from({ length: 11 }, (_, i) => (
            <option key={i} value={i}>
              {i}
            </option>
          ))}
        </select>
      </td>
      <td>
        <form
          class="inline"
          onSubmit={(e) => {
            e.preventDefault();
            onXp(Number(xp));
          }}
        >
          <input type="number" min={0} step={1} value={xp} onInput={(e) => setXp((e.target as HTMLInputElement).value)} aria-label={`${skill.name} ${t('player.xp')}`} />
          <span class="muted">{skill.nextLevelXp !== null ? ` / ${skill.nextLevelXp}` : ''}</span>
          <button type="submit" disabled={xp === String(skill.xp)}>
            {t('common.apply')}
          </button>
        </form>
      </td>
    </tr>
  );
}

function ProfessionsCard({ player, setPlayer, onChanged }: CardProps) {
  const { t } = useI18n();
  const { run, feedback, busy } = useAction(onChanged);
  const [selected, setSelected] = useState(new Set(player.professions));
  useEffect(() => setSelected(new Set(player.professions)), [player.professions]);

  const toggle = (id: number) => {
    const next = new Set(selected);
    if (next.has(id)) next.delete(id);
    else next.add(id);
    setSelected(next);
  };

  return (
    <section class="card">
      <h3>{t('player.professions')}</h3>
      <p class="muted">{t('player.professionsHint')}</p>
      <div class="profession-grid">
        {player.skills.map((skill) => {
          const professions = player.professionCatalog.filter((p) => p.skill === skill.key);
          return (
            <fieldset key={skill.key}>
              <legend>{skill.name}</legend>
              {professions
                .filter((p) => p.tier === 5)
                .map((p5) => (
                  <div key={p5.id}>
                    <label class="check">
                      <input type="checkbox" checked={selected.has(p5.id)} onChange={() => toggle(p5.id)} /> {p5.name} <span class="muted">(5)</span>
                    </label>
                    {professions
                      .filter((p10) => p10.parent === p5.id)
                      .map((p10) => (
                        <label key={p10.id} class="check indent">
                          <input type="checkbox" checked={selected.has(p10.id)} onChange={() => toggle(p10.id)} /> {p10.name} <span class="muted">(10)</span>
                        </label>
                      ))}
                  </div>
                ))}
            </fieldset>
          );
        })}
      </div>
      <button disabled={busy} onClick={() => run(() => api<Player>('PUT', '/api/player/professions', { ids: [...selected] }), setPlayer)}>
        {t('common.apply')}
      </button>
      <FeedbackLine feedback={feedback} />
    </section>
  );
}
