import { HttpInterceptorFn } from '@angular/common/http';
import { EnvironmentProviders, isDevMode, Provider, provideAppInitializer } from '@angular/core';
import { applyDemoLoginFromUrl } from '../mocks/auth/demo-auth';
import { groupOrdersMockInterceptor } from '../mocks/group-orders/group-orders-mock.interceptor';

// Register the initializer before routing and authentication consume localStorage.
export const homeworkProviders: (Provider | EnvironmentProviders)[] = isDevMode()
  ? [
      provideAppInitializer(() => {
        applyDemoLoginFromUrl();
      }),
    ]
  : [];
export const homeworkInterceptors: HttpInterceptorFn[] = isDevMode()
  ? [groupOrdersMockInterceptor]
  : [];
