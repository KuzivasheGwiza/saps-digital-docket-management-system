import { FormControl, FormGroup } from '@angular/forms';
import {
  notFutureDateValidator,
  optionalTrimmedMaxLengthValidator,
  positiveIntegerValidator,
  southAfricanIdBirthDateValidator,
  strongPasswordValidator,
  trimmedTextValidator
} from './input.validators';

describe('input validators', () => {
  it('rejects whitespace and evaluates text lengths after trimming', () => {
    const validator = trimmedTextValidator(3, 5);

    expect(validator(new FormControl('   '))).toEqual({ required: true });
    expect(validator(new FormControl('  ab  '))?.['minlength']).toBeTruthy();
    expect(validator(new FormControl('  abc  '))).toBeNull();
    expect(validator(new FormControl('abcdef'))?.['maxlength']).toBeTruthy();
  });

  it('allows blank optional text but caps supplied text', () => {
    const validator = optionalTrimmedMaxLengthValidator(4);

    expect(validator(new FormControl('  '))).toBeNull();
    expect(validator(new FormControl('abcd'))).toBeNull();
    expect(validator(new FormControl('abcde'))?.['maxlength']).toBeTruthy();
  });

  it('rejects invalid and future dates', () => {
    expect(notFutureDateValidator(new FormControl('not-a-date'))).toEqual({ invalidDate: true });
    expect(notFutureDateValidator(new FormControl(new Date(Date.now() + 60_000).toISOString()))).toEqual({ futureDate: true });
    expect(notFutureDateValidator(new FormControl(new Date(Date.now() - 60_000).toISOString()))).toBeNull();
  });

  it('accepts only positive safe integers', () => {
    for (const invalid of [0, -1, 1.5, Number.POSITIVE_INFINITY, 'abc']) {
      expect(positiveIntegerValidator(new FormControl(invalid))).toEqual({ positiveInteger: true });
    }
    expect(positiveIntegerValidator(new FormControl(1))).toBeNull();
  });

  it('requires a South African ID birth-date prefix to match the captured date', () => {
    const form = new FormGroup({
      idNumber: new FormControl('9001015009087'),
      dateOfBirth: new FormControl('1990-01-01')
    }, southAfricanIdBirthDateValidator);

    expect(form.errors).toBeNull();
    form.get('dateOfBirth')?.setValue('1991-01-01');
    expect(form.errors).toEqual({ idBirthDateMismatch: true });
  });

  it('requires all password complexity classes', () => {
    expect(strongPasswordValidator(new FormControl('longbutweakpassword'))).toEqual({ complexity: true });
    expect(strongPasswordValidator(new FormControl('StrongPass12!'))).toBeNull();
  });
});
