# DevMode — build pipeline
#
#   build   → artifacts under repo build/DevMode/  (CI-safe, no game writes)
#   deploy  → copy build/DevMode/ into game mods/DevMode/ (other mods untouched)
#   sync    → build + deploy (default local dev loop)

DOTNET ?= dotnet

# Read version from DevMode.json (Windows Store python3 alias is often broken; use python on Windows)
ifeq ($(OS),Windows_NT)
PYTHON ?= python
else
PYTHON ?= python3
endif
VERSION := $(shell $(PYTHON) -c "import json;print(json.load(open('DevMode.json',encoding='utf-8'))['version'])")

MOD_MAIN := DevMode.csproj

# Use -p: (not /p:) so Git Bash on Windows does not treat /p:... as a MSYS path.
DEPLOY_TO_GAME := -p:DeployToGame=true
BETA_FLAG     := -p:Sts2Beta=true
# Copy build/DevMode/ into mods/DevMode/ only — never republish into the game tree.
DEPLOY_COPY   := $(DOTNET) msbuild $(MOD_MAIN) -t:DeployRepoBuildToMods -p:DeployFromRepoBuild=true

# STS2 Steam beta branch game version (update when Megacrit bumps beta; see beta install release_info.json).
STS2_GAME_BETA_VERSION ?= 0.105.1
ZIP_BETA_TAG := -sts2beta-v$(STS2_GAME_BETA_VERSION)
ZIP_NAME_BETA := build/DevMode-v$(VERSION)$(ZIP_BETA_TAG).zip

.PHONY: help init icons format deps build deploy sync sync-framework-mods compile pck publish nexus nuget upload-all readme-nexus zip clean \
        build-beta deploy-beta sync-beta sync-beta-launch compile-beta pck-beta zip-beta nexus-beta nuget-beta publish-beta upload-all-beta \
        launch launch-beta sync-launch sync-beta-run upload-github upload-nexus upload-nuget

help:
	@echo "DevMode — targets"
	@echo ""
	@echo "  init         detect STS2 + Godot, generate local.props + .vscode (PYTHON=... to override)"
	@echo "  icons        tree-shake MDI (mdi-used.json + MdiIcon.Generated.cs)"
	@echo "  format       dotnet format DevMode.sln (EditorConfig / pre-commit)"
	@echo "  deps         dotnet restore (does not touch game mods/STS2-RitsuLib by default)"
	@echo ""
	@echo "  sync         build to build/DevMode/, then copy into game mods/DevMode/ only"
	@echo "  sync-launch  sync + launch game"
	@echo "  sync-framework-mods  copy DevMode NuGet STS2-RitsuLib into game (overwrites other RitsuLib builds)"
	@echo "  launch       launch via Steam (macOS/Linux) or Sts2Dir exe (Windows)"
	@echo "  build        publish to build/DevMode/ only (no game)"
	@echo "  deploy       copy build/DevMode/ into game mods/DevMode/ (no republish)"
	@echo "  compile      dotnet build to game mods (no .pck)"
	@echo "  pck          dotnet publish to game mods + .pck"
	@echo ""
	@echo "  sync-beta         build-beta + deploy-beta (STS2 Steam beta; Sts2Dir = beta install)"
	@echo "  sync-beta-launch  sync-beta + launch game (same as LAUNCH=1 make sync-beta)"
	@echo "  build-beta        publish to build/DevMode/ only (STS2 Steam beta)"
	@echo "  deploy-beta  copy build/DevMode/ into game mods/DevMode/ (STS2 Steam beta)"
	@echo "  compile-beta dotnet build to game mods, no .pck (STS2 Steam beta)"
	@echo "  pck-beta     dotnet publish to game mods + .pck (STS2 Steam beta)"
	@echo ""
	@echo "  zip          build + package build/DevMode-vX.X.X.zip"
	@echo ""
	@echo "  [upload]"
	@echo "  upload-github  zip + GitHub Release (requires gh CLI; alias: publish)"
	@echo "  upload-nexus   zip + upload stable build to Nexus Main file (NEXUS_FILE_GROUP_ID; alias: nexus)"
	@echo "  upload-nuget   zip + pack + push to NuGet (NUGET_API_KEY; optional NUGET_SOURCE; alias: nuget)"
	@echo "  upload-all     upload-github then upload-nexus then upload-nuget (one zip build)"
	@echo "  readme-nexus   merge READMEs into assets/readme.nexus.txt (Nexus BBCode)"
	@echo ""
	@echo "  zip-beta       build-beta + package …-sts2beta-v$(STS2_GAME_BETA_VERSION).zip"
	@echo "  upload-github-beta  zip-beta + GitHub Release for STS2 beta v$(STS2_GAME_BETA_VERSION) (alias: publish-beta)"
	@echo "  upload-nexus-beta   zip-beta + Nexus Optional file (NEXUS_FILE_GROUP_ID_BETA; alias: nexus-beta)"
	@echo "  upload-nuget-beta   zip-beta + NuGet push (STS2.DevMode.Beta) for STS2 beta v$(STS2_GAME_BETA_VERSION) (alias: nuget-beta)"
	@echo "  upload-all-beta     upload-github-beta then upload-nexus-beta then upload-nuget-beta (one zip build)"
	@echo ""
	@echo "  clean        remove build/ + dotnet clean"

