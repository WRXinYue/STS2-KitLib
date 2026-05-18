# DevMode — build pipeline
#
#   build   → artifacts under repo build/DevMode/  (CI-safe, no game writes)
#   deploy  → copy those artifacts into the game mods dir
#   sync    → build + deploy (default local dev loop)

DOTNET ?= dotnet

# Read version from DevMode.json
PYTHON ?= python3
VERSION := $(shell $(PYTHON) -c "import json;print(json.load(open('DevMode.json',encoding='utf-8'))['version'])")

MOD_MAIN := DevMode.csproj

DEPLOY_TO_GAME := /p:DeployToGame=true

.PHONY: help init icons format deps build deploy sync compile pck publish nexus zip clean docs docs-build

help:
	@echo DevMode — targets
	@echo.
	@echo   init       detect STS2 + Godot, generate local.props + .vscode (Python 3; PYTHON=python3 to override)
	@echo   icons      tree-shake MDI (mdi-used.json + MdiIcon.Generated.cs); downloads full icons.json on first run if missing
	@echo   format     dotnet format DevMode.sln (C#; same rules as EditorConfig, for pre-commit / CI)
	@echo   deps       dotnet restore DevMode (STS2.RitsuLib NuGet + sync to game mods when Sts2Dir is set)
	@echo   sync       deps + publish DevMode twice (repo build, then deploy to game)
	@echo   build      deps + publish DevMode to build/DevMode/ only (no game)
	@echo   deploy     deps + dotnet publish with DeployToGame=true
	@echo   compile    dotnet build to game mods (no .pck)
	@echo   pck        dotnet publish to game mods + .pck
	@echo   publish    build + create GitHub Release (requires gh CLI)
	@echo   nexus      build + upload to Nexus Mods (requires NEXUS_API_KEY + NEXUS_FILE_GROUP_ID)
	@echo   zip        build + package into build/DevMode-vX.X.X.zip
	@echo   clean      remove build/ + dotnet clean

init:
	$(PYTHON) scripts/init.py

icons:
	$(PYTHON) scripts/shake_icons.py

format:
	$(DOTNET) format DevMode.sln --verbosity quiet

deps:
	$(DOTNET) restore $(MOD_MAIN)

build: deps
	$(DOTNET) publish $(MOD_MAIN)

deploy: deps
	$(DOTNET) publish $(DEPLOY_TO_GAME) $(MOD_MAIN)

sync: deps
	$(DOTNET) publish $(MOD_MAIN)
	$(DOTNET) publish $(DEPLOY_TO_GAME) $(MOD_MAIN)

compile: deps
	$(DOTNET) build $(DEPLOY_TO_GAME) $(MOD_MAIN)

pck: deps
	$(DOTNET) publish $(DEPLOY_TO_GAME) $(MOD_MAIN)

publish:
	$(PYTHON) scripts/publish_release.py $(if $(VERSION),--version $(VERSION),)

nexus:
	$(PYTHON) scripts/publish_nexus.py $(if $(VERSION),--version $(VERSION),)

# ── zip: build + package into build/DevMode-vX.X.X.zip ──
ZIP_NAME := build/DevMode-v$(VERSION).zip
DIST_DIR := build/dist/DevMode

ifeq ($(OS),Windows_NT)
zip: build
	@if exist build\dist rmdir /s /q build\dist
	@mkdir build\dist\DevMode\editor
	@mkdir build\dist\DevMode\scripts
	@copy /y build\DevMode\DevMode.dll build\dist\DevMode\ >nul
	@if exist build\DevMode\DevMode.pck copy /y build\DevMode\DevMode.pck build\dist\DevMode\ >nul
	@copy /y build\DevMode\mod_manifest.json build\dist\DevMode\ >nul
	@xcopy /s /y /q editor\* build\dist\DevMode\editor\ >nul
	@if exist build\DevMode-v$(VERSION).zip del build\DevMode-v$(VERSION).zip
	$(PYTHON) -c "import zipfile,os;z=zipfile.ZipFile('build/DevMode-v$(VERSION).zip','w',zipfile.ZIP_DEFLATED);[z.write(os.path.join(r,f),os.path.join(os.path.relpath(r,'build/dist'),f)) for r,_,fs in os.walk('build/dist/DevMode') for f in fs];z.close()"
	@echo.
	@echo Done: build\DevMode-v$(VERSION).zip
	@echo Install: extract into "Slay the Spire 2\mods\"

clean:
	@if exist build rmdir /s /q build
	$(DOTNET) clean DevMode.sln
else
zip: build
	rm -rf build/dist
	mkdir -p $(DIST_DIR)/editor $(DIST_DIR)/scripts
	cp build/DevMode/DevMode.dll build/DevMode/mod_manifest.json $(DIST_DIR)/
	@[ -f build/DevMode/DevMode.pck ] && cp build/DevMode/DevMode.pck $(DIST_DIR)/ || echo "Warning: DevMode.pck not found (Godot not configured) — skipping"
	cp -R editor/. $(DIST_DIR)/editor/
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
