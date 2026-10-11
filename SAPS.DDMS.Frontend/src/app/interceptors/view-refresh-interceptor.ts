import { HttpInterceptorFn } from '@angular/common/http';
import { ApplicationRef, inject } from '@angular/core';
import { finalize } from 'rxjs';

/**
 * Angular 21 runs this application without Zone.js. HTTP callbacks therefore
 * need to schedule a render explicitly when a component stores response data
 * in ordinary class properties rather than signals.
 */
export const viewRefreshInterceptor: HttpInterceptorFn = (request, next) => {
  const applicationRef = inject(ApplicationRef);

  return next(request).pipe(
    finalize(() => {
      queueMicrotask(() => applicationRef.tick());
    })
  );
};
