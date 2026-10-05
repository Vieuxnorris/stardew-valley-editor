import { useEffect, useState } from 'preact/hooks';
import { api, mapImageUrl, type MapRegion, type Season, type World } from '../api';
import { FeedbackLine, NumberField, useAction } from '../components';
import { useI18n } from '../i18n';

const SEASONS: Season[] = ['spring', 'summer', 'fall', 'winter'];

/** The game's clock runs from 6:00 to 2:00 the next night, in HHMM with 10-minute steps. */
const TIMES = Array.from({ length: 20 * 6 }, (_, i) => 600 + Math.floor(i / 6) * 100 + (i % 6) * 10);
const formatTime = (time: number) => `${String(Math.floor(time / 100) % 24).padStart(2, '0')}:${String(time % 100).padStart(2, '0')}`;

export function WorldTab({ onChanged }: { onChanged: () => void }) {
  const { t } = useI18n();
  const [world, setWorld] = useState<World | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    api<World>('GET', '/api/world').then(setWorld, (e) => setError(e.message));
  }, []);

  if (!world) return <p>{error ?? t('common.loading')}</p>;

  const shared = { world, setWorld, onChanged };
  return (
    <div class="stack">
      <DateCard {...shared} />
      <WeatherCard {...shared} />
      <MapCard {...shared} />
      <WarpCard {...shared} />
    </div>
  );
}

type CardProps = { world: World; setWorld: (world: World) => void; onChanged: () => void };

function DateCard({ world, setWorld, onChanged }: CardProps) {
  const { t } = useI18n();
  const { run, feedback, busy } = useAction(onChanged);
  const [day, setDay] = useState(world.day);
  const [season, setSeason] = useState(world.season);
  const [year, setYear] = useState(String(world.year));
  const [time, setTime] = useState(world.time);

  useEffect(() => {
    setDay(world.day);
    setSeason(world.season);
    setYear(String(world.year));
    setTime(world.time);
  }, [world]);

  const submit = (e: Event) => {
    e.preventDefault();
    run(() => api<World>('PATCH', '/api/world', { day, season, year: Number(year), time }), setWorld);
  };

  return (
    <section class="card">
      <h3>{t('world.date')}</h3>
      <form class="fields" onSubmit={submit}>
        <label>
          {t('world.day')}
          <select value={day} onChange={(e) => setDay(Number((e.target as HTMLSelectElement).value))}>
            {Array.from({ length: 28 }, (_, i) => (
              <option key={i} value={i + 1}>
                {i + 1}
              </option>
            ))}
          </select>
        </label>
        <label>
          {t('world.season')}
          <select value={season} onChange={(e) => setSeason((e.target as HTMLSelectElement).value as Season)}>
            {SEASONS.map((s) => (
              <option key={s} value={s}>
                {t(`season.${s}`)}
              </option>
            ))}
          </select>
        </label>
        <NumberField label={t('world.year')} value={year} min={1} onInput={setYear} />
        <label>
          {t('world.time')}
          <select value={time} onChange={(e) => setTime(Number((e.target as HTMLSelectElement).value))}>
            {TIMES.map((value) => (
              <option key={value} value={value}>
                {formatTime(value)}
              </option>
            ))}
          </select>
        </label>
        <button type="submit" disabled={busy}>
          {t('common.apply')}
        </button>
      </form>
      <p class="muted">{t('world.dateHint')}</p>
      <FeedbackLine feedback={feedback} />
    </section>
  );
}

function WeatherCard({ world, setWorld, onChanged }: CardProps) {
  const { t } = useI18n();
  const { run, feedback } = useAction(onChanged);
  const set = (context: string, field: 'today' | 'tomorrow', value: string) =>
    run(() => api<World>('PUT', `/api/world/weather/${encodeURIComponent(context)}`, { [field]: value }), setWorld);

  const select = (context: string, field: 'today' | 'tomorrow', current: string) => (
    <select value={current} onChange={(e) => set(context, field, (e.target as HTMLSelectElement).value)} aria-label={`${t(`context.${context}`)} ${t(`world.${field}`)}`}>
      {/* festival or wedding days aren't choosable, but still shown as the current value */}
      {!world.weathers.includes(current) && <option value={current}>{t(`weather.${current}`)}</option>}
      {world.weathers.map((w) => (
        <option key={w} value={w}>
          {t(`weather.${w}`)}
        </option>
      ))}
    </select>
  );

  return (
    <section class="card">
      <h3>{t('world.weather')}</h3>
      <table class="table">
        <thead>
          <tr>
            <th />
            <th>{t('world.today')}</th>
            <th>{t('world.tomorrow')}</th>
          </tr>
        </thead>
        <tbody>
          {world.weather.map((w) => (
            <tr key={w.context}>
              <th scope="row">{t(`context.${w.context}`)}</th>
              <td>{select(w.context, 'today', w.today)}</td>
              <td>{select(w.context, 'tomorrow', w.tomorrow)}</td>
            </tr>
          ))}
        </tbody>
      </table>
      <p class="muted">{t('world.weatherHint')}</p>
      <FeedbackLine feedback={feedback} />
    </section>
  );
}

