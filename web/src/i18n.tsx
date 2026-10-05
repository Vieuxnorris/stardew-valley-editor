import { createContext, type ComponentChildren } from 'preact';
import { useContext, useState } from 'preact/hooks';

const fr = {
  'app.title': 'Valley Editor',
  'status.offline': 'Jeu injoignable — le jeu est-il lancé avec SMAPI ?',
  'status.noToken': "Jeton manquant : ouvrez l'éditeur avec la commande « editor » dans la console SMAPI.",
  'status.noSave': 'Aucune partie chargée. Chargez une sauvegarde dans le jeu.',
  'status.unsaved': 'Modifications non sauvegardées : elles seront enregistrées quand vous dormirez dans le jeu.',
  'tab.player': 'Joueur',
  'player.money': 'Or',
  'common.apply': 'Appliquer',
  'common.loading': 'Chargement…',
  'common.saved': 'Appliqué',
  'common.error': 'Erreur',
};

type Key = keyof typeof fr;

const en: Record<Key, string> = {
  'app.title': 'Valley Editor',
  'status.offline': "Can't reach the game. Is it running with SMAPI?",
  'status.noToken': "Missing token: open the editor with the 'editor' command in the SMAPI console.",
  'status.noSave': 'No save loaded. Load a save in the game.',
  'status.unsaved': 'Unsaved changes: they are written when you sleep in the game.',
  'tab.player': 'Player',
  'player.money': 'Gold',
  'common.apply': 'Apply',
  'common.loading': 'Loading…',
  'common.saved': 'Applied',
  'common.error': 'Error',
};

const dictionaries = { fr, en };
export type Lang = keyof typeof dictionaries;

const LANG_KEY = 'valley-editor-lang';

function initialLang(): Lang {
  try {
    const stored = localStorage.getItem(LANG_KEY);
    if (stored === 'fr' || stored === 'en') return stored;
  } catch {
    // storage blocked
  }
  return navigator.language.toLowerCase().startsWith('fr') ? 'fr' : 'en';
}

interface I18n {
  lang: Lang;
  setLang: (lang: Lang) => void;
  t: (key: Key) => string;
}

const I18nContext = createContext<I18n>({ lang: 'fr', setLang: () => {}, t: (key) => fr[key] });

export function I18nProvider({ children }: { children: ComponentChildren }) {
  const [lang, setLangState] = useState<Lang>(initialLang);
  const setLang = (next: Lang) => {
    setLangState(next);
    document.documentElement.lang = next;
    try {
      localStorage.setItem(LANG_KEY, next);
    } catch {
      // storage blocked
    }
  };
  const t = (key: Key) => dictionaries[lang][key];
  return <I18nContext.Provider value={{ lang, setLang, t }}>{children}</I18nContext.Provider>;
}

export const useI18n = () => useContext(I18nContext);
