# Build and deploy

## Toolchain

- .NET SDK 10.0.402+ (pinned in `global.json`, `rollForward: latestFeature`).
- `wasm-tools` workload: `dotnet workload install wasm-tools`, run from the repository root. The workload set is
  pinned in `global.json` (`sdk.workloadVersion`), and the SDK refuses to build any project in the repository until
  that set is installed (`dotnet workload --version` then prints it without "(not installed)").
  - Keep the set equal to the SDK version (SDK `10.0.402` -> set `10.0.402`). The set decides the version of the
    implicit `Microsoft.NET.Sdk.WebAssembly.Pack` reference, which is recorded in
    `src/game/LudumDare60.Game.Browser/packages.lock.json`; a set that lags behind the SDK resolves an older pack
    and breaks the locked restore in CI.
  - To upgrade: change `sdk.version` and `sdk.workloadVersion` together, run `dotnet workload install wasm-tools`,
    then `dotnet restore --force-evaluate` to refresh the lock files, and commit all of it.
- Node.js 22+ for JS tests (CI uses 24). `npm test` passes a glob to `node --test`, which Node 20 does not expand.
- `butler` (itch.io CLI) on `PATH`. Run `butler login` once locally. CI uses the `BUTLER_API_KEY` secret.

## Browser build

Game projects use `Sdk="Microsoft.NET.Sdk.WebAssembly"`. The root `Directory.Build.props` detects this
(`UsingMicrosoftNETSdkWebAssembly`) and applies the following:

| Setting | Value | Why |
| --- | --- | --- |
| `WasmEnableThreads` | `false` | itch.io cross-origin isolation is opt-in and not reliable in every browser or embed mode. |
| `WasmEnableSIMD` | `true` | Wasm SIMD128 is supported by every browser we target; `System.Numerics` / `Vector128` code gets faster. |
| `WasmEnableExceptionHandling` | `true` | Native Wasm EH gives smaller, faster code than the JS fallback. |
| `CompressionEnabled` | `false` | itch.io already gzips `.wasm`, `.js`, `.html` and `.css`. Pre-compressed `.br`/`.gz` copies would double the file count and would never be served. |
| `InvariantGlobalization` (Release) | `true` | No ICU data files (saves several MB). Culture-sensitive APIs must not be relied on. Only Release sets it, because any value that differs from the runtime pack default forces a native relink on every Debug build (about +10 s). Desktop projects and tests always run invariant. |
| `RunAOTCompilation` (Release) | `true` | IL is compiled to native Wasm. Much faster at runtime, slower to build. |
| `WasmStripILAfterAOT` (Release) | `true` | Drops IL bodies that AOT has already compiled. |
| `TrimMode` (Release) | `full` | Trims the whole app, not only the framework. |
| Feature switches (Release) | off | `DebuggerSupport`, `EventSourceSupport`, `StackTraceSupport`, `MetricsSupport`, `UseSystemResourceKeys` reduce size. Exception messages become resource keys in Release builds. |

### Static web assets

These settings apply to every static web assets project: the game (`Microsoft.NET.Sdk.WebAssembly`) and the
browser platform (`Microsoft.NET.Sdk.StaticWebAssets`, files under `_content/<project>/`).

| Setting | Value | Why |
| --- | --- | --- |
| `CompressionEnabled` | `false` | See above. Without it, library assets would still ship `.br` and `.gz` copies. |
| `StaticWebAssetFingerprintPattern` `*.js` | `#[.{fingerprint}]!` | The SDK fingerprints only `*.lib.module.js` by default. This pattern renames every `.js` asset to `name.<hash>.js` and adds an import map entry from the plain URL. Load JS modules by their plain URL (static `import`, `import()` or `JSHost.ImportAsync`); the `<script type="importmap">` placeholder in `index.html` (`OverrideHtmlAssetPlaceholders`) resolves it. |

Release publish:

```bash
rm -rf artifacts/publish/LudumDare60.Game.Browser
dotnet publish src/game/LudumDare60.Game.Browser -c Release
# output: artifacts/publish/LudumDare60.Game.Browser/release/wwwroot/
```

Always delete the publish folder first. `dotnet publish` does not clean it, and asset names carry a content hash,
so every publish adds new files next to the old ones. `butler push` would upload the stale files too.

