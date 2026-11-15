import { definePlugin, findClass, routerHook } from 'millennium';
import { useEffect, useRef, useState } from 'react';
import { findGame, parseSnapshot, Snapshot } from './model';

let snapshot: Snapshot = {schemaVersion: 1, steamAccountId: '', syncedAt: '', games: []};
let stopped = false;
const disposers = new Set<() => void>();
let ready: Promise<void> = Promise.resolve();
let loadError = '';
export function getModuleStatus() { return {error: loadError, games: snapshot.games.length, stopped}; }

/** @ffi */
export function getSnapshotForStore(): string {
  // Starlight 1.1.3 expects the resolved FFI return type; Millennium awaits the returned promise.
  return ready.then(() => JSON.stringify(snapshot)) as unknown as string;
}

function appIdFromElement(element: Element): string | undefined {
  for (let node: Element | null = element; node; node = node.parentElement) {
    const direct = node.getAttribute('data-appid');
    if (direct && /^\d+$/.test(direct)) return direct;
    const key = Object.keys(node).find(k => k.startsWith('__reactFiber$'));
    let fiber = key ? (node as any)[key] : null;
    for (let depth = 0; fiber && depth < 12; depth++, fiber = fiber.return) {
      const p = fiber.memoizedProps;
      const id = p?.overview?.appid ?? p?.appOverview?.appid ?? p?.app?.appid ?? p?.appid ?? p?.appID ?? p?.nAppID;
      if (id !== undefined && /^\d+$/.test(String(id))) return String(id);
    }
  }
  return undefined;
}

export function installLibraryView(doc: Document): () => void {
  const win = doc.defaultView;
  if (!win) return () => {};
  const classSelector = (name: string, fallback: string) => '.' + (findClass(name) || fallback);
  const names = classSelector('GameListEntryName', '_2SXJM0PeFEi3gbC7V3S5pE') + ', ' + classSelector('GameListEntryContainer', '_1vO6BoiVslZgs1kqDGdUs8') + ' > span';
  const details = classSelector('AppDetailsOverviewPanel', '_1_cYNJSvS6IXs9vLTEYjy5') + ', ' + classSelector('AppDetailsRoot', '_3fp5y6tdU77MdrCQZt_aS9');
  const detailsContainer = classSelector('AppDetailsContainer', '_2-ns_GqXzymHk0jsXBBWJ1');
  const style = doc.createElement('style');
  style.dataset.egsSync = 'style';
  style.textContent = '.egs-sync-installed{color:#66c0f4!important}.egs-sync-uninstalled{color:#a3aab9!important}.egs-sync-uninstalled:hover{color:#c8d0dc!important}.Selected .egs-sync-uninstalled{color:#c3c9d8!important}@media(prefers-contrast:more){.egs-sync-uninstalled{color:#cbced8!important}}.egs-sync-description{margin:16px 24px;padding:20px;background:rgba(0,0,0,.18);border-left:3px solid #66c0f4;color:#d6d7d8;line-height:1.6}.egs-sync-description h2{font-size:18px;margin:0 0 8px}.egs-sync-description p{white-space:pre-wrap;margin:8px 0}.egs-sync-meta{color:#a4abb5;font-size:12px}';
  doc.head.append(style);
  let frame = 0;
  function update() {
    frame = 0;
    for (const node of doc.querySelectorAll<HTMLElement>(names)) {
      const game = findGame(snapshot.games, appIdFromElement(node));
      node.classList.toggle('egs-sync-installed', !!game?.installed);
      node.classList.toggle('egs-sync-uninstalled', !!game && !game.installed);
    }
    const root = doc.querySelector(details);
    const game = root ? findGame(snapshot.games, appIdFromElement(root)) : undefined;
    let panel = doc.querySelector<HTMLElement>('[data-egs-sync="description"]');
    if (!root || !game) { panel?.remove(); return; }
    if (panel?.dataset.appId === game.appId) return;
    panel?.remove();
    panel = doc.createElement('section');
    panel.className = 'egs-sync-description';
    panel.dataset.egsSync = 'description';
    panel.dataset.appId = game.appId;
    const heading = doc.createElement('h2'); heading.textContent = 'Epic Games';
    const status = doc.createElement('div'); status.className = 'egs-sync-meta';
    status.textContent = game.installed ? 'Installed on this PC · In Epic Games Library' : 'Not installed · In Epic Games Library';
    const text = doc.createElement('p'); text.textContent = game.description || 'Epic Games did not provide a description for this game.';
    panel.append(heading, status, text);
    if (game.description && game.descriptionSource) { const source = doc.createElement('div'); source.className = 'egs-sync-meta'; source.textContent = 'Description: ' + game.descriptionSource; panel.append(source); }
    const credits = [game.developer && 'Developer: ' + game.developer, game.publisher && 'Publisher: ' + game.publisher].filter(Boolean);
    if (credits.length) { const meta = doc.createElement('div'); meta.className = 'egs-sync-meta'; meta.textContent = credits.join(' · '); panel.append(meta); }
    const target = root.querySelector(detailsContainer) || root;
    target.prepend(panel);
  }
  const observer = new MutationObserver(records => {
    if (records.every(r => (r.target as Element).closest?.('[data-egs-sync]'))) return;
    if (!frame) frame = win.requestAnimationFrame(update);
  });
  observer.observe(doc.body, {childList: true, subtree: true, characterData: true});
  update();
  return () => {
    observer.disconnect(); if (frame) win.cancelAnimationFrame(frame); style.remove();
    doc.querySelectorAll('.egs-sync-installed').forEach(e => e.classList.remove('egs-sync-installed'));
    doc.querySelectorAll('.egs-sync-uninstalled').forEach(e => e.classList.remove('egs-sync-uninstalled'));
    doc.querySelector('[data-egs-sync="description"]')?.remove();
  };
}

