# KitLib — build pipeline
#
#   build   → artifacts under repo build/KitLib/  (CI-safe, no game writes)
#   deploy  → copy build/KitLib/ into game mods/KitLib/ (other mods untouched)
#   sync    → build + deploy (default local dev loop)

DOTNET ?= dotnet

# Read version from KitLib.json (Windows Store python3 alias is often broken; use python on Windows)
ifeq ($(OS),Windows_NT)
PYTHON ?= python
else
PYTHON ?= python3
endif
UV ?= uv
VERSION := $(shell $(PYTHON) -c "import json;print(json.load(open('KitLib.json',encoding='utf-8'))['version'])")

MOD_MAIN := src/KitLib.Core/KitLib.Core.csproj
SMOKE_MOD_PROJECT := samples/KitLibSmokeMod/KitLibSmokeMod.csproj
SMOKE_MOD_TESTS := tests/KitLib.SmokeMod.Tests/KitLib.SmokeMod.Tests.csproj
MCP_PROJECT := tools/KitLib.Mcp/KitLib.Mcp.csproj
KITLOG_PROJECT := tools/KitLog.Cli/KitLog.Cli.csproj

# Runtime identifier for self-contained tool publish (override: make build-tools TOOLS_RID=linux-x64)
ifeq ($(OS),Windows_NT)
TOOLS_RID ?= win-x64
else
UNAME_S := $(shell uname -s 2>/dev/null)
UNAME_M := $(shell uname -m 2>/dev/null)
ifeq ($(UNAME_S),Darwin)
ifeq ($(UNAME_M),arm64)
TOOLS_RID ?= osx-arm64
else
TOOLS_RID ?= osx-x64
endif
else
ifeq ($(UNAME_M),aarch64)
TOOLS_RID ?= linux-arm64
else
TOOLS_RID ?= linux-x64
endif
endif
endif

TOOLS_PUBLISH_DIR := build/tools/KitLib.Mcp/$(TOOLS_RID)/publish
KITLOG_PUBLISH_DIR := build/tools/KitLog.Cli/$(TOOLS_RID)/publish
TOOLS_PUBLISH_FLAGS := -c Release -r $(TOOLS_RID) --self-contained true -p:PublishSingleFile=true -o $(TOOLS_PUBLISH_DIR)
DEPLOY_TOOLS := $(PYTHON) scripts/deploy_tools.py --tools-rid $(TOOLS_RID)
DEPLOY_TOOLS_BUILD := $(PYTHON) scripts/deploy_tools.py --tools-rid $(TOOLS_RID) --build-if-missing

# Use -p: (not /p:) so Git Bash on Windows does not treat /p:... as a MSYS path.
DEPLOY_TO_GAME := -p:DeployToGame=true

# Sts2Profile from local.props (make init) selects stable|beta ref channel (see Directory.Build.props).
STS2_COMPILE_PROFILE ?= $(shell $(PYTHON) scripts/resolve_sts2_compile_profile.py)
STS2_MSBUILD_PROFILE := -p:Sts2Profile=$(STS2_COMPILE_PROFILE)
# Copy build/KitLib/ into mods/KitLib/ only — never republish into the game tree.
DEPLOY_COPY   := $(DOTNET) msbuild $(MOD_MAIN) -t:DeployRepoBuildToMods -p:DeployFromRepoBuild=true

ZIP_MCP_NAME := build/KitLib.Mcp-v$(VERSION)-$(TOOLS_RID).zip
ZIP_KITLOG_NAME := build/KitLog.Cli-v$(VERSION)-$(TOOLS_RID).zip
MCP_PUBLISH_EXE := $(TOOLS_PUBLISH_DIR)/KitLib.Mcp.exe
MCP_PUBLISH_BIN := $(TOOLS_PUBLISH_DIR)/KitLib.Mcp
KITLOG_PUBLISH_EXE := $(KITLOG_PUBLISH_DIR)/kitlog.exe
KITLOG_PUBLISH_BIN := $(KITLOG_PUBLISH_DIR)/kitlog
KITLOG_PUBLISH_FLAGS := -c Release -r $(TOOLS_RID) --self-contained true -p:PublishSingleFile=true -o $(KITLOG_PUBLISH_DIR)

