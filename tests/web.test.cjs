const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
const source = fs.readFileSync(path.join(__dirname, '../Resources/app.js'), 'utf8');

test('UI action selectors resolve to controls, never hidden SVG symbols', () => {
  const html = fs.readFileSync(path.join(__dirname, '../Resources/index.html'), 'utf8');
  const elements = [...html.matchAll(/<([a-z][\w-]*)\b[^>]*\bid="([^"]+)"[^>]*>/gi)];
  for (const [id, tag] of [['settings', 'section'], ['pin', 'button'], ['refresh', 'button']]) {
    assert.equal(elements.find(match => match[2] === id)?.[1], tag, `#${id} must select the interactive UI`);
  }
  const ids = elements.map(match => match[2]);
  assert.equal(new Set(ids).size, ids.length, 'All document IDs must be unique');
  for (const [, id] of html.matchAll(/<use href="#([^"]+)"/g)) {
    assert.equal(elements.find(match => match[2] === id)?.[1], 'symbol', `Icon ${id} must resolve`);
  }
});

test('Provider titles are escaped as text, including quotes and HTML', () => {
  const declaration = source.split('\n').find(line => line.startsWith('const esc='));
  const context = vm.createContext({});
  vm.runInContext(declaration + ';this.escapeText=esc;', context);
  assert.equal(context.escapeText('<img src=x onerror="alert(1)"> & \'x\''), '&lt;img src=x onerror=&quot;alert(1)&quot;&gt; &amp; &#39;x&#39;');
});

test('NFC typed Korean matches NFD Google Drive filenames and preserves filtering', () => {
  const nodes = { '#file-search': { value: '중기' }, '#file-list': {}, '#file-description': {} };
  const context = vm.createContext({
    $: key => nodes[key], fileSource: 'drive',
    state: { files: [
      { id: 'match', name: '중기 발전 계획.hwpx'.normalize('NFD'), parent: '문서', source: 'drive' },
      { id: 'wrong-source', name: '중기.md', parent: '노트', source: 'obsidian' },
      { id: 'no-match', name: '연수.pdf', parent: '문서', source: 'drive' },
    ], statuses: [] },
    fileCard: file => file.id,
    empty: text => text,
  });
  const declaration = source.split('\n').find(line => line.startsWith('function renderFiles()'));
  vm.runInContext(declaration + ';renderFiles();', context);
  assert.equal(nodes['#file-list'].innerHTML, 'match');
  assert.match(nodes['#file-description'].textContent, /1개/);
});

