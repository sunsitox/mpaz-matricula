// Piezas comunes del recorrido grabado: configuración, ayudas de navegador,
// registro de casos y utilidades de sesión. Lo usan el orquestador
// (grabar-flujo.mjs) y cada módulo de flujos/.

import { spawn } from 'node:child_process';
import { createInterface } from 'node:readline';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

export const AQUI = path.dirname(fileURLToPath(import.meta.url));
export const PRUEBAS = path.resolve(AQUI, '..');
export const PROYECTO = path.resolve(PRUEBAS, '..');
export const PROYECTO_WEB = path.join(PROYECTO, 'MarcelaPaz.Web');
export const EVIDENCIAS = path.join(PRUEBAS, 'evidencias');
export const SALIDA_VIDEO_DIR = path.join(EVIDENCIAS, 'grabar-flujo', 'video');
export const SALIDA_VIDEO = path.join(EVIDENCIAS, 'grabar-flujo', 'flujo-mpaz.webm');
export const LOG_SERVIDOR = path.join(EVIDENCIAS, 'grabar-flujo', 'servidor.log');

export const BASE = (process.env.BASE_URL ?? 'http://localhost:5199').replace(/\/$/, '');
// Cada rol tiene su cuenta y su clave: el portal no comparte credenciales.
export const SECRETARIA = process.env.USUARIO ?? 'prueba.secretaria@mpaz.cl';
export const DIRECCION = process.env.USUARIO_DIRECCION ?? 'prueba.direccion@mpaz.cl';
export const ADMIN = process.env.USUARIO_ADMIN ?? 'admin.palma@mpaz.cl';
export const DOCENTE = process.env.USUARIO_DOCENTE ?? 'prueba.docente@mpaz.cl';
export const TENS = process.env.USUARIO_TENS ?? 'prueba.tens@mpaz.cl';
export const UTP = process.env.USUARIO_UTP ?? 'prueba.utp@mpaz.cl';

export const CLAVE_INICIAL = process.env.CLAVE ?? 'Mpaz2027segura';
export const CLAVE_NUEVA = process.env.CLAVE_NUEVA ?? 'Simulacion2026X';

const CLAVES = new Map([
  [SECRETARIA, process.env.CLAVE_SECRETARIA ?? process.env.CLAVE ?? 'Mpaz2027.Secretaria'],
  [DIRECCION, process.env.CLAVE_DIRECCION ?? 'Mpaz2027.Direccion'],
  [ADMIN, process.env.CLAVE_ADMIN ?? 'Mpaz2027.Admin'],
  [DOCENTE, process.env.CLAVE_DOCENTE ?? 'Mpaz2027.Docente'],
  [TENS, process.env.CLAVE_TENS ?? 'Mpaz2027.Tens'],
  [UTP, process.env.CLAVE_UTP ?? 'Mpaz2027.Utp'],
]);
export const HEADLESS = process.env.HEADLESS === '1' || process.argv.includes('--headless');
export const SLOWMO = Number(process.env.SLOWMO ?? (HEADLESS ? 0 : 350));
export const MIN_VIDEO_MS = Number(process.env.MIN_VIDEO_MS ?? 100000);
export const SOLO = (process.env.SOLO ?? '').split(',').map(s => s.trim()).filter(Boolean);
export const LEVANTAR_SERVIDOR = process.argv.includes('--levantar-servidor') || process.env.LEVANTAR_SERVIDOR === '1';
export const SERVIDOR_CMD = process.env.SERVIDOR_CMD ?? '';
export const SERVIDOR_ARGS = process.env.SERVIDOR_ARGS ?? '';
export const ANCHO = Number(process.env.ANCHO ?? 1280);
export const ALTO = Number(process.env.ALTO ?? 720);
export const ZOOM_MAX = Number(process.env.ZOOM ?? 1);

fs.mkdirSync(SALIDA_VIDEO_DIR, { recursive: true });

// Estado compartido de la corrida: el navegador se crea en el orquestador y
// los módulos lo consumen a través de pag().
export const estado = {
  page: null,
  contexto: null,
  navegador: null,
  inicio: Date.now(),
  modulo: '',
  resultados: [],
  sesion: null,
  runNuevo: null,
  codigoCertificado: process.env.CERT_CODIGO?.trim() || null,
  servidor: null,
  servidorLog: '',
};

