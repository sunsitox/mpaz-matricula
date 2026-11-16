# Simulación visual de usuario · Portal de Matrícula MPAZ

Recorrido automatizado del portal con el navegador **visible**, usando la
cuenta con más funciones del sistema:

- **Usuario:** `mteresa.lagos@mpaz.cl` · rol **SECRETARIA**
- Es el rol con más funciones (23 permisos): listado y búsqueda, wizard
  completo, ficha por pestañas, documentos y reportes con exportación:
  la mayor cantidad de casos de prueba en una sola corrida.
- La administración de cuentas se muestra como denegación (MAT-12);
  la validación institucional corresponde a DIRECCION.

## Requisitos

1. PostgreSQL local con la base `mpaz-matricula` y los datos mock.
2. La app corriendo con HTTPS en `https://localhost:7199` (el login usa
   cookie `Secure`; no tocar el puerto 5195 del Visual Studio):
   ```bash
   cd mpaz-matricula
   dotnet run --project MarcelaPaz.Web --urls "https://localhost:7199;http://localhost:5199"
   ```
3. Primera vez: instalar la dependencia y el navegador —
   ```bash
   cd mpaz-matricula/pruebas-visuales
   npm install
   npx playwright install chromium
   ```

## Uso

```bash
npm run simular            # navegador visible (demostración)
npm run simular:headless   # sin ventana (verificación)
```

Variables opcionales: `BASE_URL`, `USUARIO`, `CLAVE`, `CLAVE_NUEVA`,
`SLOWMO` (milisegundos entre acciones).

## Casos que muestra

| # | Caso | Qué demuestra |
|---|------|---------------|
| 01 | Login ADMIN_TI | Acceso y cambio forzado de clave inicial |
| 02–03 | Listado y búsqueda | Consulta con filtros por nombre/RUN |
| 04–05 | Ficha por pestañas | General, Autorizaciones, Documentos, Salud, PIE |
| 06–08 | Reportes y CSV | Estado, faltantes y descarga auditada (EXPORTAR) |
| 09 | Funcionarios (denegado) | SECRETARIA sin permiso: muestra el aviso y audita (MAT-12) |
| 10 | Wizard paso 1 | Borrador SIMULACIÓN con RUN válido generado al vuelo |
| 11 | Comprobante | Ficha imprimible para firma del apoderado |
| 12 | Salida | Cierre de sesión |

Cada caso guarda su captura en `evidencias/` y la consola resume
OK / omitidos / fallidos. Los casos no críticos que no apliquen
(por ejemplo, sin datos para validar) se omiten sin romper la corrida.

> La corrida 11 deja un borrador de prueba en la base de desarrollo,
> identificable por el nombre `SIMULACIÓN Visual`.