test('Production resources block external scripts, network fetches, and demo records', () => {
  const html = fs.readFileSync(path.join(__dirname, '../Resources/index.html'), 'utf8');
  assert.match(html, /connect-src 'none'/);
  assert.match(html, /script-src 'self'/);
  assert.doesNotMatch(source, /eval\(|innerHTML\s*=\s*(?:a|file|event)\./);
  assert.doesNotMatch(html, /주간 업무 협의|클로드 버전 업데이트|예시 데이터/);
});

test('Calendar tab exposes the seven-day event list with accessible tab wiring', () => {
  const html = fs.readFileSync(path.join(__dirname, '../Resources/index.html'), 'utf8');
  assert.match(html, /id="tab-calendar"[^>]*aria-controls="calendar"[^>]*data-tab="calendar"/);
  assert.match(html, /id="calendar"[^>]*role="tabpanel"[^>]*aria-labelledby="tab-calendar"/);
  assert.match(html, /id="calendar-list"/);
  assert.match(source, /function renderCalendar\(\)/);
  assert.match(source, /calendarDateLabel\(group\.stamp\)/);
  assert.match(source, /data-event="\$\{esc\(event\.id\)\}"/);
});

test('Readability controls expose three persisted text sizes', () => {
  const html = fs.readFileSync(path.join(__dirname, '../Resources/index.html'), 'utf8');
  const style = fs.readFileSync(path.join(__dirname, '../Resources/readability.css'), 'utf8');
  for (const value of ['normal', 'large', 'xlarge']) {
    assert.match(html, new RegExp(`data-text-size="${value}"`));
    assert.match(source, new RegExp(`['"]${value}['"]`));
  }
  assert.match(html, /href="readability\.css"/);
  assert.match(style, /--font-body:\s*14px/);
  assert.match(style, /data-text-size="large"/);
  assert.match(style, /data-text-size="xlarge"/);
  assert.match(style, /grid-template-columns:\s*1fr/);
});

test('Glass surfaces and Windows font stack remain theme-aware', () => {
  const style = fs.readFileSync(path.join(__dirname, '../Resources/readability.css'), 'utf8');
  assert.match(style, /Segoe UI Variable Text/);
  assert.match(style, /body\[data-theme="pearl"\][\s\S]*--glass-edge/);
  assert.match(style, /body\[data-theme="cobalt"\][\s\S]*--glass-edge/);
  assert.match(style, /backdrop-filter:\s*blur\(18px\)/);
});

test('Google Tasks dates are described as scheduled dates rather than deadlines', () => {
  assert.match(source, /예정일 지남/);
  assert.match(source, /오늘 예정/);
  assert.doesNotMatch(source, /기한 지남|오늘 마감/);
});

test('D-day uses calendar-day ordinals across month, year, leap day, and DST boundaries', () => {
  const context = vm.createContext({ Date });
  for (const name of ['dayKey', 'dayOrdinal', 'ddayDelta', 'ddayLabel']) {
    const declaration = source.split('\n').find(line => line.startsWith(`function ${name}(`));
    vm.runInContext(declaration, context);
  }
  assert.equal(context.ddayLabel('2026-09-18', '2026-09-17'), 'D-1');
  assert.equal(context.ddayLabel('2026-09-17', '2026-09-17'), 'D-day');
  assert.equal(context.ddayLabel('2026-09-16', '2026-09-17'), 'D+1');
  assert.equal(context.ddayDelta('2027-01-01', '2026-12-31'), 1);
  assert.equal(context.ddayDelta('2028-03-01', '2028-02-28'), 2);
  assert.equal(context.ddayDelta('2026-03-09', '2026-03-08'), 1);
  assert.equal(Number.isNaN(context.dayOrdinal('2026-02-30')), true);
});

test('D-day ordering pins first, keeps future before past, and preserves creation order for ties', () => {
  const context = vm.createContext({ Date });
  for (const name of ['dayKey', 'dayOrdinal', 'ddayDelta', 'ddaySort']) {
    const declaration = source.split('\n').find(line => line.startsWith(`function ${name}(`));
    vm.runInContext(declaration, context);
  }
  const values = [
    { id: 'c', targetDate: '2026-09-16', pinned: false, createdAt: '2026-01-03' },
    { id: 'b', targetDate: '2026-09-19', pinned: false, createdAt: '2026-01-02' },
    { id: 'a', targetDate: '2026-09-20', pinned: true, createdAt: '2026-01-01' },
    { id: 'd', targetDate: '2026-09-19', pinned: false, createdAt: '2026-01-04' },
  ];
  assert.deepEqual(Array.from(context.ddaySort(values, '2026-09-17'), item => item.id), ['a', 'b', 'd', 'c']);
});

test('D-day UI provides local add, edit, archive, and title-visible delete confirmation', () => {
  const html = fs.readFileSync(path.join(__dirname, '../Resources/index.html'), 'utf8');
  assert.match(html, /id="dday-section"/);
  assert.match(html, /id="dday-manager"/);
  assert.match(html, /id="dday-title"[^>]*maxlength="120"/);
  assert.match(html, /id="dday-date"[^>]*type="date"/);
  assert.match(source, /data-dday-action="archive"/);
  assert.match(source, /dday-delete-confirm/);
  assert.match(source, /‘\$\{title\}’/);
});

test('Both native hosts expose D-day capability and preserve corrupt local stores', () => {
  const windowsStore = fs.readFileSync(path.join(__dirname, '../Windows/src/Orbit.Windows/DdayStore.cs'), 'utf8');
  const windowsHost = fs.readFileSync(path.join(__dirname, '../Windows/src/Orbit.Windows/Program.cs'), 'utf8');
  const macStore = fs.readFileSync(path.join(__dirname, '../Sources/DdayStore.swift'), 'utf8');
  const macHost = fs.readFileSync(path.join(__dirname, '../Sources/Bridge.swift'), 'utf8');
  assert.match(windowsStore, /File\.Move\(temp, path, true\)/);
  assert.match(windowsStore, /Error = LoadMessage/);
  assert.match(macStore, /write\(to: url, options: \.atomic\)/);
  assert.match(macStore, /loadError = "중요 날짜 저장 파일을 읽지 못했습니다/);
  for (const action of ['addDday', 'updateDday', 'archiveDday', 'deleteDday']) {
    assert.match(windowsHost, new RegExp(`case "${action}"`));
    assert.match(macHost, new RegExp(`case "${action}"`));
  }
  assert.match(windowsHost, /\["dday"\]=true/);
  assert.match(macHost, /"dday": true/);
});
