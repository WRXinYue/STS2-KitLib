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
VERSION := $(shell $(PYTHON) -c "import json;print(json.load(open('KitLib.json',encoding='utf-8'))['version'])")

MOD_MAIN := KitLib.csproj
MCP_PROJECT := tools/DevMode.Mcp/KitLib.Mcp.csproj

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
DEPLOY_TOOLS := $(DOTNET) msbuild $(MOD_MAIN) -t:DeployToolsToMods -p:DeployToolsFromRepoBuild=true -p:ToolsPublishDir=$(TOOLS_PUBLISH_DIR) -p:ToolsRid=$(TOOLS_RID)

# Use -p: (not /p:) so Git Bash on Windows does not treat /p:... as a MSYS path.
DEPLOY_TO_GAME := -p:DeployToGame=true
BETA_FLAG     := -p:Sts2Beta=true
# Copy build/KitLib/ into mods/KitLib/ only — never republish into the game tree.
DEPLOY_COPY   := $(DOTNET) msbuild $(MOD_MAIN) -t:DeployRepoBuildToMods -p:DeployFromRepoBuild=true

# STS2 Steam beta branch game version (update when Megacrit bumps beta; see beta install release_info.json).
STS2_GAME_BETA_VERSION ?= 0.105.1
BETA_STS2_VER_ARG := --sts2-beta-version $(STS2_GAME_BETA_VERSION)
ZIP_BETA_TAG := -sts2beta-v$(STS2_GAME_BETA_VERSION)
ZIP_NAME_BETA := build/KitLib-v$(VERSION)$(ZIP_BETA_TAG).zip
ZIP_MCP_NAME := build/KitLib.Mcp-v$(VERSION)-$(TOOLS_RID).zip
MCP_PUBLISH_EXE := $(TOOLS_PUBLISH_DIR)/KitLib.Mcp.exe
MCP_PUBLISH_BIN := $(TOOLS_PUBLISH_DIR)/KitLib.Mcp

MOD_PROJECTS := KitLib.csproj KitLib.Shared/KitLib.Shared.csproj KitLib.Features/KitLib.Features.csproj \
	KitLib.User/KitLib.User.csproj KitLib.Cheat/KitLib.Cheat.csproj KitLib.Dev/KitLib.Dev.csproj \
	KitLib.AI/KitLib.AI.csproj KitLib.Panel/KitLib.Panel.csproj
PACKAGE_MODULES := $(PYTHON) scripts/package_modules.py

.PHONY: help init icons format deps build build-all deploy sync sync-full sync-framework-mods compile pck publish nexus nuget upload-all readme-nexus zip zip-full clean \
        build-beta deploy-beta sync-beta sync-beta-launch compile-beta pck-beta zip-beta nexus-beta nuget-beta publish-beta upload-all-beta \
        launch launch-beta sync-launch sync-beta-run dev-session compile-tools build-tools deploy-tools sync-tools zip-mcp upload-nexus-mcp nexus-mcp \
        upload-github upload-nexus upload-nuget

