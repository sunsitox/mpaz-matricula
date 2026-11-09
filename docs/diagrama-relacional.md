# Diagrama relacional (borrador) — Sistema de Matrícula Escuela Marcela Paz

> **Estado:** borrador previo al acta de constitución (22-10-2026). Alcance de la primera entrega conversado en la [reunión 1](docs/reunion-01-2026-10-21.md): estudiantes ya inscritos por proceso externo y sus apoderados. No representa decisiones aprobadas.

Render: pegar el bloque en [mermaid.live](https://mermaid.live) o abrir con la vista Markdown de GitHub/GitLab.

```mermaid
erDiagram
    ESTUDIANTE ||--o{ MATRICULA : "tiene"
    CURSO ||--o{ MATRICULA : "recibe"
    ESTUDIANTE ||--o{ ESTUDIANTE_APODERADO : "relacionado_a"
    APODERADO ||--o{ ESTUDIANTE_APODERADO : "titular/suplente"
    ROL ||--o{ USUARIO : "asigna"
    USUARIO ||--o{ AUDITORIA_LOG : "genera"

    ESTUDIANTE {
        uuid id PK
        varchar run UK "unico, con digito verificador"
        varchar nombres
        varchar apellidos
        date fecha_nacimiento
        varchar comuna
        varchar direccion
        text observaciones_salud "sensibles, acceso restringido"
        boolean pie "programa de integracion escolar"
    }

    APODERADO {
        uuid id PK
        varchar run UK
        varchar nombres
        varchar apellidos
        varchar telefono
        varchar correo "opcional: algunos hogares sin correo"
    }

    ESTUDIANTE_APODERADO {
        uuid id PK
        uuid estudiante_id FK
        uuid apoderado_id FK
        varchar parentesco "titular / suplente"
        varchar tipo_verificacion "ano de nacimiento: pendiente de validar con la escuela"
    }

    CURSO {
        uuid id PK
        varchar nivel "1 a 8 basico"
        char letra "A, B..."
        int anio_lectivo
    }

    MATRICULA {
        uuid id PK
        uuid estudiante_id FK
        uuid curso_id FK
        date fecha_matricula
        varchar estado "pendiente / activo / retirado"
    }

    USUARIO {
        uuid id PK
        varchar run UK "personal: secretaria, direccion, admin"
        varchar rol "acceso separado del de apoderados"
    }

    AUDITORIA_LOG {
        uuid id PK
        uuid usuario_id FK
        varchar accion
        jsonb detalle "sin datos sensibles ni RUN en logs"
        timestamp fecha_hora
    }

    ROL {
        int id PK
        varchar nombre "admin, secretaria, auditor"
    }
```

## Notas del borrador

- **Estudiante–Apoderado es N:M** con tabla intermedia para titular/suplente; es el punto crítico de verificación de identidad pendiente de aprobar (RUT estudiante + RUT apoderado + año de nacimiento no es autenticación suficiente por sí solo).
- **Matrícula** separa estudiante de curso/año lectivo para conservar historial por año.
- **Salud y PIE** quedan como atributos del estudiante en lugar de tabla aparte; si la escuela exige más detalle, conviene tabla propia con permisos específicos.
- Estados (`pendiente / activo / retirado`) tomados del SRS, pendientes de confirmar con la escuela.