export function pag() {
  if (!estado.page) throw new Error('El navegador todavía no está listo');
  return estado.page;
}

export function pausa(ms = 1200) {
  return new Promise(resolve => setTimeout(resolve, ms));
}

export function rutaSalida(nombre, extension = 'png') {
  return path.join(EVIDENCIAS, 'grabar-flujo', nombre + '.' + extension);
}

export function enSolo(nombre) {
  return SOLO.length === 0 || SOLO.some(prefijo => nombre.startsWith(prefijo));
}

export async function caso(nombre, fn, { critico = false } = {}) {
  if (!enSolo(nombre)) return;
  const antes = Date.now();
  try {
    const detalle = (await fn()) ?? '';
    estado.resultados.push({ caso: nombre, estado: 'OK', detalle, modulo: estado.modulo });
    console.log(`[OK] ${nombre}${detalle ? ` — ${detalle}` : ''} (${Math.round((Date.now() - antes) / 1000)}s)`);
  } catch (error) {
    const mensaje = String(error?.message ?? error).slice(0, 260);
    const estadoFinal = critico ? 'FALLO' : 'OMITIDO';
    estado.resultados.push({ caso: nombre, estado: estadoFinal, detalle: mensaje, modulo: estado.modulo });
    console.log(`[${estadoFinal}] ${nombre} — ${mensaje.slice(0, 220)}`);
    if (critico) throw error;
  }
}

// ---------------------------------------------------------------------------
// Arranque manual: el video comienza cuando el portal ya está abierto y el
// usuario pulsa ENTER. Sin terminal interactiva, arranca a los 90 segundos.
// ---------------------------------------------------------------------------
export async function esperarInicioManual() {
  if (!process.stdin.isTTY) {
    console.log('\n[Sin terminal interactiva] La grabación comienza automáticamente.\n');
    return;
  }

  console.log([
    '',
    '==============================================================',
    '  RECORRIDO AUTOMATIZADO · PORTAL DE MATRÍCULA MARCELA PAZ',
    '==============================================================',
    '',
    '  El navegador ya tiene el portal abierto (pantalla de acceso).',
    '  Todo lo que se ve desde ahora queda en el video.',
    '',
    '  >>> Pulse ENTER para comenzar la grabación <<<',
    '  (o espere 90 segundos y arranca solo; Ctrl+C para cancelar) ',
    '',
  ].join('\n'));

  const rl = createInterface({ input: process.stdin, output: process.stdout });
  const tecleado = new Promise(resolve => rl.question('', resolve));
  const limite = new Promise(resolve => setTimeout(() => resolve('timeout'), 90000));
  const respuesta = await Promise.race([tecleado, limite]);
  rl.close();

  if (respuesta === 'timeout') {
    console.log('Arrancando automáticamente tras 90 segundos sin respuesta.\n');
  } else {
    console.log('Comenzando…\n');
  }
  estado.inicio = Date.now();
}

// ---------------------------------------------------------------------------
// Servidor (se publica en temporales, nunca sobre el portal en ejecución)
// ---------------------------------------------------------------------------
async function esperarRespuesta(url, intentos = 3, esperaMs = 3000) {
  for (let i = 0; i < intentos; i += 1) {
    try {
      const respuesta = await fetch(url, { redirect: 'manual', signal: AbortSignal.timeout(5000) });
      if (respuesta.status >= 200 && respuesta.status < 500) return respuesta;
    } catch {
      // Se reintenta abajo.
    }
    await pausa(esperaMs);
  }
  return null;
}

export function detenerServidor() {
  const servidor = estado.servidor;
  if (!servidor || servidor.killed) return;
  try {
    if (process.platform === 'win32') {
      spawn('taskkill.exe', ['/PID', String(servidor.pid), '/T', '/F'], { stdio: 'ignore' });
    } else {
      servidor.kill('SIGTERM');
    }
  } catch {
    servidor.kill('KILL');
  }
}

export function guardarLogServidor() {
  if (estado.servidorLog) fs.writeFileSync(LOG_SERVIDOR, estado.servidorLog);
}

function ejecutarComando(comando, args, cwd) {
  return new Promise((resolve, reject) => {
    const child = spawn(comando, args, { cwd, windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] });
    let salida = '';
    child.stdout.on('data', data => { salida += data.toString(); });
    child.stderr.on('data', data => { salida += data.toString(); });
    child.on('error', reject);
    child.on('exit', codigo => {
      if (codigo === 0) resolve(salida);
      else reject(new Error(`${comando} ${args.join(' ')} terminó con código ${codigo}\n${salida}`));
    });
  });
}

