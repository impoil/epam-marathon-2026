# Starter verification

Verified on 2026-10-29 against `main` commit `04c7998`.

| Check | Result |
| --- | --- |
| TypeScript application (`tsconfig.app.json --noEmit`) | Passed |
| TypeScript specs (`tsconfig.spec.json --noEmit`) | Passed |
| Development build | Passed; demo markers present |
| Production build, `BACKEND_URL=https://example.com` | Passed; initial bundle 789.36 kB |
| Production bundle checker | Passed; no placeholder or demo markers |
| `test:starter`, ChromeHeadless | **49 passed** |
| `test:hw`, ChromeHeadless, before activating future UI tests | **26 passed** |
| Full `test` script on the feature branch | **221 passed, 47 failed** |
| Full test suite on an isolated copy of clean `main` | **195 passed, 47 failed** |
| Comparison of failing test names | All 47 names match; no new failures |
| `git diff --check` and staged diff check | Passed |

The full suite's existing failures include missing test providers/required
inputs in legacy component specs. They were reproduced on an isolated source
snapshot of `main` using the same dependencies and Chrome. Existing tests were
preserved; fixing them is outside this starter integration.

The production build retains the existing bundle/style budget and CommonJS
warnings. Budgets and dependency versions were not changed.

The environment had Node.js 24.22.1 and Chrome Headless 155, but no npm binary.
The existing bundled pnpm 11.25.0 executed the scripts from `package.json` with
`--config.verify-deps-before-run=false`. Dependencies were prepared from the
existing npm lockfile through a temporary pnpm import; no Windows tools were
installed. The npm lockfile remains byte-for-byte unchanged, and the temporary
pnpm lockfile is not part of this branch. Production prebuild, Angular build
and postbuild commands were also run directly with Node.

Preservation checks confirmed:

- All 28 imported lesson files, including the PNGs, match the organizers'
  archive byte for byte.
- No existing tracked files were deleted.
- Backend, infrastructure, Homework #4/#5 files, existing routes, production
  environment generation, package metadata/version/dependencies and original
  npm scripts are unchanged.
- The existing development backend URL is preserved.
- The staged changes contain no dependencies, build output, generated CSS/maps,
  production environment file, credentials or Terraform state.
- Development JavaScript contains the demo issuer, email and invite token;
  production JavaScript contains none of these markers.

No deployment or AWS commands were executed.
