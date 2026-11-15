// App shell behaviour: mobile drawer, server status pill, UTC clock, active nav.
// The markup lives in index.html; pages never touch it.
import { ROUTE_CHANGE_EVENT } from "./router.js";

const STATUS_URL = "/api/status";
const STATUS_INTERVAL_MS = 30_000;
const STATUS_TIMEOUT_MS = 5_000;
const CLOCK_INTERVAL_MS = 30_000;
const DESKTOP_QUERY = "(min-width: 1024px)";

const STATUS_VIEW = {
  checking: { text: "Checking server", tone: "chip--muted" },
  online: { text: "Server online", tone: "chip--ok" },
  offline: { text: "Server offline", tone: "chip--critical" },
};

export function initShell() {
  initSkipLink();
  initDrawer();
  initNav();
  initClock();
  initServerStatus();
}

/* ---------- Skip link ---------- */

// The router owns location.hash, so href="#app" must not change it.
function initSkipLink() {
  const link = document.querySelector(".skip-link");
  const content = document.getElementById("app");
  link?.addEventListener("click", (event) => {
    event.preventDefault();
    content.focus();
  });
}

/* ---------- Drawer (below 1024px) ---------- */

function initDrawer() {
  const shell = document.getElementById("shell");
  const sidebar = document.getElementById("sidebar");
  const main = document.getElementById("shell-main");
  const backdrop = document.getElementById("drawer-backdrop");
  const openButton = document.getElementById("drawer-open");
  const closeButton = document.getElementById("drawer-close");
  const desktop = window.matchMedia(DESKTOP_QUERY);

  const isOpen = () => shell.classList.contains("is-drawer-open");

  function open() {
    shell.classList.add("is-drawer-open");
    openButton.setAttribute("aria-expanded", "true");
    backdrop.hidden = false;
    main.inert = true;
    document.body.style.overflow = "hidden";
    (sidebar.querySelector(".nav-link.is-active") ?? sidebar.querySelector(".nav-link"))?.focus();
  }

  function close({ returnFocus = false } = {}) {
    if (!isOpen()) return;
    shell.classList.remove("is-drawer-open");
    openButton.setAttribute("aria-expanded", "false");
    backdrop.hidden = true;
    main.inert = false;
    document.body.style.overflow = "";
    if (returnFocus) openButton.focus();
  }

  openButton.addEventListener("click", () => (isOpen() ? close() : open()));
  closeButton.addEventListener("click", () => close({ returnFocus: true }));
  backdrop.addEventListener("click", () => close({ returnFocus: true }));
  sidebar.addEventListener("click", (event) => {
    if (!event.target.closest(".nav-link") || !isOpen()) return;
    close();
    // The sidebar is about to be hidden: keep focus in the page, not on <body>.
    document.getElementById("app").focus({ preventScroll: true });
  });
  // Any route change (nav click, Back/Forward, code) closes the drawer.
  window.addEventListener(ROUTE_CHANGE_EVENT, () => close());
  document.addEventListener("keydown", (event) => {
    if (event.key === "Escape" && isOpen()) close({ returnFocus: true });
  });
  desktop.addEventListener("change", (event) => {
    if (event.matches) close();
  });
}

/* ---------- Active nav ---------- */

function initNav() {
  const links = [...document.querySelectorAll(".sidebar__nav [data-route]")];

  window.addEventListener(ROUTE_CHANGE_EVENT, (event) => {
    const { path } = event.detail;
    let activeLabel = "";
    for (const link of links) {
      const active = link.dataset.route === path;
      link.classList.toggle("is-active", active);
      if (active) {
        link.setAttribute("aria-current", "page");
        activeLabel = link.querySelector(".nav-link__label")?.textContent.trim() ?? "";
      } else {
        link.removeAttribute("aria-current");
      }
    }
    document.title = activeLabel ? `${activeLabel} · BLINDSPOT` : "BLINDSPOT";
  });
}

/* ---------- UTC clock ---------- */

function initClock() {
  const clock = document.getElementById("utc-clock");
  const dateEl = document.createElement("span");
  const timeEl = document.createElement("span");
  dateEl.className = "topbar__date";
  clock.replaceChildren(dateEl, timeEl);
  const pad = (n) => String(n).padStart(2, "0");
  const months = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

  function tick() {
    const now = new Date();
    const date = `${pad(now.getUTCDate())} ${months[now.getUTCMonth()]} ${now.getUTCFullYear()}`;
    const time = `${pad(now.getUTCHours())}:${pad(now.getUTCMinutes())}`;
    clock.dateTime = now.toISOString();
    dateEl.textContent = `${date} · `;
    timeEl.textContent = time;
  }

  tick();
  setInterval(tick, CLOCK_INTERVAL_MS);
}

/* ---------- Server status pill ---------- */

function initServerStatus() {
  const pill = document.getElementById("server-status");
  let current = "checking";

  function show(state) {
    if (state === current) return; // role="status": only announce real changes
    current = state;
    const view = STATUS_VIEW[state];
    pill.className = `chip ${view.tone}`;
    pill.textContent = view.text;
  }

  async function check() {
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), STATUS_TIMEOUT_MS);
    try {
      const response = await fetch(STATUS_URL, {
        headers: { Accept: "application/json" },
        cache: "no-store",
        signal: controller.signal,
      });
      show(response.ok ? "online" : "offline");
    } catch {
      show("offline");
    } finally {
      clearTimeout(timer);
    }
  }

  check();
  setInterval(check, STATUS_INTERVAL_MS);
}