function WarpCard({ world, setWorld, onChanged }: CardProps) {
  const { t } = useI18n();
  const { run, feedback, busy } = useAction(onChanged);
  const [location, setLocation] = useState(world.locations[0]?.name ?? '');

  return (
    <section class="card">
      <h3>{t('world.otherLocations')}</h3>
      <form
        class="fields"
        onSubmit={(e) => {
          e.preventDefault();
          run(() => api<World>('POST', '/api/world/warp', { location }), setWorld);
        }}
      >
        <select value={location} onChange={(e) => setLocation((e.target as HTMLSelectElement).value)} aria-label={t('world.warp')}>
          {world.locations.map((l) => (
            <option key={l.name} value={l.name}>
              {l.displayName}
              {l.name === world.currentLocation ? ` — ${t('world.here')}` : ''}
            </option>
          ))}
        </select>
        <button type="submit" disabled={busy}>
          {t('world.go')}
        </button>
      </form>
      <FeedbackLine feedback={feedback} />
    </section>
  );
}

/** The game's world map with clickable places; clicking one teleports there. */
function MapCard({ setWorld, onChanged }: CardProps) {
  const { t } = useI18n();
  const { run, feedback, busy } = useAction(onChanged);
  const [regions, setRegions] = useState<MapRegion[] | null>(null);
  const [regionId, setRegionId] = useState('Valley');
  const [hover, setHover] = useState<string | null>(null);
  // bump to reload the image and areas after a warp (the 'you are here' marker moves)
  const [version, setVersion] = useState(0);

  useEffect(() => {
    api<MapRegion[]>('GET', '/api/world/map').then((list) => {
      setRegions(list);
      if (!list.some((r) => r.id === regionId)) setRegionId(list[0]?.id ?? '');
    });
  }, [version]);

  const region = regions?.find((r) => r.id === regionId);
  const warp = (location: string) =>
    run(
      () => api<World>('POST', '/api/world/warp', { location }),
      (w) => {
        setWorld(w);
        setVersion(version + 1);
      },
    );

  return (
    <section class="card">
      <div class="card-header">
        <h3>{t('world.map')}</h3>
        {regions && regions.length > 1 && (
          <div class="tabs compact">
            {regions.map((r) => (
              <button key={r.id} class={r.id === regionId ? 'active' : ''} onClick={() => setRegionId(r.id)}>
                {t(`region.${r.id}`)}
              </button>
            ))}
          </div>
        )}
      </div>
      <p class="muted">{t('world.mapHint')}</p>
      {region && (
        <div class="world-map" style={{ aspectRatio: `${region.width} / ${region.height}` }}>
          <img src={`${mapImageUrl(region.id)}&v=${version}`} alt={t('world.map')} />
          {region.areas
            .filter((a) => a.location)
            .map((a) => (
              <button
                key={a.id}
                class={`map-area ${a.current ? 'current' : ''}`}
                style={{
                  left: `${(a.x / region.width) * 100}%`,
                  top: `${(a.y / region.height) * 100}%`,
                  width: `${(a.width / region.width) * 100}%`,
                  height: `${(a.height / region.height) * 100}%`,
                }}
                disabled={busy}
                title={[a.name ?? a.location, a.detail].filter(Boolean).join('\n')}
                aria-label={`${t('world.go')} : ${a.name ?? a.location}`}
                onMouseEnter={() => setHover([a.name ?? a.location, a.detail].filter(Boolean).join(' — '))}
                onMouseLeave={() => setHover(null)}
                onClick={() => warp(a.location!)}
              />
            ))}
          {hover && <span class="map-label">{hover}</span>}
        </div>
      )}
      <FeedbackLine feedback={feedback} />
    </section>
  );
}
