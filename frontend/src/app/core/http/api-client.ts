import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

/**
 * En desarrollo es una ruta relativa (proxy.conf.json la reenvía a la API local); en producción
 * (build servido desde Vercel) es la URL absoluta de la API real en Azure/MonsterASP.NET — ver
 * src/environments/environment.production.ts y PROMPT-MAESTRO.md §3-bis.
 */
const API_BASE_URL = environment.apiBaseUrl;

export type QueryParams = Record<string, string | number | boolean | null | undefined>;

/**
 * Fachada tipada sobre HttpClient (spec técnico §4-D: "los componentes dependen de stores y
 * fachadas, nunca de HttpClient directamente"). Todo método exige el genérico de respuesta.
 */
@Injectable({ providedIn: 'root' })
export class ApiClient {
  private readonly http = inject(HttpClient);

  get<T>(path: string, params?: QueryParams): Observable<T> {
    return this.http.get<T>(`${API_BASE_URL}${path}`, { params: this.toHttpParams(params) });
  }

  post<T>(path: string, body: unknown): Observable<T> {
    return this.http.post<T>(`${API_BASE_URL}${path}`, body);
  }

  put<T>(path: string, body: unknown): Observable<T> {
    return this.http.put<T>(`${API_BASE_URL}${path}`, body);
  }

  delete<T>(path: string): Observable<T> {
    return this.http.delete<T>(`${API_BASE_URL}${path}`);
  }

  postForm<T>(path: string, formData: FormData): Observable<T> {
    return this.http.post<T>(`${API_BASE_URL}${path}`, formData);
  }

  private toHttpParams(params?: QueryParams): HttpParams {
    let httpParams = new HttpParams();
    if (!params) return httpParams;

    for (const [key, value] of Object.entries(params)) {
      if (value !== null && value !== undefined) {
        httpParams = httpParams.set(key, String(value));
      }
    }

    return httpParams;
  }
}
