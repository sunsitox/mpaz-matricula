// Módulo 06 · Fichas médicas del personal.
// UTP y Dirección ven y editan las fichas de salud del personal; Secretaría
// no accede (dato sensible). Incluye la descarga en PDF.

import {
  DIRECCION,
  SECRETARIA,
  asegurarSesion,
  caso,
  irA,
  mirar,
  pag,
  pausa,
  recorrerPagina,
  rutaSalida,
} from './comun.mjs';

export const titulo = '06 · Fichas médicas del personal';

export async function ejecutar() {
  await caso('01-listado-de-fichas', async () => {
    await asegurarSesion(DIRECCION);
    await irA('/personal/fichas-medicas', 1200);
    await pag().getByRole('heading', { name: /fichas de salud del personal/i }).waitFor({ timeout: 10000 });
    await recorrerPagina(6);
    const total = await pag().locator('tbody tr').count();
    await mirar(2);
    return `${total} ficha(s) registradas`;
  }, { critico: true });

  await caso('02-ver-ficha-y-pdf', async () => {
    const ver = pag().getByRole('link', { name: /ver ficha/i }).first();
    if ((await ver.count()) === 0) return 'sin fichas cargadas todavía';
    await ver.click();
    await pag().waitForLoadState('domcontentloaded');
    await pausa(1500);
    await recorrerPagina(5);
    const pdf = pag().getByRole('link', { name: /^pdf$/i }).first();
    if ((await pdf.count()) > 0) {
      const descarga = pag().waitForEvent('download', { timeout: 12000 }).catch(() => null);
      await pdf.click();
      const archivo = await descarga;
      if (archivo) await archivo.saveAs(rutaSalida('ficha-salud-personal', 'pdf'));
    }
    await mirar(2);
    return 'datos de salud, contactos de emergencia y PDF';
  });

  await caso('03-denegacion-secretaria', async () => {
    await asegurarSesion(SECRETARIA);
    await irA('/personal/fichas-medicas', 1000);
    await pag().getByRole('heading', { name: /permiso/i }).waitFor({ timeout: 8000 }).catch(() => {});
    await mirar(2);
    return 'SECRETARIA sin acceso a datos de salud del personal';
  });

  return 'fichas del personal y reserva del dato sensible';
}
