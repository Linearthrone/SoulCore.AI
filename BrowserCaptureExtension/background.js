const BRIDGE_URL = "http://127.0.0.1:17891";
const CAST_WS_URL = "ws://127.0.0.1:17891/ws/cast";
const POLL_MS = 400;
const STREAM_MIN_MS = 750;
const DEBUGGER_IDLE_MS = 60000;
const DEBUGGER_PROTOCOL = "1.3";

let lastStreamPushMs = 0;
let cachedStreamEnabled = false;
let castWs = null;
let castWsConnecting = false;
let debuggerTabId = null;
let debuggerIdleTimer = null;

function connectCastProducer() {
  if (castWs?.readyState === WebSocket.OPEN) return;
  if (castWsConnecting) return;
  castWsConnecting = true;

  try {
    castWs = new WebSocket(CAST_WS_URL);
    castWs.onopen = () => {
      castWsConnecting = false;
      castWs.send(JSON.stringify({ role: "producer" }));
    };
    castWs.onclose = () => {
      castWs = null;
      castWsConnecting = false;
    };
    castWs.onerror = () => {
      castWsConnecting = false;
    };
  } catch (_) {
    castWsConnecting = false;
  }
}

async function pollBridge() {
  try {
    const res = await fetch(`${BRIDGE_URL}/poll`, { cache: "no-store" });
    if (!res.ok) return;
    const job = await res.json();
    cachedStreamEnabled = !!job.stream_enabled;
    if (job.pending && job.job_id) {
      if (job.kind === "action") {
        await runActionJob(job);
      } else {
        await runCaptureJob(job);
      }
    } else if (cachedStreamEnabled) {
      connectCastProducer();
      await streamPushActiveTab();
    }
  } catch (_) {
    // Bridge not running — extension stays idle.
  }
}

async function isStreamEnabled() {
  try {
    const res = await fetch(`${BRIDGE_URL}/stream/status`, { cache: "no-store" });
    if (!res.ok) return false;
    const status = await res.json();
    cachedStreamEnabled = !!status.stream_enabled;
    return cachedStreamEnabled;
  } catch {
    return false;
  }
}

async function streamPushActiveTab() {
  const now = Date.now();
  if (now - lastStreamPushMs < STREAM_MIN_MS) return;

  try {
    const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
    if (!tab || tab.id == null || tab.windowId == null) return;
    if (!tab.url || tab.url.startsWith("chrome://") || tab.url.startsWith("edge://")) return;

    const dataUrl = await chrome.tabs.captureVisibleTab(tab.windowId, { format: "png" });
    const screenshotBase64 = dataUrl.replace(/^data:image\/png;base64,/, "");

    if (cachedStreamEnabled && castWs?.readyState === WebSocket.OPEN) {
      castWs.send(
        JSON.stringify({
          type: "frame",
          tab_id: tab.id,
          url: tab.url || "",
          title: tab.title || "",
          png: screenshotBase64,
        })
      );
      lastStreamPushMs = now;
      return;
    }

    const pushRes = await fetch(`${BRIDGE_URL}/stream`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        ok: true,
        tab_id: tab.id,
        url: tab.url || "",
        title: tab.title || "",
        screenshot_base64: screenshotBase64,
      }),
    });
    if (pushRes.ok) lastStreamPushMs = now;
  } catch (_) {
    // Capture denied or bridge offline — skip this tick.
  }
}

async function runCaptureJob(job) {
  const jobId = job.job_id;
  const includeScreenshot = job.include_screenshot !== false;
  const includePageMap = job.include_page_map !== false;

  try {
    const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
    if (!tab || tab.id == null) {
      await postResult(jobId, { ok: false, kind: "capture", error: "no_active_tab" });
      return;
    }

    let screenshotBase64 = null;
    if (includeScreenshot) {
      const dataUrl = await chrome.tabs.captureVisibleTab(tab.windowId, { format: "png" });
      screenshotBase64 = dataUrl.replace(/^data:image\/png;base64,/, "");
    }

    let pageMap = null;
    if (includePageMap) {
      const [{ result }] = await chrome.scripting.executeScript({
        target: { tabId: tab.id },
        func: buildPageMapInPage,
      });
      pageMap = result;
    }

    await postResult(jobId, {
      ok: true,
      kind: "capture",
      tab_id: tab.id,
      window_id: tab.windowId,
      url: tab.url || "",
      title: tab.title || "",
      screenshot_base64: screenshotBase64,
      page_map: pageMap,
    });
  } catch (err) {
    await postResult(jobId, { ok: false, kind: "capture", error: String(err?.message || err) });
  }
}