export async function asegurarServidor() {
  if (await esperarRespuesta(`${BASE}/Cuenta/Login`, 1, 1000)) return;

  if (!LEVANTAR_SERVIDOR) {
    throw new Error(`El servidor no responde en ${BASE}. Levántelo o ejecute el script con --levantar-servidor.`);
  }

  let comando = SERVIDOR_CMD;
  let args;
  let cwdServidor = PROYECTO;
  const entorno = { ...process.env };

  if (!comando) {
    const salida = path.join(os.tmpdir(), `mpazvideo-${process.pid}`);
    fs.mkdirSync(salida, { recursive: true });
    const dotnet = process.platform === 'win32' ? 'dotnet.exe' : 'dotnet';
    await ejecutarComando(dotnet, [
      'publish', PROYECTO_WEB, '-c', 'Debug', '--nologo',
      '-o', salida,
      `-p:BaseOutputPath=${path.join(salida, 'build')}`,
    ], PROYECTO);
    comando = dotnet;
    args = [path.join(salida, 'MarcelaPaz.Web.dll'), '--urls', BASE];
    cwdServidor = salida;
    entorno.ASPNETCORE_ENVIRONMENT = 'Development';
    entorno.DOTNET_ENVIRONMENT = 'Development';
  } else if (SERVIDOR_ARGS) {
    args = SERVIDOR_ARGS.split(' ');
  } else {
    const dotnet = process.platform === 'win32' ? 'dotnet.exe' : 'dotnet';
    comando = dotnet;
    args = ['run', '--project', PROYECTO_WEB, '--urls', BASE];
  }

  const servidor = spawn(comando, args, {
    cwd: cwdServidor,
    env: entorno,
    windowsHide: true,
    stdio: ['ignore', 'pipe', 'pipe'],
  });
  estado.servidor = servidor;

  servidor.stdout.on('data', data => { estado.servidorLog += data.toString(); });
  servidor.stderr.on('data', data => { estado.servidorLog += data.toString(); });

  const lista = await esperarRespuesta(`${BASE}/Cuenta/Login`, 40, 3000);
  if (!lista) {
    detenerServidor();
    guardarLogServidor();
    throw new Error(`El servidor no quedó listo en ${BASE}. Revisa evidencias/grabar-flujo/servidor.log`);
  }
}

// ---------------------------------------------------------------------------
// Datos de prueba
// ---------------------------------------------------------------------------
// RUN válido con dígito verificador (módulo 11), único por ejecución.
export function runUnico(desplazamiento = 0) {
  const ahora = new Date();
  const valor = Number('28' + String(ahora.getHours()).padStart(2, '0')
    + String(ahora.getMinutes()).padStart(2, '0')
    + String(ahora.getSeconds()).padStart(2, '0')) + desplazamiento;
  const cuerpo = String(valor).padStart(8, '0');
  let suma = 0;
  let factor = 2;
  for (let i = cuerpo.length - 1; i >= 0; i -= 1) {
    suma += Number(cuerpo[i]) * factor;
    factor = factor === 7 ? 2 : factor + 1;
  }
  const resto = 11 - (suma % 11);
  const dv = resto === 11 ? '0' : resto === 10 ? 'K' : String(resto);
  return `${cuerpo}-${dv}`;
}

// ---------------------------------------------------------------------------
// Utilidades de navegador
// ---------------------------------------------------------------------------
export async function llenar(selector, valor) {
  const page = pag();
  const campo = page.locator(selector).first();
  if ((await campo.count()) === 0) throw new Error(`No apareció el campo ${selector}`);
  await campo.fill(String(valor));
  await pausa(250);
}

export async function seleccionar(selector, valor) {
  const page = pag();
  await page.locator(selector).first().selectOption(valor);
  await pausa(250);
}

export async function radio(nombre, valor) {
  const page = pag();
  await page.locator(`[name="${nombre}"][value="${valor}"]`).first().check({ force: true });
  await pausa(250);
}

export async function abrirDetalles() {
  await pag().evaluate(() => {
    document.querySelectorAll('details').forEach(detalle => { detalle.open = true; });
  });
}

