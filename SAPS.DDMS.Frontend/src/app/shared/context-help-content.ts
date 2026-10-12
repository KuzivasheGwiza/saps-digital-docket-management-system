import { ROLES } from '../security/role-policy';

export interface ContextHelpGuide {
  title: string;
  purpose: string;
  steps: string[];
  controls: string[];
  requirements: string[];
  tips: string[];
}

function guide(
  title: string,
  purpose: string,
  steps: string[],
  controls: string[] = [],
  requirements: string[] = [],
  tips: string[] = []
): ContextHelpGuide {
  return { title, purpose, steps, controls, requirements, tips };
}

const pageGuides: Record<string, ContextHelpGuide> = {
  login: guide('Sign in', 'Access SAPS DDMS with your authorised account.',
    ['Enter your personnel number or email address.', 'Enter your password and select Sign in.', 'If prompted, enter the six-digit one-time PIN.'],
    ['Show reveals the password you typed. Forgot password? starts account recovery.'],
    ['An activated account and valid credentials are required.'],
    ['Use Forgot password? if you cannot remember your password.']),
  activate: guide('Activate account', 'Set the password for a newly created account.',
    ['Open the activation link provided for your account.', 'Enter a new password and confirm it.', 'Submit the form to activate the account.'],
    ['Password and Confirm password must match.'], ['The password must meet the requirements shown on the form.']),
  'forgot-password': guide('Forgot password', 'Request a password reset for your account.',
    ['Enter the email address linked to your account.', 'Submit the request.', 'Follow the reset instructions and one-time PIN sent through the configured channel.'],
    ['Email address identifies the account to reset.']),
  'reset-password': guide('Reset password', 'Choose a new password using your one-time PIN.',
    ['Enter the six-digit one-time PIN.', 'Enter and confirm the new password.', 'Submit the form, then sign in with the new password.'],
    ['New password and Confirm password must match.'], ['The PIN and password must satisfy the rules shown on the form.']),
  'change-password': guide('Change Password', 'Update your password while signed in.',
    ['Enter your current password.', 'Enter a new password and confirm it.', 'Submit the change.'],
    ['Current Password verifies your account.'], ['The new password must be at least 12 characters and match its confirmation.'],
    ['If you do not know the current password, use Forgot password? on the sign-in page.']),
  profile: guide('My Profile', 'Review your account details and update your contact preferences.',
    ['Review the read-only identity and account information.', 'Use the + beside your photo to choose an image or take a photo with your device camera.', 'Edit the phone number or notification preference and save the changes.'],
    ['The camera option needs device camera permission.'], ['Your email address, identity, role and station cannot be changed here.'],
    ['Ask a System Administrator to correct an email address.']),
  help: guide('Help Centre', 'Find system-wide guidance and answers to common questions.',
    ['Search by screen, role, task or permission.', 'Select a topic to read its steps.', 'Expand a Frequently Asked Question to read its answer.'],
    ['The topic list and FAQ answers use the same search text.'], [],
    ['For instructions tied to the page you are using, select the floating ? Help button on that page.']),
  dashboard: guide('Dashboard', 'See the information and shortcuts available to your role.',
    ['Review the summary cards and charts shown on your dashboard.', 'Open a recent docket or use a quick action when it is available to your role.', 'Use the main navigation to move to a work area.'],
    ['Cards and shortcuts differ by role.'], [],
    ['If a work area is not shown, your account may not have access to it.']),
  users: guide('User Management', 'Manage SAPS DDMS user accounts.',
    ['Use the role tabs or search by name or personnel number.', 'Select a row action to view details, edit an account or review role history.', 'Select Create User to register an account.'],
    ['The table shows personnel number, role and account status.'], ['Only System Administrators can use this page.']),
  'user-details': guide('User Details', 'Review the selected user account and its status.',
    ['Read the user identity, role, station and status.', 'Use the available account actions only after confirming the correct user.'],
    ['Role History shows recorded role changes.'], ['Only System Administrators can manage user accounts.']),
  roles: guide('Roles & Permissions', 'Create and maintain access profiles.',
    ['Select an access profile or choose New role.', 'Enter a role name and optional description.', 'Choose module permissions, then save.'],
    ['Select group toggles a whole permission group. Clear starts a new profile.'], ['Role name is required; locked baseline permissions cannot be removed.'],
    ['Changes affect the next authorised request; navigation refreshes on the next sign-in.']),
  'data-exchange': guide('Data Exchange', 'Export authorised operational records or import approved lookup data.',
    ['For export, select a dataset and format, then set any relevant filters.', 'For lookup import, choose a template type and JSON or XML format.', 'Upload the file, review the preview and apply only valid records.'],
    ['Date, station and dataset-specific filters narrow an export.'], ['This area is restricted to System Administrators.'],
    ['Export only the records needed for the authorised purpose.']),
  faq: guide('Frequently Asked Questions', 'Find published answers to common SAPS DDMS questions.',
    ['Search by a word in the question or answer.', 'Select View FAQ to read the full answer.'],
    ['Administrators also see publication state and can add, edit or delete entries.']),
  configuration: guide('System Configuration', 'Review and change operational timers and business rules.',
    ['Find the setting in the appropriate section.', 'Select Edit, enter a value within the displayed range and select Save.', 'Check the success message and the updated value.'],
    ['Cancel leaves the setting unchanged.'], ['Only System Administrators can change settings.'],
    ['Review the current value and unit before editing.']),
  stations: guide('Station Hierarchy', 'Browse provinces, cities, stations and their docket counts.',
    ['Expand a province to see its cities and stations.', 'Read the station docket count where shown.'],
    ['System Administrators can add cities or stations and use the edit and remove controls.'], [],
    ['A city or station with dependent records may not be removable.']),
  backup: guide('Backup & Restore', 'Create, download and restore system snapshots.',
    ['Select Create Backup Now to create a snapshot.', 'Use the download action on a completed snapshot to keep a copy.', 'Open Restore only for the intended snapshot and confirm the warning before restoring.'],
    ['The table shows file name, date, creator, size and status.'], ['Only System Administrators can use this page.'],
    ['Restoring replaces current operational data and signs users out.']),
  audit: guide('Audit Trail', 'Review recorded system activity.',
    ['Filter by search text, action type, user or date range.', 'Apply the filters and inspect the matching events.', 'Use Export CSV when a copy of the filtered records is needed.'],
    ['Search accepts an action, entity or reason.'], ['From date must not be after To date.']),
  ai: guide('AI Interaction Log', 'Review recorded AI interactions associated with dockets.',
    ['Read the date, docket, action, user and payload columns.', 'If loading fails, select Try again.'],
    ['The payload is an audit record, not an instruction to act without review.']),
  sms: guide('SMS Queue', 'Review generated messages and their dispatch status.',
    ['Use Status to filter the queue and Refresh to reload it.', 'Read the recipient, message and status before taking action.', 'Use Mark dispatched or Cancel only when those actions are shown for a queued item.'],
    ['LocalQueue keeps messages in the system until manually dispatched or a gateway is configured.'], [],
    ['Do not mark a message dispatched until it has actually been sent through the approved channel.']),
  reports: guide('Report Management', 'Generate operational reports from the available report tabs.',
    ['Select a report tab.', 'Set the From and To dates, docket status or crime category when that report offers those filters.', 'Select Apply to refresh the results.', 'Use Export CSV or Export PDF when a report is available.'],
    ['Available filters vary by report.'], ['From date must be on or before To date.'],
    ['Export controls are disabled until there is a report to export.']),
  'evidence-register': guide('Evidence, Docket & Uploader Register', 'Review evidence together with its docket and uploader.',
    ['Review the consolidated register.', 'Apply any date or other filters shown for this report.', 'Use Export CSV or Export PDF when a report is available.'],
    ['This dedicated register is available to DSC Supervisors.'], ['From date must be on or before To date when date filters are shown.']),
  calendar: guide('Docket Calendar', 'View docket creation, allocation and inspection due dates by month.',
    ['Use the previous, Today and next buttons to change month.', 'Select a docket event to open that docket.', 'Use + more when a day has additional events.'],
    ['Show latest activity is available when the current month has no events but docket data exists.']),
  'system-calendar': guide('System Calendar', 'Plan administrative reminders that are not linked to dockets.',
    ['Move between months using the calendar controls.', 'Select Add event or a date to add a reminder.', 'Select an event to edit it, or use its delete control to remove it.'],
    ['Events have a title, date, category and optional description.'], ['Only System Administrators can use this calendar.'],
    ['Docket information does not appear on this calendar.']),
  investigation: guide('Investigation Notes', 'Read the investigation record linked to this docket.',
    ['Review the notes and any attached media.', 'Use the actions available to your role to add, edit or review a note.', 'Review archive requests or version history when those controls are shown.'],
    ['Note permissions depend on your role and the docket assignment.']),
  persons: guide('Person Management', 'Find and manage people linked to investigations.',
    ['Search by name, ID number or phone number.', 'Use the row actions to view a person or open an allowed action.', 'Select Register Person when you need a new person record.'],
    ['Role and status are shown in the table.'], [],
    ['The search accepts partial names or identifiers.']),
  'register-person': guide('Register Person', 'Create a person record for a complainant, suspect, witness or other listed role.',
    ['Enter identity and contact information.', 'Select the Person Role from the searchable list.', 'Review the fields and save the record.'],
    ['Date of Birth and Person Role are part of the registration form.'],
    ['First Name, Last Name, 13-digit South African ID Number, Date of Birth, Person Role, Phone Number, Email Address and Physical Address are required.']),
  evidence: guide('Evidence Management', 'View evidence associated with a selected docket.',
    ['Choose the correct docket in Select Docket.', 'Review the evidence cards and select the view action for details.', 'Use the actions shown for your role to register, edit or transfer evidence, or inspect custody history.'],
    ['Add Evidence appears only for an Investigating Officer after a docket is selected.'], [],
    ['If no evidence appears, confirm that the intended docket is selected.']),
  dockets: guide('Docket Management', 'Search and review the dockets your role can access.',
    ['Search by docket number, OB reference or suspect, or use the Status filter.', 'Select View to read a docket.', 'Open the three-dot menu for actions available for that docket and your role.'],
    ['The table shows status, allocation, inspection and created information.'], [],
    ['Actions can be unavailable because of the docket state or your permissions.']),
  'docket-details': guide('Docket Details', 'Read the selected docket and its linked case information.',
    ['Review its references, status, complainant, station and assignment.', 'Use the permitted actions on the docket list to work with notes, evidence or bundles.'],
    ['The actions you see depend on your role and the docket state.']),
  feedback: guide('Feedback', 'Submit a support message and review responses.',
    ['Enter a Subject and Message.', 'Select Submit feedback.', 'Read the response in your feedback list when one is available.'],
    ['System Administrators can use Respond on submitted feedback.'], ['Subject needs at least 3 characters; Message needs at least 10.'])
};

