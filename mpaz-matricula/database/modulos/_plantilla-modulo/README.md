# Plantilla de módulo

Esqueleto para un módulo nuevo. No es un módulo real: no ejecutar nada de
esta carpeta.

Para crear el módulo `<nombre>`:

1. Copiar esta carpeta a `modulos/<nombre>/` y borrar este README.
2. Agregar los pasos en `pasos/`, numerados desde `10` (`10-....sql`,
   `11-....sql`, …) para no chocar con `comun/` ni con matrícula.
3. Si el módulo necesita instalación consolidada, agregarla en
   `instalacion/` siguiendo el orden estructura → PL → datos.
4. Datos de ejemplo sólo en `datos-prueba/` (el release los excluye).
5. Registrar el módulo en `modulos/README.md` con su esquema y su estado.
6. Probar con `aplicar.sh <base-prueba> --modulo <nombre> --clave-app '...'`
   sobre una base vacía antes de tocar la de desarrollo.
