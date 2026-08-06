// Nudges the background worker to cast the active tab. Stops cleanly after extension reload.
(function () {
  let timer = null;

  function extensionAlive() {
    try {
      return typeof chrome !== "undefined" && Boolean(chrome.runtime?.id);
    } catch {
      return false;
    }
  }

  function stop() {
    if (timer != null) {
      clearInterval(timer);
      timer = null;
    }
  }

  function tick() {
    if (!extensionAlive()) {
      stop();
      return;
    }

    try {
      chrome.runtime.sendMessage({ type: "hv_keepalive" }, () => {
        // Swallow "Receiving end does not exist" / invalidated context quietly.
        void chrome.runtime?.lastError;
      });
    } catch {
      stop();
    }
  }

  if (extensionAlive()) {
    timer = setInterval(tick, 800);
    tick();
  }
})();