const modalGuides: Record<string, ContextHelpGuide> = {
  'Create New Docket': guide('Create Docket', 'Register a new incident and its complainant.',
    ['Select Case Type and Sensitivity Level, then enter a Docket Summary.', 'Enter Occurrence Date, Crime / Incident Type and Location Type. Search for an address, use your location or click the map to select the incident point.', 'Write the incident Description and enter the complainant Full Name.', 'Review the form and select Create Docket.'],
    ['OB and FIC references are generated automatically. Modus Operandi and Contact Number are optional. Selecting Other requires a description of that type.'],
    ['Summary and incident description each need 10–1000 characters. A map location is required. The complainant name needs 2–100 valid characters.'],
    ['If Create Docket is disabled, check every starred field and the selected map location.']),
  'Update Docket': guide('Update Docket', 'Change permitted docket information without changing its status manually.',
    ['Review the docket number and references.', 'Edit Docket Summary, optional Modus Operandi or Sensitivity Level.', 'Enter the Reason for Update and select Save Changes.'],
    ['Closed or archived dockets cannot be updated here. CSC Officers are limited to dockets they created.'],
    ['Docket Summary and Sensitivity Level are required. Reason for Update must be at least 20 characters.']),
  'Docket Details': pageGuides['docket-details'],
  'Allocate Docket': guide('Allocate Docket', 'Assign this docket to an Investigating Officer.',
    ['Review the docket and OB reference.', 'Choose an Investigating Officer from the list.', 'Select Allocate. The officer can then accept or reject the assignment.'],
    ['Officer workload is shown; officers at capacity cannot be selected.'], ['A selected officer is required.']),
  'Reassign Docket': guide('Reassign Docket', 'Change the Investigating Officer assigned to this docket.',
    ['Review the existing docket.', 'Choose a new Investigating Officer.', 'Enter the Reason for Reassignment and select Reassign.'],
    ['Officer workload and capacity are shown in the list.'], ['The reassignment reason needs 10–500 characters.']),
  'Complete 24-hour Inspection': guide('Complete Inspection', 'Record the docket inspection outcome.',
    ['Enter Inspection Notes describing the outcome and completeness review.', 'Save the inspection.'],
    [], ['Inspection Notes are required.']),
  'Update Docket Status': guide('Update Docket Status', 'Record an allowed status transition.',
    ['Select a New Status from the available transitions.', 'Enter a Reason and save the change.'],
    ['Available transitions depend on the current docket status.'], ['New Status and Reason are required.']),
  'Archive Docket': guide('Archive Docket', 'Archive a docket that meets the archive rule.',
    ['Review the archive eligibility message.', 'Confirm the action only when the docket is eligible.'],
    ['Archive is disabled until its policy conditions are met.']),
  'Record Court Outcome': guide('Record Court Outcome', 'Record the court result for a docket pending a prosecutor outcome.',
    ['Select Court Outcome.', 'Enter Court Date and Presiding Officer.', 'If the outcome is Guilty, enter Sentence Details.', 'Select Save Court Outcome.'],
    ['Additional Notes are optional.'], ['The court date cannot precede docket creation or be in the future.']),
  'Prosecutor Bundle': guide('Prosecutor Bundle', 'Review the docket’s prosecutor bundles and related controls.',
    ['Review existing bundles and their status.', 'Use only the bundle actions available for your role and the selected bundle.', 'Review privacy controls or AI assistance where those sections are available.'],
    ['Draft and shared bundles offer different actions.']),
  'Edit Prosecutor Bundle': guide('Edit Prosecutor Bundle', 'Update a draft prosecutor bundle.',
    ['Edit Bundle Title, the summary or notes.', 'Choose whether approved notes and active evidence are included.', 'Enter the update Reason and save.'],
    [], ['Bundle Title and Reason are required.']),
  'Share with Prosecutor': guide('Share with Prosecutor', 'Send a previewed bundle to a prosecutor.',
    ['Select the Recipient Prosecutor.', 'Add an optional Sharing note.', 'Confirm the share action.'],
    [], ['Recipient Prosecutor is required.']),
  'Share with Prosecutor Preview': guide('Share with Prosecutor Preview', 'Check the bundle before it is shared.',
    ['Review the bundle content and privacy information.', 'Continue only when the preview is correct.']),
  'Register Evidence': guide('Register Evidence', 'Add an evidence item to the selected docket.',
    ['Choose Evidence Type before entering Description and Number of items.', 'Enter Date Collected, Collection Method, State of Item, Storage Location and Signature Method.', 'Attach an evidence file if one is available, then select Register Evidence.'],
    ['The docket is taken from Select Docket. The payload hash is generated automatically.'],
    ['Starred fields are required. Description needs 3–2000 characters; Number of items must be 1–1,000,000. An optional image, PDF or video file must be no larger than 50 MB.'],
    ['A file is optional; the evidence record can be registered without one.']),
  'Update Evidence': guide('Update Evidence', 'Correct the selected evidence item’s metadata.',
    ['Edit the permitted description, number of items, collection method, state, storage location or signature method.', 'Enter a Reason for Update.', 'Select Update Evidence.'],
    ['The system-generated Payload Hash is read-only.'], ['Reason for Update needs 10–500 characters.']),
  'Evidence Details': guide('Evidence Details', 'Read the selected evidence record.',
    ['Review the evidence reference, description, collection and storage information.', 'Close the details to return to the evidence list.']),
  'Record Evidence Transfer': guide('Record Evidence Transfer', 'Record a change in evidence custody or storage.',
    ['Check the locked From Location and receiving officer.', 'Select To Location and Signature Method.', 'Enter a Transfer Reason, then select Record Transfer.'],
    ['Payload Hash / Checksum can be entered when available.'], ['Transfer Reason needs 10–500 characters.']),
  'Register Person': pageGuides['register-person'],
  'Edit Person': guide('Edit Person', 'Update permitted details on the selected person record.',
    ['Review the identity before editing.', 'Change the fields available to your role.', 'Save the updated record.'],
    ['CSC Officers can change contact details only; identity fields are read-only.']),
  'Person Details': guide('Person Details', 'Read identity, contact and linked-docket information for a person.',
    ['Review the person’s details and role.', 'Close the view or choose another permitted action from the person list.']),
  'Link Person to Docket': guide('Link Person to Docket', 'Record a person’s relationship to a docket.',
    ['Select the correct docket.', 'Choose Relationship to Docket.', 'Save the link.'],
    [], ['Docket and relationship are required.']),
  'Create New User': guide('Create User', 'Register a new SAPS DDMS account.',
    ['Enter First Name, Last Name and Rank.', 'Choose Province, then City, Station and Role.', 'Enter Email Address and optional Phone Number, then create the user.'],
    ['Personnel Number is generated automatically.'], ['Starred fields are required; an optional phone number must still be valid if entered.'],
    ['If email delivery is unavailable, the secure activation link appears on User Management.']),
  'Edit User': guide('Edit User', 'Update a user’s account and assignment.',
    ['Review the selected user.', 'Update the permitted identity, rank, station, role or contact fields.', 'Enter a reason where required and save.'],
    ['Role and station affect the user’s access.'], ['Starred fields must be completed; an optional phone number must be valid if entered.']),
  'User Details': pageGuides['user-details'],
  'Role History': guide('Role History', 'Review recorded role changes for the selected user.',
    ['Read each historical entry and its date.', 'Close the view when finished.']),
  'Add Investigation Note': guide('Add Investigation Note', 'Record a finding or update on this docket.',
    ['Enter Note Content.', 'Attach a file if needed.', 'Save the note.'],
    ['Attachment is optional.'], ['Note Content is required.']),
  'Edit Investigation Note': guide('Edit Investigation Note', 'Correct a note while retaining its history.',
    ['Update Note Content.', 'Enter the Reason for Change.', 'Save the note.'],
    [], ['Note Content and Reason for Change are required.']),
  'Version History': guide('Version History', 'Review earlier versions of an investigation note.',
    ['Read the recorded versions and change details.', 'Close the view to return to the notes.']),
  'Request Note Archive': guide('Request Note Archive', 'Ask for a note to be archived.',
    ['Enter the Reason for Archive Request.', 'Submit the request for DSC Supervisor review.'],
    [], ['The reason must be at least 10 characters.']),
  'Add New FAQ': guide('Add FAQ', 'Create an answer for the published FAQ list.',
    ['Enter the Question and Answer.', 'Choose the publication setting.', 'Select Save FAQ.'],
    [], ['Question and Answer are required; the form limits them to 250 and 2,000 characters.']),
  'Edit FAQ': guide('Edit FAQ', 'Revise an existing FAQ entry.',
    ['Update the Question or Answer.', 'Review the publication setting.', 'Save the changes.'],
    [], ['Question and Answer are required.']),
  'FAQ': guide('View FAQ', 'Read the full answer to the selected question.',
    ['Read the answer, then select Close.']),
  'Delete FAQ Entry': guide('Delete FAQ Entry', 'Confirm removal of the selected FAQ.',
    ['Check the question shown in the confirmation.', 'Confirm deletion only if it is the intended entry.']),
  'Add New Station': guide('Add Station', 'Add a station to the hierarchy.',
    ['Enter Station Name.', 'Choose Province and enter City.', 'Save the station.'],
    [], ['Station Name, Province and City are required.']),
  'Add City': guide('Add City', 'Add a city within a province.',
    ['Enter City Name.', 'Choose Province and save.'],
    [], ['City Name and Province are required.']),
  'Remove Station': guide('Remove Station', 'Confirm the station selected for removal.',
    ['Check the station name and confirm only if it is the intended record.']),
  'Restore Backup': guide('Restore Backup', 'Replace operational data from the selected snapshot.',
    ['Check the snapshot file name.', 'Read the warning carefully.', 'Select Restore Now only when you intend to replace current data.'],
    [], ['Restoring signs active users out.']),
  'Add system event': guide('Add System Event', 'Create an administrative calendar reminder.',
    ['Enter Event title and Date.', 'Choose a Category and add an optional Description.', 'Select Add event.'],
    [], ['Event title and Date are required.']),
  'Edit system event': guide('Edit System Event', 'Update an administrative reminder.',
    ['Review the event title, date, category and description.', 'Make the change and select Save changes.'],
    [], ['Event title and Date are required.']),
  'Remove calendar event?': guide('Remove Calendar Event', 'Confirm removal of the selected reminder.',
    ['Check the selected event and confirm removal only when intended.']),
  'Take profile photo': guide('Take Profile Photo', 'Capture a new picture with this device’s camera.',
    ['Allow camera access if prompted.', 'Position your face in the frame and capture the picture.', 'Review or cancel before applying the photo.'],
    ['Camera access depends on the device and browser permissions.'])
};

