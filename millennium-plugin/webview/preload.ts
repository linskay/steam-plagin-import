import { parseSnapshot, Snapshot } from '../frontend/model';

export function installStoreBadges(doc: Document, snapshot: Snapshot): () => void {
  const games = new Map(snapshot.games.filter(g => g.steamAppId && /^\d+$/.test(g.steamAppId)).map(g => [g.steamAppId!, g]));
  if (!games.size || !doc.defaultView) return () => {};
  const style = doc.createElement('style');
  style.textContent = '.egs-sync-store-badge{display:block!important;position:relative;z-index:2;background:#213b4b;color:#9dd8ff;font-size:12px;font-weight:600;line-height:1.5;padding:5px 9px;border-left:3px solid #66c0f4;box-sizing:border-box}.egs-sync-store-page{margin:12px 0;font-size:14px;padding:10px 14px}';
  doc.head.append(style);
  const win = doc.defaultView;
  function appId(href: string): string | undefined {
    try { const url = new URL(href, doc.location.href); return url.hostname === 'store.steampowered.com' ? url.pathname.match(/^\/app\/(\d+)(?:\/|$)/)?.[1] : undefined; } catch { return undefined; }
  }
  function badge(id: string) {
    const node = doc.createElement('span'); node.className = 'egs-sync-store-badge'; node.dataset.egsSteamApp = id;
    node.textContent = 'In Epic Games Library';
    node.title = 'Owned in your Epic Games library as of the last Steam Epic Sync synchronization.';
    return node;
  }
  let frame = 0;
  function update() {
    frame = 0;
    for (const link of doc.querySelectorAll<HTMLAnchorElement>('a[href*="/app/"]')) {
      const id = appId(link.href);
      if (!id || !games.has(id)) continue;
      // Only store product cards; skip navigation, recommendation text and checkout links.
      if (!link.querySelector('img') || link.closest('.game_purchase_action,.game_area_purchase_game')) continue;
      if (link.querySelector(`[data-egs-steam-app="${id}"]`)) continue;
      link.append(badge(id));
    }
    const id = appId(doc.location.href);
    if (id && games.has(id) && !doc.querySelector('[data-egs-store-page]')) {
      const target = doc.querySelector('.game_area_purchase,.rightcol.game_meta_data,.game_details');
      if (target) { const node = badge(id); node.dataset.egsStorePage = 'true'; node.classList.add('egs-sync-store-page'); target.prepend(node); }
    }
  }
  const observer = new MutationObserver(records => {
    if (records.every(r => (r.target as Element).closest?.('.egs-sync-store-badge'))) return;
    if (!frame) frame = win.requestAnimationFrame(update);
  });
  observer.observe(doc.body, {childList: true, subtree: true});
  update();
  const dispose = () => {observer.disconnect(); if (frame) win.cancelAnimationFrame(frame); style.remove(); doc.querySelectorAll('.egs-sync-store-badge').forEach(e => e.remove());};
  win.addEventListener('pagehide', dispose, {once: true});
  return dispose;
}

export default async function main() {
  if (location.hostname !== 'store.steampowered.com') return;
  const snapshot = parseSnapshot(await frontend.getSnapshotForStore());
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', () => installStoreBadges(document, snapshot), {once: true});
  else installStoreBadges(document, snapshot);
}
