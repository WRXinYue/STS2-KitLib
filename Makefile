# KitLib — build pipeline
#
#   build   → artifacts under repo build/KitLib/  (CI-safe, no game writes)
#   deploy  → copy products into game mods/ (KitLib)
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

MOD_MAIN := src/KitLib/Core/KitLib.Core.csproj
SMOKE_MOD_PROJECT := samples/KitLibSmokeMod/KitLibSmokeMod.csproj
SMOKE_MOD_TESTS := tests/KitLib.SmokeMod.Tests/KitLib.SmokeMod.Tests.csproj
MCP_PROJECT := tools/KitLib.Mcp/KitLib.Mcp.csproj

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
TOOLS_PUBLISH_FLAGS := -c Release -r $(TOOLS_RID) --self-contained true -p:PublishSingleFile=true -o $(TOOLS_PUBLISH_DIR)
DEPLOY_TOOLS := $(PYTHON) scripts/deploy_tools.py --tools-rid $(TOOLS_RID)
DEPLOY_TOOLS_BUILD := $(PYTHON) scripts/deploy_tools.py --tools-rid $(TOOLS_RID) --build-if-missing

# Use -p: (not /p:) so Git Bash on Windows does not treat /p:... as a MSYS path.
DEPLOY_TO_GAME := -p:DeployToGame=true

# Sts2Profile from local.props (make init) selects stable|beta ref channel (see Directory.Build.props).
STS2_COMPILE_PROFILE ?= $(shell $(PYTHON) scripts/resolve_sts2_compile_profile.py)
STS2_MSBUILD_PROFILE := -p:Sts2Profile=$(STS2_COMPILE_PROFILE)
# Copy product builds into game mods/ — never republish into the game tree.
DEPLOY_COPY   := $(DOTNET) msbuild $(MOD_MAIN) -t:DeployRepoBuildToMods -p:DeployFromRepoBuild=true

ZIP_MCP_NAME := build/KitLib.Mcp-v$(VERSION)-$(TOOLS_RID).zip
MCP_PUBLISH_EXE := $(TOOLS_PUBLISH_DIR)/KitLib.Mcp.exe
MCP_PUBLISH_BIN := $(TOOLS_PUBLISH_DIR)/KitLib.Mcp

MOD_PROJECTS := src/KitLib/Abstractions/KitLib.Abstractions.csproj \
	src/KitLib/Core/KitLib.Core.csproj src/KitLib/Loader/KitLib.Loader.csproj \
	src/KitLib/Modules/ModPanel/KitModPanel.csproj \
	src/KitLib/Modules/Dev/KitLib.Dev.csproj \
	src/KitLib/Modules/Hud/KitLib.Hud.csproj
PACKAGE_BUNDLE := $(PYTHON) scripts/package_bundle.py
LAUNCH := $(PYTHON) scripts/launch_sts2.py
LAUNCH_MP_CLIENT_ID ?= 1001

.PHONY: help init icons format format-check lint-scripts check test hooks-install hooks-run deps build build-smoke-mod check-smoke-mod deploy-smoke-mod deploy sync sync-full sync-framework-mods compile pck readme-nexus zip-full zip-release clean docs docs-build \
        extract-touchpoints check-api verify verify-sts2-refs capture-sts2-ref \
        build-stable build-beta build-profiles build-all-variants bundle sync-bundle \
        launch sync-launch sync-full-launch launch-mp launch-mp-host launch-mp-join sync-launch-mp dev-session push-android push-android-wsdx233 compile-tools build-tools deploy-tools sync-tools zip-mcp \
        readme-nexus readme-steam readme-assets

