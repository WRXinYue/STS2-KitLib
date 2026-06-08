# Settings

- Game speed, skip animations, UI **theme**
- **Rail layout**: drag to reorder, hide tabs, reset defaults
- Global options such as show hidden cards (synced with the card browser Library row)
- **Progress protection** (also on title screen **DEVMODE → Progress protection**):
  - **Auto-backup on mod set change** — copy active profile `progress.save` (and optional related saves) before vanilla filtering when the mod fingerprint changes at startup
  - **Warn on removed-mod progress residue** — log-only when progress still references unloaded mods
  - **Prompt on mod character progress loss** — on main menu load, offer **Restore** / **Not now** when a recent backup has recoverable mod character stats that were stripped from the current save

Backups live under `mod_data/KitLib/profile_backups/{timestamp}_profile{N}/` (see README **Progress protection** for full paths). Restore is title-screen only; match the backup mod set when possible.
