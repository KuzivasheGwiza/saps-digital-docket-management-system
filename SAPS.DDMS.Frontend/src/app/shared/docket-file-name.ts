import { Docket } from '../models/docket.model';

type DocketIdentity = Pick<Docket, 'docketID' | 'docketNumber' | 'complainantFullName'>;

const WINDOWS_RESERVED_FILE_NAME = /^(?:con|prn|aux|nul|com[1-9]|lpt[1-9])(?:\.|$)/i;
const MAX_FILE_STEM_LENGTH = 100;

function normaliseLabel(value: string | null | undefined): string {
  return (value ?? '').normalize('NFKC').replace(/\s+/g, ' ').trim();
}

function fallbackDocketLabel(docket: DocketIdentity | null | undefined): string {
  const docketNumber = normaliseLabel(docket?.docketNumber);
  if (docketNumber) return docketNumber;

  const docketId = Number(docket?.docketID);
  return Number.isInteger(docketId) && docketId > 0 ? `Docket ${docketId}` : 'Docket';
}

/**
 * Returns the person whose name should identify the case file. The official
 * docket number remains the deterministic fallback for legacy/incomplete data.
 */
export function getDocketOwnerName(docket: DocketIdentity | null | undefined): string {
  return normaliseLabel(docket?.complainantFullName) || fallbackDocketLabel(docket);
}

/** Formats selectors and headings with the person's name first. */
export function formatDocketDisplayName(docket: DocketIdentity | null | undefined): string {
  if (!docket) return '';

  const ownerName = getDocketOwnerName(docket);
  const docketNumber = normaliseLabel(docket.docketNumber);
  return docketNumber && docketNumber !== ownerName
    ? `${ownerName} — ${docketNumber}`
    : ownerName;
}

/**
 * Produces a Windows-, browser- and ZIP-safe file stem. Invalid filename
 * characters are replaced rather than removed so separate names cannot be
 * accidentally joined together.
 */
export function sanitiseDocketFileStem(value: string | null | undefined, fallback: string): string {
  const clean = (candidate: string | null | undefined): string => {
    const safe = normaliseLabel(candidate)
      .replace(/[<>:"/\\|?*\u0000-\u001f]/g, ' ')
      .replace(/\s+/g, ' ')
      .replace(/^\.+/, '')
      .replace(/[. ]+$/, '')
      .trim();

    return Array.from(safe).slice(0, MAX_FILE_STEM_LENGTH).join('').replace(/[. ]+$/, '');
  };

  let result = clean(value) || clean(fallback) || 'Docket';
  if (WINDOWS_RESERVED_FILE_NAME.test(result)) result = `Docket ${result}`;
  return result;
}

/** Returns a safe person-based stem, falling back to the official docket number. */
export function getDocketFileStem(docket: DocketIdentity | null | undefined): string {
  return sanitiseDocketFileStem(
    docket?.complainantFullName,
    fallbackDocketLabel(docket)
  );
}
