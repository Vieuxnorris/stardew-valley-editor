import { useEffect, useState } from 'preact/hooks';
import { animalSpriteUrl, api, type Animal } from '../api';
import { FeedbackLine, ItemIcon, useAction } from '../components';
import { useI18n } from '../i18n';

const MAX_FRIENDSHIP = 1000;
const MAX_MOOD = 255;

export function AnimalsTab({ onChanged }: { onChanged: () => void }) {
  const { t } = useI18n();
  const [animals, setAnimals] = useState<Animal[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [grow, setGrow] = useState(false);
  const { run, feedback, busy } = useAction(onChanged);

  useEffect(() => {
    api<Animal[]>('GET', '/api/animals').then(setAnimals, (e) => setError(e.message));
  }, []);

  if (!animals) return <p>{error ?? t('common.loading')}</p>;

  return (
    <div class="stack">
      <section class="card">
        <h3>{t('animals.title')}</h3>
        <p class="muted">{t('animals.hint')}</p>
        <div class="fields">
          <button disabled={busy || animals.length === 0} onClick={() => run(() => api<Animal[]>('POST', '/api/animals/pamper', { grow }), setAnimals)}>
            💖 {t('animals.pamper')}
          </button>
          <label class="check">
            <input type="checkbox" checked={grow} onChange={(e) => setGrow((e.target as HTMLInputElement).checked)} /> {t('animals.pamperGrow')}
          </label>
        </div>
        <FeedbackLine feedback={feedback} />
        {animals.length === 0 && <p class="muted">{t('animals.none')}</p>}
      </section>
      <ul class="animal-grid">
        {animals.map((a) => (
          <AnimalCard key={a.id} animal={a} setAnimals={setAnimals} onChanged={onChanged} />
        ))}
      </ul>
    </div>
  );
}

function AnimalCard({ animal, setAnimals, onChanged }: { animal: Animal; setAnimals: (a: Animal[]) => void; onChanged: () => void }) {
  const { t } = useI18n();
  const { run, feedback, busy } = useAction(onChanged);
  const [name, setName] = useState(animal.name);
  const [friendship, setFriendship] = useState(animal.friendship);
  const [happiness, setHappiness] = useState(animal.happiness);
  const [fullness, setFullness] = useState(animal.fullness);

  useEffect(() => {
    setName(animal.name);
    setFriendship(animal.friendship);
    setHappiness(animal.happiness);
    setFullness(animal.fullness);
  }, [animal]);

  const path = `/api/animals/${encodeURIComponent(animal.id)}`;
  const hearts = friendship / 200;

  return (
    <li class="card animal-card">
      <div class="animal-head">
        <img src={animalSpriteUrl(animal.id, animal.isAdult ? 'adult' : 'baby')} alt={animal.typeName} class="icon" width={48} height={48} />
        <div>
          <strong>{animal.name}</strong>
          <p class="muted">
            {animal.typeName} · {animal.home ?? animal.locationName}
          </p>
          <p class="muted">
            {animal.isAdult ? t('animals.adult') : `${t('animals.baby')} (${animal.age}/${animal.daysToMature} ${t('farm.days')})`}
            {animal.wasPet && ` · ${t('animals.petted')}`}
          </p>
        </div>
        {animal.produce && <ItemIcon qualifiedId={animal.produce} name={t('animals.produce')} size={32} />}
      </div>
      {animal.mood && <p class="muted mood">« {animal.mood} »</p>}
      <form
        class="stack"
        onSubmit={(e) => {
          e.preventDefault();
          run(() => api<Animal[]>('PATCH', path, { name, friendship, happiness, fullness }), setAnimals);
        }}
      >
        <label>
          {t('animals.name')}
          <input type="text" maxLength={64} value={name} onInput={(e) => setName((e.target as HTMLInputElement).value)} />
        </label>
        <label>
          <span>
            {t('animals.friendship')} : {'❤'.repeat(Math.floor(hearts))}
            {hearts % 1 >= 0.5 ? '♡' : ''} <span class="muted">({friendship}/{MAX_FRIENDSHIP})</span>
          </span>
          <input type="range" min={0} max={MAX_FRIENDSHIP} step={10} value={friendship} onInput={(e) => setFriendship(Number((e.target as HTMLInputElement).value))} />
        </label>
        <label>
          <span>
            {t('animals.happiness')} <span class="muted">({happiness}/{MAX_MOOD})</span>
          </span>
          <input type="range" min={0} max={MAX_MOOD} value={happiness} onInput={(e) => setHappiness(Number((e.target as HTMLInputElement).value))} />
        </label>
        <label>
          <span>
            {t('animals.fullness')} <span class="muted">({fullness}/{MAX_MOOD})</span>
          </span>
          <input type="range" min={0} max={MAX_MOOD} value={fullness} onInput={(e) => setFullness(Number((e.target as HTMLInputElement).value))} />
        </label>
        <div class="fields">
          <button type="submit" disabled={busy}>
            {t('common.apply')}
          </button>
          {!animal.isAdult && (
            <button type="button" class="secondary" disabled={busy} onClick={() => run(() => api<Animal[]>('POST', `${path}/grow`), setAnimals)}>
              {t('animals.grow')}
            </button>
          )}
        </div>
      </form>
      <FeedbackLine feedback={feedback} />
    </li>
  );
}
