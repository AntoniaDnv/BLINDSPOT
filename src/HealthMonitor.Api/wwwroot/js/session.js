// Session + active crew member. Loaded once from /api/auth/me and cached.
// Astronaut: the active crew is always their own crewId.
// Flight surgeon: the chosen crew member (remembered in localStorage), default = first crew.
// Pages: call getActiveCrewId() for every data fetch (pass it as ?crewId=). The router
// re-renders the current page when CREW_CHANGE_EVENT ("crewchange") fires, so pages do not
// need their own listener (they may add one for extra work).

export const CREW_CHANGE_EVENT = "crewchange";
export const SESSION_CHANGE_EVENT = "app:sessionchange";
const STORAGE_KEY = "blindspot.activeCrewId";

let session = null; // { email, displayName, role, crewId } or null
let crewList = []; // flight surgeon only: [{ crewId, displayName, hasVitals, ... }]
let activeCrewId = null;
let loadPromise = null;

async function getJson(url, options) {
  const response = await fetch(url, {
    headers: { Accept: "application/json", "Content-Type": "application/json" },
    ...options,
  });
  const isJson = response.headers.get("content-type")?.includes("json");
  const data = response.status === 204 ? null : isJson ? await response.json() : null;
  return { response, data };
}

function readStored() {
  try {
    return localStorage.getItem(STORAGE_KEY);
  } catch {
    return null;
  }
}

function writeStored(id) {
  try {
    localStorage.setItem(STORAGE_KEY, id);
  } catch {
    /* storage unavailable: the choice just is not remembered */
  }
}

async function applySession(me) {
  session = me;
  crewList = [];
  activeCrewId = me?.crewId ?? null;
  if (me?.role === "flight_surgeon") {
    const { response, data } = await getJson("/api/crew");
    crewList = response.ok && Array.isArray(data) ? data : [];
    const stored = readStored();
    activeCrewId =
      crewList.find((c) => c.crewId === stored)?.crewId ?? crewList[0]?.crewId ?? null;
  }
  window.dispatchEvent(new CustomEvent(SESSION_CHANGE_EVENT));
}

/** Resolves to the session object, or null when signed out. Cached after the first call. */
export function loadSession(force = false) {
  if (force || !loadPromise) {
    loadPromise = (async () => {
      try {
        const { response, data } = await getJson("/api/auth/me");
        await applySession(response.ok ? data : null);
      } catch (error) {
        loadPromise = null; // server unreachable: try again next time
        throw error;
      }
      return session;
    })();
  }
  return loadPromise;
}

export const getSession = () => session;
export const isFlightSurgeon = () => session?.role === "flight_surgeon";
export const getCrewList = () => crewList;
export const getActiveCrewId = () => activeCrewId;

export function setActiveCrewId(id) {
  if (!isFlightSurgeon() || id === activeCrewId) return;
  if (!crewList.some((c) => c.crewId === id)) return;
  activeCrewId = id;
  writeStored(id);
  window.dispatchEvent(new CustomEvent(CREW_CHANGE_EVENT, { detail: { crewId: id } }));
}

/** Forget the session (sign out, or the API answered 401). */
export function clearSession() {
  session = null;
  crewList = [];
  activeCrewId = null;
  loadPromise = Promise.resolve(null);
  window.dispatchEvent(new CustomEvent(SESSION_CHANGE_EVENT));
}

async function authPost(url, body) {
  const { response, data } = await getJson(url, { method: "POST", body: JSON.stringify(body) });
  if (!response.ok) {
    const message =
      data?.error ||
      (data?.errors && Object.values(data.errors).flat().join(" ")) ||
      data?.title ||
      `Request failed (${response.status})`;
    const error = new Error(message);
    error.status = response.status;
    throw error;
  }
  return data;
}

function describeNetwork(error) {
  if (error.status) return error;
  return new Error("The BLINDSPOT server is not responding. Start it with `dotnet run`, then retry.");
}

export async function login(email, password) {
  try {
    const me = await authPost("/api/auth/login", { email, password });
    loadPromise = Promise.resolve(me);
    await applySession(me);
    return me;
  } catch (error) {
    throw describeNetwork(error);
  }
}

export async function signup(displayName, email, password) {
  try {
    const me = await authPost("/api/auth/signup", { displayName, email, password });
    loadPromise = Promise.resolve(me);
    await applySession(me);
    return me;
  } catch (error) {
    throw describeNetwork(error);
  }
}

export async function logout() {
  try {
    await getJson("/api/auth/logout", { method: "POST" });
  } catch {
    /* even if the request fails, drop the local session */
  }
  clearSession();
}
