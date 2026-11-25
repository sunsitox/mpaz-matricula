/* Tablas con filtro sin recarga completa: al filtrar, ordenar o paginar solo
   se reemplaza la región de resultados. Sin JS, sin fetch o ante cualquier
   error, el navegador navega como siempre (progresivo, sin cambiar el flujo). */
(() => {
    'use strict';

    if (!('fetch' in window) || !('DOMParser' in window)) {
        return;
    }

    // Una petición en vuelo por región: lo último que se pide es lo que vale.
    const enVuelo = new Map();

    // Avisos para lector de pantalla (la región usa aria-busy mientras carga).
    const avisos = document.createElement('div');
    avisos.className = 'sr-only';
    avisos.setAttribute('role', 'status');
    avisos.setAttribute('aria-live', 'polite');
    document.body.appendChild(avisos);

    const anunciar = texto => {
        avisos.textContent = '';
        window.setTimeout(() => {
            avisos.textContent = texto;
        }, 30);
    };

    // ux.js marca el formulario (bloqueo de doble envío) y muestra la barra
    // de progreso; al no haber recarga, se retira aquí ese estado visual.
    const limpiarUx = formulario => {
        if (formulario instanceof HTMLFormElement) {
            delete formulario.dataset.enviando;
            formulario.removeAttribute('aria-busy');
            formulario.querySelectorAll('.is-loading').forEach(boton => {
                boton.classList.remove('is-loading');
                boton.removeAttribute('aria-disabled');
            });
        }

        const barra = document.getElementById('ux-progreso');

        if (barra) {
            barra.classList.remove('ux-progreso--visible');
            barra.style.width = '0';
        }
    };

    const navegar = async (url, regionId, formulario) => {
        const region = document.getElementById(regionId);

        if (!region) {
            window.location.assign(url);
            return;
        }

        const formSyncId = formulario instanceof HTMLFormElement
            ? formulario.dataset.uxTablaForm || null
            : (typeof formulario === 'string' ? formulario : null);
        const nodoForm = formulario instanceof HTMLFormElement ? formulario : null;

        const anterior = enVuelo.get(regionId);

        if (anterior) {
            anterior.abort();
        }

        const control = new AbortController();
        enVuelo.set(regionId, control);
        region.setAttribute('aria-busy', 'true');

        try {
            const respuesta = await fetch(url, {
                headers: { 'Accept': 'text/html' },
                credentials: 'same-origin',
                signal: control.signal
            });

            if (!respuesta.ok) {
                window.location.assign(url);
                return;
            }

            const html = await respuesta.text();
            const doc = new DOMParser().parseFromString(html, 'text/html');
            const nueva = doc.getElementById(regionId);

            // Sin la región (sesión vencida, error del servidor): navegación normal.
            if (!nueva) {
                window.location.assign(respuesta.url || url);
                return;
            }

            region.replaceWith(nueva);

            // Filtros fuera de la región (Alumnos): se sincronizan con lo que
            // devolvió el servidor para no mostrar valores viejos.
            if (formSyncId) {
                const formaNueva = doc.getElementById(formSyncId);
                const formaActual = document.getElementById(formSyncId);

                if (formaNueva && formaActual) {
                    formaActual.replaceWith(formaNueva);
                }
            }

            try {
                window.history.replaceState(null, '', url);
            } catch (e) { /* contextos sin historial: se ignora */ }

            if (doc.title) {
                document.title = doc.title;
            }

            const actual = document.getElementById(regionId);

            if (actual) {
                actual.removeAttribute('aria-busy');
                anunciar((actual.dataset.uxRegion || 'Tabla') + ' actualizada.');
            }

            // Si el foco quedó en el aire (enlace dentro de la región), se
            // lleva a la región sin mover el desplazamiento de la página.
            if (!document.contains(document.activeElement)
                || document.activeElement === document.body) {
                const destino = document.getElementById(regionId);

                if (destino) {
                    if (!destino.hasAttribute('tabindex')) {
                        destino.setAttribute('tabindex', '-1');
                    }

                    destino.focus({ preventScroll: true });
                }
            }
        } catch (error) {
            if (error && error.name === 'AbortError') {
                return;
            }

            // Sin red u otro fallo: envío o navegación normal del navegador.
            window.location.assign(url);
            return;
        } finally {
            enVuelo.delete(regionId);
            limpiarUx(nodoForm);
        }
    };

    // Formularios de filtro marcados con data-ux-tabla (solo GET).
    document.addEventListener('submit', evento => {
        const formulario = evento.target;

        if (!(formulario instanceof HTMLFormElement)) {
            return;
        }

        const regionId = formulario.dataset.uxTabla;

        if (!regionId) {
            return;
        }

        if ((formulario.method || 'get').toLowerCase() !== 'get') {
            return;
        }

        evento.preventDefault();

        const url = new URL(formulario.action, window.location.href);
        url.search = new URLSearchParams(new FormData(formulario)).toString();
        navegar(url.toString(), regionId, formulario);
    });

    // Orden y paginación dentro de la región: solo enlaces de la misma
    // página (los de ficha, CSV u otras acciones navegan como siempre).
    document.addEventListener('click', evento => {
        if (evento.defaultPrevented || evento.button !== 0
            || evento.metaKey || evento.ctrlKey || evento.shiftKey || evento.altKey) {
            return;
        }

        const enlace = evento.target instanceof Element
            ? evento.target.closest('a[href]')
            : null;

        if (!enlace || enlace.target === '_blank' || enlace.hasAttribute('download')) {
            return;
        }

        const region = enlace.closest('[data-ux-region]');

        if (!region || !region.id) {
            return;
        }

        let destino = null;

        try {
            destino = new URL(enlace.href, window.location.href);
        } catch (e) {
            return;
        }

        if (destino.origin !== window.location.origin
            || destino.pathname !== window.location.pathname
            || destino.hash) {
            return;
        }

        evento.preventDefault();
        navegar(destino.toString(), region.id, enlace.dataset.uxTablaForm || null);
    });
})();