function LibraryBridge() {
  const anchor = useRef<HTMLSpanElement>(null);
  useEffect(() => {
    if (!anchor.current || stopped) return undefined;
    const dispose = installLibraryView(anchor.current.ownerDocument);
    disposers.add(dispose);
    return () => { dispose(); disposers.delete(dispose); };
  }, []);
  return <span ref={anchor} hidden data-egs-sync="bridge" />;
}

function SettingsContent() {
  const [loaded, setLoaded] = useState(false);
  const [settings, setSettings] = useState({LaunchWithSteam: true, SyncOnSteamStart: false, CollectionName: 'Есть в EGS'});
  const [message, setMessage] = useState('');
  const [saving, setSaving] = useState(false);
  useEffect(() => {
    let active = true;
    Promise.all([ready, backend.getSettings()]).then(([,raw]) => {
      if (!active) return;
      const value = typeof raw === 'string' ? JSON.parse(raw) : raw;
      setSettings(value); setLoaded(true);
    }).catch(e => {if (active) setMessage(String(e));});
    return () => {active = false;};
  }, []);
  async function save() {
    setSaving(true);
    try {
      await backend.saveSettings(settings.LaunchWithSteam, settings.SyncOnSteamStart, settings.CollectionName);
      setMessage('Сохранено. Название коллекции применяется при следующей синхронизации.');
    } catch (e) {setMessage(String(e));}
    finally {setSaving(false);}
  }
  return <div style={{padding: 16}}><h2>Steam Epic Sync</h2>
    <p>{!loaded ? 'Загрузка…' : loadError || `${snapshot.games.length} игр · установлено ${snapshot.games.filter(g => g.installed).length}`}</p>
    <label style={{display: 'block', margin: '14px 0'}}><input type="checkbox" checked={settings.LaunchWithSteam} onChange={e => setSettings({...settings, LaunchWithSteam: e.target.checked})} /> Запуск вместе со Steam</label>
    <label style={{display: 'block', margin: '14px 0'}}><input type="checkbox" checked={settings.SyncOnSteamStart} onChange={e => setSettings({...settings, SyncOnSteamStart: e.target.checked})} /> Синхронизация при запуске Steam</label>
    <p style={{fontSize: 12, opacity: .75}}>Для безопасной записи библиотеки автосинхронизация один раз перезапускает Steam. При обычном выходе из Steam утилита и её фоновые процессы завершаются.</p>
    <label>Название коллекции<input type="text" maxLength={80} value={settings.CollectionName} onChange={e => setSettings({...settings, CollectionName: e.target.value})} style={{display: 'block', width: '100%', boxSizing: 'border-box', padding: 9, margin: '8px 0 14px'}} /></label>
    <button disabled={!loaded || saving} onClick={save} style={{padding: '9px 16px'}}>Сохранить настройки</button><p>{message}</p>
    <p>Последняя синхронизация: {snapshot.syncedAt || 'не выполнялась'}</p><p style={{fontSize: 12, opacity: .75}}>Описания и статус установки обновляются при синхронизации. Фонового сканирования диска и запросов к Epic нет.</p>
  </div>;
}

export default definePlugin(() => {
  // Return immediately: Millennium sends its frontend-loaded handshake only after this callback returns.
  // Awaiting a backend RPC here can deadlock startup.
  ready = (async () => {
    try {
      await new Promise<void>(resolve => { void routerHook.registerForRouterSetup(resolve); });
      const accountId = String(await SteamClient.WebChat.GetCurrentUserAccountID());
      snapshot = parseSnapshot(await backend.getSnapshot(accountId), accountId);
      if (!stopped) routerHook.addGlobalComponent('steam-epic-sync', LibraryBridge);
    } catch (e) { loadError = String(e); console.error('[SteamEpicSync]', e); }
  })();
  return {
    title: 'Steam Epic Sync',
    icon: <span>EGS</span>,
    content: <SettingsContent />,
    onDismount() { stopped = true; routerHook.removeGlobalComponent('steam-epic-sync'); disposers.forEach(f => f()); disposers.clear(); snapshot = {...snapshot, games: []}; }
  };
});
