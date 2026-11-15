import { RELOAD_EVENT, renderErrorState } from "./utils.js";
import { CREW_CHANGE_EVENT, isFlightSurgeon, loadSession } from "./session.js";

const routes = {
  "/": "home",
  "/blood-panels": "blood-panels",
  "/nutrition": "nutrition",
  "/radiation": "radiation",
  "/log": "log",
  "/crew": "crew", // flight surgeon only
  // '/dashboard': 'dashboard',
};

// Auth screens render full-screen in #auth-root, without the shell.
const AUTH_ROUTES = { "/login": "login", "/signup": "signup" };
const SURGEON_ROUTES = new Set(["/crew"]);

const NOT_FOUND_PAGE = "not-found";

/**
 * Fired on window at the start of every render with detail { path, pageName }.
 * js/shell.js uses it to highlight the nav and to close the mobile drawer on
 * every route change (nav click, Back/Forward, navigate()).
 */
export const ROUTE_CHANGE_EVENT = "app:routechange";

const templateCache = new Map();

let currentPage = null;
let appRoot = null;
let authRoot = null;
let renderId = 0;

export function startRouter(root, authContainer) {
  appRoot = root;
  authRoot = authContainer;
  window.addEventListener("hashchange", render);
  window.addEventListener(RELOAD_EVENT, render); // Retry buttons re-run the page
  // Another crew member was picked: re-run the current page so it refetches with the new crewId.
  window.addEventListener(CREW_CHANGE_EVENT, render);
  render();
}

/** Navigate from code, e.g. navigate('/crew'). */
export function navigate(path) {
  window.location.hash = `#${path}`;
}

function setLayout(auth) {
  document.body.classList.toggle("is-auth", auth);
  document.getElementById("shell").hidden = auth;
  authRoot.hidden = !auth;
}

/** Query params of the current hash, e.g. "#/login?next=%2Fcrew" -> URLSearchParams. */
export function getQuery() {
  return new URLSearchParams(window.location.hash.split("?")[1] ?? "");
}

/** Only allow in-app paths as a post-login target (no open redirects). */
export function safeNext(value) {
  if (!value || !value.startsWith("/") || value.startsWith("//")) return "/";
  const path = value.split("?")[0];
  return path in AUTH_ROUTES || value.startsWith("/login") ? "/" : value;
}

async function render() {
  const myId = ++renderId;
  const path = getPath();
  const authPage = AUTH_ROUTES[path];

  let session;
  try {
    session = await loadSession();
  } catch (err) {
    if (myId !== renderId) return;
    currentPage?.destroy?.();
    currentPage = null;
    setLayout(true);
    renderErrorState(authRoot, { title: "Cannot reach BLINDSPOT", error: err, onRetry: render });
    return;
  }
  if (myId !== renderId) return;

  // Route guards.
  if (authPage && session) {
    navigate(safeNext(getQuery().get("next")));
    return;
  }
  if (!authPage && !session) {
    navigate(`/login?next=${encodeURIComponent(window.location.hash.slice(1) || "/")}`);
    return;
  }
  if (SURGEON_ROUTES.has(path) && !isFlightSurgeon()) {
    navigate("/");
    return;
  }

  const pageName = authPage ?? routes[path] ?? NOT_FOUND_PAGE;
  const root = authPage ? authRoot : appRoot;
  setLayout(Boolean(authPage));

  window.dispatchEvent(
    new CustomEvent(ROUTE_CHANGE_EVENT, { detail: { path, pageName } }),
  );

  try {
    currentPage?.destroy?.();
    currentPage = null;

    const html = await loadTemplate(pageName);
    if (myId !== renderId) return; // a newer navigation started meanwhile
    root.innerHTML = html;

    const module = await import(`./pages/${pageName}/${pageName}.js`);
    if (myId !== renderId) return;
    currentPage = module;
    await module.init?.(root);

    if (myId === renderId) window.scrollTo(0, 0);
  } catch (err) {
    if (myId !== renderId) return;
    console.error(`Failed to load page "${pageName}"`, err);
    renderErrorState(root, {
      title: "This page could not be loaded",
      error: err,
      message:
        err instanceof TypeError
          ? undefined
          : `The page "${pageName}" failed to load. Details are in the browser console.`,
      onRetry: render,
    });
  }
}

function getPath() {
  // "#/crew?x=1" -> "/crew"
  const hash = window.location.hash.slice(1) || "/";
  return hash.split("?")[0] || "/";
}

async function loadTemplate(pageName) {
  if (templateCache.has(pageName)) {
    return templateCache.get(pageName);
  }

  const response = await fetch(`/js/pages/${pageName}/${pageName}.html`);
  if (!response.ok) {
    throw new Error(
      `Template not found for page "${pageName}" (HTTP ${response.status})`,
    );
  }

  const html = await response.text();
  templateCache.set(pageName, html);
  return html;
}
