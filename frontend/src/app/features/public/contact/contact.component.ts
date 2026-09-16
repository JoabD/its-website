import { Component } from '@angular/core';
import { CardComponent } from '../../../shared/ui/card/card.component';

@Component({
  selector: 'shk-contact',
  imports: [CardComponent],
  template: `
    <section class="bg-[var(--shk-color-primary)] py-16 text-center text-white md:py-20">
      <div class="mx-auto max-w-2xl px-4">
        <p class="text-sm font-bold tracking-widest text-[var(--shk-color-accent)]">CONTACTO</p>
        <h1 class="mt-3 font-[var(--shk-font-heading)] text-4xl font-extrabold">Hablemos</h1>
        <p class="mt-4 text-slate-200">Escríbenos o llámanos, con gusto resolvemos tus dudas sobre el instituto.</p>
      </div>
    </section>

    <section class="mx-auto max-w-3xl px-4 py-16">
      <shk-card>
        <dl class="grid gap-6 sm:grid-cols-3">
          <div>
            <dt class="text-xs font-bold uppercase tracking-wide text-[var(--shk-color-accent-dark)]">Correo</dt>
            <dd class="mt-1 text-sm text-slate-700">contacto&#64;its-shekinah.edu.mx</dd>
          </div>
          <div>
            <dt class="text-xs font-bold uppercase tracking-wide text-[var(--shk-color-accent-dark)]">Teléfono</dt>
            <dd class="mt-1 text-sm text-slate-700">(55) 0000 0000</dd>
          </div>
          <div>
            <dt class="text-xs font-bold uppercase tracking-wide text-[var(--shk-color-accent-dark)]">Asociación</dt>
            <dd class="mt-1 text-sm text-slate-700">Instituto Comunidad Apostólica Pentecostal (ICAP) A.R.</dd>
          </div>
        </dl>
      </shk-card>
    </section>
  `,
})
export class ContactComponent {}