help:
	@echo "KitLib — targets"
	@echo ""
	@echo "  init         detect STS2 + Godot, generate local.props + pre-commit hooks"
	@echo "  icons        tree-shake MDI (mdi-used.json + MdiIcon.Generated.cs)"
	@echo "  format       dotnet format KitLib.sln + black scripts/ (EditorConfig / pre-commit)"
	@echo "  format-check dotnet format whitespace+style --verify-no-changes (uses eng/sts2-refs when Sts2Dir unset)"
	@echo "  lint-scripts flake8 scripts/ (setup.cfg)"
	@echo "  check        format-check + lint-scripts"
	@echo "  test         dotnet test KitLib.ModPanel.Tests (sidebar planner + embed probe)"
	@echo "  hooks-install uv sync (dev) + pre-commit git hook"
	@echo "  hooks-run    pre-commit run --all-files"
	@echo "  deps         dotnet restore (does not touch game mods/STS2-RitsuLib by default)"
	@echo ""
	@echo "  sync         build + deploy KitLib"
	@echo "  sync PRODUCT=X  deploy one product only"
	@echo "  sync VARIANTS=all  build pinned variants; ALT_GAME_DIR deploys a second install"
	@echo "  sync-full    sync (legacy MCP tools deploy is manual: make deploy-tools)"
	@echo "  sync-full-launch  sync-full + launch game"
	@echo "  build        build all bundle projects (auto-cleans stale build servers)"
	@echo "  extract-touchpoints  scan src/ → eng/api_touchpoints.yaml"
	@echo "  check-api    reflect KitLib API touchpoints against sts2.dll"
	@echo "  verify       build + check-api (pre-release)"
	@echo "  check-smoke-mod  build + content-mod smoke fixture + Cecil/ref/load tests"
	@echo "  deploy-smoke-mod copy smoke mod into game mods/KitLibSmokeMod/ (manual in-game check)"
	@echo "  capture-sts2-ref  copy sts2.dll into eng/sts2-refs/<version>/"
	@echo "  build-stable      compile against stable ref (0.111.1)"
	@echo "  build-beta        compile against beta ref (0.113.1)"
	@echo "  build-profiles    build-stable then build-beta"
	@echo "  bundle            multi-API bundle → build/KitLib-release/ (release / Workshop / zip)"
	@echo "  sync-bundle       bundle + deploy to game mods/KitLib/"
	@echo "  zip-release  bundle + package build/KitLib-vX.X.X.zip (multi-API)"
	@echo "  zip-full     bundle + package build/<Product>-vX.Y.Z.zip for each mod"
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
	@echo "  deploy       copy product builds into game mods/ (PRODUCT= optional)"
	@echo "  compile      dotnet build to game mods (no .pck)"
	@echo "  pck          dotnet publish to game mods + .pck"
	@echo ""
	@echo "  [legacy MCP stdio proxy — optional, not part of releases]"
	@echo "  compile-tools dotnet build KitLib.Mcp Release (local stdio proxy / Cursor)"
	@echo "  build-tools  publish KitLib.Mcp self-contained exe to build/tools/ (TOOLS_RID=$(TOOLS_RID))"
	@echo "  deploy-tools copy KitLib.Mcp into mods/KitLib/tools/ (build-if-missing)"
	@echo "  sync-tools   build-tools + deploy-tools (force copy)"
	@echo "  zip-mcp      build-tools + package build/KitLib.Mcp-vX.X.X-<rid>.zip (exe only)"
	@echo ""
	@echo "  [release — script CLI, no Make targets]"
	@echo "  python3 scripts/release_cli.py            interactive GitHub / Nexus / Steam picker"
	@echo "  python3 scripts/release_cli.py --help     status, preflight, build, github, nexus, steam, all"
	@echo "  python3 scripts/setup_steam_uploader.py --write-env   one-time ModUploader install"
	@echo ""
	@echo "  [readme]"
	@echo "  readme-nexus   merge READMEs into assets/readme.nexus.txt (Nexus BBCode)"
	@echo "  readme-steam   README*.md -> assets/readme.steam.* (PRODUCT= optional; default all)"
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
	$(PYTHON) scripts/verify_sts2_refs.py
	$(DOTNET) format whitespace KitLib.sln --verify-no-changes
	$(DOTNET) format style KitLib.sln --verify-no-changes

lint-scripts:
	@if command -v uv >/dev/null 2>&1; then uv run flake8 scripts; \
	elif [ -x .venv/bin/flake8 ]; then .venv/bin/flake8 scripts; \
	else echo "flake8 not found (install uv or create .venv with flake8)" >&2; exit 1; fi

check: format-check lint-scripts

test:
ifeq ($(OS),Windows_NT)
	@set DOTNET_ROLL_FORWARD=Major&& $(DOTNET) test tests/KitLib.ModPanel.Tests/KitLib.ModPanel.Tests.csproj -c Debug
	@set DOTNET_ROLL_FORWARD=Major&& $(DOTNET) test tests/KitLib.DevTools.Tests/KitLib.DevTools.Tests.csproj -c Debug
else
	DOTNET_ROLL_FORWARD=Major $(DOTNET) test tests/KitLib.ModPanel.Tests/KitLib.ModPanel.Tests.csproj -c Debug
	DOTNET_ROLL_FORWARD=Major $(DOTNET) test tests/KitLib.DevTools.Tests/KitLib.DevTools.Tests.csproj -c Debug
