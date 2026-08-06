document.addEventListener("DOMContentLoaded", () => {
  const status = document.getElementById("status");
  chrome.runtime.sendMessage({ type: "hv_health" }, (res) => {
    if (res?.bridge_ok) {
      status.textContent = "connected :17891";
      status.className = "ok";
    } else {
      status.textContent = "bridge offline";
      status.className = "bad";
    }
  });
});
