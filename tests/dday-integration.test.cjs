const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const { spawnSync } = require('node:child_process');
const path = require('node:path');
const source = fs.readFileSync(path.join(__dirname, '../Resources/app.js'), 'utf8');
const html = fs.readFileSync(path.join(__dirname, '../Resources/index.html'), 'utf8');

// DOM/bridge test doubles: these execute the actual application script, not a browser layout engine.
function fixture() {
  const nodes = new Map(), listeners = {}, calls = [], timers = new Map();
  let timerId = 0;
  const document = {
    hidden: false, activeElement: null, body: { dataset: {} },
    addEventListener(name, fn) { (listeners[name] ||= []).push(fn); },
    querySelector: selector => get(selector), querySelectorAll: () => [],
  };
  function get(selector) {
    if (!nodes.has(selector)) nodes.set(selector, {
      id: selector.slice(1), hidden: false, value: '', checked: false, disabled: false,
      dataset: {}, innerHTML: '', textContent: '', style: {}, isConnected: true, inert: false,
      classList: { toggle() {} }, listeners: {}, attrs: {},
      setAttribute(key, value) { this.attrs[key] = value; }, getAttribute(key) { return this.attrs[key]; },
      addEventListener(name, fn) { this.listeners[name] = fn; },
      focus() { document.activeElement = this; }, matches: () => false,
      insertAdjacentHTML(_, text) { this.innerHTML += text; },
      querySelectorAll: () => [],
      reset() { get('#dday-title').value = ''; get('#dday-date').value = ''; get('#dday-pinned').checked = false; },
    });
    return nodes.get(selector);
  }
  for (const match of html.matchAll(/id="([^"]+)"/g)) get('#' + match[1]);
  get('#dday-manager').hidden = true;
  const context = vm.createContext({
    document, Date, console,
    setTimeout(fn, delay) { const id = ++timerId; timers.set(id, { fn, delay }); return id; },
    clearTimeout(id) { timers.delete(id); },
    window: { addEventListener() {}, orbitPlatform: {
      name: 'windows', canSend: () => true, post: message => calls.push(message),
      shortcut: () => 'Ctrl K', launchAtLoginSupported: () => false, agentHelp: () => '',
      agentActionLabel: () => '', hasMeeting: () => false,
    } },
  });
  vm.runInContext(source, context);
  const snapshot = overrides => context.window.orbit.receive({ kind: 'snapshot', files: [], agents: [],
    events: [], tasks: [], statuses: [], settings: {}, capabilities: { dday: true },
    theme: 'moss', ddays: [], ...overrides });
  snapshot();
  const submit = () => get('#dday-form').listeners.submit({ preventDefault() {} });
  return { context, get, document, calls, listeners, snapshot, submit, timers };
}
function item(id, extra = {}) {
  return { id, title: '합성 시험 날짜', targetDate: '2030-01-02', pinned: false, archived: false,
    createdAt: '2026-01-01T00:00:00Z', updatedAt: '2026-01-01T00:00:00Z', ...extra };
}

