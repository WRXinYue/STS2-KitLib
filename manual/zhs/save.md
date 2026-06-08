# 存档

- 命名槽位保存、加载 run 快照
- 与游戏内 `progress.save` 独立，存于 DevMode 用户数据（`mod_data/KitLib/snapshots/`）

**进度保护**（标题画面 **DEVMODE → 进度保护**）在 mod 集变化时备份 profile 的 `progress.save`，路径：`mod_data/KitLib/profile_backups/{时间戳}_profile{N}/`。恢复覆盖前会写入 `progress.save.pre_restore_{timestamp}`。
