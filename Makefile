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
STEAM_SYNC_FLAGS := $(if $(CHANGE_NOTE),--change-note "$(CHANGE_NOTE)",) $(if $(UNRELEASED),--unreleased,)
STEAM_SYNC := $(PYTHON) scripts/publish_steam.py sync $(STEAM_SYNC_FLAGS)
STEAM_UPLOAD := $(PYTHON) scripts/publish_steam.py upload

.PHONY: help init icons format format-check lint-scripts check test hooks-install hooks-run deps build build-all deploy sync sync-full sync-framework-mods compile pck publish nexus nuget upload-all readme-nexus zip zip-full clean docs docs-build \
        build-stable build-beta build-profiles extract-touchpoints check-api verify-profiles capture-sts2-ref \
        launch sync-launch sync-full-launch dev-session push-android push-android-wsdx233 compile-tools build-tools deploy-tools sync-tools zip-mcp upload-nexus-mcp nexus-mcp \
        compile-kitlog build-kitlog zip-kitlog upload-nexus-kitlog nexus-kitlog \
        upload-github upload-nexus upload-nuget steam-workspace upload-steam

help:
	@echo "KitLib — targets"
	@echo ""
	@echo "  init         detect STS2 + Godot, generate local.props + .vscode + pre-commit hooks"
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
	@echo "  sync         build Core to build/KitLib/, then copy into game mods/KitLib/ only"
	@echo "  sync-full    build-all + deploy mods/KitLib/ + deploy tools/ (kitlog + MCP)"
	@echo "  sync-full-launch  sync-full + launch game"
	@echo "  build-all    dotnet build solution (Core + satellites)"
	@echo "  build-stable dotnet build KitLib.sln against stable ref (eng/sts2-refs/)"
	@echo "  build-beta   dotnet build KitLib.sln against beta ref"
	@echo "  build-profiles build-stable then build-beta"
	@echo "  extract-touchpoints  scan src/ → eng/api_touchpoints.yaml"
	@echo "  check-api    reflect KitLib API touchpoints against both sts2.dll"
	@echo "  verify-profiles  build-profiles + check-api (pre-release)"
	@echo "  capture-sts2-ref PROFILE=stable|beta  copy sts2.dll into eng/sts2-refs/ (validates release_info)"
	@echo "  zip-full     build-all + package build/KitLib-vX.X.X.zip"
	@echo "  sync-launch  sync + launch game"
	@echo "  dev-session  sync + launch + wait for MCP bridge (agent bootstrap)"
	@echo "  sync-framework-mods  copy DevMode NuGet STS2-RitsuLib into game (overwrites other RitsuLib builds)"
	@echo "  launch       launch via Steam (macOS/Linux) or Sts2Dir exe (Windows)"
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
	@echo "  upload-github  zip + GitHub Release (requires gh CLI; alias: publish)"
	@echo "  upload-nexus   zip + upload to Nexus Main file (NEXUS_FILE_GROUP_ID; alias: nexus)"
	@echo "  upload-nexus-mcp  zip-mcp + Nexus Optional MCP proxy (NEXUS_FILE_GROUP_ID_MCP; alias: nexus-mcp)"
	@echo "  upload-nexus-kitlog  zip-kitlog + Nexus Optional KitLog CLI (NEXUS_FILE_GROUP_ID_KITLOG; alias: nexus-kitlog)"
	@echo "  upload-nuget   zip + pack + push to NuGet (NUGET_API_KEY; optional NUGET_SOURCE; alias: nuget)"
	@echo "  upload-all     upload-github + upload-nexus + upload-nuget + upload-steam (one zip build)"
	@echo "  steam-workspace  build-all + fill steam/workshop/ (content, image, bilingual changeNote)"
	@echo "  upload-steam   steam-workspace + ModUploader (STS2_MOD_UPLOADER in .env)"
	@echo "  readme-nexus   merge READMEs into assets/readme.nexus.txt (Nexus BBCode)"
	@echo ""
	@echo "  docs           Valaxy docs dev server (docs/)"
	@echo "  docs-build     static site → docs/dist/"
	@echo ""
	@echo "  clean        remove build/ + dotnet clean"

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
	DOTNET_ROLL_FORWARD=Major $(DOTNET) test tests/KitLib.ModPanel.Tests/KitLib.ModPanel.Tests.csproj -c Debug

hooks-install:
	$(UV) sync
	$(UV) run pre-commit install

hooks-run:
	$(UV) run pre-commit run --all-files

deps:
	$(DOTNET) restore $(MOD_MAIN)

build:
	@echo "STS2 compile profile: $(STS2_COMPILE_PROFILE) (local.props Sts2Profile or auto from install)"
	$(DOTNET) publish $(MOD_MAIN) $(STS2_MSBUILD_PROFILE)

build-all:
	@echo "STS2 compile profile: $(STS2_COMPILE_PROFILE) (local.props Sts2Profile or auto from install)"
	$(DOTNET) build KitLib.sln $(STS2_MSBUILD_PROFILE)

build-stable:
	$(DOTNET) build KitLib.sln -p:Sts2Dir="$(shell $(PYTHON) scripts/resolve_sts2_profile_dir.py stable)" -p:Sts2Profile=stable -p:KitLibProfileBuild=true

build-beta:
	$(DOTNET) build KitLib.sln -p:Sts2Dir="$(shell $(PYTHON) scripts/resolve_sts2_profile_dir.py beta)" -p:Sts2Profile=beta -p:KitLibProfileBuild=true

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
	$(DEPLOY_COPY)

sync: build deploy

sync-full: build-all
	$(PYTHON) scripts/deploy_modules.py
	$(DEPLOY_TOOLS_BUILD)

sync-full-launch: sync-full launch

sync-framework-mods:
	$(DOTNET) msbuild $(MOD_MAIN) -t:SyncFrameworkModsToGame -p:DisableFrameworkModsAfterRestore=false

compile: deps
	$(DOTNET) build $(DEPLOY_TO_GAME) $(STS2_MSBUILD_PROFILE) $(MOD_MAIN)

launch:
	$(PYTHON) scripts/launch_sts2.py

sync-launch: sync launch

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

nexus-mcp upload-nexus-mcp:
	$(PYTHON) scripts/publish_nexus.py --mcp $(if $(VERSION),--version $(VERSION),) $(if $(TOOLS_RID),--tools-rid $(TOOLS_RID),)

nexus-kitlog upload-nexus-kitlog:
	$(PYTHON) scripts/publish_nexus.py --kitlog $(if $(VERSION),--version $(VERSION),) $(if $(TOOLS_RID),--tools-rid $(TOOLS_RID),)

nuget upload-nuget:
	$(PYTHON) scripts/publish_nuget.py $(if $(VERSION),--version $(VERSION),)

upload-all: publish nexus nuget
	$(PYTHON) scripts/publish_nuget.py --skip-build $(if $(VERSION),--version $(VERSION),)
	$(STEAM_SYNC) --skip-build
	$(STEAM_UPLOAD)

steam-workspace: build-all
	$(STEAM_SYNC) --skip-build

upload-steam: steam-workspace
	$(STEAM_UPLOAD)

readme-nexus:
	$(PYTHON) scripts/readme_to_nexus.py

docs:
	cd docs && pnpm install && pnpm dev

docs-build:
	cd docs && pnpm install && pnpm run build:ssg

# ── zip: modular release (Core + satellites under modules/) ──
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
