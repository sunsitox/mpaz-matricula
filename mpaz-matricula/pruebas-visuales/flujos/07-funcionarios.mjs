// Módulo 07 · Funcionarios y roles.
// ADMIN_TI da de alta cuentas, entrega clave temporal, desactiva/reactiva,
// desbloquea y cierra roles. Secretaría ya se mostró denegada en el módulo 01.

import {
  ADMIN,
  asegurarSesion,
  caso,
  irA,
  mirar,
  pag,
  pausa,
  recorrerPagina,
} from './comun.mjs';

export const titulo = '07 · Funcionarios y roles';

const DEMO = 'demo.video@mpaz.cl';

export async function ejecutar() {
  await caso('01-administracion-funcionarios', async () => {
    const detalle = await asegurarSesion(ADMIN);
    await irA('/Admin/Funcionarios', 1000);
    await pag().getByRole('heading', { name: /^funcionarios$/i }).waitFor({ timeout: 10000 });
    await recorrerPagina(6);
    await mirar(2);

    // Alta de un funcionario de demostración (si ya existe, se reutiliza).
    const yaExiste = (await pag().locator('tr', { hasText: DEMO }).count()) > 0;
    if (!yaExiste) {
      await pag().getByRole('link', { name: /crear funcionario/i }).click();
      await pag().waitForLoadState('domcontentloaded');
      await pag().locator('#Nombre').fill('Video Demo Grav');
      await pag().locator('#Correo').fill(DEMO);
      await pag().locator('#Contrasena').fill('VideoDemo2027X');
      await pag().locator('input[name="Roles"][value="DOCENTE"]').check({ force: true });
      await pag().getByRole('button', { name: /^crear funcionario$/i }).click();
      await pag().waitForLoadState('domcontentloaded');
      await pausa(2500);
    }

    const fila = pag().locator('tr', { hasText: DEMO }).first();
    await fila.scrollIntoViewIfNeeded();

    // Clave temporal: se muestra una sola vez.
    await fila.getByRole('button', { name: /restablecer contraseña/i }).click();
    await pag().waitForLoadState('domcontentloaded');
    await pag().getByRole('heading', { name: /contraseña temporal/i }).waitFor({ timeout: 8000 }).catch(() => {});
    await pausa(2500);

    // Desactivar y reactivar.
    await fila.getByRole('button', { name: /^desactivar$/i }).click();
    await pag().waitForLoadState('domcontentloaded');
    await fila.getByRole('button', { name: /^activar$/i }).click();
    await pag().waitForLoadState('domcontentloaded');
    await pausa(2000);
    return `${detalle}; alta, clave temporal, desactivar/reactivar`;
  }, { critico: true });

  await caso('02-cerrar-rol', async () => {
    await asegurarSesion(ADMIN);
    await irA('/Admin/Funcionarios', 1000);
    const fila = pag().locator('tr', { hasText: DEMO }).first();
    await fila.waitFor({ timeout: 8000 });
    const cerrar = fila.getByRole('button', { name: /cerrar rol/i }).first();
    if ((await cerrar.count()) === 0) return 'el funcionario de demostración no tiene roles abiertos';
    await cerrar.click();
    await pag().waitForLoadState('domcontentloaded');
    await pausa(2000);
    return 'rol cerrado con fecha de término (historial conservado)';
  });

  return 'altas, claves, estado y cierre de roles';
}
