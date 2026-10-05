// Client for the mod's JSON API. Every call carries the per-session token from the URL the
// mod printed (`?token=...`), kept in sessionStorage so reloads keep working.

const TOKEN_KEY = 'valley-editor-token';

function readToken(): string | null {
  const params = new URLSearchParams(location.search);
  const fromUrl = params.get('token');
  if (fromUrl) {
    try {
      sessionStorage.setItem(TOKEN_KEY, fromUrl);
    } catch {
      // storage blocked: the token still works for this page view
    }
    params.delete('token');
    const query = params.toString();
    history.replaceState(null, '', location.pathname + (query ? `?${query}` : '') + location.hash);
    return fromUrl;
  }
  try {
    return sessionStorage.getItem(TOKEN_KEY);
  } catch {
    return null;
  }
}

const token = readToken();

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    message: string,
  ) {
    super(message);
  }
}

export async function api<T>(method: string, path: string, body?: unknown): Promise<T> {
  let response: Response;
  try {
    response = await fetch(path, {
      method,
      headers: {
        'X-Editor-Token': token ?? '',
        ...(body === undefined ? {} : { 'Content-Type': 'application/json' }),
      },
      body: body === undefined ? undefined : JSON.stringify(body),
    });
  } catch {
    throw new ApiError(0, 'offline');
  }

  const data = await response.json().catch(() => null);
  if (!response.ok) throw new ApiError(response.status, data?.error ?? response.statusText);
  return data as T;
}

export const hasToken = token !== null;

/** URL of an item icon; images can't send headers, so the token goes in the query. */
export const spriteUrl = (qualifiedId: string) => `/api/sprites/${encodeURIComponent(qualifiedId)}?token=${token ?? ''}`;

export interface Status {
  worldReady: boolean;
  saveName: string | null;
  farmName: string | null;
  unsavedChanges: boolean;
  gameVersion: string;
  modVersion: string;
}

export type SkillKey = 'farming' | 'fishing' | 'foraging' | 'mining' | 'combat';

export interface Skill {
  key: SkillKey;
  name: string;
  level: number;
  xp: number;
  nextLevelXp: number | null;
  pendingLevelUps: number;
}

export interface Profession {
  id: number;
  skill: SkillKey;
  tier: 5 | 10;
  parent: number | null;
  name: string;
}

export interface Player {
  name: string;
  farmName: string;
  money: number;
  qiGems: number;
  goldenWalnuts: number;
  health: number;
  maxHealth: number;
  stamina: number;
  maxStamina: number;
  masteryExp: number;
  masteryLevel: number;
  skills: Skill[];
  professions: number[];
  professionCatalog: Profession[];
}

export interface ItemStack {
  qualifiedId: string;
  name: string;
  stack: number;
  maxStack: number;
  quality: number;
  canHaveQuality: boolean;
}

export interface Inventory {
  size: number;
  slots: (ItemStack | null)[];
}

export interface CatalogEntry {
  qualifiedId: string;
  name: string;
  internalName: string;
  type: string;
  category: number;
  categoryName: string;
  modId: string | null;
  modName: string | null;
}

export interface CatalogPage {
  total: number;
  items: CatalogEntry[];
}

export interface Facets {
  types: { id: string; count: number }[];
  mods: { id: string; name: string | null; count: number }[];
}

export type Season = 'spring' | 'summer' | 'fall' | 'winter';

export interface World {
  day: number;
  season: Season;
  year: number;
  time: number;
  daysPlayed: number;
  weathers: string[];
  weather: { context: string; today: string; tomorrow: string }[];
  currentLocation: string | null;
  locations: { name: string; displayName: string }[];
}

export interface Progression {
  unlocks: { id: string; value: boolean }[];
  mines: { minesLevel: number; skullCavernLevel: number };
  communityCenter: { area: number; name: string; complete: boolean }[];
  museum: { donated: number; donatable: number };
}

export interface Villager {
  name: string;
  displayName: string;
  met: boolean;
  points: number;
  hearts: number;
  maxHearts: number;
  maxPoints: number;
  datable: boolean;
  status: 'Friendly' | 'Dating' | 'Engaged' | 'Married' | 'Divorced';
  giftsThisWeek: number;
  giftsToday: number;
  talkedToToday: boolean;
  birthSeason: Season | null;
  birthDay: number | null;
  location: string | null;
}

export const monsterSpriteUrl = (name: string) => `/api/monster-sprites/${encodeURIComponent(name)}?token=${token ?? ''}`;
export const mapImageUrl = (region: string) => `/api/world/map/${encodeURIComponent(region)}/image?token=${token ?? ''}`;

export interface MapRegion {
  id: string;
  width: number;
  height: number;
  areas: { id: string; name: string | null; detail: string | null; x: number; y: number; width: number; height: number; location: string | null; current: boolean }[];
}

export const portraitUrl = (name: string) => `/api/portraits/${encodeURIComponent(name)}?token=${token ?? ''}`;

export type MineBand = 'copper' | 'iron' | 'gold' | 'iridium';