init:
	$(PYTHON) scripts/init.py

icons:
	$(PYTHON) scripts/shake_icons.py

format:
	$(DOTNET) format DevMode.sln --verbosity quiet

deps:
	$(DOTNET) restore $(MOD_MAIN)

build:
	$(DOTNET) publish $(MOD_MAIN)

deploy:
	$(DEPLOY_COPY)

sync: build deploy

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

compile-beta: deps
	$(DOTNET) build $(DEPLOY_TO_GAME) $(BETA_FLAG) $(MOD_MAIN)

pck: deps
	$(DOTNET) publish $(DEPLOY_TO_GAME) $(MOD_MAIN)

pck-beta: deps
	$(DOTNET) publish $(DEPLOY_TO_GAME) $(BETA_FLAG) $(MOD_MAIN)

publish upload-github:
	$(PYTHON) scripts/publish_release.py $(if $(VERSION),--version $(VERSION),)

publish-beta upload-github-beta:
	STS2_GAME_BETA_VERSION=$(STS2_GAME_BETA_VERSION) $(PYTHON) scripts/publish_release.py --beta $(if $(VERSION),--version $(VERSION),)

nexus upload-nexus:
	$(PYTHON) scripts/publish_nexus.py $(if $(VERSION),--version $(VERSION),)

nexus-beta upload-nexus-beta:
	STS2_GAME_BETA_VERSION=$(STS2_GAME_BETA_VERSION) $(PYTHON) scripts/publish_nexus.py --beta $(if $(VERSION),--version $(VERSION),)

nuget upload-nuget:
	$(PYTHON) scripts/publish_nuget.py $(if $(VERSION),--version $(VERSION),)

nuget-beta upload-nuget-beta:
	STS2_GAME_BETA_VERSION=$(STS2_GAME_BETA_VERSION) $(PYTHON) scripts/publish_nuget.py --beta $(if $(VERSION),--version $(VERSION),)

upload-all: publish nexus nuget
	$(PYTHON) scripts/publish_nuget.py --skip-build $(if $(VERSION),--version $(VERSION),)

upload-all-beta: publish-beta nexus-beta nuget-beta
	STS2_GAME_BETA_VERSION=$(STS2_GAME_BETA_VERSION) $(PYTHON) scripts/publish_nuget.py --beta --skip-build $(if $(VERSION),--version $(VERSION),)

readme-nexus:
	$(PYTHON) scripts/readme_to_nexus.py

# ── zip: build + package into build/DevMode-vX.X.X.zip ──
ZIP_NAME := build/DevMode-v$(VERSION).zip
DIST_DIR := build/dist/DevMode

