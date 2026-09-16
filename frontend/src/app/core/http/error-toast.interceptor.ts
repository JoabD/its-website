import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { ToastService } from '../../shared/ui/toast/toast.service';

/** Traduce ProblemDetails (RFC 9457) a un toast legible; nunca expone detalles técnicos crudos. */
export const errorToastInterceptor: HttpInterceptorFn = (req, next) => {
  const toast = inject(ToastService);

  return next(req).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && req.url.startsWith('/api/') && error.status !== 401) {
        const problem = error.error as { detail?: string; title?: string } | null;
        toast.error(problem?.detail ?? 'Ocurrió un error al comunicarse con el servidor.');
      }

      return throwError(() => error);
    }),
  );
};
