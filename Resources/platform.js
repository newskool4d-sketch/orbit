'use strict';
(() => {
  const webView = window.chrome?.webview;
  const webKit = window.webkit?.messageHandlers?.orbit;
  const name = webView ? 'windows' : webKit ? 'macos' : 'preview';

  if (document?.documentElement) document.documentElement.dataset.platform = name;

  if (webView) {
    webView.addEventListener('message', event => window.orbit?.receive(event.data));
  }

  function snapshotPlatform(snapshot = {}) {
    return snapshot.platform || (name === 'preview' ? 'macos' : name);
  }

  function applicationName(provider) {
    return provider === 'codex' ? 'Codex' : 'Claude';
  }

  window.orbitPlatform = Object.freeze({
    name,
    canSend() {
      return Boolean(webView || webKit);
    },
    post(message) {
      if (webView) {
        webView.postMessage(message);
        return;
      }
      if (webKit) {
        webKit.postMessage(message);
        return;
      }
      throw new Error('실제 연동은 Orbit 앱에서 사용할 수 있습니다.');
    },
    shortcut(snapshot = {}) {
      return snapshotPlatform(snapshot) === 'windows' ? 'Ctrl K' : '⌘ K';
    },
    launchAtLoginSupported(snapshot = {}) {
      const declared = snapshot.capabilities?.launchAtLogin;
      return typeof declared === 'boolean'
        ? declared
        : snapshotPlatform(snapshot) !== 'windows';
    },
    agentActionLabel(agent, snapshot = {}) {
      const app = applicationName(agent.provider);
      switch (agent.openMode) {
        case 'projectNew': return `같은 프로젝트에서 ${app} 새 작업 열기`;
        case 'appHome': return `${app} 앱 열기 · 작업 직접 선택`;
        case 'unavailable': return `${app} 앱 설치 또는 업데이트 필요`;
        case 'exact': return `${app} 작업 열기`;
        default:
          return agent.provider === 'claude' && snapshotPlatform(snapshot) !== 'windows'
            ? '이어하기 명령 복사 · Terminal 열기'
            : `${app} 작업 열기`;
      }
    },
    agentHelp(snapshot = {}) {
      return snapshotPlatform(snapshot) === 'windows'
        ? 'AI 작업은 Codex 또는 Claude 앱에서 엽니다. 기존 작업을 정확히 열 수 없으면 카드에 대안을 표시합니다.'
        : 'Claude Code 이어하기는 명령을 복사하고 Terminal을 엽니다. Claude Desktop 일반 채팅은 포함하지 않습니다.';
    },
    hasMeeting(event = {}) {
      return typeof event.hasMeeting === 'boolean'
        ? event.hasMeeting
        : Boolean(event.meetingURL);
    },
  });
})();
