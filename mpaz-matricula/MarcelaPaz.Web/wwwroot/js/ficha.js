/* Ficha de matrícula: guarda y cambia de paso sin recargar la página.
   Intercepta el envío del formulario y lo manda con fetch; el servidor
   responde sólo el contenido del paso siguiente y aquí se reemplaza dentro de
   la tarjeta. Si el envío falla o no hay JS, el formulario hace su POST normal. */
(() => {
    'use strict';

    const tarjeta = document.getElementById('ficha-paso');

    if (!tarjeta || !window.fetch || !window.FormData) {
        return;
    }

    const reducido = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    const aviso = document.getElementById('ficha-aviso');
    const pasos = Array.from(document.querySelectorAll('.wizard__paso'));
    const cuerpo = document.querySelector('.wizard__cuerpo');
    const contexto = document.querySelector('.wizard__contexto');
    const barra = document.getElementById('ux-progreso');

    let ocupado = false;

    const desplazar = destino => {
        if (destino && typeof destino.scrollIntoView === 'function') {
            destino.scrollIntoView({ block: 'center', behavior: reducido ? 'auto' : 'smooth' });
        }
    };

    const anunciar = texto => {
        if (aviso) {
            aviso.textContent = texto;
        }
    };

    const marcarPaso = paso => {
        tarjeta.dataset.paso = String(paso);

        pasos.forEach((nodo, indice) => {
            const numero = indice + 1;
            const estado = numero < paso ? 'hecho' : numero === paso ? 'activo' : 'pendiente';

            nodo.classList.remove('wizard__paso--hecho', 'wizard__paso--activo', 'wizard__paso--pendiente');
            nodo.classList.add('wizard__paso--' + estado);

            if (numero === paso) {
                nodo.setAttribute('aria-current', 'step');
            } else {
                nodo.removeAttribute('aria-current');
            }

            const oculto = nodo.querySelector('.sr-only');

            if (oculto) {
                oculto.textContent = 'Paso ' + numero + ' de 8: '
                    + (estado === 'hecho' ? 'completado' : estado === 'activo' ? 'en curso' : 'pendiente');
            }
        });
    };

    const marcarAncho = amplio => {
        if (cuerpo) {
            cuerpo.classList.toggle('wizard__cuerpo--amplio', amplio);
        }

        if (contexto) {
            contexto.classList.toggle('wizard__contexto--amplio', amplio);
        }
    };

    // Errores del paso recién pintado: abrir el bloque plegado que los contiene
    // y vincular cada mensaje con su campo (ux.js sólo lo hace al cargar).
    const senalarErrores = () => {
        tarjeta.querySelectorAll('.field-validation-error[data-valmsg-for]').forEach((mensaje, indice) => {
            const nombre = mensaje.getAttribute('data-valmsg-for');
            const campo = tarjeta.querySelector('[name="' + nombre + '"]');

            if (!campo) {
                return;
            }

            if (!mensaje.id) {
                mensaje.id = 'error-campo-' + indice;
            }

            campo.setAttribute('aria-invalid', 'true');

            const bloque = campo.closest('details');

            if (bloque) {
                bloque.open = true;
            }

            const ids = (campo.getAttribute('aria-describedby') || '').split(/\s+/).filter(Boolean);

            if (!ids.includes(mensaje.id)) {
                ids.push(mensaje.id);
                campo.setAttribute('aria-describedby', ids.join(' '));
            }
        });
    };

    // Foco: primero el campo que el servidor indicó, luego el resumen de
    // errores y, si todo salió bien, el título del paso nuevo.
    const enfocar = (respuesta, huboErrores) => {
        const campo = respuesta.headers.get('X-Ficha-Campo');
        const resumen = tarjeta.querySelector('[data-ux-resumen-errores]');

        if (campo) {
            const destino = tarjeta.querySelector('[name="' + campo + '"]');

            if (destino) {
                const bloque = destino.closest('details');

                if (bloque) {
                    bloque.open = true;
                }

                destino.focus({ preventScroll: true });
                desplazar(destino);
                return;
            }
        }

        if (resumen) {
            resumen.focus();
            desplazar(resumen);
            return;
        }

        const titulo = tarjeta.querySelector('.wizard__titulo-paso');

        if (titulo) {
            titulo.setAttribute('tabindex', '-1');
            titulo.focus({ preventScroll: true });
        }

        if (!huboErrores) {
            window.scrollTo({ top: 0, behavior: reducido ? 'auto' : 'smooth' });
        }
    };

    const pintar = (html, respuesta) => {
        tarjeta.innerHTML = html;

        const paso = parseInt(respuesta.headers.get('X-Ficha-Paso') || '0', 10);

        if (paso > 0) {
            marcarPaso(paso);
            anunciar('Paso ' + paso + ' de 8 en pantalla.');
        }

        marcarAncho(respuesta.headers.get('X-Ficha-Amplio') === '1');

        const url = respuesta.headers.get('X-Ficha-Url');

        if (url) {
            window.history.replaceState(null, '', url);
        }

        senalarErrores();

        // Si el paso incluye vista previa del PDF (paso 8), quitar el spinner apenas cargue el iframe.
        const marco = tarjeta.querySelector('.wizard__pdf-marco');
        if (marco) {
            const caja = marco.closest('.wizard__pdf');
            if (caja) {
                const listo = () => caja.classList.add('wizard__pdf--lista');
                if (marco.contentDocument && marco.contentDocument.readyState === 'complete') {
                    listo();
                } else {
                    marco.addEventListener('load', listo, { once: true });
                }
            }
        }
    };

    const cargando = (activo, formulario) => {
        if (formulario) {
            formulario.setAttribute('aria-busy', activo ? 'true' : 'false');
        }

        const boton = formulario
            ? formulario.querySelector('button[type="submit"]')
            : tarjeta.querySelector('button[type="submit"]');

        if (boton) {
            boton.classList.toggle('is-loading', activo);
        }

        tarjeta.classList.toggle('ficha__paso--cambiando', activo);

        if (barra && !activo) {
            barra.classList.remove('ux-progreso--visible');
            barra.style.width = '0';
        }
    };

    // Respuesta que no es el parcial (aviso, redirección, sesión vencida):
    // se deja navegar como siempre.
    const esPaginaCompleta = (html, respuesta) =>
        !respuesta.ok || /<!DOCTYPE html/i.test(html);

    const traer = async url => {
        const respuesta = await fetch(url, {
            headers: { 'X-Ficha-Ajax': '1', 'Accept': 'text/html' },
            credentials: 'same-origin'
        });

        const html = await respuesta.text();

        if (esPaginaCompleta(html, respuesta)) {
            window.location.assign(respuesta.url || url);
            return;
        }

        pintar(html, respuesta);
        enfocar(respuesta, Boolean(tarjeta.querySelector('[data-ux-resumen-errores]')));
    };

    const guardar = async (formulario, boton) => {
        if (ocupado) {
            return;
        }

        ocupado = true;
        cargando(true, formulario);

        try {
            const respuesta = await fetch(formulario.action, {
                method: 'POST',
                body: new FormData(formulario),
                headers: { 'X-Ficha-Ajax': '1', 'Accept': 'text/html' },
                credentials: 'same-origin'
            });

            const tipo = respuesta.headers.get('content-type') || '';

            if (tipo.indexOf('application/json') !== -1) {
                const cuerpoJson = await respuesta.json();

                if (cuerpoJson && cuerpoJson.ir) {
                    window.location.assign(cuerpoJson.ir);
                }

                return;
            }

            const html = await respuesta.text();

            if (esPaginaCompleta(html, respuesta)) {
                window.location.assign(respuesta.url || window.location.href);
                return;
            }

            pintar(html, respuesta);
            enfocar(respuesta, Boolean(tarjeta.querySelector('[data-ux-resumen-errores]')));
        } catch (error) {
            // Sin red o sin fetch disponible: envío normal del navegador.
            formulario.dataset.fichaNativo = 'si';
            formulario.submit();
            return;
        } finally {
            ocupado = false;
            cargando(false, formulario);

            if (boton) {
                boton.classList.remove('is-loading');
            }
        }
    };

    document.addEventListener('submit', evento => {
        const formulario = evento.target;

        if (!(formulario instanceof HTMLFormElement) || !tarjeta.contains(formulario)) {
            return;
        }

        if (formulario.dataset.fichaNativo === 'si') {
            delete formulario.dataset.fichaNativo;
            return;
        }

        if (formulario.method && formulario.method.toLowerCase() !== 'post') {
            return;
        }

        evento.preventDefault();
        // Evita que ux.js muestre el velo de página completa: aquí sólo cambia
        // un trozo de la tarjeta.
        evento.stopImmediatePropagation();

        guardar(formulario, evento.submitter);
    }, true);

    // Enlaces del propio asistente («Volver», pasos vecinos): mismo cambio
    // parcial en lugar de una recarga.
    document.addEventListener('click', evento => {
        if (evento.defaultPrevented || evento.button !== 0 || evento.metaKey
            || evento.ctrlKey || evento.shiftKey || evento.altKey) {
            return;
        }

        const enlace = evento.target.closest('a[href]');

        if (!enlace || enlace.target === '_blank' || enlace.hasAttribute('download')) {
            return;
        }

        const destino = new URL(enlace.href, window.location.href);

        if (destino.origin !== window.location.origin || !/\/Ficha\/Paso\/\d+$/.test(destino.pathname)) {
            return;
        }

        evento.preventDefault();
        evento.stopImmediatePropagation();

        if (ocupado) {
            return;
        }

        ocupado = true;
        cargando(true, null);

        traer(destino.href).catch(() => window.location.assign(destino.href))
            .finally(() => {
                ocupado = false;
                cargando(false, null);
            });
    }, true);
})();