async function runActionJob(job) {
  const jobId = job.job_id;
  try {
    const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
    if (!tab || tab.id == null) {
      await postResult(jobId, { ok: false, kind: "action", error: "no_active_tab" });
      return;
    }
    if (!tab.url || tab.url.startsWith("chrome://") || tab.url.startsWith("edge://") || tab.url.startsWith("chrome-extension://")) {
      await postResult(jobId, {
        ok: false,
        kind: "action",
        error: "action_failed",
        detail: "Cannot drive chrome:// or extension pages",
        tab_id: tab.id,
        url: tab.url || "",
        title: tab.title || "",
      });
      return;
    }

    const action = job.action;
    const hasElementTarget =
      (typeof job.selector === "string" && job.selector.length > 0) ||
      (job.index !== null && job.index !== undefined && Number.isFinite(Number(job.index)));
    const hasCoords = job.x !== null && job.x !== undefined && job.y !== null && job.y !== undefined;

    let outcome;
    if (action === "key") {
      outcome = await dispatchKeyViaDebugger(tab.id, job.key, job.modifiers || []);
    } else if (action === "click" && hasElementTarget) {
      outcome = await runDomAction(tab.id, {
        mode: "click",
        selector: job.selector || null,
        index: job.index,
      });
    } else if (action === "click" && hasCoords) {
      outcome = await dispatchMouseClickViaDebugger(tab.id, Number(job.x), Number(job.y), job.button || "left");
    } else if (action === "type") {
      if (hasElementTarget) {
        outcome = await runDomAction(tab.id, {
          mode: "type",
          selector: job.selector || null,
          index: job.index,
          text: job.text || "",
          clear: !!job.clear,
        });
      } else {
        // Type into focused element via CDP key events for each character.
        outcome = await typeTextViaDebugger(tab.id, job.text || "", !!job.clear);
      }
    } else if (action === "scroll") {
      if (hasElementTarget) {
        outcome = await runDomAction(tab.id, {
          mode: "scroll_to",
          selector: job.selector || null,
          index: job.index,
        });
      } else {
        outcome = await runDomAction(tab.id, {
          mode: "scroll_by",
          delta_x: Number(job.delta_x) || 0,
          delta_y: Number(job.delta_y) || 0,
        });
      }
    } else {
      outcome = { ok: false, error: "action_failed", detail: `Unsupported or incomplete action: ${action}` };
    }

    await postResult(jobId, {
      ok: !!outcome.ok,
      kind: "action",
      error: outcome.error || null,
      detail: outcome.detail || null,
      tab_id: tab.id,
      window_id: tab.windowId,
      url: tab.url || "",
      title: tab.title || "",
    });
  } catch (err) {
    await postResult(jobId, {
      ok: false,
      kind: "action",
      error: "action_failed",
      detail: String(err?.message || err),
    });
  }
}

async function runDomAction(tabId, opts) {
  const [{ result }] = await chrome.scripting.executeScript({
    target: { tabId },
    func: executeDomActionInPage,
    args: [opts],
  });
  if (!result) {
    return { ok: false, error: "action_failed", detail: "No result from page script" };
  }
  return result;
}

