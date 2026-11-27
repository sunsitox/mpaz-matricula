// Módulo 09 · Configuración (año escolar).
// Consulta del año activo, su periodo de matrícula y el formulario de alta.
// Es una demostración de lectura: no se registra ni se activa nada para no
// alterar los datos con los que se graban los demás módulos.

import {
  SECRETARIA,
  asegurarSesion,
  caso,
  irA,
  mirar,
  pag,
  pausa,
  recorrerPagina,
} from './comun.mjs';

export const titulo = '09 · Configuración del año escolar';

export async function ejecutar() {
  await caso('01-anio-escolar', async () => {
    await asegurarSesion(SECRETARIA);
    await irA('/Configuracion/AnioEscolar', 1200);
    await pag().getByRole('heading', { name: /año escolar de matrícula/i }).waitFor({ timeout: 10000 });
    await recorrerPagina(6);

    const activo = await pag().locator('tbody tr', { hasText: /activo/i }).first().innerText().catch(() => '');
    await mirar(2);
    return activo ? activo.replace(/\s+/g, ' ').slice(0, 120) : 'años registrados y estado';
  }, { critico: true });

  await caso('02-formulario-de-alta', async () => {
    const campoAnio = pag().locator('#nuevo-anio');
    if ((await campoAnio.count()) === 0) return 'sin formulario de alta visible';
    // El año y su periodo salen de la misma fecha: así el formulario sigue
    // siendo válido en cualquier año en que se grabe la demostración.
    const anio = new Date().getFullYear() + 6;
    await campoAnio.scrollIntoViewIfNeeded();
    await campoAnio.fill(String(anio));
    await pag().locator('#apertura').fill(`${anio}-08-01`);
    await pag().locator('#cierre').fill(`${anio}-12-31`);
    await pausa(2500);
    // Se descarta el formulario: la grabación no modifica la configuración.
    await irA('/Configuracion/AnioEscolar', 800);
    return 'alta de año con periodo de apertura y cierre (sin guardar)';
  });

  return 'consulta del año escolar activo';
}
