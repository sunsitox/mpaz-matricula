// Capturas para el manual de usuarios.
//
// Recorre las pantallas reales del portal con las cuentas de prueba y guarda un
// PNG por pantalla en documentacion/manual-usuarios/capturas/. No usa datos de
// alumnos reales: trabaja sobre las matrículas de demostración.
//
// Uso:  node capturar-manual.mjs        (requiere el portal en BASE_URL)
//       BASE_URL=http://localhost:5199 node capturar-manual.mjs

import { chromium } from 'playwright';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  ADMIN,
  BASE,
  DIRECCION,
  DOCENTE,
  SECRETARIA,
  TENS,
  UTP,
  asegurarSesion,
  estado,
  irA,
  pag,
  pausa,
} from './flujos/comun.mjs';

const AQUI = path.dirname(fileURLToPath(import.meta.url));
const SALIDA = path.resolve(AQUI, '..', '..', 'documentacion', 'manual-usuarios', 'capturas');

const ANCHO = 1440;
const ALTO = 860;

fs.mkdirSync(SALIDA, { recursive: true });

// Ley 19.628 y 21.719: el manual es un documento que circula, así que las
// capturas no llevan datos de alumnos reales. Antes de cada foto se sustituyen
// RUN, correos y nombres por valores de ejemplo.
async function anonimizar() {
  await pag().evaluate(() => {
    const RUN = /\b\d{1,2}\.\d{3}\.\d{3}-[\dkK]\b/g;
    const CORREO = /[\w.+-]+@[\w.-]+\.\w+/g;
    const NOMBRE = /^[A-ZÁÉÍÓÚÑÜ][\wáéíóúñü.]*(\s+[A-ZÁÉÍÓÚÑÜ][\wáéíóúñü.]*){1,4}$/;
    const neutral = /^(Año|AÑO|Portal|PORTAL|Matrícula|MATRÍCULA|Estudiantes|ESTUDIANTES)/;

    // Se respetan las columnas que no son de personas (curso, comuna, estado,
    // fechas, acciones) para no convertir un curso en «Estudiante de Prueba».
    const columnasSeguras = /curso|comuna|estado|año|periodo|fecha|acción|accion|total|avance|rol|folio|emitido|por$/i;
    const esColumnaDePersona = (celda) => {
      const fila = celda.parentElement;
      const tabla = celda.closest('table');
      if (!fila || !tabla || !fila.cells) return true;
      const cabeceras = tabla.querySelectorAll('thead th');
      const indice = [...fila.cells].indexOf(celda);
      const cabecera = cabeceras[indice]?.innerText?.trim() ?? '';
      return !columnasSeguras.test(cabecera);
    };
    const whitelist = /^(Portal|Escuela|Año|Solo|Filtros|El detalle|Ver|Panel|Matrícula|Estudiantes|Cerrar|Buscar)/i;

    const esAdmin = /funcionario/i.test(location.pathname);
    const etiqueta = esAdmin ? 'Funcionario de Prueba' : 'Estudiante de Prueba';

    const visitar = (nodo) => {
      for (const hijo of [...nodo.childNodes]) {
        if (hijo.nodeType === Node.TEXT_NODE) {
          let texto = hijo.nodeValue ?? '';
          if (!texto.trim()) continue;
          texto = texto.replace(RUN, '12.345.678-9').replace(CORREO, 'funcionario@mpaz.cl');
          // «Última actualización: 27-11-2026 03:40 · Nombre Apellido»
          if (/actualizaci[oó]n|Actualizado/i.test(texto)) {
            texto = texto.replace(/·\s*[^·]+$/, '· funcionario de prueba');
          }
          texto = texto.replace(/Hola,\s*[A-ZÁÉÍÓÚÑ][^.]*\./, 'Hola, funcionario.');
          // «Abierto por Dirección: Nombre Apellido el 27-11-2026»
          texto = texto.replace(
            /((?:por|Por)\s+(?:Dirección|Secretaría|Administración TI|UTP|Inspectoría|TENS|Convivencia):?\s+)[A-ZÁÉÍÓÚÑ][^,.;]*?(?=\s+(?:el|del|en|a las)\b)/g,
            '$1funcionario de prueba');
          // «Validada por Dirección (Nombre Apellido) el ...»
          texto = texto.replace(
            /((?:por|Por)\s+(?:Dirección|Secretaría|Administración TI|UTP|Inspectoría|TENS|Convivencia)\s*\()[^)]+(\))/g,
            '$1funcionario de prueba$2');
          const padre = hijo.parentElement;
          const limpio = texto.trim();
          const enTabla = padre?.tagName === 'TD' && esColumnaDePersona(padre);
          const esNombre = padre
            && (enTabla || /^(H1|H2|H3|STRONG|SPAN|P|DIV|A)$/.test(padre.tagName))
            && limpio.length > 8 && !neutral.test(limpio) && !whitelist.test(limpio)
            && !/[.,:;]$/.test(limpio) && NOMBRE.test(limpio);
          let etiquetaNodo = etiqueta;
          if (padre?.tagName === 'TD') {
            const tabla = padre.closest('table');
            if (tabla && /funcionario/i.test(tabla.querySelector('thead')?.innerText ?? '')) {
              etiquetaNodo = 'Funcionario de Prueba';
            }
          }
          if (esNombre) texto = texto.replace(limpio, etiquetaNodo);
          hijo.nodeValue = texto;
        } else if (hijo.nodeType === Node.ELEMENT_NODE) {
          visitar(hijo);
        }
      }
    };
    visitar(document.body);
  }).catch(error => console.log(`  (aviso) no se pudo anonimizar: ${error.message}`));
}