/** Runs inside the page — must stay self-contained. */
function executeDomActionInPage(opts) {
  const INTERACTIVE =
    "a[href],button,input,textarea,select,[role='button'],[role='link'],[role='textbox'],[contenteditable='true'],[onclick]";

  function collectInteractive() {
    const list = [];
    document.querySelectorAll(INTERACTIVE).forEach((el) => {
      if (!(el instanceof HTMLElement)) return;
      const style = window.getComputedStyle(el);
      if (style.display === "none" || style.visibility === "hidden" || style.opacity === "0") return;
      const rect = el.getBoundingClientRect();
      if (rect.width < 2 || rect.height < 2) return;
      if (rect.bottom < 0 || rect.right < 0 || rect.top > window.innerHeight || rect.left > window.innerWidth)
        return;
      list.push(el);
    });
    return list;
  }

  function resolveElement(selector, index) {
    if (selector) {
      try {
        const el = document.querySelector(selector);
        if (el instanceof HTMLElement) return el;
      } catch (_) {
        return null;
      }
    }
    if (index !== null && index !== undefined && Number.isFinite(Number(index))) {
      // page_map.elements[].index is the querySelectorAll(INTERACTIVE) index.
      const all = Array.from(document.querySelectorAll(INTERACTIVE));
      const byQueryIndex = all[Number(index)];
      if (byQueryIndex instanceof HTMLElement) return byQueryIndex;
      const list = collectInteractive();
      if (list[Number(index)]) return list[Number(index)];
    }
    return null;
  }

  if (opts.mode === "scroll_by") {
    window.scrollBy(opts.delta_x || 0, opts.delta_y || 0);
    return { ok: true, detail: `scrolled_by dx=${opts.delta_x || 0} dy=${opts.delta_y || 0}` };
  }

  const el = resolveElement(opts.selector, opts.index);
  if (!el) {
    return { ok: false, error: "element_not_found", detail: opts.selector || `index=${opts.index}` };
  }

  if (opts.mode === "scroll_to") {
    el.scrollIntoView({ block: "center", inline: "nearest" });
    return { ok: true, detail: "scrolled_to_element" };
  }

  if (opts.mode === "click") {
    el.focus({ preventScroll: true });
    el.click();
    return { ok: true, detail: "clicked_element" };
  }

  if (opts.mode === "type") {
    el.focus({ preventScroll: true });
    const text = opts.text || "";
    if (el.isContentEditable) {
      if (opts.clear) el.textContent = "";
      el.textContent = (el.textContent || "") + text;
      el.dispatchEvent(new InputEvent("input", { bubbles: true, data: text, inputType: "insertText" }));
    } else if ("value" in el) {
      if (opts.clear) el.value = "";
      const proto = el.tagName === "TEXTAREA" ? HTMLTextAreaElement.prototype : HTMLInputElement.prototype;
      const nativeSetter = Object.getOwnPropertyDescriptor(proto, "value")?.set;
      const next = (opts.clear ? "" : el.value || "") + text;
      if (nativeSetter) nativeSetter.call(el, next);
      else el.value = next;
      el.dispatchEvent(new Event("input", { bubbles: true }));
      el.dispatchEvent(new Event("change", { bubbles: true }));
    } else {
      return { ok: false, error: "action_failed", detail: "Element is not typable" };
    }
    return { ok: true, detail: `typed_${text.length}_chars` };
  }

  return { ok: false, error: "action_failed", detail: `Unknown mode ${opts.mode}` };
}

function scheduleDebuggerIdleDetach() {
  if (debuggerIdleTimer) clearTimeout(debuggerIdleTimer);
  debuggerIdleTimer = setTimeout(() => {
    detachDebugger().catch(() => {});
  }, DEBUGGER_IDLE_MS);
}

async function detachDebugger() {
  if (debuggerTabId == null) return;
  const tabId = debuggerTabId;
  debuggerTabId = null;
  if (debuggerIdleTimer) {
    clearTimeout(debuggerIdleTimer);
    debuggerIdleTimer = null;
  }
  try {
    await chrome.debugger.detach({ tabId });
  } catch (_) {
    // Already detached.
  }
}

async function ensureDebugger(tabId) {
  if (debuggerTabId === tabId) {
    scheduleDebuggerIdleDetach();
    return;
  }
  if (debuggerTabId != null && debuggerTabId !== tabId) {
    await detachDebugger();
  }
  try {
    await chrome.debugger.attach({ tabId }, DEBUGGER_PROTOCOL);
  } catch (err) {
    const msg = String(err?.message || err);
    if (!/already attached/i.test(msg)) {
      throw Object.assign(new Error(msg), { code: "debugger_attach_failed" });
    }
  }
  debuggerTabId = tabId;
  scheduleDebuggerIdleDetach();
}

