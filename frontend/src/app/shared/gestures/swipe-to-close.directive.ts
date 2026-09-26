import { Directive, ElementRef, OnDestroy, OnInit, inject, output } from '@angular/core';
import { Gesture, createGesture } from '@ionic/angular';

/**
 * Cierre por deslizamiento (swipe) para los drawers/paneles laterales (pedido del cliente, 2026-09:
 * "agregar Ionic para darle más versatilidad móvil" — alcance acordado: piezas puntuales, sin tocar
 * el sistema de diseño propio). Usa `createGesture`, la utilidad de gestos de Ionic (la misma que
 * usan sus propios componentes como ion-modal) directamente sobre el `<aside>` de cada drawer — no
 * depende de `<ion-modal>` ni cambia su estructura o estilo visual, solo agrega el gesto nativo de
 * "arrastrar para cerrar" en pantallas angostas.
 *
 * Solo se activa por debajo de 1024px (breakpoint "lg" de Tailwind, que ya usan estos mismos
 * drawers): en escritorio el panel es angosto y queda a la derecha — deslizarlo con el dedo no es
 * el gesto esperado ahí, y se sigue cerrando con clic fuera o Esc, como ya funcionaba.
 *
 * Uso: agregar el atributo `shkSwipeToClose` (y escuchar `(swipeClose)`) en el mismo `<aside>` que
 * ya tiene la referencia `#panel` en cada drawer — ver add-student-drawer.component.ts y hermanos.
 */
@Directive({
  selector: '[shkSwipeToClose]',
  standalone: true,
})
export class SwipeToCloseDirective implements OnInit, OnDestroy {
  private readonly elementRef = inject(ElementRef<HTMLElement>);
  private gesture?: Gesture;

  readonly swipeClose = output<void>();

  ngOnInit(): void {
    if (typeof window === 'undefined' || !window.matchMedia('(max-width: 1023px)').matches) {
      return;
    }

    const host = this.elementRef.nativeElement;

    this.gesture = createGesture({
      el: host,
      gestureName: 'shk-drawer-swipe-close',
      direction: 'x',
      threshold: 10,
      onStart: () => {
        host.style.transition = 'none';
      },
      onMove: (detail) => {
        host.style.transform = `translateX(${Math.max(0, detail.deltaX)}px)`;
      },
      onEnd: (detail) => {
        host.style.transition = 'transform 0.2s ease-out';
        const delta = Math.max(0, detail.deltaX);
        const pastThreshold = delta > host.clientWidth * 0.35 || detail.velocityX > 0.5;

        if (pastThreshold) {
          host.style.transform = 'translateX(100%)';
          this.swipeClose.emit();
        } else {
          host.style.transform = 'translateX(0)';
        }
      },
    });

    this.gesture.enable(true);
  }

  ngOnDestroy(): void {
    this.gesture?.destroy();
  }
}
