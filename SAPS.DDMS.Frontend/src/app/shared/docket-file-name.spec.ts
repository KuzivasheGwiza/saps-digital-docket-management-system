import {
  formatDocketDisplayName,
  getDocketFileStem,
  getDocketOwnerName,
  sanitiseDocketFileStem
} from './docket-file-name';

describe('docket file naming', () => {
  it('uses the complainant first name and surname as the case-file name', () => {
    const docket = {
      docketID: 17,
      docketNumber: 'SAPS-10-20260809-12345',
      complainantFullName: '  Thabo   Mokoena  '
    } as any;

    expect(getDocketOwnerName(docket)).toBe('Thabo Mokoena');
    expect(getDocketFileStem(docket)).toBe('Thabo Mokoena');
    expect(formatDocketDisplayName(docket)).toBe('Thabo Mokoena — SAPS-10-20260809-12345');
  });

  it('falls back deterministically to the docket number when a legacy record has no complainant', () => {
    const docket = {
      docketID: 17,
      docketNumber: 'SAPS-10-20260809-12345',
      complainantFullName: '   '
    } as any;

    expect(getDocketOwnerName(docket)).toBe('SAPS-10-20260809-12345');
    expect(getDocketFileStem(docket)).toBe('SAPS-10-20260809-12345');
  });

  it('replaces path and control characters and protects reserved Windows names', () => {
    expect(sanitiseDocketFileStem('Jane / Smith:*?', 'CASE-1')).toBe('Jane Smith');
    expect(sanitiseDocketFileStem('CON', 'CASE-1')).toBe('Docket CON');
    expect(sanitiseDocketFileStem('../../', 'CASE/2026/1')).toBe('CASE 2026 1');
  });

  it('uses a stable docket-id fallback only when both names are absent', () => {
    expect(getDocketFileStem({ docketID: 42, docketNumber: '', complainantFullName: '' } as any))
      .toBe('Docket 42');
  });
});
