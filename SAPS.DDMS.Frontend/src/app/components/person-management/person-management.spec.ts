import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { NEVER, of } from 'rxjs';
import { ToastrService } from 'ngx-toastr';
import { PersonManagement } from './person-management';
import { PersonService } from '../../services/person';
import { DocketService } from '../../services/docket';
import { AuthService } from '../../services/auth';

describe('PersonManagement', () => {
  let component: PersonManagement;
  let fixture: ComponentFixture<PersonManagement>;
  let personService: any;

  beforeEach(async () => {
    personService = {
      getAllPersons: vi.fn().mockReturnValue(of([])),
      searchPersons: vi.fn().mockReturnValue(of([])),
      getPersonRoles: vi.fn().mockReturnValue(of([])),
      createPerson: vi.fn().mockReturnValue(NEVER),
      updatePerson: vi.fn().mockReturnValue(NEVER),
      linkPersonToDocket: vi.fn().mockReturnValue(NEVER)
    };
    await TestBed.configureTestingModule({
      imports: [RouterTestingModule, PersonManagement],
      providers: [
        { provide: PersonService, useValue: personService },
        { provide: DocketService, useValue: { getDockets: vi.fn().mockReturnValue(of([])) } },
        { provide: AuthService, useValue: { getCurrentUser: vi.fn().mockReturnValue({ roleName: 'System Administrator' }) } },
        { provide: ToastrService, useValue: { success: vi.fn(), error: vi.fn(), warning: vi.fn() } }
      ]
    }).compileComponents();
    fixture = TestBed.createComponent(PersonManagement);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('creates and loads person reference data', () => {
    expect(component).toBeTruthy();
    expect(component.linkForm.contains('relationshipRole')).toBe(true);
  });

  it('opens the create workflow', () => {
    component.openCreateModal();
    expect(component.showCreateModal).toBe(true);
  });

  it('rejects whitespace-only names and a future date of birth', () => {
    component.createForm.patchValue({ firstName: '   ', lastName: 'Citizen' });
    expect(component.createForm.get('firstName')?.hasError('required')).toBe(true);

    component.createForm.patchValue({
      firstName: 'Test',
      dateOfBirth: new Date(Date.now() + 86_400_000).toISOString()
    });
    expect(component.createForm.get('dateOfBirth')?.hasError('futureDate')).toBe(true);
  });

  it('normalises person fields and blocks a duplicate create request', () => {
    component.createForm.patchValue({
      firstName: '  Test  ',
      lastName: '  Citizen ',
      emailAddress: ' TEST@EXAMPLE.COM ',
      physicalAddress: '  1 Main Road ',
      phoneNumber: '0821234567',
      idNumber: '9001015009087',
      dateOfBirth: '1990-01-01',
      personRoleID: 1
    });

    component.createPerson();
    component.createPerson();

    expect(personService.createPerson).toHaveBeenCalledTimes(1);
    expect(personService.createPerson).toHaveBeenCalledWith(expect.objectContaining({
      firstName: 'Test',
      lastName: 'Citizen',
      emailAddress: 'test@example.com',
      physicalAddress: '1 Main Road'
    }));
  });
});
