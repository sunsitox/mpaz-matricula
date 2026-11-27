// Módulo 01 · Acceso y sesión (Identity).
// Demuestra el ingreso, el panel con KPIs, la actividad auditada y la
// denegación de una pantalla de administración para un rol sin permiso.

import {
  ADMIN,
  SECRETARIA,
  asegurarSesion,
  caso,
  irA,
  mirar,
  pag,
  recorrerPagina,
} from './comun.mjs';

export const titulo = '01 · Acceso y sesión';

export async function ejecutar() {
  await caso('01-login-secretaria', () => asegurarSesion(SECRETARIA), { critico: true });

  await caso('02-panel', async () => {
    await asegurarSesion(SECRETARIA);
    await irA('/', 1200);
    await pag().getByRole('heading', { name: /panel de matrícula/i }).waitFor({ timeout: 10000 });
    await recorrerPagina();
    await mirar(3);
    return 'KPIs, avance por curso y pendientes';
  }, { critico: true });

  await caso('03-actividad', async () => {
    await asegurarSesion(SECRETARIA);
    await irA('/Home/Actividad', 800);
    await pag().getByRole('heading', { name: /actividad reciente/i }).waitFor({ timeout: 10000 });
    await recorrerPagina(5);
    await mirar(2);
    return 'consultas, descargas y accesos denegados';
  });

  await caso('04-denegacion-funcionarios', async () => {
    await asegurarSesion(SECRETARIA);
    await irA('/Admin/Funcionarios', 800);
    await pag().getByRole('heading', { name: /permiso/i }).waitFor({ timeout: 8000 }).catch(() => {});
    await mirar(2);
    return `SECRETARIA sin ADMIN:ADMINISTRAR (${ADMIN} sí lo tiene)`;
  });

  return 'acceso, panel, auditoría y denegación';
}
