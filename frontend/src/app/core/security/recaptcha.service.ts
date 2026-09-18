import { Injectable } from '@angular/core';
import { environment } from '../../../environments/environment';

declare const grecaptcha: {
  ready: (callback: () => void) => void;
  execute: (siteKey: string, options: { action: string }) => Promise<string>;
};

/**
 * reCAPTCHA v3 (protección antispam/antibots del formulario público de inscripción). Carga el
 * script oficial de Google de forma perezosa (solo cuando algo realmente lo necesita, no en cada
 * carga del sitio) y expone un único método: "dame un token para esta acción". El backend es quien
 * decide si el token/score es válido (ver SubmitApplication.cs) — el frontend nunca confía en sí
 * mismo para esto, solo ofrece una buena experiencia.
 */
@Injectable({ providedIn: 'root' })
export class RecaptchaService {
  private scriptLoadPromise: Promise<void> | null = null;

  private loadScript(): Promise<void> {
    if (typeof grecaptcha !== 'undefined') {
      return Promise.resolve();
    }

    this.scriptLoadPromise ??= new Promise<void>((resolve, reject) => {
      const script = document.createElement('script');
      script.src = `https://www.google.com/recaptcha/api.js?render=${environment.recaptchaSiteKey}`;
      script.async = true;
      script.onload = () => resolve();
      script.onerror = () => reject(new Error('No se pudo cargar reCAPTCHA. Verifica tu conexión a internet.'));
      document.head.appendChild(script);
    });

    return this.scriptLoadPromise;
  }

  async execute(action: string): Promise<string> {
    await this.loadScript();
    return new Promise<string>((resolve, reject) => {
      grecaptcha.ready(() => {
        grecaptcha.execute(environment.recaptchaSiteKey, { action }).then(resolve, reject);
      });
    });
  }
}