async function sendCdp(tabId, method, params = {}) {
  await ensureDebugger(tabId);
  try {
    return await chrome.debugger.sendCommand({ tabId }, method, params);
  } catch (err) {
    // Retry once after re-attach.
    await detachDebugger();
    await ensureDebugger(tabId);
    return await chrome.debugger.sendCommand({ tabId }, method, params);
  } finally {
    scheduleDebuggerIdleDetach();
  }
}

function mouseButtonToCdp(button) {
  if (button === "right") return "right";
  if (button === "middle") return "middle";
  return "left";
}

async function dispatchMouseClickViaDebugger(tabId, x, y, button) {
  try {
    const btn = mouseButtonToCdp(button);
    const buttons = btn === "right" ? 2 : btn === "middle" ? 4 : 1;
    await sendCdp(tabId, "Input.dispatchMouseEvent", {
      type: "mouseMoved",
      x,
      y,
    });
    await sendCdp(tabId, "Input.dispatchMouseEvent", {
      type: "mousePressed",
      x,
      y,
      button: btn,
      buttons,
      clickCount: 1,
    });
    await sendCdp(tabId, "Input.dispatchMouseEvent", {
      type: "mouseReleased",
      x,
      y,
      button: btn,
      buttons: 0,
      clickCount: 1,
    });
    return { ok: true, detail: `clicked_xy ${Math.round(x)},${Math.round(y)}` };
  } catch (err) {
    const code = err?.code === "debugger_attach_failed" ? "debugger_attach_failed" : "action_failed";
    return { ok: false, error: code, detail: String(err?.message || err) };
  }
}

const KEY_DEFS = {
  Enter: { key: "Enter", code: "Enter", keyCode: 13, text: "\r" },
  Tab: { key: "Tab", code: "Tab", keyCode: 9 },
  Escape: { key: "Escape", code: "Escape", keyCode: 27 },
  Esc: { key: "Escape", code: "Escape", keyCode: 27 },
  Backspace: { key: "Backspace", code: "Backspace", keyCode: 8 },
  Delete: { key: "Delete", code: "Delete", keyCode: 46 },
  ArrowUp: { key: "ArrowUp", code: "ArrowUp", keyCode: 38 },
  ArrowDown: { key: "ArrowDown", code: "ArrowDown", keyCode: 40 },
  ArrowLeft: { key: "ArrowLeft", code: "ArrowLeft", keyCode: 37 },
  ArrowRight: { key: "ArrowRight", code: "ArrowRight", keyCode: 39 },
  Home: { key: "Home", code: "Home", keyCode: 36 },
  End: { key: "End", code: "End", keyCode: 35 },
  PageUp: { key: "PageUp", code: "PageUp", keyCode: 33 },
  PageDown: { key: "PageDown", code: "PageDown", keyCode: 34 },
  Space: { key: " ", code: "Space", keyCode: 32, text: " " },
};

function normalizeModifiers(modifiers) {
  const set = new Set((modifiers || []).map((m) => String(m).toLowerCase()));
  let mask = 0;
  if (set.has("alt")) mask |= 1;
  if (set.has("ctrl") || set.has("control")) mask |= 2;
  if (set.has("meta") || set.has("command") || set.has("cmd")) mask |= 4;
  if (set.has("shift")) mask |= 8;
  return { set, mask };
}

function resolveKeyDef(keyName) {
  if (!keyName) return null;
  if (KEY_DEFS[keyName]) return KEY_DEFS[keyName];
  if (keyName.length === 1) {
    const upper = keyName.toUpperCase();
    const isLetter = upper >= "A" && upper <= "Z";
    return {
      key: keyName,
      code: isLetter ? `Key${upper}` : keyName,
      keyCode: keyName.toUpperCase().charCodeAt(0),
      text: keyName,
    };
  }
  return { key: keyName, code: keyName, keyCode: 0 };
}

