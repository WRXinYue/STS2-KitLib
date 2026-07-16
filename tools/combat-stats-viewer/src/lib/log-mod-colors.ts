/** Mod highlight palette — matches LogSourceColors.ModPalette / KitLog.Cli LogModColors. */

const PALETTE = [
  "7399f2",
  "85cc85",
  "f2a861",
  "d185eb",
  "7adcdc",
  "eb7a8c",
  "d1c26b",
  "9e99f2",
] as const;

export function modColorHex(modId: string): string {
  let hash = 17;
  for (const c of modId)
    hash = (hash * 31 + c.charCodeAt(0)) | 0;
  return PALETTE[Math.abs(hash) % PALETTE.length] ?? PALETTE[0];
}
