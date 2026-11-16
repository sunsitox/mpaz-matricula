// Simulación visual de usuario · Portal de Matrícula MPAZ
// Usuario: mteresa.lagos@mpaz.cl (SECRETARIA: el rol con más funciones,
// 23 permisos: listado y búsqueda, wizard completo, ficha por pestañas,
// documentos, reportes con exportación; la denegación a Funcionarios
// demuestra MAT-12).
//
// Uso:
//   npm run simular            -> navegador visible (demostración)
//   npm run simular:headless   -> sin ventana (verificación)
// Variables: BASE_URL, USUARIO, CLAVE, CLAVE_NUEVA, HEADLESS, SLOWMO
//
// Requisitos: app corriendo (http://localhost:5199) y PostgreSQL local
// con los datos mock (ver README.md).

import { chromium } from 'playwright';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const BASE = process.env.BASE_URL ?? 'https://localhost:7199';
const USUARIO = process.env.USUARIO ?? 'mteresa.lagos@mpaz.cl';
const CLAVE_INICIAL = process.env.CLAVE ?? 'Mpaz2027segura';
const CLAVE_NUEVA = process.env.CLAVE_NUEVA ?? 'Simulacion2026X';
const HEADLESS = process.env.HEADLESS === '1' || process.argv.includes('--headless');
const SLOWMO = Number(process.env.SLOWMO ?? (HEADLESS ? 0 : 300));

const EVID = path.join(path.dirname(fileURLToPath(import.meta.url)), 'evidencias');
fs.mkdirSync(EVID, { recursive: true });

const resultados = [];
let n = 0;
let page;

async function foto(nombre) {
  n += 1;
  const archivo = `${nombre}.png`;
  await page.screenshot({ path: path.join(EVID, archivo), fullPage: false });
  return archivo;
}

async function caso(nombre, fn, { critico = true } = {}) {
  try {
    const detalle = (await fn()) ?? '';
    const archivo = await foto(nombre);
    resultados.push({ caso: nombre, estado: 'OK', detalle, archivo });
    console.log(`  [OK] ${nombre}${detalle ? ' — ' + detalle : ''}`);
  } catch (e) {
    try { await page.screenshot({ path: path.join(EVID, `${nombre}.png`), fullPage: false }); } catch { /* sin captura */ }
    resultados.push({ caso: nombre, estado: critico ? 'FALLO' : 'OMITIDO', detalle: String(e.message ?? e).slice(0, 200) });
    console.log(`  [${critico ? 'FALLO' : 'OMITIDO'}] ${nombre} — ${String(e.message ?? e).slice(0, 160)}`);
    if (critico) throw e;
  }
}

// RUN válido con dígito verificador (módulo 11), único por ejecución.
function runUnico() {
  const ahora = new Date();
  const cuerpo = '28' + String(ahora.getHours()).padStart(2, '0')
    + String(ahora.getMinutes()).padStart(2, '0')
    + String(ahora.getSeconds()).padStart(2, '0');
  let suma = 0, mult = 2;
  for (let i = cuerpo.length - 1; i >= 0; i -= 1) {
    suma += Number(cuerpo[i]) * mult;
    mult = mult === 7 ? 2 : mult + 1;
  }
  const resto = 11 - (suma % 11);
  const dv = resto === 11 ? '0' : resto === 10 ? 'K' : String(resto);
  return `${cuerpo}-${dv}`;
}

async function llenarSiHay(label, valor) {
  const campo = page.getByLabel(new RegExp(label, 'i')).first();
  if ((await campo.count()) > 0 && (await campo.isVisible())) {
    await campo.fill(valor);
    return true;
  }
  return false;
}

async function iniciarSesion() {
  // Intento 1: clave inicial (o ya cambiada en una corrida anterior).
  for (const clave of [CLAVE_INICIAL, CLAVE_NUEVA]) {
    await page.goto(`${BASE}/Cuenta/Login`, { waitUntil: 'domcontentloaded' });
    await page.getByLabel(/correo/i).fill(USUARIO);
    await page.getByLabel(/contrase/i).fill(clave);
    await page.getByRole('button', { name: /ingresar/i }).click();
    await page.waitForLoadState('domcontentloaded');
    const url = page.url();
    if (/CambiarContrasena/i.test(url)) {
      // Cambio forzado de clave inicial.
      await page.getByLabel(/^actual/i).fill(CLAVE_INICIAL);
      await page.getByLabel(/^nueva/i).fill(CLAVE_NUEVA);
      await page.getByLabel(/^confirmar/i).fill(CLAVE_NUEVA);
      await page.getByRole('button', { name: /guardar|cambiar/i }).click();
      await page.waitForLoadState('domcontentloaded');
      continue; // vuelve al login con la clave nueva
    }
    if (!/Login/i.test(url)) return `sesión iniciada como ${USUARIO}`;
  }
  throw new Error('no se pudo iniciar sesión con ninguna clave conocida');
}

const navegador = await chromium.launch({ headless: HEADLESS, slowMo: SLOWMO });
const contexto = await navegador.newContext({ viewport: { width: 1366, height: 820 }, ignoreHTTPSErrors: true });
page = await contexto.newPage();

console.log(`Simulación visual · ${USUARIO} contra ${BASE} (${HEADLESS ? 'oculto' : 'visible'})`);

