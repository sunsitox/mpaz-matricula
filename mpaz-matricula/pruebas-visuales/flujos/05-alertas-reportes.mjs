// Módulo 05 · Alertas y reportes.
// Alertas de vencimiento por año, reporte de matrículas con exportación CSV
// auditada y documento faltantes.

import {
  SECRETARIA,
  asegurarSesion,
  caso,
  irA,
  mirar,
  pag,
  recorrerPagina,
  rutaSalida,
} from './comun.mjs';

export const titulo = '05 · Alertas y reportes';

export async function ejecutar() {
  await caso('01-alertas-de-vencimiento', async () => {
    await asegurarSesion(SECRETARIA);
    await irA('/Alertas', 1000);
    await pag().getByRole('heading', { name: /alertas de vencimiento/i }).waitFor({ timeout: 10000 });
    const anio = pag().locator('#anio-alertas');
    if ((await anio.count()) > 0) {
      await anio.selectOption({ index: 0 });
      await pag().getByRole('button', { name: /^ver$/i }).click();
      await pag().waitForLoadState('domcontentloaded');
    }
    await recorrerPagina(6);
    await mirar(2);
    return 'documentos por vencer, vencidos y sin subir';
  }, { critico: true });

  await caso('02-reporte-matriculas-y-csv', async () => {
    await asegurarSesion(SECRETARIA);
    await irA('/Reportes/Matriculas', 1000);
    await pag().getByRole('heading', { name: /reporte de matrículas/i }).waitFor({ timeout: 10000 });
    const descarga = pag().waitForEvent('download', { timeout: 12000 }).catch(() => null);
    await pag().getByRole('link', { name: /descargar csv/i }).first().click();
    const archivo = await descarga;
    if (archivo) await archivo.saveAs(rutaSalida('reporte-matriculas', 'csv'));
    await recorrerPagina(5);
    return archivo ? 'CSV auditado como GENERAL:EXPORTAR' : 'tabla de estados por curso';
  }, { critico: true });

  await caso('03-faltantes', async () => {
    await irA('/Reportes/Faltantes', 1000);
    await pag().getByRole('heading', { name: /documentos faltantes/i }).waitFor({ timeout: 10000 }).catch(() => {});
    const descarga = pag().waitForEvent('download', { timeout: 12000 }).catch(() => null);
    const boton = pag().getByRole('link', { name: /descargar csv/i }).first();
    if ((await boton.count()) > 0) {
      await boton.click();
      const archivo = await descarga;
      if (archivo) await archivo.saveAs(rutaSalida('reporte-faltantes', 'csv'));
    }
    await recorrerPagina(5);
    await mirar(2);
    return 'expedientes incompletos por curso y documento';
  });

  return 'alertas y reportes con exportación';
}