MOD_PROJECTS := src/KitLib.Core/KitLib.Core.csproj \
	src/KitLib.Modules.User/KitLib.User.csproj src/KitLib.Modules.ModPanel/KitLib.ModPanel.csproj \
	src/KitLib.Modules.Cheat/KitLib.Cheat.csproj \
	src/KitLib.Modules.Dev/KitLib.Dev.csproj src/KitLib.Modules.AI/KitLib.AI.csproj \
	src/KitLib.Modules.Panel/KitLib.Panel.csproj
PACKAGE_MODULES := $(PYTHON) scripts/package_modules.py
STEAM_SYNC_FLAGS := $(if $(CHANGE_NOTE),--change-note "$(CHANGE_NOTE)",) $(if $(UNRELEASED),--unreleased,) $(if $(NO_BRANCH_TARGETING),--no-branch-targeting,)
STEAM_UPLOAD_FLAGS := $(if $(NO_BRANCH_TARGETING),--no-branch-targeting,)
STEAM_SYNC := $(PYTHON) scripts/publish_steam.py sync all $(STEAM_SYNC_FLAGS)
STEAM_SYNC_STABLE := $(PYTHON) scripts/publish_steam.py sync stable $(STEAM_SYNC_FLAGS)
STEAM_SYNC_BETA := $(PYTHON) scripts/publish_steam.py sync beta $(STEAM_SYNC_FLAGS)
STEAM_UPLOAD := $(PYTHON) scripts/publish_steam.py upload all --optional $(STEAM_UPLOAD_FLAGS)
STEAM_UPLOAD_STRICT := $(PYTHON) scripts/publish_steam.py upload all $(STEAM_UPLOAD_FLAGS)
LAUNCH := $(PYTHON) scripts/launch_sts2.py
LAUNCH_MP_CLIENT_ID ?= 1001

.PHONY: help init icons format format-check lint-scripts check test hooks-install hooks-run deps build build-all build-smoke-mod check-smoke-mod deploy-smoke-mod deploy sync sync-full sync-framework-mods compile pck publish nexus nexus-beta upload-all readme-nexus zip zip-full zip-profiles clean docs docs-build \
        build-stable build-beta build-profiles build-flat workshop workshop-stable workshop-beta extract-touchpoints check-api verify-profiles capture-sts2-ref \
        launch sync-launch sync-full-launch launch-mp launch-mp-host launch-mp-join sync-launch-mp dev-session push-android push-android-wsdx233 compile-tools build-tools deploy-tools sync-tools zip-mcp upload-nexus-mcp nexus-mcp \
        compile-kitlog build-kitlog zip-kitlog upload-nexus-kitlog nexus-kitlog \
        upload-github upload-nexus upload-nexus-beta workshop upload-steam upload-steam-stable upload-steam-beta readme-nexus readme-steam readme-assets