async function dispatchKeyViaDebugger(tabId, keyName, modifiers) {
  try {
    const def = resolveKeyDef(keyName);
    if (!def) {
      return { ok: false, error: "action_failed", detail: "Missing key" };
    }
    const { set, mask } = normalizeModifiers(modifiers);
    const modsToPress = [];
    if (set.has("ctrl") || set.has("control")) modsToPress.push({ key: "Control", code: "ControlLeft", keyCode: 17 });
    if (set.has("alt")) modsToPress.push({ key: "Alt", code: "AltLeft", keyCode: 18 });
    if (set.has("shift")) modsToPress.push({ key: "Shift", code: "ShiftLeft", keyCode: 16 });
    if (set.has("meta") || set.has("command") || set.has("cmd"))
      modsToPress.push({ key: "Meta", code: "MetaLeft", keyCode: 91 });

    for (const m of modsToPress) {
      await sendCdp(tabId, "Input.dispatchKeyEvent", {
        type: "rawKeyDown",
        key: m.key,
        code: m.code,
        windowsVirtualKeyCode: m.keyCode,
        nativeVirtualKeyCode: m.keyCode,
        modifiers: mask,
      });
    }

    const useText = def.text && modsToPress.length === 0;
    await sendCdp(tabId, "Input.dispatchKeyEvent", {
      type: useText ? "keyDown" : "rawKeyDown",
      key: def.key,
      code: def.code,
      windowsVirtualKeyCode: def.keyCode,
      nativeVirtualKeyCode: def.keyCode,
      text: useText ? def.text : undefined,
      unmodifiedText: useText ? def.text : undefined,
      modifiers: mask,
    });
    await sendCdp(tabId, "Input.dispatchKeyEvent", {
      type: "keyUp",
      key: def.key,
      code: def.code,
      windowsVirtualKeyCode: def.keyCode,
      nativeVirtualKeyCode: def.keyCode,
      modifiers: mask,
    });

    for (const m of [...modsToPress].reverse()) {
      await sendCdp(tabId, "Input.dispatchKeyEvent", {
        type: "keyUp",
        key: m.key,
        code: m.code,
        windowsVirtualKeyCode: m.keyCode,
        nativeVirtualKeyCode: m.keyCode,
        modifiers: 0,
      });
    }

    const modLabel = [...set].join("+");
    return { ok: true, detail: modLabel ? `key ${modLabel}+${def.key}` : `key ${def.key}` };
  } catch (err) {
    const code = err?.code === "debugger_attach_failed" ? "debugger_attach_failed" : "action_failed";
    return { ok: false, error: code, detail: String(err?.message || err) };
  }
}

async function typeTextViaDebugger(tabId, text, clear) {
  try {
    if (clear) {
      await dispatchKeyViaDebugger(tabId, "a", ["ctrl"]);
      await dispatchKeyViaDebugger(tabId, "Backspace", []);
    }
    for (const ch of text) {
      if (ch === "\n" || ch === "\r") {
        await dispatchKeyViaDebugger(tabId, "Enter", []);
        continue;
      }
      await sendCdp(tabId, "Input.dispatchKeyEvent", {
        type: "keyDown",
        key: ch,
        text: ch,
        unmodifiedText: ch,
      });
      await sendCdp(tabId, "Input.dispatchKeyEvent", {
        type: "keyUp",
        key: ch,
      });
    }
    return { ok: true, detail: `typed_${text.length}_chars_focused` };
  } catch (err) {
    const code = err?.code === "debugger_attach_failed" ? "debugger_attach_failed" : "action_failed";
    return { ok: false, error: code, detail: String(err?.message || err) };
  }
}

async function postResult(jobId, payload) {
  await fetch(`${BRIDGE_URL}/result`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ job_id: jobId, ...payload }),
  });
}

async function checkBridgeHealth() {
  try {
    const res = await fetch(`${BRIDGE_URL}/health`, { cache: "no-store" });
    return res.ok;
  } catch {
    return false;
  }
}

