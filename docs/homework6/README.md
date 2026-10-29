# Homework #6 — Frontend starter integration

This branch prepares the existing application for the frontend lesson. It does
not implement the participant card, owner review page or group-orders service.

Validation results and the baseline comparison: [verification.md](verification.md).

## Imported lesson files

- [Homework](../../homework-frontend.md) and [practice](../../practice-frontend.md).
- [Design exports](../../homework-design/README.md): three PNG mockups.
- `frontend/restaurant-frontend-app/homework-tests/`: practice and levels 1–3,
  including the optional Remove Guest tests. These remain outside `src/` until
  their corresponding feature is implemented.
- `src/app/models/group-orders/`, the preview data, demo authentication and
  in-memory group-orders backend under `src/app/shared/mocks/`.
- `consolidatePreOrder()` and its tests under `src/app/shared/helpers/`.

The source was the organizers' `green-and-tasty.zip`. Existing frontend files
were compared before copying; generated CSS/maps were excluded. Backend,
infrastructure, Homework #4/#5, the package version (`1.1.1`), dependencies,
`package-lock.json`, existing routes and the configured development API URL
are preserved. The archive's unrelated reservation-card test change was not
imported. The other helpers already matched the archive.

## Commands

Run from `frontend/restaurant-frontend-app`:

```powershell
npm ci
npm start
npm test -- --watch=false --browsers=ChromeHeadless
npm run test:starter -- --browsers=ChromeHeadless
npm run test:hw -- --browsers=ChromeHeadless
```

`test` still runs the existing application suite. `test:starter` checks the
imported helpers, mock backend, demo login and interceptor integration.
`test:hw` is the lesson command; copy each level's tests into `src/app/` only
when starting its implementation, as described in `homework-tests/README.md`.
Keeping future tests in `homework-tests/` avoids imports of components and
services that do not exist yet.

The route `/reservations/:reservationId/group-order` is intentionally not
registered yet: the lesson creates its target component during practice step 2.
Existing routes and their guards remain intact. After that step, the documented
demo URL is `/reservations/demo-reservation/group-order?demo`. The initializer
itself can already be tried on an existing route such as `/main?demo`.

## Development and production separation

Only Angular's `development` build replaces `homework.providers.ts` with
`homework.providers.development.ts`. The development providers also check
`isDevMode()`. They apply `?demo` before routing/authentication reads storage and
register the mock interceptor after `tokenInterceptor`. Demo reservation/group
requests stay in memory; requests for real IDs retain the existing API flow.
`SKIP_ERROR_TOAST` now also suppresses errors on guest endpoints, which is needed
for the lesson's silent background polling, without changing guest tokens or
request bodies.

Production uses the empty default providers and contains no demo-login or mock
backend implementation. `scripts/check-bundle.mjs` also rejects demo issuer,
demo email and invite-token markers if they accidentally reach an emitted bundle.
The original `BACKEND_URL` generation, production environment replacement and
placeholder check remain in place:

```powershell
$env:BACKEND_URL = 'https://example.com'
npm run build
```

Demo JWTs are unsigned development fixtures and cannot authenticate with the
real backend. Sign out of the demo account and sign in with a real account
before working with real API IDs, as required by the lesson.

No AWS commands are needed for this starter integration.
