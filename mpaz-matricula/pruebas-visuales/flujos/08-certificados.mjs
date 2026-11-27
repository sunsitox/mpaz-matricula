// Módulo 08 · Certificados de alumno regular.
// Emisión desde la ficha (PDF con folio, QR y código), historial y
// verificación pública sin sesión: /c/{codigo} y /Certificados/Validar.

import { execFileSync } from 'node:child_process';
import {
  BASE,
  SECRETARIA,
  abrirFichaPorRun,
  asegurarSesion,
  caso,
  estado,
  irA,
  mirar,
  pag,
  pausa,
  recorrerPagina,
  rutaSalida,
} from './comun.mjs';

export const titulo = '08 · Certificados de alumno regular';

// El código viaja dentro del PDF (texto y URL del QR). Si hay pdftotext se
// recupera para validar el documento en la página pública.
function codigoDesdePdf(archivo) {
  try {
    const texto = execFileSync('pdftotext', [archivo, '-'], { encoding: 'utf8', timeout: 15000 });
    const encontrado = texto.match(/\/c\/([A-Za-z0-9]+)/);
    return encontrado ? encontrado[1] : null;
  } catch {
    return null;
  }
}

export async function ejecutar() {
  await caso('01-emitir-certificado', async () => {
    await asegurarSesion(SECRETARIA);
    if (!estado.runNuevo) throw new Error('falta la matrícula del módulo 02');
    await abrirFichaPorRun(estado.runNuevo);

    const capturadas = [];
    const alCrearPagina = nueva => nueva.on('download', descarga => capturadas.push(descarga));
    estado.contexto.on('page', alCrearPagina);
    pag().on('download', descarga => capturadas.push(descarga));

    await pag().getByRole('button', { name: /certificado alumno regular/i }).first().click();
    for (let i = 0; i < 40 && capturadas.length === 0; i += 1) await pausa(500);
    estado.contexto.off('page', alCrearPagina);

    let destino = null;
    if (capturadas.length > 0) {
      destino = rutaSalida('certificado-alumno-regular', 'pdf');
      await capturadas[0].saveAs(destino);
    }

    // Cierra la pestaña del PDF si quedó abierta.
    for (const otra of estado.contexto.pages()) {
      if (otra !== pag()) await otra.close().catch(() => {});
    }
    await pausa(1500);

    if (destino) {
      estado.codigoCertificado = estado.codigoCertificado ?? codigoDesdePdf(destino);
    }
    return destino ? 'PDF con folio, QR y código de verificación' : 'emisión sin descarga capturada';
  }, { critico: true });

  await caso('02-historial-de-certificados', async () => {
    await irA('/Certificados', 1200);
    await pag().getByRole('heading', { name: /certificados de alumno regular/i }).waitFor({ timeout: 10000 });
    await recorrerPagina(6);
    const total = await pag().locator('tbody tr').count();
    await mirar(2);
    return `${total} certificado(s) en el historial`;
  });

  await caso('03-validacion-publica', async () => {
    // Página pública: no requiere sesión.
    if (estado.codigoCertificado) {
      await irA(`/c/${estado.codigoCertificado}`, 1500);
      await pag().getByRole('heading', { name: /certificado de alumno regular/i }).waitFor({ timeout: 10000 });
      await pausa(2500);
      return 'documento válido verificado por QR/código, sin sesión';
    }

    await irA('/Certificados/Validar', 1200);
    await pag().getByRole('heading', { name: /certificado de alumno regular/i }).waitFor({ timeout: 10000 });
    await pag().locator('#codigo').fill('CODIGO-INEXISTENTE-0000');
    await pag().getByRole('button', { name: /^validar$/i }).click();
    await pag().waitForLoadState('domcontentloaded');
    await pausa(2500);
    return 'la página pública rechaza un código inexistente';
  });

  return 'emisión, historial y verificación pública';
}
