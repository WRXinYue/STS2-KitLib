# Save / Load

- Named slots for run snapshots
- Stored under DevMode user data (`mod_data/KitLib/snapshots/`), separate from vanilla `progress.save`

**Progress protection** (title screen **DEVMODE → Progress protection**) backs up the game’s profile `progress.save` when the mod set changes. Backups: `mod_data/KitLib/profile_backups/{timestamp}_profile{N}/`. Restore writes `progress.save.pre_restore_{timestamp}` before overwriting the active save.