const routeGuideKeys: Record<string, string> = {
  'faq-management': 'faq',
  'system-config': 'configuration',
  'station-tree': 'stations',
  'audit-log': 'audit',
  'ai-log': 'ai',
  'sms-queue': 'sms'
};

function docketListGuide(role: string): ContextHelpGuide {
  const base = pageGuides['dockets'];
  const steps = [...base.steps];
  const controls = [...base.controls];
  if (role === ROLES.cscOfficer) {
    steps.push('Use Create Docket to register an incident. Use Update on a docket you created when that button is enabled.');
  } else if (role === ROLES.dscSupervisor) {
    steps.push('Use Allocate or Reassign for officer assignments. Complete inspections or record court outcomes only when those actions are shown.');
  } else if (role === ROLES.investigatingOfficer) {
    steps.push('When a docket is assigned to you, use Accept assignment or Reject assignment in its action menu.');
  } else if (role === ROLES.docketAdministrator) {
    steps.push('Use Archive only when the docket meets the displayed archive rule.');
  }
  if (role === ROLES.prosecutor) controls.push('Shared prosecutor bundles are available only for dockets you are authorised to see.');
  return { ...base, steps, controls };
}

function roleAdjustedGuide(key: string, role: string): ContextHelpGuide {
  const base = pageGuides[key];
  const steps = [...base.steps];
  if (key === 'faq' && role === ROLES.systemAdministrator) {
    steps.push('Use Add FAQ to create an entry, or the edit and delete actions to maintain existing answers. Publish only reviewed content.');
  }
  if (key === 'evidence') {
    if (role === ROLES.investigatingOfficer) steps.push('After selecting a docket, use Add Evidence or the edit and transfer actions when available.');
    if (role === ROLES.recordsClerk) steps.push('Use the transfer action to record a custody movement when it is available.');
  }
  if (key === 'investigation') {
    if (role === ROLES.investigatingOfficer) steps.push('Use Add Note for a new entry; you can request archive review for an eligible note.');
    if (role === ROLES.dscSupervisor) steps.push('Review pending note archive requests in the Archive Requests section.');
  }
  return { ...base, steps };
}