chrome.debugger.onDetach.addListener((source) => {
  if (source.tabId != null && source.tabId === debuggerTabId) {
    debuggerTabId = null;
    if (debuggerIdleTimer) {
      clearTimeout(debuggerIdleTimer);
      debuggerIdleTimer = null;
    }
  }
});

chrome.runtime.onMessage.addListener((msg, sender, sendResponse) => {
  if (msg?.type === "hv_health") {
    checkBridgeHealth().then((ok) => sendResponse({ bridge_ok: ok }));
    return true;
  }

  if (msg?.type === "hv_keepalive") {
    (async () => {
      if (!(await isStreamEnabled())) return;
      const tabId = sender.tab?.id;
      if (tabId == null) return;
      const [active] = await chrome.tabs.query({ active: true, currentWindow: true });
      if (!active || active.id !== tabId) return;
      connectCastProducer();
      await streamPushActiveTab();
    })();
    return false;
  }

  return false;
});

chrome.alarms.create("hv-poll", { periodInMinutes: 1 });
chrome.alarms.onAlarm.addListener((alarm) => {
  if (alarm.name === "hv-poll") pollBridge();
});

setInterval(pollBridge, POLL_MS);
pollBridge();

/** Runs inside the page context — must stay self-contained (no outer closures). */
function buildPageMapInPage() {
  const INTERACTIVE =
    "a[href],button,input,textarea,select,[role='button'],[role='link'],[role='textbox'],[contenteditable='true'],[onclick]";
  const viewport = {
    width: window.innerWidth,
    height: window.innerHeight,
    scrollX: window.scrollX,
    scrollY: window.scrollY,
  };
  const elements = [];
  const seen = new Set();

  function shortText(el) {
    const t = (el.innerText || el.value || el.getAttribute("aria-label") || el.title || "").trim();
    return t.length > 120 ? t.slice(0, 117) + "..." : t;
  }

  function cssPath(el) {
    if (el.id) return `#${CSS.escape(el.id)}`;
    const parts = [];
    let node = el;
    while (node && node.nodeType === 1 && parts.length < 6) {
      let part = node.tagName.toLowerCase();
      if (node.id) {
        part += `#${CSS.escape(node.id)}`;
        parts.unshift(part);
        break;
      }
      const parent = node.parentElement;
      if (parent) {
        const siblings = Array.from(parent.children).filter((c) => c.tagName === node.tagName);
        if (siblings.length > 1) {
          part += `:nth-of-type(${siblings.indexOf(node) + 1})`;
        }
      }
      parts.unshift(part);
      node = parent;
    }
    return parts.join(" > ");
  }

  document.querySelectorAll(INTERACTIVE).forEach((el, index) => {
    if (!(el instanceof HTMLElement)) return;
    const style = window.getComputedStyle(el);
    if (style.display === "none" || style.visibility === "hidden" || style.opacity === "0") return;
    const rect = el.getBoundingClientRect();
    if (rect.width < 2 || rect.height < 2) return;
    if (rect.bottom < 0 || rect.right < 0 || rect.top > viewport.height || rect.left > viewport.width) return;
    const key = `${el.tagName}|${rect.x}|${rect.y}|${shortText(el)}`;
    if (seen.has(key)) return;
    seen.add(key);
    elements.push({
      index,
      tag: el.tagName.toLowerCase(),
      text: shortText(el),
      id: el.id || null,
      name: el.getAttribute("name"),
      type: el.getAttribute("type"),
      href: el.tagName === "A" ? el.getAttribute("href") : null,
      role: el.getAttribute("role"),
      selector: cssPath(el),
      bounds: {
        x: Math.round(rect.x),
        y: Math.round(rect.y),
        width: Math.round(rect.width),
        height: Math.round(rect.height),
      },
      center: {
        x: Math.round(rect.x + rect.width / 2),
        y: Math.round(rect.y + rect.height / 2),
      },
    });
  });

  return {
    url: location.href,
    title: document.title,
    viewport,
    elementCount: elements.length,
    elements: elements.slice(0, 200),
  };
}