help:
	@echo "KitLib — targets"
	@echo ""
	@echo "  init         detect STS2 + Godot, generate local.props + .vscode (PYTHON=... to override)"
	@echo "  icons        tree-shake MDI (mdi-used.json + MdiIcon.Generated.cs)"
	@echo "  format       dotnet format KitLib.sln (EditorConfig / pre-commit)"
	@echo "  deps         dotnet restore (does not touch game mods/STS2-RitsuLib by default)"
	@echo ""
	@echo "  sync         build Core to build/KitLib/, then copy into game mods/KitLib/ only"
	@echo "  sync-full    build-all + deploy every KitLib* mod into game mods/"
	@echo "  build-all    dotnet build solution (Core + Features + satellites)"
	@echo "  zip-full     build-all + package Core/Full/per-module zips under build/"
	@echo "  sync-launch  sync + launch game"
	@echo "  dev-session  sync + launch + wait for MCP bridge (agent bootstrap)"
	@echo "  sync-framework-mods  copy DevMode NuGet STS2-RitsuLib into game (overwrites other RitsuLib builds)"
	@echo "  launch       launch via Steam (macOS/Linux) or Sts2Dir exe (Windows)"
	@echo "  build        publish to build/KitLib/ only (no game)"
	@echo "  deploy       copy build/KitLib/ into game mods/KitLib/ (no republish)"
	@echo "  compile      dotnet build to game mods (no .pck)"
	@echo "  pck          dotnet publish to game mods + .pck"
	@echo ""
	@echo "  compile-tools dotnet build KitLib.Mcp Release (local MCP / Cursor)"
	@echo "  build-tools  publish KitLib.Mcp self-contained exe to build/tools/ (TOOLS_RID=$(TOOLS_RID))"
	@echo "  deploy-tools copy MCP exe into game mods/KitLib/tools/ (requires make init)"
	@echo "  sync-tools   build-tools + deploy-tools"
	@echo "  zip-mcp      build-tools + package build/KitLib.Mcp-vX.X.X-<rid>.zip (exe only)"
	@echo ""
	@echo "  sync-beta         build-beta + deploy-beta (STS2 Steam beta; Sts2Dir = beta install)"
	@echo "  sync-beta-launch  sync-beta + launch game (same as LAUNCH=1 make sync-beta)"
	@echo "  build-beta        publish to build/KitLib/ only (STS2 Steam beta)"
	@echo "  deploy-beta  copy build/KitLib/ into game mods/KitLib/ (STS2 Steam beta)"
	@echo "  compile-beta dotnet build to game mods, no .pck (STS2 Steam beta)"
	@echo "  pck-beta     dotnet publish to game mods + .pck (STS2 Steam beta)"
	@echo ""
	@echo "  zip          build + package build/KitLib-vX.X.X.zip"
	@echo ""
	@echo "  [upload]"
	@echo "  upload-github  zip + GitHub Release (requires gh CLI; alias: publish)"
	@echo "  upload-nexus   zip + upload stable build to Nexus Main file (NEXUS_FILE_GROUP_ID; alias: nexus)"
	@echo "  upload-nexus-mcp  zip-mcp + Nexus Optional MCP proxy (NEXUS_FILE_GROUP_ID_MCP; alias: nexus-mcp)"
	@echo "  upload-nuget   zip + pack + push to NuGet (NUGET_API_KEY; optional NUGET_SOURCE; alias: nuget)"
	@echo "  upload-all     upload-github then upload-nexus then upload-nuget (one zip build)"
	@echo "  readme-nexus   merge READMEs into assets/readme.nexus.txt (Nexus BBCode)"
	@echo ""
	@echo "  zip-beta       build-beta + package …-sts2beta-v$(STS2_GAME_BETA_VERSION).zip"
	@echo "  upload-github-beta  zip-beta + GitHub Release for STS2 beta v$(STS2_GAME_BETA_VERSION) (alias: publish-beta)"
	@echo "  upload-nexus-beta   zip-beta + Nexus Optional file (NEXUS_FILE_GROUP_ID_BETA; alias: nexus-beta)"
	@echo "  upload-nuget-beta   zip-beta + NuGet push (STS2.KitLib.Beta) for STS2 beta v$(STS2_GAME_BETA_VERSION) (alias: nuget-beta)"
	@echo "  upload-all-beta     upload-github-beta then upload-nexus-beta then upload-nuget-beta (one zip build)"
	@echo ""
	@echo "  clean        remove build/ + dotnet clean"

init:
	$(PYTHON) scripts/init.py

icons:
	$(PYTHON) scripts/shake_icons.py

format:
	$(DOTNET) format KitLib.sln --verbosity quiet

deps:
	$(DOTNET) restore $(MOD_MAIN)

build:
	$(DOTNET) publish $(MOD_MAIN)

build-all:
	$(DOTNET) build KitLib.sln

deploy:
	$(DEPLOY_COPY)

sync: build deploy

sync-full: build-all
	$(PYTHON) scripts/deploy_modules.py

sync-framework-mods:
	$(DOTNET) msbuild $(MOD_MAIN) -t:SyncFrameworkModsToGame -p:DisableFrameworkModsAfterRestore=false

compile: deps
	$(DOTNET) build $(DEPLOY_TO_GAME) $(MOD_MAIN)

build-beta:
	$(DOTNET) publish $(BETA_FLAG) $(MOD_MAIN)

deploy-beta:
	$(DEPLOY_COPY)

