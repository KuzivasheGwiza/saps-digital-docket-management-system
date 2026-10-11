import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { FaqService, Faq } from '../../services/faq';

interface HelpTopic {
  id: string;
  title: string;
  audience: string;
  keywords: string;
  steps: string[];
}

@Component({
  selector: 'app-help',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './help.html',
  styleUrls: ['./help.css']
})
export class Help implements OnInit {
  private route = inject(ActivatedRoute);
  private faqService = inject(FaqService);
  private cdr = inject(ChangeDetectorRef);

  searchTerm = '';
  selectedTopicId = 'dashboard';
  faqs: Faq[] = [];

  topics: HelpTopic[] = [
    {
      id: 'dashboard',
      title: 'Dashboard and Graphs',
      audience: 'All signed-in users',
      keywords: 'dashboard graphs charts statistics recent dockets',
      steps: [
        'Use the dashboard to view total, open, allocated and investigation docket counts.',
        'Use the status chart to compare docket status distribution.',
        'Use the monthly trend chart to monitor docket activity over time.',
        'Open recent dockets from the table using the view action.'
      ]
    },
    {
      id: 'users',
      title: 'User Management',
      audience: 'System Administrator',
      keywords: 'admin users records clerk prosecutor activation roles permissions',
      steps: [
        'Open User Management to create, view or update user accounts.',
        'Select a role tab before creating a user to preselect that role.',
        'Create users for System Administrator, DSC Supervisor, Investigating Officer, CSC Officer, Docket Administrator, Records Clerk and Prosecutor.',
        'Use the activation link shown in the modal if an activation email cannot be delivered.'
      ]
    },
    {
      id: 'dockets',
      title: 'Docket Management',
      audience: 'CSC Officer, DSC Supervisor, Investigating Officer, Docket Administrator, Records Clerk',
      keywords: 'docket create read update allocate status case incident',
      steps: [
        'CSC Officers create new dockets from the Dockets area.',
        'Supervisors allocate dockets to investigating officers.',
        'Authorized users can search and read docket information according to their role.',
        'Use the action buttons to view, update or allocate where your role allows it.'
      ]
    },
    {
      id: 'calendar',
      title: 'Docket Calendar',
      audience: 'Docket users',
      keywords: 'calendar month docket allocation inspection due dates',
      steps: [
        'Open Calendar to view docket events in a monthly grid.',
        'Use the previous, today, and next controls to move between months.',
        'Select a docket event to open that docket.',
        'Calendar entries show created, allocated, and inspection due dates where available.'
      ]
    },
    {
      id: 'reports',
      title: 'Reports',
      audience: 'Administrators, supervisors, records clerks and prosecutors',
      keywords: 'reports pdf simple transactional criteria management graph totals',
      steps: [
        'Open Reports to select a simple list, transactional, criteria, or management report.',
        'Use the Docket Criteria filters to adjust dates and status.',
        'Review totals and management bars on screen.',
        'Use the PDF action to export the selected report with date and user details.'
      ]
    },
    {
      id: 'sms',
      title: 'SMS Queue',
      audience: 'System Administrator and DSC Supervisor',
      keywords: 'sms otp queue dispatch gateway communications',
      steps: [
        'Open SMS Queue to review generated OTP and notification messages.',
        'Messages remain in LocalQueue mode until a communications officer dispatches them or a gateway is configured.',
        'Use Dispatched after a message has been sent through the approved SAPS communication channel.',
        'Cancelled SMS records remain visible for audit purposes.'
      ]
    },
    {
      id: 'persons',
      title: 'Person Management',
      audience: 'CSC Officer, Investigating Officer, DSC Supervisor, Docket Administrator',
      keywords: 'person complainant suspect witness victim link docket search details',
      steps: [
        'Register a person with a role such as complainant, suspect, witness or victim.',
        'Search with at least two characters to find registered persons.',
        'Use the view action to load the full person record.',
        'Link a person to a docket where your role has permission.'
      ]
    },
    {
      id: 'evidence',
      title: 'Evidence',
      audience: 'Investigating Officer, DSC Supervisor, Docket Administrator, Records Clerk',
      keywords: 'evidence image upload file photo read update',
      steps: [
        'Investigating Officers add evidence to authorized dockets.',
        'Evidence photos are uploaded with the evidence record and stored by the API.',
        'Records Clerks and supervisors can read evidence where their role permits.',
        'Docket Administrators can update evidence metadata where allowed.'
      ]
    },
    {
      id: 'investigation',
      title: 'Investigation Notes',
      audience: 'Investigating Officer, DSC Supervisor, Docket Administrator',
      keywords: 'investigation notes attachment pdf image versions',
      steps: [
        'Open investigation notes from a docket that has an investigation record.',
        'Investigating Officers create notes and attach PDF or image files.',
        'Authorized users can read notes according to assigned docket permissions.',
        'Updated notes keep version history for audit purposes.'
      ]
    },
    {
      id: 'security',
      title: 'Security and Passwords',
      audience: 'All users',
      keywords: 'login password activate reset change encrypted account',
      steps: [
        'Log in with your personnel number and password.',
        'New accounts must be activated before first login.',
        'Use Forgot Password if your account password needs to be reset.',
        'Use Change Password after login to update your password.'
      ]
    },
    {
      id: 'roles', title: 'Roles and Permissions', audience: 'System Administrator',
      keywords: 'roles permissions access matrix authorisation',
      steps: ['Review what each role may view and change.', 'Grant only the permissions required for the user’s duties.', 'A denied action explains which role or permission is required.', 'Save role changes only after confirming the operational impact.']
    },
    {
      id: 'faq', title: 'FAQ Management', audience: 'System Administrator',
      keywords: 'faq questions answers publish pages',
      steps: ['Create or update frequently asked questions.', 'Publish only reviewed answers.', 'Use search and paging to locate an entry.', 'Published entries appear automatically in system Help.']
    },
    {
      id: 'stations', title: 'Station Hierarchy', audience: 'Authorised users',
      keywords: 'station province region hierarchy tree',
      steps: ['Provinces load expanded so their stations are immediately visible.', 'Use search to locate a province, region or station.', 'Select a station to review its details.', 'Only authorised administrators may change hierarchy records.']
    },
    {
      id: 'audit', title: 'Audit Trail', audience: 'System Administrator',
      keywords: 'audit action user date payload hash export',
      steps: ['Filter events by action, user or date range.', 'Payload hashes appear in the Details column when recorded.', 'Use the reason to understand why a change was made.', 'Export the filtered audit results to CSV when required.']
    },
    {
      id: 'configuration', title: 'System Configuration', audience: 'System Administrator',
      keywords: 'configuration policies limits settings',
      steps: ['Review the current operational setting before changing it.', 'Enter a reason where the setting requires audit justification.', 'Save changes and confirm the success notification.', 'Officer workload and retention rules use these configured values.']
    },
    {
      id: 'backup', title: 'Backup and Restore', audience: 'System Administrator',
      keywords: 'backup restore download database',
      steps: ['Create a backup before a significant system change.', 'Download and retain backups according to policy.', 'Restore only the intended verified backup.', 'Review the audit trail after a restore operation.']
    },
    {
      id: 'ai', title: 'AI Interaction Log', audience: 'System Administrator and DSC Supervisor',
      keywords: 'ai log prompt payload response docket',
      steps: ['Review AI interactions associated with a docket.', 'Use Case Readiness Intelligence to inspect evidence integrity, custody controls, SLA risk and prosecution blockers.', 'Confirm human verification before relying on generated content.', 'Use the logged payload and response for accountability.', 'Report unexpected or sensitive output through Feedback.']
    },
    {
      id: 'data-exchange', title: 'Data Exchange', audience: 'System Administrator',
      keywords: 'import export lookup crimes evidence locations',
      steps: ['Choose the dataset or lookup type.', 'Preview and validate lookup records before applying an import.', 'Use incident-type imports to add approved crime options.', 'Export only the minimum data needed for the authorised purpose.']
    },
    {
      id: 'feedback', title: 'Feedback', audience: 'All signed-in users',
      keywords: 'feedback response support issue',
      steps: ['Enter a clear subject and description of the issue.', 'Submit feedback once all required details are present.', 'Administrators respond from the aligned Respond action.', 'Return later to review the recorded response.']
    },
    {
      id: 'create-docket', title: 'Create New Docket', audience: 'CSC Officer',
      keywords: 'create new docket incident complainant map ob fic validation location',
      steps: ['Complete Incident Details first, including the case and incident classifications.', 'Use the address search or map picker to capture the incident area and coordinates.', 'Enter a clear incident narrative and complete the complainant details.', 'Correct every validation message before submitting; references are generated automatically where applicable.']
    },
    {
      id: 'docket-details', title: 'Docket Details and Actions', audience: 'Authorised docket users',
      keywords: 'view docket details actions allocation notes evidence bundle archive status',
      steps: ['Review the docket summary, references, status and allocation before taking action.', 'Use the visible primary action and the three-dot menu for actions permitted for your role.', 'Open Evidence or Notes from the docket so records remain linked to the correct case.', 'Unavailable actions are hidden or blocked according to the docket state and your permissions.']
    },
    {
      id: 'register-person', title: 'Register a Person', audience: 'Authorised person-management users',
      keywords: 'register person complainant suspect witness victim contact docket link validation',
      steps: ['Enter the person’s identity and valid contact information.', 'Select the correct person type and complete every required field.', 'Save the person record, then link it to the relevant docket where authorised.', 'A person may be linked to more than one docket when the business relationship requires it.']
    },
    {
      id: 'user-details', title: 'User Account Details', audience: 'System Administrator',
      keywords: 'user details account activate suspend role station profile permissions',
      steps: ['Review the selected user’s identity, station, role and account status.', 'Use only the account actions appropriate to the requested administrative change.', 'Confirm the role and station before saving an update.', 'Review the success or error message to confirm whether the action was completed.']
    },
    {
      id: 'evidence-register', title: 'Evidence Register Report', audience: 'DSC Supervisor',
      keywords: 'evidence register report docket uploader custody export',
      steps: ['Review evidence, its linked docket and the user who uploaded it in one report.', 'Use the available filters to narrow the operational records.', 'Check custody and evidence metadata before making a management decision.', 'Export the authorised report when a documented copy is required.']
    },
    {
      id: 'system-calendar', title: 'System Calendar', audience: 'System Administrator',
      keywords: 'system calendar administration maintenance backup security reminder event',
      steps: ['Use this calendar for administrative reminders only; docket information is not displayed.', 'Select Add event or a calendar date to create a reminder.', 'Choose Administration, Backup, Maintenance or Security as the category.', 'Select an event to edit it, or remove it using the confirmation dialog.']
    },
    {
      id: 'change-password', title: 'Change Password', audience: 'All signed-in users',
      keywords: 'change password current new confirm security validation',
      steps: ['Enter your current password.', 'Enter and confirm a new password that satisfies every displayed security requirement.', 'Submit the form once the passwords match.', 'Use Forgot Password from the sign-in page if you do not know the current password.']
    },
    {
      id: 'profile', title: 'Profile', audience: 'All signed-in users',
      keywords: 'profile contact password otp security',
      steps: ['Review your personnel, role and station information.', 'Update only the editable contact and notification preferences.', 'Choose an existing profile picture or use Take photo to capture one with the device camera.', 'Your email address is read-only; contact a System Administrator if it must be corrected.']
    }
  ];