help:
	@echo "KitLib — targets"
	@echo ""
	@echo "  init         detect STS2 + Godot, generate local.props + pre-commit hooks"
	@echo "  icons        tree-shake MDI (mdi-used.json + MdiIcon.Generated.cs)"
	@echo "  format       dotnet format KitLib.sln + black scripts/ (EditorConfig / pre-commit)"
	@echo "  format-check dotnet format --verify-no-changes (uses eng/sts2-refs when Sts2Dir unset)"
	@echo "  lint-scripts flake8 scripts/ (setup.cfg)"
	@echo "  check        format-check + lint-scripts"
	@echo "  test         dotnet test KitLib.ModPanel.Tests (sidebar planner + embed probe)"
	@echo "  hooks-install uv sync (dev) + pre-commit git hook"
	@echo "  hooks-run    pre-commit run --all-files"
	@echo "  deps         dotnet restore (does not touch game mods/STS2-RitsuLib by default)"
	@echo ""
	@echo "  sync         build-flat + deploy full mods/KitLib/ bundle (Core + modules/)"
	@echo "  sync-full    sync + deploy tools/ (kitlog + MCP)"
	@echo "  sync-full-launch  sync-full + launch game"
	@echo "  build-all    same as build-flat (current Sts2Profile)"
	@echo "  build-flat   dotnet build for local.props Sts2Profile (flat KitLib.dll)"
	@echo "  workshop-stable  stage build/dist/workshop-stable/ (public branch, same workshop item)"
	@echo "  workshop-beta    stage build/dist/workshop-beta/ (public-beta branch, same workshop item)"
	@echo "  workshop         workshop-stable + workshop-beta"
	@echo "  build-stable dotnet build KitLib.sln against stable sts2 ref (API compile check)"
	@echo "  build-beta   dotnet build KitLib.sln against beta sts2 ref"
	@echo "  build-profiles build-stable then build-beta (CI / pre-release)"
	@echo "  extract-touchpoints  scan src/ → eng/api_touchpoints.yaml"
	@echo "  check-api    reflect KitLib API touchpoints against both sts2.dll"
	@echo "  verify-profiles  build-profiles + check-api (pre-release)"
	@echo "  check-smoke-mod  build + content-mod smoke fixture + Cecil/ref/load tests"
	@echo "  deploy-smoke-mod copy smoke mod into game mods/KitLibSmokeMod/ (manual in-game check)"
	@echo "  capture-sts2-ref PROFILE=stable|beta  copy sts2.dll into eng/sts2-refs/ (validates release_info)"
	@echo "  zip-profiles build stable + beta release zips (KitLib-vX.zip + KitLib-vX-beta.zip)"
	@echo "  zip-full     build-all + package build/KitLib-vX.X.X.zip (local profile only)"
	@echo "  sync-launch  sync + launch game"
	@echo "  launch       launch STS2 (Vulkan on Windows; Steam on macOS/Linux)"
	@echo "  launch-mp-host  fastmp host instance (Vulkan)"
	@echo "  launch-mp-join  fastmp join client (default clientId=1001)"
	@echo "  launch-mp    dual-launch host + join for LAN multiplayer test"
	@echo "  sync-launch-mp  sync + launch-mp"
	@echo "  dev-session  sync + launch + wait for MCP bridge (agent bootstrap)"
	@echo "  sync-framework-mods  copy DevMode NuGet STS2-RitsuLib into game (overwrites other RitsuLib builds)"
	@echo "  push-android build then adb push to Android mods dir (default: StS2LauncherMM/Mods)"
	@echo "  push-android-wsdx233  push to game sandbox (run-as) + restart game"
	@echo "  build        publish to build/KitLib/ only (no game)"
	@echo "  deploy       copy build/KitLib/ into game mods/KitLib/ (no republish)"
	@echo "  compile      dotnet build to game mods (no .pck)"
	@echo "  pck          dotnet publish to game mods + .pck"
	@echo ""
	@echo "  compile-tools dotnet build KitLib.Mcp Release (local MCP / Cursor)"
	@echo "  build-tools  publish KitLib.Mcp self-contained exe to build/tools/ (TOOLS_RID=$(TOOLS_RID))"
	@echo "  deploy-tools copy kitlog + MCP into mods/KitLib/tools/ (build-if-missing)"
	@echo "  sync-tools   build-tools + build-kitlog + deploy-tools (force copy)"
	@echo "  zip-mcp      build-tools + package build/KitLib.Mcp-vX.X.X-<rid>.zip (exe only)"
	@echo "  compile-kitlog dotnet build KitLog.Cli Release"
	@echo "  build-kitlog publish kitlog self-contained to build/tools/ (TOOLS_RID=$(TOOLS_RID))"
	@echo "  zip-kitlog   build-kitlog + package build/KitLog.Cli-vX.X.X-<rid>.zip (exe only)"
	@echo ""
	@echo "  zip          build-all + package build/KitLib-vX.X.X.zip (alias: zip-full)"
	@echo ""
	@echo "  [upload]"
	@echo "  upload-github  stable + beta mod zips + MCP/kitlog exes → GitHub Release (alias: publish)"
	@echo "  upload-nexus   stable main zip → Nexus (NEXUS_FILE_GROUP_ID; alias: nexus)"
	@echo "  upload-nexus-beta  beta main zip → Nexus (NEXUS_FILE_GROUP_ID_BETA; alias: nexus-beta)"
	@echo "  upload-nexus-mcp  zip-mcp + Nexus Optional MCP proxy (NEXUS_FILE_GROUP_ID_MCP; alias: nexus-mcp)"
	@echo "  upload-nexus-kitlog  zip-kitlog + Nexus Optional KitLog CLI (NEXUS_FILE_GROUP_ID_KITLOG; alias: nexus-kitlog)"
	@echo "  upload-all     GitHub + Nexus stable/beta/tools + Steam"
	@echo "  upload-steam       workshop + upload stable and beta branch payloads (one STS2_WORKSHOP_ID)"
	@echo "  upload-steam-stable  workshop-stable + upload public branch"
	@echo "  upload-steam-beta    workshop-beta + upload public-beta branch"
	@echo "  readme-nexus   merge READMEs into assets/readme.nexus.txt (Nexus BBCode)"
	@echo "  readme-steam   README.md + README.zh-CN.md → assets/readme.steam.en.txt + .zh-CN.txt"
	@echo "  readme-assets  readme-nexus + readme-steam"
	@echo ""
	@echo "  docs           Valaxy docs dev server (docs/)"
	@echo "  docs-build     static site → docs/dist/"
	@echo ""
	@echo "  clean        remove build/, src/**/*.uid|.import, dotnet clean"

