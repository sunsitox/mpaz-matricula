# Documentación — Portal de Matrícula MPAZ 2027

Dos documentos, cada uno en su carpeta y dividido en segmentos para revisar
de a poco sin abrir archivos largos.

## `modulo-matricula/` — Documentación técnica

Aquí vive todo lo técnico del sistema. El documento se llama **documentación
técnica**; la carpeta conserva el nombre del proyecto.

- `documentacion-tecnica.tex`: documento formal (preámbulo + `\input`).
  Compilar con `pdflatex documentacion-tecnica.tex` (2 pasadas); el resultado
  es `documentacion-tecnica.pdf`.
- `bloques/`: un archivo por bloque:
  `01-descripcion`, `02-arquitectura`, `03-base-de-datos`,
  `04-roles-y-permisos`, `05-flujos`, `06-seguridad`, `07-despliegue`,
  `08-pruebas-y-estado`, `09-pendientes`, `10-modulos-adicionales`
  (módulo Auth/SSO, módulo Asistencia, web service, estructura por módulos).
- `documentacion-tecnica.md`: arquitectura 4+1 en Mermaid (escenarios, lógica,
  procesos, desarrollo y física), más modelo de datos, estados, seguridad,
  protección de datos (Ley 21.719) y operación. Es la fuente única de la
  arquitectura: el bloque `02-arquitectura` remite a él.
- `diagramas/`: los 15 diagramas renderizados en `.svg` (para PDF) y `.png`
  (para presentaciones); las fuentes Mermaid quedan en `diagramas/fuentes/`.
- `extraer_diagramas.py`: extrae los bloques del Markdown a `diagramas/fuentes/`.
- `_herramientas/`: `mermaid-cli` instalado sólo para esta carpeta. No se
  versiona: está en `.gitignore` y se recrea con `npm install`.

## `manual-usuarios/` — Manual de uso (maqueta con TODO)

- `manual-usuarios.tex`: documento principal (preámbulo + `\input`).
  Compilar con `pdflatex manual-usuarios.tex` (2 pasadas).
- `bloques/`: un archivo por segmento:
  `01-introduccion-acceso`, `02-panel`, `03-secretaria`,
  `04-direccion-inspectoria`, `05-roles-consulta` (PIE/UTP/Convivencia/
  Docente), `06-tens`, `07-admin-ti`,
  `08-certificados-reportes-alertas`, `09-faq-glosario-soporte`.
  Los textos marcados TODO se completan después con capturas reales.

## `presentacion-colegio.md`

Documento de presentación para Dirección: estado por módulo, decisiones
pendientes y datos que debe entregar el colegio.

Supuestos a confirmar con el colegio: matriz final, protocolo TENS, textos
legales y Ley 21.719, quién formaliza, registro público, borrado de `mpaz_web`.
