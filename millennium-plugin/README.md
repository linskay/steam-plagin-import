# Steam Epic Sync UI

Millennium 3.6.3+ module for the Windows Steam client.

- Installed Epic titles appear in blue in Steam's sidebar.
- Epic descriptions, developer and publisher appear on imported game pages.
- Matching Steam Store product cards and purchase pages show **In Epic Games Library**.
- Epic authentication, inventory fetch, install detection and Steam catalog matching happen **only during synchronization** in `SteamEpicSync.exe`.
- The module reads one local snapshot on Steam startup. It uses DOM rendering events to decorate visible content, with no timers, disk scans or store/Epic API polling.
- A downloaded or removed game changes status after the next synchronization and Steam restart.
- Store matching accepts a unique normalized exact title, rejects ambiguous results and never changes Steam ownership or checkout. Unmatched titles receive no store badge.
- Descriptions are inserted as text, not HTML. Snapshots contain no Epic credentials or installation paths and are separated by Steam account ID.

## Build and test

Node.js 24:

```powershell
npm ci
npm run build
node node_modules/typescript/bin/tsc --noEmit
node tests/ui-tests.mjs
```

Output: `dist/com.linskay.steamepicsync.star` (about 20 KB; Millennium runtime is a separate dependency).

## Install

Completely close Steam, then:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\install.ps1 -InstallRuntime
```

The installer downloads the official pinned Millennium release only if needed, verifies its SHA-256, installs this local module and preserves other enabled plugins. It refuses to overwrite an unrelated `wsock32.dll`. Existing Millennium configuration is backed up.

Keep the `millennium-plugin` directory next to `SteamEpicSync.exe`; future synchronizations deploy the bundled module and save `Steam/config/SteamEpicSync/<accountID>.json`. Start Steam after synchronization. Disabling the module in Millennium removes its UI effects; the imported shortcuts and covers continue to work.

The module depends on Steam's private UI structure. If Steam changes its interface, the library selectors may need updating. It does not alter Steam's installed/owned app flags.

Sources for the integration API: [official template](https://github.com/SteamClientHomebrew/PluginTemplate), [Millennium SDK](https://github.com/SteamClientHomebrew/Millennium/tree/v3.6.3/src/typescript/sdk).

Неустановленные игры используют стандартный серый цвет Steam (#a3aab9), включая варианты для наведения, выделения и повышенной контрастности. Если Epic возвращает название вместо описания, при точном совпадении загружается текст из Steam Store; источник указан в блоке описания.
