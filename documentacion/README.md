# Documentación — Portal de Matrícula MPAZ 2027

Dos documentos, cada uno en su carpeta y dividido en segmentos para revisar
de a poco sin abrir archivos largos:

## `modulo-matricula/` — Documentación técnica (completa al 18-11-2026)

- `modulo-matricula.tex`: documento principal (preámbulo + `\input`).
  Compilar con `pdflatex modulo-matricula.tex` (2 pasadas).
- `bloques/`: un archivo por bloque:
  `01-descripcion`, `02-arquitectura`, `03-base-de-datos`,
  `04-roles-y-permisos`, `05-flujos`, `06-seguridad`, `07-despliegue`,
  `08-pruebas-y-estado`, `09-pendientes`, `10-modulos-adicionales`
  (módulo Auth/SSO, módulo Asistencia, web service, estructura por módulos).

## `manual-usuarios/` — Manual de uso (maqueta con TODO)

- `manual-usuarios.tex`: documento principal (preámbulo + `\input`).
  Compilar con `pdflatex manual-usuarios.tex` (2 pasadas).
- `bloques/`: un archivo por segmento:
  `01-introduccion-acceso`, `02-panel`, `03-secretaria`,
  `04-direccion-inspectoria`, `05-roles-consulta` (PIE/UTP/Convivencia/
  Docente), `06-tens`, `07-admin-ti`,
  `08-certificados-reportes-alertas`, `09-faq-glosario-soporte`.
  Los textos marcados TODO se completan después con capturas reales.

Supuestos a confirmar con el colegio: matriz final, protocolo TENS, textos
legales y Ley 21.719, quién formaliza, registro público, borrado de `mpaz_web`.