sync-beta: build-beta deploy-beta
ifneq ($(LAUNCH),)
	$(PYTHON) scripts/launch_sts2.py
endif

sync-beta-launch sync-beta-run: sync-beta
	$(PYTHON) scripts/launch_sts2.py

launch launch-beta:
	$(PYTHON) scripts/launch_sts2.py

sync-launch: sync launch

dev-session:
	$(PYTHON) scripts/dev_session.py --sync --launch --wait-bridge 120

compile-tools:
	$(DOTNET) build $(MCP_PROJECT) -c Release

build-tools:
	$(DOTNET) publish $(MCP_PROJECT) $(TOOLS_PUBLISH_FLAGS)

deploy-tools:
	$(DEPLOY_TOOLS)

sync-tools: build-tools deploy-tools

compile-beta: deps
	$(DOTNET) build $(DEPLOY_TO_GAME) $(BETA_FLAG) $(MOD_MAIN)

pck: deps
	$(DOTNET) publish $(DEPLOY_TO_GAME) $(MOD_MAIN)

pck-beta: deps
	$(DOTNET) publish $(DEPLOY_TO_GAME) $(BETA_FLAG) $(MOD_MAIN)

publish upload-github:
	$(PYTHON) scripts/publish_release.py $(if $(VERSION),--version $(VERSION),)

publish-beta upload-github-beta:
	$(PYTHON) scripts/publish_release.py --beta $(BETA_STS2_VER_ARG) $(if $(VERSION),--version $(VERSION),)

nexus upload-nexus:
	$(PYTHON) scripts/publish_nexus.py $(if $(VERSION),--version $(VERSION),)

nexus-beta upload-nexus-beta:
	$(PYTHON) scripts/publish_nexus.py --beta $(BETA_STS2_VER_ARG) $(if $(VERSION),--version $(VERSION),)

nexus-mcp upload-nexus-mcp:
	$(PYTHON) scripts/publish_nexus.py --mcp $(if $(VERSION),--version $(VERSION),) $(if $(TOOLS_RID),--tools-rid $(TOOLS_RID),)

nuget upload-nuget:
	$(PYTHON) scripts/publish_nuget.py $(if $(VERSION),--version $(VERSION),)

nuget-beta upload-nuget-beta:
	$(PYTHON) scripts/publish_nuget.py --beta $(BETA_STS2_VER_ARG) $(if $(VERSION),--version $(VERSION),)

upload-all: publish nexus nuget
	$(PYTHON) scripts/publish_nuget.py --skip-build $(if $(VERSION),--version $(VERSION),)

upload-all-beta: publish-beta nexus-beta nuget-beta
	$(PYTHON) scripts/publish_nuget.py --beta $(BETA_STS2_VER_ARG) --skip-build $(if $(VERSION),--version $(VERSION),)

readme-nexus:
	$(PYTHON) scripts/readme_to_nexus.py

# ── zip: build + package into build/KitLib-vX.X.X.zip ──
ZIP_NAME := build/KitLib-v$(VERSION).zip
DIST_DIR := build/dist/KitLib

zip-full: build-all
	$(PACKAGE_MODULES) --skip-build

ifeq ($(OS),Windows_NT)
zip-mcp: build-tools
	@if not exist $(MCP_PUBLISH_EXE) (echo ERROR: KitLib.Mcp.exe not found. Run make build-tools first. & exit /b 1)
	$(PYTHON) -c "import zipfile;z=zipfile.ZipFile('$(ZIP_MCP_NAME)','w',zipfile.ZIP_DEFLATED);z.write(r'$(MCP_PUBLISH_EXE)','KitLib.Mcp.exe');z.close()"
	@echo.
	@echo Done: $(ZIP_MCP_NAME)

