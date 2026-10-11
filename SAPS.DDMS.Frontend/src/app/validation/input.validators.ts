import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

/**
 * Validates user-entered text after trimming leading/trailing whitespace.
 * Angular's built-in required/minLength validators intentionally count spaces,
 * which otherwise allows values such as "          " to reach the API.
 */
export function trimmedTextValidator(minLength = 1, maxLength?: number): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const rawValue = control.value;
    const value = typeof rawValue === 'string' ? rawValue.trim() : '';

    if (!value) {
      return { required: true };
    }
    if (value.length < minLength) {
      return { minlength: { requiredLength: minLength, actualLength: value.length } };
    }
    if (maxLength !== undefined && value.length > maxLength) {
      return { maxlength: { requiredLength: maxLength, actualLength: value.length } };
    }

    return null;
  };
}

/** Allows an empty optional field but validates the trimmed value when supplied. */
export function optionalTrimmedMaxLengthValidator(maxLength: number): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    if (control.value === null || control.value === undefined || `${control.value}`.trim() === '') {
      return null;
    }

    const value = `${control.value}`.trim();
    return value.length <= maxLength
      ? null
      : { maxlength: { requiredLength: maxLength, actualLength: value.length } };
  };
}

/** Rejects malformed dates and values later than the instant when validation runs. */
export const notFutureDateValidator: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  if (control.value === null || control.value === undefined || control.value === '') {
    return null;
  }

  const value = new Date(control.value);
  if (Number.isNaN(value.getTime())) {
    return { invalidDate: true };
  }

  return value.getTime() <= Date.now() ? null : { futureDate: true };
};

/** Ensures a South African ID number starts with the supplied birth date (YYMMDD). */
export const southAfricanIdBirthDateValidator: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const idNumber = `${control.get('idNumber')?.value ?? ''}`.trim();
  const dateOfBirth = `${control.get('dateOfBirth')?.value ?? ''}`.trim();
  if (!/^\d{13}$/.test(idNumber) || !/^\d{4}-\d{2}-\d{2}$/.test(dateOfBirth)) return null;

  const expectedPrefix = `${dateOfBirth.slice(2, 4)}${dateOfBirth.slice(5, 7)}${dateOfBirth.slice(8, 10)}`;
  return idNumber.startsWith(expectedPrefix) ? null : { idBirthDateMismatch: true };
};

/** Prevents zero, negative, fractional, infinite and non-numeric quantities. */
export const positiveIntegerValidator: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  if (control.value === null || control.value === undefined || control.value === '') {
    return null;
  }

  const value = Number(control.value);
  return Number.isSafeInteger(value) && value > 0 ? null : { positiveInteger: true };
};

/** Password rule shared by activation, reset and change-password workflows. */
export const strongPasswordValidator: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const value = `${control.value ?? ''}`;
  if (!value) {
    return null;
  }

  const strong = value.length >= 12
    && /[A-Z]/.test(value)
    && /[a-z]/.test(value)
    && /\d/.test(value)
    && /[^A-Za-z0-9]/.test(value);

  return strong ? null : { complexity: true };
};
