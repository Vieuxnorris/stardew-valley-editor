import { useEffect, useState } from 'preact/hooks';
import { api, type Player } from '../api';
import { useI18n } from '../i18n';

export function PlayerTab({ onChanged }: { onChanged: () => void }) {
  const { t } = useI18n();
  const [player, setPlayer] = useState<Player | null>(null);
  const [money, setMoney] = useState('');
  const [message, setMessage] = useState<{ ok: boolean; text: string } | null>(null);

  const show = (p: Player) => {
    setPlayer(p);
    setMoney(String(p.money));
  };

  useEffect(() => {
    api<Player>('GET', '/api/player')
      .then(show)
      .catch((e) => setMessage({ ok: false, text: e.message }));
  }, []);

  const apply = async (e: Event) => {
    e.preventDefault();
    setMessage(null);
    try {
      show(await api<Player>('PATCH', '/api/player', { money: Number(money) }));
      setMessage({ ok: true, text: t('common.saved') });
      onChanged();
    } catch (err) {
      setMessage({ ok: false, text: `${t('common.error')} : ${(err as Error).message}` });
    }
  };

  if (!player) return <p>{message?.text ?? t('common.loading')}</p>;

  return (
    <section class="card">
      <h2>
        {player.name} — {player.farmName}
      </h2>
      <form class="fields" onSubmit={apply}>
        <label>
          {t('player.money')}
          <input type="number" min={0} step={1} value={money} onInput={(e) => setMoney((e.target as HTMLInputElement).value)} />
        </label>
        <button type="submit">{t('common.apply')}</button>
      </form>
      {message && <p class={message.ok ? 'ok' : 'error'}>{message.text}</p>}
    </section>
  );
}