init:
	$(PYTHON) scripts/init.py
	-$(MAKE) hooks-install

icons:
	$(PYTHON) scripts/shake_icons.py

format:
	$(DOTNET) format KitLib.sln
	$(UV) run black scripts

format-check:
	@test -f eng/sts2-refs/stable/0.107.1/data_sts2_windows_x86_64/sts2.dll || (echo "Missing eng/sts2-refs (git lfs pull?). Run: make capture-sts2-ref PROFILE=stable" >&2; exit 1)
	$(DOTNET) format KitLib.sln --verify-no-changes

lint-scripts:
	$(UV) run flake8 scripts

check: format-check lint-scripts

test:
ifeq ($(OS),Windows_NT)
	@set DOTNET_ROLL_FORWARD=Major&& $(DOTNET) test tests/KitLib.ModPanel.Tests/KitLib.ModPanel.Tests.csproj -c Debug
else
	DOTNET_ROLL_FORWARD=Major $(DOTNET) test tests/KitLib.ModPanel.Tests/KitLib.ModPanel.Tests.csproj -c Debug
endif

hooks-install:
	$(UV) sync
	$(UV) run pre-commit install

hooks-run:
	$(UV) run pre-commit run --all-files

deps:
	$(DOTNET) restore $(MOD_MAIN)

build: build-flat
	@echo "KitLib flat bundle for profile $(STS2_COMPILE_PROFILE)"

build-flat:
	$(PYTHON) scripts/build_bundle.py --configuration Debug --sts2-profile $(STS2_COMPILE_PROFILE)

build-all: build-flat

workshop-stable:
	$(STEAM_SYNC_STABLE)

workshop-beta:
	$(STEAM_SYNC_BETA)

workshop: workshop-stable workshop-beta

build-smoke-mod: build
	$(DOTNET) build $(SMOKE_MOD_PROJECT) $(STS2_MSBUILD_PROFILE)

check-smoke-mod: build-smoke-mod
ifeq ($(OS),Windows_NT)
	@set DOTNET_ROLL_FORWARD=Major&& $(DOTNET) test $(SMOKE_MOD_TESTS) -c Debug