  ngOnInit(): void {
    this.route.queryParamMap.subscribe(params => {
      const topic = params.get('topic');
      if (topic && this.topics.some(t => t.id === topic)) {
        this.selectedTopicId = topic;
        this.cdr.detectChanges();
      }
    });
    this.faqService.getFaqs().subscribe({
      next: faqs => {
        this.faqs = faqs || [];
        this.cdr.detectChanges();
      },
      error: () => {
        this.faqs = [];
        this.cdr.detectChanges();
      }
    });
  }

  get filteredTopics(): HelpTopic[] {
    const term = this.searchTerm.trim().toLowerCase();
    if (!term) {
      return this.topics;
    }

    return this.topics.filter(topic =>
      topic.title.toLowerCase().includes(term) ||
      topic.audience.toLowerCase().includes(term) ||
      topic.keywords.toLowerCase().includes(term) ||
      topic.steps.some(step => step.toLowerCase().includes(term))
    );
  }

  get selectedTopic(): HelpTopic {
    return this.topics.find(topic => topic.id === this.selectedTopicId) ?? this.topics[0];
  }

  get filteredFaqs(): Faq[] {
    const term = this.searchTerm.trim().toLowerCase();
    if (!term) return this.faqs;
    return this.faqs.filter(faq =>
      faq.question.toLowerCase().includes(term) ||
      faq.answer.toLowerCase().includes(term)
    );
  }

  selectTopic(topicId: string): void {
    this.selectedTopicId = topicId;
  }
}
