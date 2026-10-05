import { createContext, type ComponentChildren } from 'preact';
import { useContext, useState } from 'preact/hooks';

const fr = {
  'app.title': 'Valley Editor',
  'status.offline': 'Jeu injoignable — le jeu est-il lancé avec SMAPI ?',
  'status.noToken': "Jeton manquant : ouvrez l'éditeur avec la commande « editor » dans la console SMAPI.",
  'status.noSave': 'Aucune partie chargée. Chargez une sauvegarde dans le jeu.',
  'status.unsaved': 'Modifications non sauvegardées : elles seront enregistrées quand vous dormirez dans le jeu.',
  'tab.player': 'Joueur',
  'tab.inventory': 'Inventaire',
  'player.resources': 'Ressources',
  'player.money': 'Or',
  'player.qiGems': 'Gemmes Qi',
  'player.goldenWalnuts': 'Noix dorées',
  'player.vitals': 'Santé et énergie',
  'player.health': 'PV',
  'player.maxHealth': 'PV max',
  'player.stamina': 'Énergie',
  'player.maxStamina': 'Énergie max',
  'player.skills': 'Compétences',
  'player.skillsHint': 'Monter un niveau déclenche les écrans de montée de niveau au coucher (recettes, choix de profession), comme en jeu normal. Modifier l’XP directement ne les déclenche pas.',
  'player.level': 'Niveau',
  'player.xp': 'XP',
  'player.pending': 'au coucher',
  'player.professions': 'Professions',
  'player.professionsHint': 'Le jeu n’autorise normalement qu’une profession par palier ; ici, vous pouvez tout cocher.',
  'player.mastery': 'Maîtrise',
  'player.masteryExp': 'XP de maîtrise',
  'player.masteryLevel': 'Niveau de maîtrise',
  'inv.backpack': 'Sac à dos',
  'inv.size': 'Taille',
  'inv.slots': 'cases',
  'inv.empty': 'Case vide',
  'inv.dragHint': 'Glissez-déposez pour déplacer. Cliquez sur une case pour la modifier.',
  'inv.stack': 'Quantité',
  'inv.quality': 'Qualité',
  'inv.delete': 'Supprimer',
  'inv.add': 'Ajouter un objet',
  'inv.addTo': 'Ajouter au sac',
  'quality.0': 'Normale',
  'quality.1': 'Argent',
  'quality.2': 'Or',
  'quality.4': 'Iridium',
  'catalog.search': 'Rechercher (nom ou ID)…',
  'catalog.allTypes': 'Tous les types',
  'catalog.allMods': 'Toutes les origines',
  'catalog.vanilla': 'Jeu de base',
  'catalog.results': 'résultats',
  'catalog.more': 'Plus de résultats',
  'catalog.pick': 'Choisissez un objet dans la liste.',
  'type.(O)': 'Objets',
  'type.(BC)': 'Gros objets',
  'type.(F)': 'Meubles',
  'type.(H)': 'Chapeaux',
  'type.(B)': 'Bottes',
  'type.(P)': 'Pantalons',
  'type.(S)': 'Chemises',
  'type.(T)': 'Outils',
  'type.(W)': 'Armes',
  'type.(TR)': 'Bibelots',
  'type.(FL)': 'Sols',
  'type.(WP)': 'Papiers peints',
  'type.(M)': 'Mannequins',
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
  'tab.inventory': 'Inventory',
  'player.resources': 'Resources',
  'player.money': 'Gold',
  'player.qiGems': 'Qi Gems',
  'player.goldenWalnuts': 'Golden Walnuts',
  'player.vitals': 'Health and energy',
  'player.health': 'Health',
  'player.maxHealth': 'Max health',
  'player.stamina': 'Energy',
  'player.maxStamina': 'Max energy',
  'player.skills': 'Skills',
  'player.skillsHint': 'Raising a level shows the level-up screens when you sleep (recipes, profession choice), like normal play. Editing XP directly does not.',
  'player.level': 'Level',
  'player.xp': 'XP',
  'player.pending': 'at bedtime',
  'player.professions': 'Professions',
  'player.professionsHint': 'The game normally allows one profession per tier; here you can tick any.',
  'player.mastery': 'Mastery',
  'player.masteryExp': 'Mastery XP',
  'player.masteryLevel': 'Mastery level',
  'inv.backpack': 'Backpack',
  'inv.size': 'Size',
  'inv.slots': 'slots',
  'inv.empty': 'Empty slot',
  'inv.dragHint': 'Drag and drop to move. Click a slot to edit it.',
  'inv.stack': 'Quantity',
  'inv.quality': 'Quality',
  'inv.delete': 'Delete',
  'inv.add': 'Add an item',
  'inv.addTo': 'Add to backpack',
  'quality.0': 'Normal',
  'quality.1': 'Silver',
  'quality.2': 'Gold',
  'quality.4': 'Iridium',
  'catalog.search': 'Search (name or ID)…',
  'catalog.allTypes': 'All types',
  'catalog.allMods': 'All sources',
  'catalog.vanilla': 'Base game',
  'catalog.results': 'results',
  'catalog.more': 'More results',
  'catalog.pick': 'Pick an item from the list.',
  'type.(O)': 'Objects',
  'type.(BC)': 'Big craftables',
  'type.(F)': 'Furniture',
  'type.(H)': 'Hats',
  'type.(B)': 'Boots',
  'type.(P)': 'Pants',
  'type.(S)': 'Shirts',
  'type.(T)': 'Tools',
  'type.(W)': 'Weapons',
  'type.(TR)': 'Trinkets',
  'type.(FL)': 'Flooring',
  'type.(WP)': 'Wallpaper',
  'type.(M)': 'Mannequins',
  'common.apply': 'Apply',
  'common.loading': 'Loading…',
  'common.saved': 'Applied',
  'common.error': 'Error',
};

const dictionaries: Record<string, Record<string, string>> = { fr, en };
export type Lang = 'fr' | 'en';

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
  /** Translate a key; unknown keys (e.g. a new item type) fall back to the key itself. */
  t: (key: Key | (string & {})) => string;
}

const translate = (lang: Lang, key: string) => dictionaries[lang][key] ?? key;

const I18nContext = createContext<I18n>({ lang: 'fr', setLang: () => {}, t: (key) => translate('fr', key) });

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
  return <I18nContext.Provider value={{ lang, setLang, t: (key) => translate(lang, key) }}>{children}</I18nContext.Provider>;
}

export const useI18n = () => useContext(I18nContext);