export async function guardarYSiguiente(pasoEsperado) {
  const page = pag();
  await page.getByRole('button', { name: /continuar|guardar y finalizar/i }).click();
  await page.getByRole('heading', { name: new RegExp(`Paso ${pasoEsperado}\\b|Ficha guardada`, 'i') })
    .waitFor({ timeout: 15000 })
    .catch(async () => {
      const texto = await page.evaluate(() => document.body?.innerText ?? '').catch(() => '');
      await page.screenshot({ path: rutaSalida(`diagnostico-paso-${pasoEsperado}`), fullPage: false }).catch(() => {});
      throw new Error(`No se avanzó al paso ${pasoEsperado}: ${page.url()} — ${texto.slice(0, 400)}`);
    });
  await pausa(1500);
}

export async function pantallaFinal() {
  const page = pag();
  const texto = await page.locator('body').innerText().catch(() => '');
  if (!/Ficha guardada|Panel de matrícula|Estudiantes/i.test(texto)) {
    throw new Error('La pantalla final no coincide con el cierre esperado');
  }
  await pausa(3000);
}

// Paneó suave de arriba a abajo con solape, para que en el video se vea
// TODA la página sin saltos que corten contenido.
export async function recorrerPagina(pasos = 7) {
  await pag().evaluate(async (n) => {
    const total = Math.max(document.documentElement.scrollHeight - window.innerHeight, 1);
    const paso = Math.max(Math.round(total / n), Math.round(window.innerHeight * 0.6));
    for (let y = paso; y <= total; y += paso) {
      window.scrollTo({ top: y, behavior: 'smooth' });
      await new Promise(resolve => setTimeout(resolve, 560));
    }
    window.scrollTo({ top: total, behavior: 'smooth' });
    await new Promise(resolve => setTimeout(resolve, 500));
    // Desplaza también las tablas con scroll horizontal interno.
    document.querySelectorAll('.tabla-envoltura, .alumnos__tabla-envoltura')
      .forEach(contenedor => {
        contenedor.scrollTo({ left: contenedor.scrollWidth, behavior: 'smooth' });
      });
    await new Promise(resolve => setTimeout(resolve, 500));
    document.querySelectorAll('.tabla-envoltura, .alumnos__tabla-envoltura')
      .forEach(contenedor => {
        contenedor.scrollTo({ left: 0, behavior: 'smooth' });
      });
    window.scrollTo({ top: 0, behavior: 'smooth' });
    await new Promise(resolve => setTimeout(resolve, 500));
  }).catch(() => {});
}

// Ajusta el zoom para que el contenido ocupe exactamente la ventana:
// nunca más ancho (sin scroll horizontal) y sin dejar espacios.
export async function ajustarAlAncho() {
  await pag().evaluate((maximo) => {
    const ancho = document.documentElement.scrollWidth;
    const ventana = window.innerWidth;
    const zoom = ancho > ventana ? Math.floor((ventana / ancho) * 100) / 100 : maximo;
    document.documentElement.style.zoom = String(Math.min(zoom, maximo));
  }, ZOOM_MAX).catch(() => {});
}

// Pausa viva: el mouse se mueve por la pantalla como si alguien mirara.
export async function mirar(segundos = 3) {
  const page = pag();
  const puntos = [[240, 160], [900, 220], [500, 420], [1100, 480], [320, 560], [760, 300]];
  for (let i = 0; i < segundos; i += 1) {
    const [x, y] = puntos[i % puntos.length];
    await page.mouse.move(x, y).catch(() => {});
    await pausa(900);
  }
}

export async function esperarMinimoParaVideo() {
  const restante = MIN_VIDEO_MS - (Date.now() - estado.inicio);
  if (restante > 0) await pausa(restante);
}

// La confirmación de envío (.envio) es una capa fija a pantalla completa: se
// cierra para poder seguir interactuando con el portal.
export async function cerrarAviso() {
  const page = pag();
  const capa = page.locator('.envio').first();
  if ((await capa.count()) === 0) return;
  const boton = capa.getByRole('link').or(capa.getByRole('button')).first();
  if ((await boton.count()) > 0) await boton.click({ timeout: 5000 }).catch(() => {});
  await page.evaluate(() => {
    document.querySelectorAll('.envio').forEach(nodo => nodo.remove());
  }).catch(() => {});
  await pausa(600);
}