ifeq ($(OS),Windows_NT)
zip-beta: build-beta
	@if exist build\dist rmdir /s /q build\dist
	@mkdir build\dist\DevMode\editor
	@mkdir build\dist\DevMode\manual
	@mkdir build\dist\DevMode\scripts
	@copy /y build\DevMode\DevMode.dll build\dist\DevMode\ >nul
	@if exist build\DevMode\DevMode.pck copy /y build\DevMode\DevMode.pck build\dist\DevMode\ >nul
	@copy /y build\DevMode\mod_manifest.json build\dist\DevMode\ >nul
	@xcopy /s /y /q editor\* build\dist\DevMode\editor\ >nul
	@xcopy /s /y /q manual\* build\dist\DevMode\manual\ >nul
	@if exist $(ZIP_NAME_BETA) del $(ZIP_NAME_BETA)
	$(PYTHON) -c "import zipfile,os;z=zipfile.ZipFile('$(ZIP_NAME_BETA)','w',zipfile.ZIP_DEFLATED);[z.write(os.path.join(r,f),os.path.join(os.path.relpath(r,'build/dist'),f)) for r,_,fs in os.walk('build/dist/DevMode') for f in fs];z.close()"
	@echo.
	@echo Done: $(ZIP_NAME_BETA)  (STS2 Steam beta v$(STS2_GAME_BETA_VERSION))
	@echo Install: extract into "Slay the Spire 2" beta branch mods folder

zip: build
	@if exist build\dist rmdir /s /q build\dist
	@mkdir build\dist\DevMode\editor
	@mkdir build\dist\DevMode\manual
	@mkdir build\dist\DevMode\scripts
	@copy /y build\DevMode\DevMode.dll build\dist\DevMode\ >nul
	@if exist build\DevMode\DevMode.pck copy /y build\DevMode\DevMode.pck build\dist\DevMode\ >nul
	@copy /y build\DevMode\mod_manifest.json build\dist\DevMode\ >nul
	@xcopy /s /y /q editor\* build\dist\DevMode\editor\ >nul
	@xcopy /s /y /q manual\* build\dist\DevMode\manual\ >nul
	@if exist build\DevMode-v$(VERSION).zip del build\DevMode-v$(VERSION).zip
	$(PYTHON) -c "import zipfile,os;z=zipfile.ZipFile('build/DevMode-v$(VERSION).zip','w',zipfile.ZIP_DEFLATED);[z.write(os.path.join(r,f),os.path.join(os.path.relpath(r,'build/dist'),f)) for r,_,fs in os.walk('build/dist/DevMode') for f in fs];z.close()"
	@echo.
	@echo Done: build\DevMode-v$(VERSION).zip
	@echo Install: extract into "Slay the Spire 2\mods\"

clean:
	@if exist build rmdir /s /q build
	$(DOTNET) clean DevMode.sln
else
zip-beta: build-beta
	rm -rf build/dist
	mkdir -p $(DIST_DIR)/editor $(DIST_DIR)/manual $(DIST_DIR)/scripts
	cp build/DevMode/DevMode.dll build/DevMode/mod_manifest.json $(DIST_DIR)/
	@[ -f build/DevMode/DevMode.pck ] && cp build/DevMode/DevMode.pck $(DIST_DIR)/ || echo "Warning: DevMode.pck not found (Godot not configured) — skipping"
	cp -R editor/. $(DIST_DIR)/editor/
	cp -R manual/. $(DIST_DIR)/manual/
	rm -f $(ZIP_NAME_BETA)
	cd build/dist && zip -qr ../DevMode-v$(VERSION)$(ZIP_BETA_TAG).zip DevMode
	@echo ""
	@echo "Done: $(ZIP_NAME_BETA)  (STS2 Steam beta v$(STS2_GAME_BETA_VERSION))"
	@echo 'Install: extract into "Slay the Spire 2" beta branch mods/'

zip: build
	rm -rf build/dist
	mkdir -p $(DIST_DIR)/editor $(DIST_DIR)/manual $(DIST_DIR)/scripts
	cp build/DevMode/DevMode.dll build/DevMode/mod_manifest.json $(DIST_DIR)/
	@[ -f build/DevMode/DevMode.pck ] && cp build/DevMode/DevMode.pck $(DIST_DIR)/ || echo "Warning: DevMode.pck not found (Godot not configured) — skipping"
	cp -R editor/. $(DIST_DIR)/editor/
	cp -R manual/. $(DIST_DIR)/manual/
	rm -f $(ZIP_NAME)
	cd build/dist && zip -qr ../DevMode-v$(VERSION).zip DevMode
	@echo ""
	@echo "Done: $(ZIP_NAME)"
	@echo 'Install: extract into "Slay the Spire 2/mods/"'

clean:
	rm -rf build
	find src -type f \( -name '*.uid' -o -name '*.import' \) -exec rm -f {} +
	$(DOTNET) clean DevMode.sln
endif