else
	DOTNET_ROLL_FORWARD=Major $(DOTNET) test $(SMOKE_MOD_TESTS) -c Debug
endif

deploy-smoke-mod: build-smoke-mod
	$(DOTNET) build $(SMOKE_MOD_PROJECT) $(STS2_MSBUILD_PROFILE) -p:DeploySmokeMod=true

build-stable:
	$(DOTNET) build KitLib.sln -p:Sts2Dir="$(shell $(PYTHON) scripts/resolve_sts2_profile_dir.py stable)" -p:Sts2Profile=stable -c Debug

build-beta:
	$(DOTNET) build KitLib.sln -p:Sts2Dir="$(shell $(PYTHON) scripts/resolve_sts2_profile_dir.py beta)" -p:Sts2Profile=beta -c Debug

build-profiles: build-stable build-beta

extract-touchpoints:
	$(PYTHON) scripts/extract_api_touchpoints.py

check-api:
	$(PYTHON) scripts/check_api_touchpoints.py

verify-profiles: build-profiles check-api

capture-sts2-ref:
ifndef PROFILE
	$(error Usage: make capture-sts2-ref PROFILE=stable|beta)
endif
	$(PYTHON) scripts/capture_sts2_ref.py $(PROFILE)

deploy:
	$(PYTHON) scripts/deploy_modules.py

sync: build deploy

sync-full: sync
	$(DEPLOY_TOOLS_BUILD)

sync-full-launch: sync-full launch

sync-framework-mods:
	$(DOTNET) msbuild $(MOD_MAIN) -t:SyncFrameworkModsToGame -p:DisableFrameworkModsAfterRestore=false

compile: deps
	$(DOTNET) build KitLib.sln $(STS2_MSBUILD_PROFILE)
	$(DEPLOY_COPY)

launch:
	$(LAUNCH)

launch-mp-host:
	$(LAUNCH) --fastmp host

launch-mp-join:
	$(LAUNCH) --fastmp join --client-id $(LAUNCH_MP_CLIENT_ID)

launch-mp:
	$(LAUNCH) --fastmp dual --client-id $(LAUNCH_MP_CLIENT_ID)

sync-launch: sync launch

sync-launch-mp: sync launch-mp

dev-session:
	$(PYTHON) scripts/dev_session.py --sync --launch --wait-bridge 120

push-android: build
	$(PYTHON) scripts/push_android.py --no-build

push-android-wsdx233: build
	$(PYTHON) scripts/push_android.py --no-build --target game --restart

compile-tools:
	$(DOTNET) build $(MCP_PROJECT) -c Release

build-tools:
	$(DOTNET) publish $(MCP_PROJECT) $(TOOLS_PUBLISH_FLAGS)

deploy-tools:
	$(DEPLOY_TOOLS_BUILD)

sync-tools: build-tools build-kitlog
	$(DEPLOY_TOOLS)

compile-kitlog:
	$(DOTNET) build $(KITLOG_PROJECT) -c Release

build-kitlog:
	$(DOTNET) publish $(KITLOG_PROJECT) $(KITLOG_PUBLISH_FLAGS)

pck: deps
	$(DOTNET) publish $(DEPLOY_TO_GAME) $(MOD_MAIN)

publish upload-github:
	$(PYTHON) scripts/publish_release.py $(if $(VERSION),--version $(VERSION),)

nexus upload-nexus:
	$(PYTHON) scripts/publish_nexus.py $(if $(VERSION),--version $(VERSION),)

nexus-beta upload-nexus-beta:
	$(PYTHON) scripts/publish_nexus.py --beta $(if $(VERSION),--version $(VERSION),)

nexus-mcp upload-nexus-mcp:
	$(PYTHON) scripts/publish_nexus.py --mcp $(if $(VERSION),--version $(VERSION),) $(if $(TOOLS_RID),--tools-rid $(TOOLS_RID),)

