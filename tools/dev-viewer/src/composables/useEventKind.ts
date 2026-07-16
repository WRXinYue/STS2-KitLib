export function kindBadgeClass(kind: string): string {
  const map: Record<string, string> = {
    DamageDealt: "border-amber-500/40 bg-amber-500/15 text-amber-400",
    DamageTaken: "border-red-400/40 bg-red-400/15 text-red-300",
    BlockGained: "border-sky-400/40 bg-sky-400/15 text-sky-300",
    CardPlayed: "border-indigo-400/40 bg-indigo-400/15 text-indigo-300",
    EnergySpent: "border-slate-400/40 bg-slate-400/15 text-slate-300",
    PotionUsed: "border-yellow-400/40 bg-yellow-400/15 text-yellow-300",
    DebuffApplied: "border-violet-400/40 bg-violet-400/15 text-violet-300",
    BuffApplied: "border-emerald-400/40 bg-emerald-400/15 text-emerald-300",
    PowerSynergy: "border-violet-400/40 bg-violet-400/15 text-violet-300",
    EnemyMove: "border-zinc-400/40 bg-zinc-400/15 text-zinc-300",
  };
  return map[kind] ?? "border-border bg-muted text-muted-foreground";
}
