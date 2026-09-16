/**
 * Entorno de desarrollo: la API corre en localhost (perfil "https" de launchSettings.json, puerto
 * 5081) y `proxy.conf.json` reenvía `/api` hacia esa URL https (ver package.json → "start"), así
 * que basta con una ruta relativa.
 */
export const environment = {
  production: false,
  apiBaseUrl: '/api/v1',
};
