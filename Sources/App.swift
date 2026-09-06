import AppKit
import ServiceManagement
import WebKit

@main struct OrbitMain {
  @MainActor static func main() async {
    if CommandLine.arguments.contains("--self-test") {
      let ok = await SelfTests.run()
      exit(ok ? 0 : 1)
    }
    if CommandLine.arguments.contains("--smoke-test") {
      let p = Preferences()
      let s = LocalProviders.scan(drive: p.drive, vault: p.vault, codex: p.codex, claude: p.claude)
      print(
        "local smoke: files=\(s.files.count), codex=\(s.agents.filter{$0.provider=="codex"}.count), claude=\(s.agents.filter{$0.provider=="claude"}.count)"
      )
      for status in s.statuses { print("\(status.source): \(status.state)") }
      exit(s.statuses.contains { $0.state == "error" } ? 1 : 0)
    }
    let app = NSApplication.shared
    let peers = NSRunningApplication.runningApplications(
      withBundleIdentifier: "local.orbit.dashboard"
    ).filter { $0.processIdentifier != ProcessInfo.processInfo.processIdentifier }
    if let other = peers.first {
      other.activate(options: [.activateAllWindows])
      exit(0)
    }
    let delegate = OrbitApp()
    app.delegate = delegate
    app.setActivationPolicy(.accessory)
    app.run()
  }
}

@MainActor final class OrbitApp: NSObject, NSApplicationDelegate, NSPopoverDelegate {
  var statusItem: NSStatusItem!
  let popover = NSPopover()
  var bridge: Bridge!
  var timer: Timer?
  var pinned = false
  func applicationDidFinishLaunching(_ notification: Notification) {
    statusItem = NSStatusBar.system.statusItem(withLength: 28)
    if let b = statusItem.button {
      b.image = Self.icon()
      b.toolTip = "Orbit · 오늘의 업무"
      b.target = self
      b.action = #selector(toggle)
      b.sendAction(on: [.leftMouseUp, .rightMouseUp])
      b.setAccessibilityLabel("Orbit 업무 대시보드")
    }
    bridge = Bridge(app: self)
    let controller = NSViewController()
    controller.view = bridge.webView
    popover.contentViewController = controller
    popover.contentSize = NSSize(width: 560, height: 700)
    popover.behavior = .transient
    popover.delegate = self
    bridge.load()
    timer = Timer.scheduledTimer(withTimeInterval: 60, repeats: true) { [weak self] _ in
      Task { @MainActor in
        guard let self = self, self.popover.isShown else { return }
        await self.bridge.refresh(forceFiles: false)
      }
    }
    if !CommandLine.arguments.contains("--background") {
      DispatchQueue.main.asyncAfter(deadline: .now() + 0.4) { [weak self] in self?.show() }
    }
  }
  func applicationShouldHandleReopen(_ sender: NSApplication, hasVisibleWindows flag: Bool) -> Bool
  {
    show()
    return true
  }
  @objc func toggle() {
    if NSApp.currentEvent?.type == .rightMouseUp {
      let menu = NSMenu()
      menu.addItem(withTitle: "Orbit 열기", action: #selector(showFromMenu), keyEquivalent: "")
        .target = self
      menu.addItem(.separator())
      menu.addItem(withTitle: "Orbit 종료", action: #selector(quit), keyEquivalent: "q").target = self
      statusItem.menu = menu
      statusItem.button?.performClick(nil)
      statusItem.menu = nil
      return
    }
    if popover.isShown { popover.performClose(nil) } else { show() }
  }
  @objc func showFromMenu() { show() }
  func show() {
    guard let b = statusItem.button else { return }
    let height = min(740, max(520, (b.window?.screen?.visibleFrame.height ?? 850) - 65))
    popover.contentSize = NSSize(width: 560, height: height)
    NSApp.activate(ignoringOtherApps: true)
    popover.show(relativeTo: b.bounds, of: b, preferredEdge: .minY)
    popover.contentViewController?.view.window?.makeKey()
    Task { await bridge.refresh(forceFiles: false) }
  }
  func setPinned(_ value: Bool) {
    pinned = value
    popover.behavior = value ? .applicationDefined : .transient
  }
  func applyTheme(_ theme: String) {
    popover.appearance = NSAppearance(named: theme == "pearl" ? .aqua : .darkAqua)
  }
  @objc func quit() { NSApp.terminate(nil) }
  func applicationWillTerminate(_ notification: Notification) {
    bridge?.google.oauth.cancel()
    timer?.invalidate()
  }
  static func icon() -> NSImage {
    let image = NSImage(size: NSSize(width: 22, height: 22), flipped: false) { _ in
      NSColor.labelColor.setStroke()
      let circle = NSBezierPath(ovalIn: NSRect(x: 5.5, y: 5.5, width: 11, height: 11))
      circle.lineWidth = 1.5
      circle.stroke()
      let orbit = NSBezierPath(ovalIn: NSRect(x: 0.6, y: 7, width: 20.8, height: 8))
      let t = AffineTransform(translationByX: 11, byY: 11)
      var rot = t
      rot.rotate(byDegrees: 35)
      rot.translate(x: -11, y: -11)
      orbit.transform(using: rot)
      orbit.lineWidth = 1.3
      orbit.stroke()
      NSColor.labelColor.setFill()
      NSBezierPath(ovalIn: NSRect(x: 17, y: 14.5, width: 3, height: 3)).fill()
      return true
    }
    image.isTemplate = true
    return image
  }
}
