import { Directive, OnDestroy } from '@angular/core';
import { SubSink } from 'subsink';

/**
 * R7: "toda suscripción manual a un Observable en Angular se registra en un SubSink. Sin
 * excepciones." Jerarquía de preferencia: signal → toSignal → async pipe → rxMethod → subscribe+SubSink.
 * Esta clase base solo se usa cuando una suscripción manual es genuinamente inevitable (WebSocket,
 * evento del DOM como Observable, integración de terceros) — no como atajo por comodidad.
 */
@Directive()
export abstract class SubscriberComponent implements OnDestroy {
  protected readonly subs = new SubSink();

  ngOnDestroy(): void {
    this.subs.unsubscribe();
  }
}
