import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import ts from 'typescript';
import {JSDOM} from 'jsdom';

const uri = source => 'data:text/javascript;base64,' + Buffer.from(source).toString('base64');
async function compile(path, replacements = []) {
  let code = ts.transpileModule(await fs.readFile(new URL(path, import.meta.url), 'utf8'), {compilerOptions: {target: ts.ScriptTarget.ES2020, module: ts.ModuleKind.ES2020, jsx: ts.JsxEmit.ReactJSX}}).outputText;
  for (const [from, to] of replacements) code = code.replaceAll(from, to);
  return uri(code);
}
const modelUrl = await compile('../frontend/model.ts');
const model = await import(modelUrl);
const react = uri('export const useRef=()=>({current:null}); export const useEffect=()=>{};export const useState=v=>[v,()=>{}];');
const jsx = uri('export const jsx=()=>null;export const jsxs=()=>null;');
const storeUrl = await compile('../webview/preload.ts', [["'../frontend/model'",JSON.stringify(modelUrl)]]);
const store = await import(storeUrl);

const snapshot = {schemaVersion:1,steamAccountId:'123',syncedAt:'2026-11-15',games:[
  {appId:'3000000001',steamAppId:'1206060',name:'Happy Game',installed:true,description:'<script>window.injected=true</script>',developer:'Test',publisher:'Test'},
  {appId:'3000000002',name:'Remote Game',installed:false,description:'Remote description',developer:'',publisher:''}
]};
assert.throws(()=>model.parseSnapshot(JSON.stringify(snapshot),'456'), /another Steam account/);
assert.equal(model.parseSnapshot({type:'string',value:JSON.stringify(snapshot)},'123').games.length,2,'Millennium store FFI string envelope is decoded');
assert.equal(model.findGame(snapshot.games, '1', 'Happy Game'),undefined,'native games with the same title are not marked');
assert.equal(model.findGame([...snapshot.games,snapshot.games[0]],undefined,'Happy Game'),undefined,'ambiguous title is rejected');

const page = new JSDOM('<html><head></head><body><a href="https://store.steampowered.com/app/1206060/Happy_Game/"><img></a><a href="https://store.steampowered.com/app/42/Other/"><img></a><div class="game_area_purchase"></div></body></html>',{url:'https://store.steampowered.com/app/1206060/Happy_Game/',pretendToBeVisual:true});
globalThis.MutationObserver = page.window.MutationObserver;
const cleanup = store.installStoreBadges(page.window.document,snapshot);
assert.equal(page.window.document.querySelectorAll('.egs-sync-store-badge').length,2);
assert.equal(page.window.document.querySelector('a[href*="/42/"] .egs-sync-store-badge'),null);
page.window.document.body.append(page.window.document.createElement('div'));
await new Promise(resolve=>setTimeout(resolve,50));
assert.equal(page.window.document.querySelectorAll('.egs-sync-store-badge').length,2,'mutations do not duplicate badges');
cleanup();
assert.equal(page.window.document.querySelector('.egs-sync-store-badge'),null);
page.window.close();

// Run the real library initializer once against a mock local snapshot and native DOM.
const library = new JSDOM('<html><head></head><body><span class="_2SXJM0PeFEi3gbC7V3S5pE" data-appid="3000000001">Happy Game</span><span class="_2SXJM0PeFEi3gbC7V3S5pE" data-appid="3000000002">Remote Game</span><span class="_2SXJM0PeFEi3gbC7V3S5pE" data-appid="42">Happy Game</span><main class="_3fp5y6tdU77MdrCQZt_aS9" data-appid="3000000001"><div class="_2-ns_GqXzymHk0jsXBBWJ1"></div></main></body></html>',{pretendToBeVisual:true});
globalThis.MutationObserver = library.window.MutationObserver;
globalThis.SteamClient = {WebChat:{GetCurrentUserAccountID:async()=>123}};
globalThis.backend = {getSnapshot:async()=>JSON.stringify(snapshot)};
// An SDK stub allows initialization without starting Steam; production uses Millennium's SDK.
const testSdk = uri('export const definePlugin=f=>f;export const findClass=()=>undefined;export const routerHook={registerForRouterSetup(cb){cb();},addGlobalComponent(){},removeGlobalComponent(){}};');
const actualLibrary = await import(await compile('../frontend/index.tsx', [["'./model'",JSON.stringify(modelUrl)],["'millennium'",JSON.stringify(testSdk)],["'react'",JSON.stringify(react)],['"react/jsx-runtime"',JSON.stringify(jsx)]]));
const plugin = await actualLibrary.default();
await actualLibrary.getSnapshotForStore();
const dispose = actualLibrary.installLibraryView(library.window.document);
assert.equal(library.window.document.querySelectorAll('.egs-sync-installed').length,1);
assert.equal(library.window.document.querySelectorAll('.egs-sync-uninstalled').length,1);
assert.equal(library.window.getComputedStyle(library.window.document.querySelector('[data-appid="3000000002"]')).color,'rgb(163, 170, 185)','uninstalled Epic game uses native Steam gray');
assert.equal(library.window.document.querySelector('[data-appid="42"]').classList.contains('egs-sync-installed'),false);
assert.match(library.window.document.querySelector('[data-egs-sync="description"]').textContent, /<script>/);
assert.equal(library.window.document.querySelector('[data-egs-sync="description"] script'),null,'Epic descriptions are rendered as text, never HTML');
library.window.document.querySelector('main').setAttribute('data-appid','3000000002');
library.window.document.querySelector('main').append(library.window.document.createElement('div'));
await new Promise(resolve=>setTimeout(resolve,50));
assert.match(library.window.document.querySelector('[data-egs-sync="description"]').textContent,/Remote description/);
dispose(); plugin.onDismount();
assert.equal(library.window.document.querySelector('.egs-sync-installed'),null);
assert.equal(library.window.document.querySelector('.egs-sync-uninstalled'),null);
library.window.close();
console.log('PASS UI: account isolation, installed-only blue text, descriptions, safe text rendering, exact store badges, no duplicate badges, route changes and cleanup.');
