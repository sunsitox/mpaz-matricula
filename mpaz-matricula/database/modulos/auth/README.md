# Módulo `auth` — identidad neutral del colegio

Esquema `mpaz_auth`. No pertenece a ningún sistema: es el contrato que todos
leen y ninguno edita directamente.

| Tabla | Contenido |
|---|---|
| `persona` | Una fila por persona: `id` UUID (= el `sub` de los tokens), `correo` único, `nombre`, `activo`. Sembrada desde `funcionario`; el `id` se conserva para siempre aunque cambie el correo. |
| `credencial` | Reservada para fase 2 (mudar aquí los hashes de Identity). Hoy vacía: las claves siguen en `AspNetUsers`. |
| `OpenIddict*` | Aplicaciones, autorizaciones, scopes y tokens del emisor. Las crea la migración EF del `SsoDbContext`, no estos scripts. |

## Reglas

1. Las altas, bajas y cambios de correo los hace Administración TI.
2. Las aplicaciones solo leen `persona` (por `id` o `correo`). Nunca la editan, salvo el flujo del emisor.
3. Ningún sistema guarda claves fuera de aquí (fase 2) ni duplica nombre/correo como verdad propia.
4. `funcionario.sso_sujeto = persona.id`. Si un día el emisor se muda a un servicio aparte, este esquema se lleva tal cual.

## Archivos

```text
auth/
  pasos/          DDL del esquema (numerado desde 10).
  datos-prueba/   Siembra desde `funcionario`. Sólo desarrollo.
```