nexus-kitlog upload-nexus-kitlog:
	$(PYTHON) scripts/publish_nexus.py --kitlog $(if $(VERSION),--version $(VERSION),) $(if $(TOOLS_RID),--tools-rid $(TOOLS_RID),)

upload-all: publish nexus nexus-beta nexus-kitlog nexus-mcp
	$(STEAM_SYNC)
	$(STEAM_UPLOAD)

upload-steam: workshop
	$(STEAM_UPLOAD_STRICT)

upload-steam-stable: workshop-stable
	$(PYTHON) scripts/publish_steam.py upload stable $(STEAM_UPLOAD_FLAGS)

upload-steam-beta: workshop-beta
	$(PYTHON) scripts/publish_steam.py upload beta $(STEAM_UPLOAD_FLAGS)

readme-nexus:
	$(PYTHON) scripts/readme_to_nexus.py

readme-steam:
	$(PYTHON) scripts/readme_to_steam.py

readme-assets: readme-nexus readme-steam

docs:
	cd docs && pnpm install && pnpm dev

docs-build:
	cd docs && pnpm install && pnpm run build:ssg

# ── zip: modular release (Core + satellites under modules/) ──
zip-profiles:
	$(PYTHON) scripts/package_modules.py --all-profiles --configuration Release

zip-full: build-all
	$(PACKAGE_MODULES) --skip-build

zip: zip-full

ifeq ($(OS),Windows_NT)
zip-mcp: build-tools
	@if not exist $(MCP_PUBLISH_EXE) (echo ERROR: KitLib.Mcp.exe not found. Run make build-tools first. & exit /b 1)
	$(PYTHON) -c "import zipfile;z=zipfile.ZipFile('$(ZIP_MCP_NAME)','w',zipfile.ZIP_DEFLATED);z.write(r'$(MCP_PUBLISH_EXE)','KitLib.Mcp.exe');z.close()"
	@echo.
	@echo Done: $(ZIP_MCP_NAME)

zip-kitlog: build-kitlog
	@if not exist $(KITLOG_PUBLISH_EXE) (echo ERROR: kitlog.exe not found. Run make build-kitlog first. & exit /b 1)
	$(PYTHON) -c "import zipfile;z=zipfile.ZipFile('$(ZIP_KITLOG_NAME)','w',zipfile.ZIP_DEFLATED);z.write(r'$(KITLOG_PUBLISH_EXE)','kitlog.exe');z.close()"
	@echo.
	@echo Done: $(ZIP_KITLOG_NAME)

clean:
	@if exist build rmdir /s /q build
	powershell -NoProfile -Command "Get-ChildItem -Path src -Recurse -Include '*.uid','*.import' -File -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue"
	$(DOTNET) clean KitLib.sln
else
zip-mcp: build-tools
	@test -f $(MCP_PUBLISH_BIN) || (echo "ERROR: KitLib.Mcp not found. Run make build-tools first." >&2; exit 1)
	$(PYTHON) -c "import zipfile;z=zipfile.ZipFile('$(ZIP_MCP_NAME)','w',zipfile.ZIP_DEFLATED);z.write('$(MCP_PUBLISH_BIN)','KitLib.Mcp');z.close()"
	@echo ""
	@echo "Done: $(ZIP_MCP_NAME)"

zip-kitlog: build-kitlog
	@test -f $(KITLOG_PUBLISH_BIN) || (echo "ERROR: kitlog not found. Run make build-kitlog first." >&2; exit 1)
	$(PYTHON) -c "import zipfile;z=zipfile.ZipFile('$(ZIP_KITLOG_NAME)','w',zipfile.ZIP_DEFLATED);z.write('$(KITLOG_PUBLISH_BIN)','kitlog');z.close()"
	@echo ""
	@echo "Done: $(ZIP_KITLOG_NAME)"

clean:
	rm -rf build
	find src -type f \( -name '*.uid' -o -name '*.import' \) -exec rm -f {} +
	$(DOTNET) clean KitLib.sln
endif
