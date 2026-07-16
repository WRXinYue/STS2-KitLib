/// <reference types="vite/client" />

declare global {
  interface Window {
    __COMBAT_STATS__?: import("./src/types").CombatStatsLiveDto;
  }
}

export {};