export interface Rules {
  cropGrowth: number;
  cropGrowthOverrides: Record<string, number>;
  fruitTreeSpeed: number;
  wildTreeGrowth: number;
  machineTime: number;
  mineOre: Partial<Record<MineBand, number>>;
  mineStones: number;
  mineMonsters: number;
  mineGems: number;
  mineAlwaysLadder: boolean;
  instantFishing: boolean;
  perfectCatch: boolean;
  alwaysTreasure: boolean;
  pickupMultiplier: number;
  minQuality: number;
  monsterLootRolls: number;
  sellPrice: number;
  infiniteHealth: boolean;
  infiniteStamina: boolean;
  freezeTime: boolean;
  maxDailyLuck: boolean;
  freeBuild: boolean;
  instantBuild: boolean;
  freeCrafting: boolean;
  speedBonus: number;
  magnetRadiusBonus: number;
  luckBonus: number;
}

export interface CropRule {
  seedId: string;
  name: string;
  harvestItemId: string | null;
  baseDays: number | null;
  days: number;
  baseRegrowDays: number | null;
  regrowDays: number;
  override: number | null;
}

export interface RulesSnapshot {
  rules: Rules;
  mineBands: MineBand[];
  crops: CropRule[];
}

export interface LootEntry {
  itemId: string;
  name?: string | null;
  chance: number;
  minStack: number;
  maxStack: number;
  quality: number;
}

export interface FishingSnapshot {
  settings: {
    fishPerCatch: number;
    fishQuality: number;
    fishMaxSize: boolean;
    forcedFishId: string | null;
    treasureMultiplier: number;
    treasureRolls: number;
    treasureReplaceVanilla: boolean;
    treasureLoot: LootEntry[];
  };
  fish: { id: string; name: string; crabPot: boolean; maxSize: number | null; caught: number; recordSize: number }[];
}

export interface FishTable {
  location: string;
  displayName: string;
  entries: {
    id: string | null;
    itemId: string | null;
    name: string;
    baseChance: number;
    chance: number;
    removed: boolean;
    baseSeason: Season | null;
    /** Season override: a season, 'any', or null for the vanilla season. */
    season: Season | 'any' | null;
    unrestricted: boolean;
    priority: boolean;
    catchLimit: number;
    condition: string | null;
    isBossFish: boolean;
  }[];
  added: LootEntry[];
}

export interface MonsterDrop {
  itemId: string;
  name: string | null;
  chance: number;
}

export interface Monster {
  key: string;
  displayName: string;
  edited: boolean;
  baseDrops: MonsterDrop[];
  drops: MonsterDrop[];
}

export interface SpecialOrders {
  active: { index: number; key: string; name: string; state: string; objectives: { description: string; current: number; max: number }[] }[];
  catalog: { key: string; name: string; requester: string; completed: boolean }[];
}

export interface Quest {
  index: number;
  id: string | null;
  name: string;
  completed: boolean;
  daysLeft: number | null;
}

export interface ChestInfo {
  id: string;
  name: string;
  isFridge: boolean;
  qualifiedId: string;
  location: string;
  locationName: string;
  x: number | null;
  y: number | null;
  used: number;
  capacity: number;
  color: string | null;
}

export interface FieldStats {
  location: string;
  displayName: string;
  crops: number;
  dry: number;
  ready: number;
  dead: number;
  fruitTrees: number;
  youngTrees: number;
}

export interface BuildingInfo {
  id: string;
  type: string;
  name: string;
  locationName: string;
  x: number;
  y: number;
  daysOfConstructionLeft: number;
  daysUntilUpgrade: number;
  upgradeName: string | null;
  animals: number | null;
  animalLimit: number | null;
  upgrades: { type: string; name: string }[];
}

export interface Farm {
  fields: FieldStats[];
  buildings: BuildingInfo[];
  house: { level: number; maxLevel: number; daysUntilUpgrade: number };
}

export interface Animal {
  id: string;
  name: string;
  type: string;
  typeName: string;
  locationName: string;
  home: string | null;
  friendship: number;
  happiness: number;
  fullness: number;
  age: number;
  daysToMature: number;
  isAdult: boolean;
  wasPet: boolean;
  produce: string | null;
  mood: string;
}

export const buildingSpriteUrl = (id: string, version: string) => `/api/building-sprites/${encodeURIComponent(id)}?token=${token ?? ''}&v=${encodeURIComponent(version)}`;
export const animalSpriteUrl = (id: string, version: string) => `/api/animal-sprites/${encodeURIComponent(id)}?token=${token ?? ''}&v=${encodeURIComponent(version)}`;

export interface PetInfo {
  id: string;
  name: string;
  type: string;
  typeName: string;
  locationName: string | null;
  friendship: number;
  maxFriendship: number;
  pettedToday: boolean;
  timesPet: number;
  hasBowl: boolean;
  bowlWatered: boolean;
}

export const petSpriteUrl = (id: string) => `/api/pet-sprites/${encodeURIComponent(id)}?token=${token ?? ''}`;
