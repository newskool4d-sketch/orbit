const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
const app = fs.readFileSync(path.join(__dirname, '../Resources/app.js'), 'utf8');
function fixture() {
  const nodes = new Map(), calls = [], listeners = {};
  const context = vm.createContext({
    window: { chrome: { webview: { postMessage() {}, addEventListener() {} } } },
    document: { documentElement: { dataset: {} }, addEventListener(n, f) { listeners[n] = f; } },
    $: s => { if (!nodes.has(s)) nodes.set(s, { innerHTML: '', insertAdjacentHTML() {} }); return nodes.get(s); },
    state: { platform: 'windows', events: [], tasks: [], files: [], agents: [], statuses: [], settings: { googleConnected: true }, refreshing: false },
    busyTasks: new Set(), showAllTasks: false, status: () => ({ state: 'connected' }),
    warning: () => '', empty: () => '', dayKey: () => '2026-09-07', icon: () => '', relative: () => '',
    labels: { claude: 'Claude Code' }, act: (action, args) => calls.push({ action, args }),
  });
  vm.runInContext(fs.readFileSync(path.join(__dirname, '../Resources/platform.js'), 'utf8'), context);
  for (const prefix of ['const esc=', 'function agentCard(', "document.addEventListener('click'"])
    vm.runInContext(app.split('\n').find(l => l.startsWith(prefix)), context);
  vm.runInContext(app.slice(app.indexOf('function renderToday('), app.indexOf('function renderCalendar(')), context);
  return { context, nodes, calls, listeners };
}
test('pending and unknown server mutations remain locked without a local request', () => {
  const { context, nodes } = fixture();
  for (const mutationState of ['pending', 'unknown']) {
    context.state.tasks = [{ id: 'fixture', title: 'fixture', completed: false, mutationState }];
    vm.runInContext('renderToday([])', context);
    assert.match(nodes.get('#task-section').innerHTML.match(/<input\b[^>]*>/)[0], /\bdisabled\b/);
  }
});
test('new-project behavior is visible without hovering', () => {
  const { context } = fixture();
  const html = vm.runInContext("agentCard({id:'fixture',provider:'claude',title:'fixture',openMode:'projectNew'})", context);
  assert.match(html.replace(/<[^>]*>/g, ''), /새 작업 열기/);
});
test('unavailable agent clicks never reach the native bridge', () => {
  const { listeners, calls } = fixture();
  listeners.click({ target: { closest: () => ({ disabled: true, dataset: { agent: 'fixture' }, getAttribute: () => 'true' }) } });
  assert.equal(calls.length, 0);
});
