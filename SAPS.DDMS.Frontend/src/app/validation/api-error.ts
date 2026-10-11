/** Converts common ASP.NET API error shapes into a safe, actionable UI message. */
export function getApiErrorMessage(error: any, fallback: string): string {
  const payload = error?.error;
  const directMessage = typeof payload === 'string'
    ? payload.trim()
    : typeof payload?.message === 'string'
      ? payload.message.trim()
      : typeof payload?.title === 'string'
        ? payload.title.trim()
        : '';

  if (directMessage) {
    return directMessage;
  }

  if (payload?.errors && typeof payload.errors === 'object') {
    const details = Object.entries(payload.errors)
      .flatMap(([field, messages]) => {
        const values = Array.isArray(messages) ? messages : [messages];
        return values
          .filter(message => typeof message === 'string' && message.trim())
          .map(message => `${humaniseField(field)}: ${String(message).trim()}`);
      });
    if (details.length) {
      return details.join(' ');
    }
  }

  if (error?.status === 0) {
    return `${fallback} because the server could not be reached. Check the connection and try again.`;
  }
  if (error?.status === 401) {
    return `${fallback} because your session is no longer valid. Sign in again and retry.`;
  }
  if (error?.status === 403) {
    return `${fallback} because your role is not authorised to perform this action.`;
  }
  if (error?.status === 404) {
    return `${fallback} because the requested record no longer exists or is outside your station access.`;
  }
  if (error?.status === 409) {
    return `${fallback} because it conflicts with an existing or recently changed record. Refresh and try again.`;
  }
  if (error?.status >= 500) {
    return `${fallback} because the server encountered an unexpected error. No changes were confirmed; try again shortly.`;
  }

  return fallback;
}

function humaniseField(field: string): string {
  const leaf = field.split('.').pop() || field;
  return leaf
    .replace(/([a-z])([A-Z])/g, '$1 $2')
    .replace(/^./, character => character.toUpperCase());
}