Reference point, measured on 2026-10-17 with SDK 10.0.402: an empty `wasmbrowser` app published in Release with
these settings produced **9 files and 6.7 MB uncompressed** (`dotnet.native.wasm` is about 5.5 MB). AOT took
about 4 minutes. The build ran fine when served from a subdirectory and did not need cross-origin isolation.

## itch.io HTML5 requirements

Source: <https://itch.io/docs/creators/html5> (checked 2026-10-17).

- Upload a **ZIP** (no other archive formats) with **`index.html` at the root**. `butler` handles the packaging:
  push the directory itself.
- **Relative paths only.** The game runs in a subdirectory of the itch.io CDN, so a leading `/` breaks it. The
  `wasmbrowser` template uses `./_framework/...`. Keep it that way.
- **File names are case-sensitive** and must be UTF-8. Requesting a folder URL (`.../`) returns 403.
- Limits: **at most 1000 files**, **at most 240 characters per path**, **500 MB in total**, **200 MB per file**.
- Compression is served automatically: `html`, `js`, `css`, `svg`, `wasm`, `wav`, `glb` and `pck` are gzipped on
  the fly. Files ending in `.br` are served with `content-encoding: br`, and `.gz` files are detected by content.
  To use Brotli later, ship only `*.br` files and load them through `dotnet.withResourceLoader(...)`.
- The game runs inside an `iframe`. Embed modes:
  - *Embed in page*: fixed viewport size, and itch.io adds its own fullscreen button.
  - *Click to launch in fullscreen*: arbitrary size. The canvas **must** follow its container size and
    `devicePixelRatio`.
  - *Click to play* is on by default and provides the user gesture needed to start audio. Keep it on.
  - Mobile always launches fullscreen. Tick *Mobile Friendly* only if touch input works.
- **SharedArrayBuffer** is an experimental opt-in embed setting (COOP/COEP headers). We do not depend on it. This
  is why threads are off.
- Requests to other domains must use HTTPS. The build should be self-contained anyway.
- HTML5 projects can only accept donations. They cannot be sold.
- Project settings: Kind of project = **HTML**. After the first push, mark the upload as *This file will be
  played in the browser*.
- Unverified: the MIME type itch.io uses for `.wasm`. If it is not `application/wasm`, the .NET loader falls
  back from streaming instantiation to an ArrayBuffer. That still works but starts slower. Check the network
  tab after the first upload.

## Asset caching

Checked on 2026-10-17 against live HTML5 games on itch.io:

- Games are served from `https://html-classic.itch.zone/html/<id>/` through Cloudflare, with
  `cache-control: max-age` of about 2.5 million seconds (**about 29 days**), plus `etag` and `last-modified`.
- itch.io loads `index.html` with `?v=<timestamp>`, so the page itself is always fresh after an upload.
- An itch.io admin wrote (2018) that every re-upload gets new file URLs. Whether `<id>` also changes for a `butler push`
  to the same channel is **not confirmed**. To check it, push twice to the dev channel and compare the `<id>` in the
  game iframe's URL.

Files whose name carries a content hash (`_framework/*`, every `.js` through the import map) are safe either way. Asset
files under `wwwroot/assets/` are not. If `<id>` stayed the same, a returning player could get the new code with an
asset from the previous build, cached for up to a month.

What the build does about it:

- **Release** builds append `?v=<commit hash>` to every asset URL. The hash is `SourceRevisionId` from the SDK's git
  integration, written into `BuildInfo.SourceRevision` (generated in `LudumDare60.Engine.Platform.Browser`); outside a
  git checkout the build time is used instead. Cloudflare and the browser cache by full URL, so each deploy reads
  fresh files, and within one build they stay cached. A deploy re-downloads every asset, which is fine at our sizes.
  CI deploys commits only. A local Release build with uncommitted asset edits keeps the old hash.
- **Debug** builds add no version and fetch with `cache: 'no-cache'`: the browser revalidates every read with its
  ETag, so an edited asset shows up on reload without a commit.

