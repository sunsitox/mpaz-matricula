#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Extrae los bloques Mermaid de documentacion-tecnica.md a diagramas/fuentes/.

Cada bloque va precedido por la marca <!-- diagrama: nombre -->; el nombre del
archivo .mmd es ese nombre. Así el Markdown sigue siendo la fuente única: se
edita el documento y se vuelve a extraer.
"""
import os
import re

AQUI = os.path.dirname(os.path.abspath(__file__))
DOC = os.path.join(AQUI, "documentacion-tecnica.md")
SALIDA = os.path.join(AQUI, "diagramas", "fuentes")

MARCA = re.compile(r"<!--\s*diagrama:\s*([\w.-]+)\s*-->")
BLOQUE = re.compile(r"```mermaid\n(.*?)```", re.S)


def main() -> int:
    with open(DOC, encoding="utf-8") as archivo:
        texto = archivo.read()

    os.makedirs(SALIDA, exist_ok=True)
    escritos = []

    for marca in MARCA.finditer(texto):
        nombre = marca.group(1)
        bloque = BLOQUE.search(texto, marca.end())
        if bloque is None:
            raise SystemExit(f"La marca {nombre} no tiene un bloque mermaid después.")

        destino = os.path.join(SALIDA, f"{nombre}.mmd")
        with open(destino, "w", encoding="utf-8", newline="\n") as archivo:
            archivo.write(bloque.group(1).strip() + "\n")
        escritos.append(nombre)

    print(f"{len(escritos)} diagramas en {SALIDA}:")
    for nombre in escritos:
        print(" ", nombre)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