zip-beta: build-beta
	@if not exist build\KitLib\KitLib.pck (echo ERROR: KitLib.pck not found. Set GodotPath in local.props ^(make init^) and rebuild. & exit /b 1)
	@if exist build\dist rmdir /s /q build\dist
	@mkdir build\dist\KitLib\editor
	@copy /y build\KitLib\KitLib.dll build\dist\KitLib\ >nul
	@copy /y build\KitLib\KitLib.pck build\dist\KitLib\ >nul
	@copy /y build\KitLib\mod_manifest.json build\dist\KitLib\ >nul
	@xcopy /s /y /q editor\* build\dist\KitLib\editor\ >nul
	$(PYTHON) -c "import zipfile,os;z=zipfile.ZipFile('$(ZIP_NAME_BETA)','w',zipfile.ZIP_DEFLATED);[z.write(os.path.join(r,f),os.path.join(os.path.relpath(r,'build/dist'),f)) for r,_,fs in os.walk('build/dist/KitLib') for f in fs];z.close()"
	@echo.
	@echo Done: $(ZIP_NAME_BETA)  (STS2 Steam beta v$(STS2_GAME_BETA_VERSION))
	@echo Install: extract into "Slay the Spire 2" beta branch mods folder

zip: build
	@if not exist build\KitLib\KitLib.pck (echo ERROR: KitLib.pck not found. Set GodotPath in local.props ^(make init^) and rebuild. & exit /b 1)
	@if exist build\dist rmdir /s /q build\dist
	@mkdir build\dist\KitLib\editor
	@copy /y build\KitLib\KitLib.dll build\dist\KitLib\ >nul
	@copy /y build\KitLib\KitLib.pck build\dist\KitLib\ >nul
	@copy /y build\KitLib\mod_manifest.json build\dist\KitLib\ >nul
	@xcopy /s /y /q editor\* build\dist\KitLib\editor\ >nul
	$(PYTHON) -c "import zipfile,os;z=zipfile.ZipFile('$(ZIP_NAME)','w',zipfile.ZIP_DEFLATED);[z.write(os.path.join(r,f),os.path.join(os.path.relpath(r,'build/dist'),f)) for r,_,fs in os.walk('build/dist/KitLib') for f in fs];z.close()"
	@echo.
	@echo Done: build\KitLib-v$(VERSION).zip
	@echo Install: extract into "Slay the Spire 2\mods\"

clean:
	@if exist build rmdir /s /q build
	$(DOTNET) clean KitLib.sln
else
zip-mcp: build-tools
	@test -f $(MCP_PUBLISH_BIN) || (echo "ERROR: KitLib.Mcp not found. Run make build-tools first." >&2; exit 1)
	$(PYTHON) -c "import zipfile;z=zipfile.ZipFile('$(ZIP_MCP_NAME)','w',zipfile.ZIP_DEFLATED);z.write('$(MCP_PUBLISH_BIN)','KitLib.Mcp');z.close()"
	@echo ""
	@echo "Done: $(ZIP_MCP_NAME)"

zip-beta: build-beta
	@test -f build/KitLib/KitLib.pck || (echo "ERROR: KitLib.pck not found. Set GodotPath in local.props (make init) and rebuild." >&2; exit 1)
	rm -rf build/dist
	mkdir -p $(DIST_DIR)/editor
	cp build/KitLib/KitLib.dll build/KitLib/mod_manifest.json build/KitLib/KitLib.pck $(DIST_DIR)/
	cp -R editor/. $(DIST_DIR)/editor/
	rm -f $(ZIP_NAME_BETA)
	cd build/dist && zip -qr ../KitLib-v$(VERSION)$(ZIP_BETA_TAG).zip KitLib
	@echo ""
	@echo "Done: $(ZIP_NAME_BETA)  (STS2 Steam beta v$(STS2_GAME_BETA_VERSION))"
	@echo 'Install: extract into "Slay the Spire 2" beta branch mods/'

zip: build
	@test -f build/KitLib/KitLib.pck || (echo "ERROR: KitLib.pck not found. Set GodotPath in local.props (make init) and rebuild." >&2; exit 1)
	rm -rf build/dist
	mkdir -p $(DIST_DIR)/editor
	cp build/KitLib/KitLib.dll build/KitLib/mod_manifest.json build/KitLib/KitLib.pck $(DIST_DIR)/
	cp -R editor/. $(DIST_DIR)/editor/
	rm -f $(ZIP_NAME)
	cd build/dist && zip -qr ../KitLib-v$(VERSION).zip KitLib
	@echo ""
	@echo "Done: $(ZIP_NAME)"
	@echo 'Install: extract into "Slay the Spire 2/mods/"'

clean:
	rm -rf build
	find src -type f \( -name '*.uid' -o -name '*.import' \) -exec rm -f {} +
	$(DOTNET) clean KitLib.sln
endif
