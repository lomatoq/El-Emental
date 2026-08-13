# M0 Bootstrap

Status: complete

## Deliverables

- Unity 6000.5.7f1 (017862109af0), URP and the minimum package set are pinned.
- Core/Simulation/Runtime/Presentation/Input/Authoring assembly boundaries and repository instructions are in place.
- Simulation clock, deterministic random helper, clean Bootstrap scene and batch test scripts cover both PowerShell and shell environments.
- GitHub Actions defines Windows/macOS EditMode + PlayMode jobs and native platform build jobs; credentials remain repository secrets.

## Gate evidence

- Final local EditMode suite: 73/73 passed.
- Final local PlayMode suite: 23/23 passed.
- Windows x64 build: 165,882,014 bytes, 0 warnings/errors; headless player smoke exited 0 after 120 ticks.
- macOS app cross-build: 290,339,930 bytes, 0 warnings/errors.
- WebGL2 build is additionally proven under M9.

The workflow is ready for hosted runner execution once Unity license secrets are configured in GitHub; the repository does not embed credentials.
