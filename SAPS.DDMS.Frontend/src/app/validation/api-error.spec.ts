import { getApiErrorMessage } from './api-error';

describe('getApiErrorMessage', () => {
  it('uses explicit server messages', () => {
    expect(getApiErrorMessage({ error: { message: 'Officer has reached the configured maximum.' } }, 'Failed'))
      .toBe('Officer has reached the configured maximum.');
  });

  it('flattens ASP.NET validation errors with field names', () => {
    expect(getApiErrorMessage({ error: { errors: { firstName: ['First name is required.'] } } }, 'Failed'))
      .toBe('First Name: First name is required.');
  });

  it('explains permission and network failures', () => {
    expect(getApiErrorMessage({ status: 403 }, 'Evidence could not be saved'))
      .toContain('role is not authorised');
    expect(getApiErrorMessage({ status: 0 }, 'Evidence could not be saved'))
      .toContain('server could not be reached');
  });
});
