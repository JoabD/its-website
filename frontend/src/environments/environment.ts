/**
 * Entorno de desarrollo: la API corre en localhost (perfil "https" de launchSettings.json, puerto
 * 5081) y `proxy.conf.json` reenvía `/api` hacia esa URL https (ver package.json → "start"), así
 * que basta con una ruta relativa.
 */
export const environment = {
  production: false,
  apiBaseUrl: '/api/v1',
  // SiteKey de reCAPTCHA v3: es pública por diseño de Google (viaja al navegador de cualquier
  // visitante) — el secreto real vive solo en el backend (appsettings.Development.json, git-ignored).
  recaptchaSiteKey: '6LdzlsEtAAAAAPwB_Ojm3paMQ86FL7tgTPNfabkg',
};
