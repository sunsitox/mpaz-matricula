-- Módulo auth · paso 10: esquema neutral de identidad del colegio.
-- No pertenece a ningún sistema: es el contrato que todos leen.
-- Convención de la casa: pasos numerados, CREATE IF NOT EXISTS.

CREATE SCHEMA IF NOT EXISTS mpaz_auth;

-- Una fila por persona, para siempre. El id es el `sub` de los tokens SSO
-- y se conserva aunque a la persona le cambien el correo.
CREATE TABLE IF NOT EXISTS mpaz_auth.persona (
    id uuid PRIMARY KEY,
    correo text NOT NULL UNIQUE,
    nombre text NOT NULL,
    activo boolean NOT NULL DEFAULT true,
    creada_en timestamptz NOT NULL DEFAULT now()
);

-- El correo se compara sin distinguir mayúsculas (igual que en asistencia).
CREATE UNIQUE INDEX IF NOT EXISTS uq_persona_correo_ci
    ON mpaz_auth.persona (lower(correo));

-- Fase 2: aquí vivirán los hashes de las claves. Hoy vacía a propósito:
-- las credenciales siguen en mpaz_matricula.AspNetUsers hasta mudar Identity.
CREATE TABLE IF NOT EXISTS mpaz_auth.credencial (
    persona_id uuid PRIMARY KEY REFERENCES mpaz_auth.persona (id),
    hash text NOT NULL,
    algoritmo text NOT NULL DEFAULT 'identity-v3',
    debe_cambiar boolean NOT NULL DEFAULT false,
    actualizada_en timestamptz NOT NULL DEFAULT now()
);
