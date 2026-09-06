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