Content-hashed asset names are possible: a `StaticWebAssetFingerprintPattern` for `*.png`, `*.ogg` and so on renames
the files (`gun.6lf3cyd0pu.png`). But the SDK writes only `.js` files into the import map, `fetch` ignores import maps
anyway, and the name mapping lands only in `*.staticwebassets.endpoints.json`, which is not shipped. Using them needs a
generated name table (see [docs/backlog.md](../backlog.md#asset-manager-follow-ups)).

## Deploying with butler

Source: <https://itch.io/docs/butler/pushing.html>.

```bash
butler push artifacts/publish/LudumDare60.Game.Browser/release/wwwroot <user>/<game>:html5 --userversion 0.1.0 --if-changed
butler status <user>/<game>:html5
```

- Channel name: `html5`. Channel names only auto-tag desktop and Android platforms. Browser play is enabled in
  the itch.io UI.
- `--dry-run` lists the files without pushing. `butler push-preview` diffs against the last build.
- Push an exact release folder. Do not rely on `--ignore` patterns.
- `BUTLER_API_KEY` is a secret. If it ever appears in a log, revoke it on the itch.io API keys page.

## Continuous deployment

Two GitHub Actions workflows live in `.github/workflows/`:

| Workflow | Runs on | Does |
| --- | --- | --- |
| `ci.yml` | pull requests, manual runs, and as the first job of `deploy.yml` | Installs `wasm-tools` (the pinned workload set is required for any build), then locked restore (`CI=true`), Debug build, `dotnet test`, `npm test`, `dotnet format --verify-no-changes`. |
| `deploy.yml` | push to `main`, tags `v*`, manual runs | Runs `ci.yml`, publishes Release AOT, checks the itch.io limits, uploads the build as the `html5` workflow artifact, then pushes it with butler. |

Where a build goes:

| Trigger | Target | itch.io project | `--userversion` | GitHub environment |
| --- | --- | --- | --- | --- |
| push to `main` | dev | `vars.ITCH_DEV_GAME` | `dev-<run number>-<short sha>` | `itch-dev` |
| tag `v1.2.0` | release | `vars.ITCH_GAME` | `1.2.0` | `itch` |
| manual run | picked in the form | as above | release: the `version` input (required) | as above |

Commits on `main` are checked by the CI job inside `deploy.yml`, not by a separate CI run. A release is a tag:

```bash
git tag v0.1.0
git push origin v0.1.0
```

Pushing a commit and a tag at once starts two deploys, one per target. Deploys to the same target are queued, never
run in parallel.

### One-time setup

1. On itch.io, create two HTML projects: the public game and a test copy that stays non-public (for example the
   game slug with `-dev`). Generate an API key at <https://itch.io/user/settings/api-keys>.
2. In the GitHub repository, *Settings > Secrets and variables > Actions > Variables*: add `ITCH_USER` (the itch.io
   user or team name in the project URL), `ITCH_GAME` and `ITCH_DEV_GAME` (the project slugs).
3. *Settings > Environments*: create `itch` and `itch-dev`, and add the API key as the `BUTLER_API_KEY` secret in
   both. Only the push job of `deploy.yml` uses these environments, so pull requests never see the key. Optionally
   add required reviewers to `itch` to approve each release by hand.
4. After the first push to each project, open its *Edit game* page and mark the `html5` upload as *This file will
   be played in the browser*.

The `plan` job fails within seconds if a variable is missing, before the long AOT build. The push job fails if
`BUTLER_API_KEY` is missing from the environment.

### Updating butler

`deploy.yml` downloads butler from `broth.itch.zone` at a pinned version and checks its SHA-256. To update, read the
latest version from <https://broth.itch.zone/butler/linux-amd64/LATEST>, then set `BUTLER_VERSION` and
`BUTLER_SHA256` (`curl -fsSL https://broth.itch.zone/butler/linux-amd64/<version>/archive/default | sha256sum`).

## Pre-release checklist

- [ ] `dotnet build -c Release`, `dotnet test` and `npm test` pass.
- [ ] Published build works when served from a subdirectory (`python3 -m http.server` one level up).
- [ ] Both renderer paths work: WebGPU, and WebGL2 with WebGPU disabled.
- [ ] Canvas resizes correctly in embed and fullscreen modes, and on high-DPI screens.
- [ ] Audio starts after the first click or keypress.
- [ ] No console errors. File count is under 1000.
- [ ] Asset requests in the network tab carry `?v=<commit hash>`.