export async function irA(ruta, espera = 1500) {
  const page = pag();
  await cerrarAviso();
  await page.goto(`${BASE}${ruta}`, { waitUntil: 'domcontentloaded' });
  await pausa(espera);
}

// ---------------------------------------------------------------------------
// Sesiones
// ---------------------------------------------------------------------------
export async function iniciarSesion(usuario) {
  const page = pag();
  const candidatas = [CLAVES.get(usuario), CLAVE_INICIAL, CLAVE_NUEVA].filter(Boolean);
  for (const clave of candidatas) {
    await page.goto(`${BASE}/Cuenta/Login`, { waitUntil: 'domcontentloaded' });
    const formulario = page.getByLabel(/correo institucional/i);
    const aparecio = await formulario.waitFor({ timeout: 5000 }).then(() => true).catch(() => false);
    if (!aparecio) {
      // Ya autenticado: el portal redirige al panel en lugar del acceso.
      const enPanel = await page.getByRole('heading', { name: /panel de matrícula|estudiantes/i }).first()
        .waitFor({ timeout: 3000 }).then(() => true).catch(() => false);
      if (enPanel) {
        estado.sesion = usuario;
        return `sesión activa como ${usuario}`;
      }
    }
    await formulario.fill(usuario);
    await page.getByLabel(/^contraseña$/i).first().fill(clave);
    await page.getByRole('button', { name: /ingresar/i }).click();
    await page.waitForLoadState('domcontentloaded');
    await pausa(1000);

    if (/CambiarContrasena/i.test(page.url())) {
      await page.getByLabel(/^actual/i).fill(CLAVE_INICIAL);
      await page.getByLabel(/^nueva/i).fill(CLAVE_NUEVA);
      await page.getByLabel(/^confirmar/i).fill(CLAVE_NUEVA);
      await page.getByRole('button', { name: /guardar|cambiar/i }).click();
      await page.waitForLoadState('domcontentloaded');
      await pausa(1000);
      continue;
    }

    if (!/Cuenta\/Login/i.test(page.url())) {
      await page.getByRole('heading', { name: /panel de matrícula|estudiantes/i }).first()
        .waitFor({ timeout: 10000 })
        .catch(() => {});
      estado.sesion = usuario;
      return `sesión iniciada como ${usuario}`;
    }
  }
  throw new Error(`no se pudo iniciar sesión como ${usuario}: revise CLAVE_* en el entorno o la cuenta`);
}

// Garantiza que la sesión abierta sea la del usuario pedido. Se lleva la
// cuenta en estado.sesion: el nombre visible no sirve para comparar y una
// lista de funcionarios puede contener el correo de otro rol.
export async function asegurarSesion(usuario) {
  if (estado.sesion === usuario) return `sesión activa como ${usuario}`;
  if (!/Cuenta\/(Login|CambiarContrasena)/i.test(pag().url())) await cerrarSesion();
  return iniciarSesion(usuario);
}

export async function cerrarSesion() {
  const page = pag();
  estado.sesion = null;
  await cerrarAviso();
  const salir = page.getByRole('button', { name: /cerrar sesión/i }).first();
  if ((await salir.count()) > 0) {
    await salir.click();
    await page.waitForLoadState('domcontentloaded');
  }
  await pausa(800);
}

// ---------------------------------------------------------------------------
// Ficha del alumno
// ---------------------------------------------------------------------------
export async function abrirFichaPorRun(run) {
  const page = pag();
  await cerrarAviso();
  await page.goto(`${BASE}/Alumnos?q=${encodeURIComponent(run)}`, { waitUntil: 'domcontentloaded' });
  const fila = page.locator('tbody tr').first();
  await fila.waitFor({ timeout: 10000 });
  await page.locator('.alumnos__ver').first().click();
  await page.waitForLoadState('domcontentloaded');
  await pausa(1500);
}

export async function abrirPestana(nombre) {
  const page = pag();
  await cerrarAviso();
  const pestana = page.locator(`#pestana-${nombre}`).first();
  await pestana.click();
  await pausa(1200);
}

export async function subirArchivoDemo(selector, nombre) {
  const page = pag();
  const archivo = rutaSalida(nombre ?? 'archivo-demo', 'png');
  await page.screenshot({ path: archivo });
  await page.locator(selector).first().setInputFiles(archivo);
  return archivo;
}
