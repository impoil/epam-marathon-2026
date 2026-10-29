import { HttpInterceptorFn } from '@angular/common/http';
import { EnvironmentProviders, Provider } from '@angular/core';

// Safe default for production and tests. Angular replaces this file only in development.
export const homeworkProviders: (Provider | EnvironmentProviders)[] = [];
export const homeworkInterceptors: HttpInterceptorFn[] = [];
