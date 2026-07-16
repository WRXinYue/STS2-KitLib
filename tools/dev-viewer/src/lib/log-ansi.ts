/** ANSI colors aligned with in-game LogViewerUI / KitLog.Cli. */

export const ANSI_RESET = "\x1b[0m";

export const COL_TIME = "55556A";
export const COL_INFO = "C8C8DC";
export const COL_WARN = "FFC840";
export const COL_ERROR = "FF5F5F";
export const COL_DEBUG = "6A6A8A";
export const COL_LOAD = "7ADCDC";
export const COL_BOUNDARY = "7ADCDC";

const GAME_DIM_AMOUNT = 0.18;

export function ansiTrueColorFg(hexRgb: string): string {
  const hex = hexRgb.replace("#", "");
  if (hex.length !== 6)
    return "\x1b[37m";
  const r = Number.parseInt(hex.slice(0, 2), 16);
  const g = Number.parseInt(hex.slice(2, 4), 16);
  const b = Number.parseInt(hex.slice(4, 6), 16);
  return `\x1b[38;2;${r};${g};${b}m`;
}

export function ansiForStreamLevel(lvl: string): string {
  switch (lvl.toLowerCase()) {
    case "error":
      return ansiTrueColorFg(COL_ERROR);
    case "warn":
    case "warning":
      return ansiTrueColorFg(COL_WARN);
    case "debug":
    case "dbg":
    case "vdb":
    case "verydebug":
      return ansiTrueColorFg(COL_DEBUG);
    case "load":
      return ansiTrueColorFg(COL_LOAD);
    default:
      return ansiTrueColorFg(COL_INFO);
  }
}

export function levelColorHex(lvl: string): string {
  switch (lvl.toLowerCase()) {
    case "error":
      return COL_ERROR;
    case "warn":
    case "warning":
      return COL_WARN;
    case "load":
      return COL_LOAD;
    case "info":
      return COL_INFO;
    default:
      return COL_DEBUG;
  }
}

export function dimHex(hex: string, amount = GAME_DIM_AMOUNT): string {
  const raw = hex.replace("#", "");
  if (raw.length !== 6)
    return hex;
  const factor = 1 - amount;
  const r = Math.round(Number.parseInt(raw.slice(0, 2), 16) * factor);
  const g = Math.round(Number.parseInt(raw.slice(2, 4), 16) * factor);
  const b = Math.round(Number.parseInt(raw.slice(4, 6), 16) * factor);
  return [r, g, b].map((v) => v.toString(16).padStart(2, "0")).join("");
}

export function ansiScopeDim(): string {
  return ansiTrueColorFg(COL_TIME);
}

export function appendAnsiSegment(sb: string[], ansi: string, text: string) {
  if (!text)
    return;
  sb.push(ansi, text, ANSI_RESET);
}