endif

hooks-install:
	$(UV) sync
	$(UV) run pre-commit install

hooks-run:
	$(UV) run pre-commit run --all-files

deps:
	$(DOTNET) restore $(MOD_MAIN)

build: verify-sts2-refs
	-$(DOTNET) build-server shutdown
	$(PYTHON) scripts/build_bundle.py --configuration Debug --sts2-profile $(STS2_COMPILE_PROFILE)
	@echo "KitLib variant bundle for profile $(STS2_COMPILE_PROFILE)"

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

verify-sts2-refs:
	$(PYTHON) scripts/verify_sts2_refs.py

check-api:
	$(PYTHON) scripts/check_api_touchpoints.py

verify: build check-api

extract-touchpoints:
	$(PYTHON) scripts/extract_api_touchpoints.py

capture-sts2-ref:
	$(PYTHON) scripts/capture_sts2_ref.py $(or $(PROFILE),beta)

build-stable:
	$(PYTHON) scripts/build_bundle.py --configuration Debug --sts2-profile stable

build-beta:
	$(PYTHON) scripts/build_bundle.py --configuration Debug --sts2-profile beta

build-profiles: build-stable build-beta

# Build every product against each pinned reference.
build-all-variants:
	$(PYTHON) scripts/build_bundle.py --configuration Debug --sts2-profile stable
	$(PYTHON) scripts/build_bundle.py --configuration Debug --sts2-profile beta

bundle:
	$(PACKAGE_BUNDLE) --no-zip -c Release

sync-bundle:
	$(PACKAGE_BUNDLE) --no-zip -c Release --deploy

deploy:
	$(PYTHON) scripts/deploy_modules.py $(if $(PRODUCT),--product $(PRODUCT),)

# Default builds the current profile; make sync VARIANTS=all also builds the
# other pinned variant so build/*/lib/ holds both (Steam branch switches then
# just work). ALT_GAME_DIR additionally deploys to a second install (e.g. a
# separate beta copy). Note: satellites (KitModPanel, KitLib.Hud, KitLib.Dev)
# follow the last built profile; KitLib.Core ships both variants.
SYNC_BUILD_TARGET = $(if $(filter all,$(VARIANTS)),build-all-variants,build)

sync: $(SYNC_BUILD_TARGET)
	$(PYTHON) scripts/deploy_modules.py $(if $(PRODUCT),--product $(PRODUCT),)
	$(if $(filter all,$(VARIANTS)),$(if $(ALT_GAME_DIR),$(PYTHON) scripts/deploy_modules.py --game-root "$(ALT_GAME_DIR)",),)

# Legacy alias: the MCP stdio proxy is optional and deployed manually (`make deploy-tools`).
sync-full: sync

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

sync-tools: build-tools
	$(DEPLOY_TOOLS)

pck: deps
	$(DOTNET) publish $(DEPLOY_TO_GAME) $(MOD_MAIN)

# Release publishing is not a Make target any more — use the script CLI:
#   python3 scripts/release_cli.py [status|preflight|build|github|nexus|steam|all]
# No verb on a TTY opens the interactive channel picker. See --help.

readme-nexus:
	$(PYTHON) scripts/readme_to_nexus.py

readme-steam:
	$(PYTHON) scripts/readme_to_steam.py $(if $(PRODUCT),--product $(PRODUCT),)

readme-assets: readme-nexus readme-steam

docs:
	cd docs && pnpm install && pnpm dev

docs-build:
	cd docs && pnpm install && pnpm run build:ssg

# ── zip: multi-API release (loader + lib/<api>/) ──
zip-release: bundle
	$(PACKAGE_BUNDLE) --zip-only

zip-full:
	$(PACKAGE_BUNDLE) --zip-all -c Release

ifeq ($(OS),Windows_NT)
zip-mcp: build-tools
	@if not exist $(MCP_PUBLISH_EXE) (echo ERROR: KitLib.Mcp.exe not found. Run make build-tools first. & exit /b 1)
	$(PYTHON) -c "import zipfile;z=zipfile.ZipFile('$(ZIP_MCP_NAME)','w',zipfile.ZIP_DEFLATED);z.write(r'$(MCP_PUBLISH_EXE)','KitLib.Mcp.exe');z.close()"
	@echo.
	@echo Done: $(ZIP_MCP_NAME)

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

clean:
	rm -rf build
	find src -type f \( -name '*.uid' -o -name '*.import' \) -exec rm -f {} +
	$(DOTNET) clean KitLib.sln
endif