test('today renders 0/1/3/4 items without Google, excludes archived, and escapes titles', () => {
  const f = fixture();
  for (const count of [0, 1, 3, 4]) {
    f.snapshot({ ddays: Array.from({ length: count }, (_, i) => item(String(i))) });
    assert.equal((f.get('#dday-section').innerHTML.match(/class="dday-card /g) || []).length, Math.min(3, count));
  }
  f.snapshot({ ddays: [item('a', { title: '<img onerror="x"> & 긴 제목' }), item('b', { archived: true })] });
  assert.match(f.get('#dday-section').innerHTML, /&lt;img/);
  assert.doesNotMatch(f.get('#dday-section').innerHTML, /data-dday-edit="b"/);
  f.snapshot({ capabilities: {} });
  assert.equal(f.get('#dday-section').hidden, true);
});

test('invalid form values never reach the bridge; failure preserves entered values', async () => {
  const f = fixture(); const writes = [];
  f.context.act = async (...args) => { writes.push(args); return false; };
  for (const [title, date] of [[' ', '2030-01-01'], ['fixture', '0000-01-01'], ['fixture', '2026-02-29'], ['a\tb', '2030-01-01']]) {
    f.get('#dday-title').value = title; f.get('#dday-date').value = date;
    await f.submit();
  }
  assert.equal(writes.length, 0);
  f.get('#dday-title').value = 'fixture'; f.get('#dday-date').value = '2030-01-01';
  await f.submit();
  assert.equal(writes.length, 1);
  assert.equal(f.get('#dday-title').value, 'fixture');
  assert.equal(f.get('#dday-form-error').hidden, false);
});

test('double submit sends one write until acknowledgement', async () => {
  const f = fixture(); let finish; let writes = 0;
  f.context.act = () => { writes++; return new Promise(resolve => { finish = resolve; }); };
  f.get('#dday-title').value = 'fixture'; f.get('#dday-date').value = '2030-01-01';
  const first = f.submit(); f.submit();
  assert.equal(writes, 1);
  assert.equal(f.get('#dday-save').disabled, true);
  finish(true); await first;
  assert.equal(f.get('#dday-save').disabled, false);
  assert.equal(f.get('#dday-title').value, '');
});

test('internal edit retains outside return focus and isolates covered controls', () => {
  const f = fixture(); f.snapshot({ ddays: [item('a')] });
  const outside = f.get('#settings-button'); outside.focus();
  f.context.openDdayManager();
  f.get('#dday-title').focus(); f.context.openDdayManager('a');
  assert.equal(f.get('#content').inert, true);
  f.context.closeDdayManager();
  assert.equal(f.document.activeElement, outside);
  assert.equal(f.get('#content').inert, false);
});

test('refresh preserves delete confirmation and unchanged manager controls', () => {
  const f = fixture(); const snapshot = { ddays: [item('a')] };
  f.snapshot(snapshot); f.context.openDdayManager();
  const list = f.get('#dday-list');
  let writes = 0, value = list.innerHTML;
  Object.defineProperty(list, 'innerHTML', { get: () => value, set: next => { writes++; value = next; } });
  f.snapshot(snapshot);
  assert.equal(writes, 0);
});

test('creation order remains stable when timestamps tie', () => {
  const f = fixture();
  assert.deepEqual(Array.from(f.context.ddaySort([item('z'), item('a')]), value => value.id), ['z', 'a']);
});

test('add, edit, pin, archive, restore, and confirmed delete dispatch local actions', async () => {
  const f=fixture(); let records=[]; const actions=[];
  f.context.act=async (action,args) => {
    actions.push(action);
    if(action==='addDday')records.push(item('a',args));
    if(action==='updateDday')Object.assign(records[0],args);
    if(action==='archiveDday')records[0].archived=args.value;
    if(action==='deleteDday')records=[];
    f.snapshot({ddays:records}); return true;
  };
  f.get('#dday-title').value='fixture';f.get('#dday-date').value='2030-01-01';await f.submit();
  f.context.openDdayManager('a');f.get('#dday-title').value='edited';await f.submit();
  assert.equal(records[0].title,'edited');
  const actionButton=action=>({dataset:{ddayId:'a',ddayAction:action}});
  await f.context.handleDdayAction(actionButton('pin'));assert.equal(records[0].pinned,true);
  await f.context.handleDdayAction(actionButton('archive'));assert.equal(records[0].archived,true);
  assert.doesNotMatch(f.get('#dday-section').innerHTML,/dday-card/);
  await f.context.handleDdayAction(actionButton('archive'));assert.equal(records[0].archived,false);
  const confirmation={dataset:{ddayDelete:'a'},getAttribute:()=>null};
  await f.listeners.click[0]({target:{closest:()=>confirmation}});
  assert.equal(actions.length,5); // Opening the confirmation alone must not delete.
  assert.equal(f.get('[data-dday-confirm="a"]').hidden,false);
  await f.context.handleDdayAction(actionButton('delete'));assert.equal(records.length,0);
  assert.deepEqual(actions,['addDday','updateDday','updateDday','archiveDday','archiveDday','deleteDday']);
});

test('a late save reply cannot erase a newly opened form', async () => {
  const f=fixture();let finish;
  f.context.act=()=>new Promise(resolve=>{finish=resolve});
  f.get('#dday-title').value='old';f.get('#dday-date').value='2030-01-01';
  const saving=f.submit();f.context.closeDdayManager();f.context.openDdayManager();
  finish(true);await saving;
  assert.equal(f.get('#dday-title').value,'');
  assert.equal(f.get('#dday-manager').hidden,false);
  assert.equal(f.get('#dday-save').disabled,false);
});

test('midnight refresh is one-shot, hidden views cancel it, and foreground reschedules', () => {
  const f=fixture();
  let midnight=Array.from(f.timers.values()).filter(timer=>timer.fn===f.context.refreshDdayClock);
  assert.equal(midnight.length,1);assert.ok(midnight[0].delay>0&&midnight[0].delay<=86400050);
  f.document.hidden=true;f.context.refreshDdayClock();
  assert.equal(Array.from(f.timers.values()).filter(timer=>timer.fn===f.context.refreshDdayClock).length,0);
  f.document.hidden=false;f.context.refreshDdayClock();
  assert.equal(Array.from(f.timers.values()).filter(timer=>timer.fn===f.context.refreshDdayClock).length,1);
});

test('corrupt-store state blocks writes while leaving the manager close control focused', async () => {
  const f=fixture();f.snapshot({ddayError:'fixture load failure'});f.context.openDdayManager();
  let writes=0;f.context.act=async()=>{writes++;return true};
  await f.submit();
  assert.equal(writes,0);assert.equal(f.get('#dday-save').disabled,true);
  assert.equal(f.document.activeElement,f.get('#dday-close'));
});

test('Tab and Shift-Tab stay inside the open manager', () => {
  const f=fixture();f.context.openDdayManager();
  const first=f.get('#dday-close'),last=f.get('#dday-archived-toggle');
  first.closest=last.closest=()=>null;f.get('#dday-manager').querySelectorAll=()=>[first,last];
  let prevented=0;last.focus();f.context.trapDdayFocus({key:'Tab',preventDefault(){prevented++}});
  assert.equal(f.document.activeElement,first);
  f.context.trapDdayFocus({key:'Tab',shiftKey:true,preventDefault(){prevented++}});
  assert.equal(f.document.activeElement,last);assert.equal(prevented,2);
});

test('calendar dates stay one day apart through DST and use the local date across the date line', () => {
  const declarations = source.split('\n').filter(line => /^function (dayKey|dayOrdinal|ddayDelta)\(/.test(line)).join('\n');
  for (const [zone, expected] of [['America/New_York', '2026-03-07'], ['Pacific/Kiritimati', '2026-03-08'], ['Pacific/Honolulu', '2026-03-07']]) {
    const code = declarations + '\nprocess.stdout.write(JSON.stringify([dayKey(new Date("2026-03-08T09:30:00Z")),ddayDelta("2026-03-09","2026-03-08"),ddayDelta("2026-11-02","2026-11-01")]));';
    const result = spawnSync(process.execPath, ['-e', code], { env: { ...process.env, TZ: zone }, encoding: 'utf8' });
    assert.equal(result.status, 0);
    // New York is already March 8 at this instant; Hawaii still has March 7.
    const day = zone === 'America/New_York' ? '2026-03-08' : expected;
    assert.deepEqual(JSON.parse(result.stdout), [day, 1, 1]);
  }
});
