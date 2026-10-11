// Prefer the backend directly for localhost browser sessions because the local
// dev proxy can leave long-running JSON requests pending on some heavy screens
// such as Dockets and Calendar. Non-localhost and deployed builds continue to
// use the relative same-origin API path.
const isBrowser = typeof window !== 'undefined';
const isLocalhostDev =
  isBrowser &&
  window.location.hostname.toLowerCase() === 'localhost' &&
  window.location.port === '4200';

export const API_URL = isLocalhostDev
  ? 'http://localhost:5043/api'
  : '/api';
