import { Directive, ElementRef, OnDestroy, OnInit, inject, output } from '@angular/core';
import { Gesture, createGesture } from '@ionic/angular';

/**
 * Detecta un swipe horizontal (izquierda/derecha) sobre el elemento host y emite `swipeLeft` /
 * `swipeRight` — pensado para el carrusel de la galería del home (único uso actual, ver
 * home.component.ts), que hoy solo se controla con las flechas y los puntos (sin respuesta a
 * deslizar con el dedo, el gesto más natural en un celular al ver una galería).
 *
 * A diferencia de SwipeToCloseDirective (drawers de admin, con "arrastre en vivo" tipo bottom-sheet),
 * este directive NO mueve nada visualmente durante el gesto: el carrusel ya anima el cambio de slide
 * con un crossfade (opacity) vía CSS — un arrastre en vivo (translateX) se vería roto contra ese
 * crossfade. Solo detecta la intención del gesto; la lógica existente (nextSlide()/prevSlide()) y su
 * transición CSS hacen el resto, igual que ya pasa al hacer click en las flechas.
 *
 * Igual que en los drawers, solo se activa por debajo de 1024px (breakpoint `lg` de Tailwind) para
 * no introducir ningún comportamiento nuevo en desktop.
 */
@Directive({
  selector: '[shkSwipeHorizontal]',
  standalone: true,
})
export class SwipeHorizontalDirective implements OnInit, OnDestroy {
  private readonly elementRef = inject(ElementRef<HTMLElement>);
  private gesture?: Gesture;

  readonly swipeLeft = output<void>();
  readonly swipeRight = output<void>();

  ngOnInit(): void {
    if (typeof window === 'undefined' || !window.matchMedia('(max-width: 1023px)').matches) {
      return;
    }

    this.gesture = createGesture({
      el: this.elementRef.nativeElement,
      gestureName: 'shk-swipe-horizontal',
      direction: 'x',
      threshold: 10,
      onEnd: (detail) => {
        const pastThreshold = Math.abs(detail.deltaX) > 40 || Math.abs(detail.velocityX) > 0.3;
        if (!pastThreshold) return;

        if (detail.deltaX < 0) {
          this.swipeLeft.emit();
        } else {
          this.swipeRight.emit();
        }
      },
    });

    this.gesture.enable(true);
  }

  ngOnDestroy(): void {
    this.gesture?.destroy();
  }
}
