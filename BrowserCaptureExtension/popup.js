document.addEventListener("DOMContentLoaded", () => {
  const status = document.getElementById("status");
  chrome.runtime.sendMessage({ type: "hv_health" }, (res) => {
    if (res?.bridge_ok) {
      status.textContent = "connected :17920";
      status.className = "ok";
    } else {
      status.textContent = "bridge offline";
      status.className = "bad";
    }
  });
});