async function captura(nombre) {
  const page = pag();
  await pausa(700);
  await anonimizar();
  const archivo = path.join(SALIDA, `${nombre}.png`);
  await page.screenshot({ path: archivo, fullPage: false });
  console.log(`  ${nombre}.png`);
}

async function paso(usuario, ruta, nombre, { titulo, antes } = {}) {
  if (usuario) await asegurarSesion(usuario);
  if (ruta) await irA(ruta, 1200);
  if (titulo) {
    await pag().getByRole('heading', { name: titulo }).first()
      .waitFor({ timeout: 10000 })
      .catch(() => console.log(`  (aviso) no apareció el título ${titulo} en ${ruta}`));
  }
  if (antes) await antes();
  await captura(nombre);
}

async function main() {
  const navegador = await chromium.launch({ headless: true });
  const contexto = await navegador.newContext({
    viewport: { width: ANCHO, height: ALTO },
    locale: 'es-CL',
    deviceScaleFactor: 1,
  });
  estado.navegador = navegador;
  estado.contexto = contexto;
  estado.page = await contexto.newPage();

  console.log(`Capturando desde ${BASE} → ${SALIDA}`);

  // 01 · Acceso, sin sesión.
  await paso(null, '/Cuenta/Login', '01-acceso', { titulo: /acceso|ingresar/i });

  // 02 · Panel y actividad de Secretaría.
  await paso(SECRETARIA, '/', '02-panel', { titulo: /panel de matrícula/i });
  await paso(SECRETARIA, '/Home/Actividad', '03-actividad', { titulo: /actividad/i });

  // 03 · Búsqueda y ficha del estudiante. Se busca «simulaci» para quedarse
  // con los registros de demostración y no con el registro del colegio.
  await paso(SECRETARIA, '/Alumnos?q=simulaci', '04-estudiantes', { titulo: /estudiantes/i });
  await paso(SECRETARIA, null, '05-ficha-general', {
    titulo: /estudiante|ficha/i,
    antes: async () => {
      const page = pag();
      await page.locator('.alumnos__ver').first().click();
      await page.waitForLoadState('domcontentloaded');
      await pausa(1500);
      await page.locator('#pestana-general').first().click().catch(() => {});
      await pausa(900);
    },
  });
  await paso(SECRETARIA, null, '06-ficha-expediente', {
    antes: async () => {
      await pag().locator('#pestana-documentos').first().click();
      await pausa(1200);
    },
  });
  await paso(SECRETARIA, null, '07-ficha-autorizaciones', {
    antes: async () => {
      await pag().locator('#pestana-autorizaciones').first().click();
      await pausa(1200);
    },
  });

  // 04 · Asistente de matrícula.
  await paso(SECRETARIA, '/Ficha/Buscar', '08-asistente-busqueda');
  await paso(SECRETARIA, null, '09-asistente-paso1', {
    antes: async () => {
      const page = pag();
      const nueva = page.getByRole('link', { name: /nueva matr/i }).first();
      if (await nueva.count()) {
        await nueva.click();
        await page.waitForLoadState('domcontentloaded');
        await pausa(1500);
      }
    },
  });

  // 05 · Dirección: alertas, certificados y reportes.
  await paso(DIRECCION, '/Alertas', '10-alertas', { titulo: /alertas/i });
  await paso(DIRECCION, '/Certificados', '11-certificados', { titulo: /certificado/i });
  await paso(DIRECCION, '/Reportes/Matriculas', '12-reportes');

  // 06 · Administración de cuentas y año escolar.
  await paso(ADMIN, '/admin/funcionarios', '13-funcionarios', { titulo: /funcionario/i });
  await paso(ADMIN, '/Configuracion/AnioEscolar', '14-anio-escolar');

  // 07 · Fichas médicas del personal (UTP) y medicación (TENS).
  await paso(UTP, '/personal/fichas-medicas', '15-fichas-medicas', { titulo: /ficha/i });
  await paso(TENS, '/Alumnos?q=simulaci', '16-tens-estudiantes', { titulo: /estudiante/i });
  await paso(TENS, null, '17-tens-salud', {
    antes: async () => {
      const page = pag();
      await page.locator('.alumnos__ver').first().click();
      await page.waitForLoadState('domcontentloaded');
      await pausa(1500);
      await page.locator('#pestana-salud').first().click().catch(() => {});
      await pausa(1200);
    },
  });

  // 08 · Rol de consulta y denegación.
  await paso(DOCENTE, '/', '18-docente-panel', { titulo: /panel de matrícula/i });
  await paso(DOCENTE, '/admin/funcionarios', '19-sin-permiso');

  await navegador.close();
  console.log(`Listo: ${fs.readdirSync(SALIDA).length} capturas en ${SALIDA}`);
}

main().catch(error => {
  console.error('Fallo la captura:', error.message);
  process.exitCode = 1;
});
