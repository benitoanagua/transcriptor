SHELL    := /bin/bash
DIST     := dist
WIN_HOME := $(shell wslpath "$$(cmd.exe /c 'echo %USERPROFILE%' 2>/dev/null | tr -d '\r')" 2>/dev/null)
WIN_DIR  := $(WIN_HOME)/Transcriptor

.PHONY: help build publish install run transcribe fmt clean

help:       ## Muestra los comandos
	@grep -E '^[a-z]+:.*##' Makefile | sed 's/:.*##/ –/'

build:      ## Compila el grabador (comprueba errores)
	dotnet build rec -c Release

publish:    ## Genera dist/rec.exe autocontenido
	dotnet publish rec -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o $(DIST)

install: publish  ## Copia rec.exe a la carpeta de Windows
	mkdir -p "$(WIN_DIR)"
	cp $(DIST)/rec.exe "$(WIN_DIR)/rec.exe"

run: install  ## Compila y graba (ej.: make run ARGS='--ref "FAE"')
	"$(WIN_DIR)/rec.exe" $(ARGS)

transcribe: ## Transcribe las llamadas nuevas
	./transcribe/transcribe.sh

fmt:        ## Formatea el código C#
	dotnet format rec

clean:      ## Borra binarios
	rm -rf $(DIST) rec/bin rec/obj
