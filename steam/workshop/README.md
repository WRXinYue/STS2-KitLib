# KitLib Steam Workshop workspace

Used with [Mega Crit's sts2-mod-uploader](https://github.com/megacrit/sts2-mod-uploader).

## Setup

1. Download `ModUploader.exe` from the [releases](https://github.com/megacrit/sts2-mod-uploader/releases) page.
2. Place it at e.g. `C:\tools\sts2-mod-uploader\ModUploader.exe`.
3. In the repo root `.env` (copy from `.env.example`):

   ```env
   STS2_MOD_UPLOADER=C:\tools\sts2-mod-uploader\ModUploader.exe
   ```

## Publish

```bash
make steam-workspace   # build + fill content/ + bilingual changeNote from CHANGELOG
make upload-steam      # run ModUploader (Steam client must be running)
```

`sync` writes `changeNote.preview.txt` (git-ignored) so you can review BBCode before upload.
Uses the latest released changelog section, or `[Unreleased]` when no release section exists yet.
Pass `make steam-workspace UNRELEASED=1` to force `[Unreleased]`, or `CHANGE_NOTE=...` to override.

First upload creates `mod_id.txt` (git-ignored). Later uploads update the same Workshop item.

Edit `workshop.json` for visibility, tags, and dependencies before going public. Keep `visibility` as `private` until you have tested a subscribed install.