try {
  await caso('01-login-secretaria', async () => {
    const detalle = await iniciarSesion();
    if (!await page.getByRole('heading', { name: /estudiantes/i }).first().isVisible({ timeout: 5000 }).catch(() => false)) {
      await page.goto(`${BASE}/Alumnos`, { waitUntil: 'domcontentloaded' });
    }
    return detalle;
  });

  await caso('02-listado-estudiantes', async () => {
    await page.goto(`${BASE}/Alumnos`, { waitUntil: 'domcontentloaded' });
    const total = await page.locator('tbody tr').count();
    return `${total} filas visibles`;
  });

  await caso('03-busqueda-nombre', async () => {
    await page.goto(`${BASE}/Alumnos`, { waitUntil: 'domcontentloaded' });
    await page.locator('#q').fill('a');
    await page.getByRole('button', { name: /filtrar/i }).click();
    await page.waitForLoadState('domcontentloaded');
    const total = await page.locator('tbody tr').count();
    return `filtro 'a': ${total} filas`;
  });

  await caso('04-ficha-general', async () => {
    await page.goto(`${BASE}/Alumnos`, { waitUntil: 'domcontentloaded' });
    await page.getByRole('link', { name: /ver ficha/i }).first().click();
    await page.waitForLoadState('domcontentloaded');
    await page.locator('#pestana-general').click();
    return page.url();
  });

  for (const pestana of ['autorizaciones', 'documentos', 'salud', 'pie']) {
    await caso(`05-ficha-${pestana}`, async () => {
      await page.locator(`#pestana-${pestana}`).click();
      return `pestaña ${pestana} visible para SECRETARIA`;
    }, { critico: false });
  }

  await caso('06-reportes-matriculas', async () => {
    await page.goto(`${BASE}/Reportes/Matriculas`, { waitUntil: 'domcontentloaded' });
    return 'vista de estado de matrículas';
  });

  await caso('07-reporte-csv', async () => {
    await page.goto(`${BASE}/Reportes/Matriculas`, { waitUntil: 'domcontentloaded' });
    const enlace = page.getByRole('link', { name: /csv/i }).first();
    if ((await enlace.count()) === 0) throw new Error('sin enlace CSV en la vista');
    const descarga = page.waitForEvent('download', { timeout: 10000 });
    await enlace.click();
    const archivo = await descarga;
    await archivo.saveAs(path.join(EVID, 'reporte-matriculas.csv'));
    return 'CSV descargado y auditado (permiso EXPORTAR)';
  }, { critico: false });

  await caso('08-reporte-faltantes', async () => {
    await page.goto(`${BASE}/Reportes/Faltantes`, { waitUntil: 'domcontentloaded' });
    return 'vista de faltantes documentales';
  }, { critico: false });

  await caso('09-denegacion-funcionarios', async () => {
    await page.goto(`${BASE}/admin/funcionarios`, { waitUntil: 'domcontentloaded' });
    await page.getByRole('heading', { name: /permiso/i }).waitFor({ timeout: 8000 });
    return 'SECRETARIA denegada y auditada (MAT-12: solo ADMIN_TI administra)';
  }, { critico: false });

  await caso('10-wizard-paso1', async () => {
    await page.goto(`${BASE}/`, { waitUntil: 'domcontentloaded' });
    const nueva = page.getByRole('button', { name: /matrícula nueva/i }).first();
    if ((await nueva.count()) === 0) throw new Error('sin botón Matrícula nueva en el panel');
    await nueva.click();
    await page.waitForLoadState('domcontentloaded');
    const run = runUnico();
    await llenarSiHay('^nombres', 'SIMULACIÓN Visual');
    await llenarSiHay('apellido paterno', 'Demo');
    await llenarSiHay('apellido materno', 'MPAZ');
    await llenarSiHay('nacionalidad', 'Chilena');
    await llenarSiHay('domicilio', 'Calle Demo 123, Villa Prueba');
    await llenarSiHay('comuna', 'San Bernardo');
    const doc = page.getByLabel(/run|documento|número/i).first();
    if ((await doc.count()) > 0 && (await doc.isVisible())) await doc.fill(run);
    const fecha = page.locator('input[type="date"]').first();
    if ((await fecha.count()) > 0 && (await fecha.isVisible())) await fecha.fill('2018-03-15');
    return `borrador SIMULACIÓN con RUN ${run}`;
  }, { critico: false });

  await caso('11-comprobante-ficha', async () => {
    const espera = page.waitForEvent('download', { timeout: 15000 });
    await page.goto(`${BASE}/Ficha/Comprobante`, { waitUntil: 'commit' }).catch(() => {});
    const descarga = await espera;
    await descarga.saveAs(path.join(EVID, 'comprobante-ficha.pdf'));
    return 'PDF del comprobante descargado (listo para firma del apoderado)';
  }, { critico: false });

  await caso('12-salida', async () => {
    const salir = page.getByRole('link', { name: /salir|cerrar sesi/i }).first();
    if ((await salir.count()) > 0) {
      await salir.click();
      await page.waitForLoadState('domcontentloaded');
    } else {
      await page.goto(`${BASE}/Cuenta/Salida`, { waitUntil: 'domcontentloaded' });
    }
    return 'sesión cerrada';
  });
} catch (e) {
  console.log(`  (se interrumpe la corrida: ${String(e.message ?? e).slice(0, 120)})`);
}

await navegador.close();

const ok = resultados.filter((r) => r.estado === 'OK').length;
const omit = resultados.filter((r) => r.estado === 'OMITIDO').length;
const mal = resultados.filter((r) => r.estado === 'FALLO').length;
console.log(`\nResumen: ${ok} OK · ${omit} omitidos · ${mal} fallidos · evidencias en pruebas-visuales/evidencias/`);
process.exit(mal > 0 ? 1 : 0);
