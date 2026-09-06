const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const source = fs.readFileSync(path.join(__dirname, '../Resources/platform.js'), 'utf8');

function load(window) {
  const document = { documentElement: { dataset: {} } };
  vm.runInNewContext(source, { window, document, Object, Error, Boolean });
  return { platform: window.orbitPlatform, document };
}

test('Windows posts once and receives through the platform-neutral entry point', () => {
  const sent = [];
  let listener;
  const received = [];
  const window = {
    chrome: { webview: {
      postMessage: message => sent.push(message),
      addEventListener: (name, handler) => {
        assert.equal(name, 'message');
        listener = handler;
      },
    } },
    orbit: { receive: message => received.push(message) },
  };

  const { platform, document } = load(window);
  platform.post({ action: 'ready', request: '1' });
  listener({ data: { kind: 'reply', request: '1' } });

  assert.equal(platform.name, 'windows');
  assert.equal(platform.canSend(), true);
  assert.deepEqual(sent, [{ action: 'ready', request: '1' }]);
  assert.deepEqual(received, [{ kind: 'reply', request: '1' }]);
  assert.equal(document.documentElement.dataset.platform, 'windows');
});

test('macOS uses WebKit without registering a WebView2 listener', () => {
  const sent = [];
  const window = {
    webkit: { messageHandlers: { orbit: {
      postMessage: message => sent.push(message),
    } } },
  };

  const { platform } = load(window);
  platform.post({ action: 'refresh', request: '2' });

  assert.equal(platform.name, 'macos');
  assert.equal(platform.canSend(), true);
  assert.deepEqual(sent, [{ action: 'refresh', request: '2' }]);
});

test('preview mode rejects native actions with a platform-neutral error', () => {
  const { platform } = load({});
  assert.equal(platform.name, 'preview');
  assert.equal(platform.canSend(), false);
  assert.throws(() => platform.post({ action: 'quit' }), /Orbit 앱/);
});

test('capabilities control startup UI and app-opening descriptions', () => {
  const { platform } = load({ chrome: { webview: {
    postMessage() {}, addEventListener() {},
  } } });
  const windows = {
    platform: 'windows',
    capabilities: { launchAtLogin: false, agentDesktopOpen: true },
  };

  assert.equal(platform.shortcut(windows), 'Ctrl K');
  assert.equal(platform.launchAtLoginSupported(windows), false);
  assert.equal(
    platform.agentActionLabel({ provider: 'claude', openMode: 'projectNew' }, windows),
    '같은 프로젝트에서 Claude 새 작업 열기',
  );
  assert.equal(
    platform.agentActionLabel({ provider: 'codex', openMode: 'appHome' }, windows),
    'Codex 앱 열기 · 작업 직접 선택',
  );
  assert.equal(
    platform.agentActionLabel({ provider: 'claude', openMode: 'unavailable' }, windows),
    'Claude 앱 설치 또는 업데이트 필요',
  );
  assert.doesNotMatch(platform.agentHelp(windows), /Terminal|명령 복사/);
});

test('legacy macOS snapshots preserve launch at login and Claude behavior', () => {
  const { platform } = load({ webkit: { messageHandlers: { orbit: {
    postMessage() {},
  } } } });
  const legacy = {};

  assert.equal(platform.shortcut(legacy), '⌘ K');
  assert.equal(platform.launchAtLoginSupported(legacy), true);
  assert.match(
    platform.agentActionLabel({ provider: 'claude' }, legacy),
    /Terminal/,
  );
  assert.match(platform.agentHelp(legacy), /Terminal/);
});

test('meeting availability accepts the Windows boolean and macOS URL shape', () => {
  const { platform } = load({});
  assert.equal(platform.hasMeeting({ hasMeeting: true }), true);
  assert.equal(platform.hasMeeting({ hasMeeting: false, meetingURL: 'https://ignored.example' }), false);
  assert.equal(platform.hasMeeting({ meetingURL: 'https://meet.example' }), true);
  assert.equal(platform.hasMeeting({ meetingURL: '' }), false);
});

test('the shared document loads the platform bridge before application code', () => {
  const html = fs.readFileSync(path.join(__dirname, '../Resources/index.html'), 'utf8');
  const app = fs.readFileSync(path.join(__dirname, '../Resources/app.js'), 'utf8');
  const style = fs.readFileSync(path.join(__dirname, '../Resources/platform.css'), 'utf8');
  assert.ok(html.indexOf('src="platform.js"') < html.indexOf('src="app.js"'));
  assert.match(html, /href="platform\.css"/);
  assert.match(html, /id="agent-open-help"/);
  assert.match(html, /id="launch-login-option"/);
  assert.equal((html.match(/data-shortcut/g) || []).length, 2);
  assert.doesNotMatch(app, /window\.webkit|window\.chrome/);
  assert.match(app, /orbitPlatform\.post/);
  assert.match(app, /launchAtLoginSupported/);
  assert.match(app, /agentActionLabel/);
  assert.match(style, /Segoe UI/);
  assert.match(style, /html\[data-platform=windows\] #launch-login-option/);
});
