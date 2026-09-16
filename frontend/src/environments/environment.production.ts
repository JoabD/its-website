/**
 * Entorno de producción (build servido desde Vercel): no hay backend en el mismo origen, así que la
 * URL de la API debe ser absoluta — la del App Service de Azure o de MonsterASP.NET donde se publicó
 * Shekinah.Api (ver PROMPT-MAESTRO.md §3-bis). Cambia este valor por tu dominio real antes de
 * publicar; no hay ningún secreto aquí, es información pública (la URL de tu propia API).
 */
export const environment = {
  production: true,
  apiBaseUrl: 'https://shekinah-its-api.azurewebsites.net/api/v1',
};