export function resolveContextHelp(path: string, role: string, modalTitle = ''): ContextHelpGuide {
  const title = modalTitle.trim().replace(/\s+/g, ' ');
  if (title) {
    if (title.startsWith('Prosecutor Bundle —')) return modalGuides['Prosecutor Bundle'];
    if (modalGuides[title]) return modalGuides[title];
  }
  const cleanPath = path.split(/[?#]/)[0];
  if (cleanPath === '/dockets/create') return modalGuides['Create New Docket'];
  if (/^\/dockets\/[^/]+$/.test(cleanPath)) return pageGuides['docket-details'];
  if (cleanPath === '/persons/create') return pageGuides['register-person'];
  if (/^\/users\/[^/]+$/.test(cleanPath)) return pageGuides['user-details'];
  if (cleanPath.startsWith('/investigation/')) return roleAdjustedGuide('investigation', role);
  const routeKey = cleanPath.slice(1);
  const key = routeGuideKeys[routeKey] ?? routeKey;
  if (key === 'dockets') return docketListGuide(role);
  if (key === 'dashboard' && role === ROLES.systemAdministrator) {
    return guide('System Administrator Dashboard', 'Open the administrative areas available to your account.',
      ['Choose User Management, Roles and Permissions, FAQ Management, System Configuration, Audit Trail or Backup and Restore from the dashboard.', 'Use Administration in the navigation for other authorised system areas.'],
      ['Dockets are not available to the System Administrator role.']);
  }
  if (['faq', 'evidence', 'investigation'].includes(key)) return roleAdjustedGuide(key, role);
  return pageGuides[key] ?? pageGuides['dashboard'];
}
