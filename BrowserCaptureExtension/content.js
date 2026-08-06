/**
 * Builds an interactive element map for the active tab (viewport-relative bounds).
 * Injected on demand by the background service worker.
 */
(function buildPageMap() {
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
          const idx = siblings.indexOf(node) + 1;
          part += `:nth-of-type(${idx})`;
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
})();
