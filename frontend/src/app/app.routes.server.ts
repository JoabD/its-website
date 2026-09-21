import { RenderMode, ServerRoute } from '@angular/ssr';

/**
 * Renderizado por ruta para el build estático (fase 3 del plan SEO — ver
 * docs/Plan-SEO-Google-Search.md). `outputMode: 'static'` (angular.json) significa que esto NO
 * levanta un servidor Node en producción: cada ruta en Prerender se convierte en un archivo HTML
 * real durante `ng build` (dist/frontend/browser/<ruta>/index.html), servido por Vercel como
 * archivo estático — así un rastreador ve contenido real sin ejecutar JavaScript.
 *
 * Prerender solo para lo que es 100% estático y no depende del backend: home, planes, contacto.
 *
 * `programas` (catalog.component.ts) e `inscripcion` (admission-form.component.ts) SÍ dependían de
 * prerenderizarse en un intento anterior, pero ambas piden datos reales a la API al iniciar
 * (`/catalog/curriculum`, `/catalog/regions`) — y `ng build` intentando esa llamada real contra
 * Azure/MonsterASP.NET se topó con un timeout real (`AbortError`, backend con cold start o
 * inalcanzable desde la máquina de build). Prerenderizarlas dejaría cada build dependiendo de que
 * el backend esté despierto y responda rápido, algo demasiado frágil para este proyecto — así que
 * se quedan en Client, exactamente como funcionaban antes de esta fase 3 (sin regresión: seguían
 * pidiendo esos mismos datos en el navegador, solo que ahora también en el primer render).
 *
 * `calendario` se deja en Client por la misma razón (además de que su contenido cambia todo el
 * tiempo — prerenderizarla horneraría datos viejos). Es la fase 8 (opcional, más grande) del plan:
 * SSR real en cada visita, que requeriría un servidor Node corriendo (outputMode: 'server'), un
 * cambio de infraestructura mayor que no se activó aquí.
 *
 * Todo `/admin/**` también queda en Client: es el panel privado, no debe generarse como HTML
 * estático (expondría estructura/contenido del panel) ni indexarse — para eso ya existe
 * `data: { noIndex: true }` en app.routes.ts y el Disallow de robots.txt.
 */
export const serverRoutes: ServerRoute[] = [
  { path: '', renderMode: RenderMode.Prerender },
  { path: 'planes', renderMode: RenderMode.Prerender },
  { path: 'contacto', renderMode: RenderMode.Prerender },
  { path: 'programas', renderMode: RenderMode.Client },
  { path: 'inscripcion', renderMode: RenderMode.Client },
  { path: 'calendario', renderMode: RenderMode.Client },
  { path: 'admin/**', renderMode: RenderMode.Client },
  { path: '**', renderMode: RenderMode.Client },
];
