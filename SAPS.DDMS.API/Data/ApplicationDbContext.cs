using Microsoft.EntityFrameworkCore;
using SAPS.DDMS.API.Models;

namespace SAPS.DDMS.API.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Identity and system administration
        public DbSet<User> Users { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<RolePermissions> RolePermissions { get; set; }
        public DbSet<Permissions> Permissions { get; set; }
        public DbSet<Station> Stations { get; set; }
        public DbSet<Session> Sessions { get; set; }
        public DbSet<SessionLog> SessionLogs { get; set; }
        public DbSet<SessionTimer> SessionTimers { get; set; }
        public DbSet<UserAuditLog> UserAuditLogs { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<Feedback> Feedback { get; set; }
        public DbSet<SystemSetting> SystemSettings { get; set; }
        public DbSet<BackupRecord> BackupRecords { get; set; }
        public DbSet<SmsMessage> SmsMessages { get; set; }

        // Docket management
        public DbSet<GeoLocation> GeoLocations { get; set; }
        public DbSet<IncidentType> IncidentTypes { get; set; }
        public DbSet<Incident> Incidents { get; set; }
        public DbSet<DocketStatus> DocketStatuses { get; set; }
        public DbSet<Docket> Dockets { get; set; }
        public DbSet<DocketTimer> DocketTimers { get; set; }
        public DbSet<DocketPrivacyFlag> DocketPrivacyFlags { get; set; }

        // Workflow, history and audit records
        public DbSet<DocketStatusHistory> DocketStatusHistories { get; set; }
        public DbSet<EvidenceTransfer> EvidenceTransfers { get; set; }
        public DbSet<ProsecutorBundle> ProsecutorBundles { get; set; }
        public DbSet<ProsecutorBundleItem> ProsecutorBundleItems { get; set; }
        public DbSet<ProsecutorBundleRevision> ProsecutorBundleRevisions { get; set; }
        public DbSet<PrivacyFlagHistory> PrivacyFlagHistories { get; set; }
        public DbSet<CourtAppearance> CourtAppearances { get; set; }
        public DbSet<AIInteractionLog> AIInteractionLogs { get; set; }
        public DbSet<AIExtraction> AIExtractions { get; set; }
        public DbSet<AISuggestion> AISuggestions { get; set; }
        public DbSet<CourtOutcome> CourtOutcomes { get; set; }
        public DbSet<PersonArchive> PersonArchives { get; set; }
        public DbSet<UserRoleHistory> UserRoleHistories { get; set; }
        public DbSet<UserPasswordHistory> UserPasswordHistories { get; set; }
        public DbSet<Faq> Faqs { get; set; }
        public DbSet<InvestigationNoteArchiveRequest> InvestigationNoteArchiveRequests { get; set; }

        // Investigations
        public DbSet<InvestigationStatus> InvestigationStatuses { get; set; }
        public DbSet<Investigation> Investigations { get; set; }
        public DbSet<InvestigationAssignment> InvestigationAssignments { get; set; }
        public DbSet<InvestigationNote> InvestigationNotes { get; set; }
        public DbSet<NoteVersion> NoteVersions { get; set; }
        public DbSet<NoteAttachment> NoteAttachments { get; set; }

        // Evidence management
        public DbSet<EvidenceType> EvidenceTypes { get; set; }
        public DbSet<EvidenceLocation> EvidenceLocations { get; set; }
        public DbSet<Evidence> Evidences { get; set; }

        // People linked to dockets
        public DbSet<PersonRole> PersonRoles { get; set; }
        public DbSet<Person> Persons { get; set; }
        public DbSet<DocketPersonLink> DocketPersonLinks { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<RolePermissions>()
                .HasKey(rp => new { rp.RoleID, rp.PermissionID });

            modelBuilder.Entity<User>().HasIndex(u => u.Username).IsUnique();
            modelBuilder.Entity<User>().HasIndex(u => u.EmailAddress).IsUnique();
            modelBuilder.Entity<Docket>().HasIndex(d => d.DocketNumber).IsUnique();
            modelBuilder.Entity<Docket>().HasIndex(d => d.OBReferenceNumber).IsUnique();
            modelBuilder.Entity<SystemSetting>().HasIndex(s => s.SettingKey).IsUnique();
            modelBuilder.Entity<Docket>()
                .ToTable("Dockets", tableBuilder => tableBuilder.UseSqlOutputClause(false));

            // Seed stable reference values used by validation and workflow rules.
            modelBuilder.Entity<Role>().HasData(
                new Role { RoleID = 1, Name = "System Administrator", Description = "Full system access" },
                new Role { RoleID = 2, Name = "DSC Supervisor", Description = "Supervises docket allocation" },
                new Role { RoleID = 3, Name = "Investigating Officer", Description = "Conducts investigations" },
                new Role { RoleID = 4, Name = "CSC Officer", Description = "Creates and manages dockets" },
                new Role { RoleID = 5, Name = "Docket Administrator", Description = "Manages docket administration" },
                new Role { RoleID = 6, Name = "Records Clerk", Description = "Maintains docket records" },
                new Role { RoleID = 7, Name = "Prosecutor", Description = "Reviews prosecutor bundles" }
            );
            modelBuilder.Entity<DocketStatus>().HasData(
                new DocketStatus { DocketStatusID = 1, StatusName = "OPEN" },
                new DocketStatus { DocketStatusID = 2, StatusName = "ALLOCATED" },
                new DocketStatus { DocketStatusID = 3, StatusName = "UNDER_INVESTIGATION" },
                new DocketStatus { DocketStatusID = 4, StatusName = "PENDING_PROSECUTOR" },
                new DocketStatus { DocketStatusID = 5, StatusName = "CLOSED" },
                new DocketStatus { DocketStatusID = 6, StatusName = "ARCHIVED" },
                new DocketStatus { DocketStatusID = 7, StatusName = "COURT_OUTCOME_RECORDED" },
                new DocketStatus { DocketStatusID = 8, StatusName = "REOPENED" }
            );

            modelBuilder.Entity<InvestigationStatus>().HasData(
                new InvestigationStatus { InvestigationStatusID = 1, StatusName = "OPEN" },
                new InvestigationStatus { InvestigationStatusID = 2, StatusName = "IN_PROGRESS" },
                new InvestigationStatus { InvestigationStatusID = 3, StatusName = "COMPLETED" },
                new InvestigationStatus { InvestigationStatusID = 4, StatusName = "SUSPENDED" }
            );

            modelBuilder.Entity<IncidentType>().HasData(
                new IncidentType { IncidentTypeID = 1, TypeName = "Robbery", Description = "Theft by force" },
                new IncidentType { IncidentTypeID = 2, TypeName = "Burglary", Description = "Breaking and entering" },
                new IncidentType { IncidentTypeID = 3, TypeName = "Assault GBH", Description = "Grievous bodily harm" },
                new IncidentType { IncidentTypeID = 4, TypeName = "Murder", Description = "Unlawful killing" },
                new IncidentType { IncidentTypeID = 5, TypeName = "Sexual Offense", Description = "Sexual assault" },
                new IncidentType { IncidentTypeID = 6, TypeName = "Fraud", Description = "Financial crime" },
                new IncidentType { IncidentTypeID = 7, TypeName = "Corruption", Description = "Financial crime" },
                new IncidentType { IncidentTypeID = 8, TypeName = "Money Laundering", Description = "Financial crime" },
                new IncidentType { IncidentTypeID = 9, TypeName = "Theft", Description = "Property theft" },
                new IncidentType { IncidentTypeID = 10, TypeName = "Drug Related", Description = "Narcotics offense" },
                new IncidentType { IncidentTypeID = 11, TypeName = "Hijacking", Description = "Vehicle hijacking" },
                new IncidentType { IncidentTypeID = 12, TypeName = "Domestic Violence", Description = "Domestic violence complaint" },
                new IncidentType { IncidentTypeID = 13, TypeName = "Kidnapping", Description = "Abduction or kidnapping" },
                new IncidentType { IncidentTypeID = 14, TypeName = "Attempted Murder", Description = "Attempted unlawful killing" },
                new IncidentType { IncidentTypeID = 15, TypeName = "Arson", Description = "Intentional fire setting" },
                new IncidentType { IncidentTypeID = 16, TypeName = "Human Trafficking", Description = "Trafficking in persons" },
                new IncidentType { IncidentTypeID = 17, TypeName = "Cybercrime", Description = "Digital or online criminal activity" },
                new IncidentType { IncidentTypeID = 18, TypeName = "Stock Theft", Description = "Theft of livestock" },
                new IncidentType { IncidentTypeID = 19, TypeName = "Illegal Firearm Possession", Description = "Unlawful possession of a firearm" },
                new IncidentType { IncidentTypeID = 20, TypeName = "Public Violence", Description = "Public disorder or violence" }
            );

            modelBuilder.Entity<EvidenceType>().HasData(
                new EvidenceType { EvidenceTypeID = 1, Name = "Physical" },
                new EvidenceType { EvidenceTypeID = 2, Name = "Digital" },
                new EvidenceType { EvidenceTypeID = 3, Name = "Document" },
                new EvidenceType { EvidenceTypeID = 4, Name = "Biological" },
                new EvidenceType { EvidenceTypeID = 5, Name = "Fingerprint" },
                new EvidenceType { EvidenceTypeID = 6, Name = "Photographic" },
                new EvidenceType { EvidenceTypeID = 7, Name = "Audio" },
                new EvidenceType { EvidenceTypeID = 8, Name = "Video" }
            );

            modelBuilder.Entity<EvidenceLocation>().HasData(
                new EvidenceLocation { EvidenceLocationID = 1, LocationName = "Evidence Room - Main", PhysicalAddress = "SAPS HQ", StorageRequirements = "Climate controlled" },
                  new EvidenceLocation { EvidenceLocationID = 2, LocationName = "Evidence Room - Secondary", PhysicalAddress = "SAPS Annex", StorageRequirements = "Standard" },
                new EvidenceLocation { EvidenceLocationID = 3, LocationName = "Forensics Lab", PhysicalAddress = "Forensics Division", StorageRequirements = "Controlled access" },
                new EvidenceLocation { EvidenceLocationID = 4, LocationName = "Digital Evidence Lab", PhysicalAddress = "Cyber Unit", StorageRequirements = "Static-safe" },
                new EvidenceLocation { EvidenceLocationID = 5, LocationName = "With IO", PhysicalAddress = "In custody of IO", StorageRequirements = "Officer responsible" },
                new EvidenceLocation { EvidenceLocationID = 6, LocationName = "Court", PhysicalAddress = "Submitted to court", StorageRequirements = "Court registry" }
            );

            modelBuilder.Entity<PersonRole>().HasData(
                new PersonRole { PersonRoleID = 1, RoleName = "Complainant", Code = "CMP", IsActive = true, Description = "Person who reports the crime" },
                new PersonRole { PersonRoleID = 2, RoleName = "Suspect", Code = "SUS", IsActive = true, Description = "Person accused of the crime" },
                new PersonRole { PersonRoleID = 3, RoleName = "Witness", Code = "WIT", IsActive = true, Description = "Person who witnessed the incident" },
                new PersonRole { PersonRoleID = 4, RoleName = "Victim", Code = "VIC", IsActive = true, Description = "Person who suffered harm" }
            );

            modelBuilder.Entity<Station>().HasData(
                new Station { StationID = 1, StationName = "Pretoria Central", Province = "Gauteng", Region = "Tshwane" },
                new Station { StationID = 2, StationName = "Johannesburg Central", Province = "Gauteng", Region = "Johannesburg" },
                new Station { StationID = 3, StationName = "Cape Town Central", Province = "Western Cape", Region = "Cape Town" },
                new Station { StationID = 4, StationName = "Durban Central", Province = "KwaZulu-Natal", Region = "Durban" },
                new Station { StationID = 5, StationName = "Port Elizabeth Central", Province = "Eastern Cape", Region = "Nelson Mandela Bay" },
                new Station { StationID = 6, StationName = "Sunnyside", Province = "Gauteng", Region = "Tshwane" },
                new Station { StationID = 7, StationName = "Brooklyn", Province = "Gauteng", Region = "Tshwane" },
                new Station { StationID = 8, StationName = "Sandton", Province = "Gauteng", Region = "Johannesburg" },
                new Station { StationID = 9, StationName = "Soweto", Province = "Gauteng", Region = "Johannesburg" },
                new Station { StationID = 10, StationName = "Mitchells Plain", Province = "Western Cape", Region = "Cape Town" }
            );

            // Seed the development administrator. Rotate bootstrap credentials before deployment.
            var adminPasswordHash = "$2a$12$9LkgrFL1aJP1AKD31CTviuo3sSPmDn1Z/WfDVf5Ujfq8KdI.LQ3t2";

            modelBuilder.Entity<User>().HasData(
                new User
                {
                    UserID = 1,
                    FirstName = "System",
                    LastName = "Administrator",
                    EmailAddress = "admin@saps.gov.za",
                    Username = "ADMIN001",
                    PersonnelNumber = "ADMIN001",
                    PasswordHash = adminPasswordHash,
                    Rank = "Senior Officer",
                    IsActivated = true,
                    IsActive = true,
                    AccountStatus = "Active",
                    IsLocked = false,
                    IsTwoFactorEnabled = false,
                    FailedLoginAttempts = 0,
                    FK_RoleID = 1,
                    FK_StationID = 1,
                    DateCreated = DateTime.UtcNow,
                    CreatedByUserID = null
                }
            );

            modelBuilder.Entity<SystemSetting>().HasData(
                new SystemSetting { SettingID = 1, SettingKey = "SESSION_TIMEOUT_MINUTES", SettingValue = "60", Description = "Inactive session timeout used when creating login sessions.", DataType = "int", UpdatedAt = new DateTime(2026, 1, 1) },
                new SystemSetting { SettingID = 2, SettingKey = "OTP_EXPIRY_MINUTES", SettingValue = "5", Description = "Minutes before a login one-time PIN expires.", DataType = "int", UpdatedAt = new DateTime(2026, 1, 1) },
                new SystemSetting { SettingID = 3, SettingKey = "PASSWORD_RESET_EXPIRY_HOURS", SettingValue = "1", Description = "Hours before a password reset link expires.", DataType = "int", UpdatedAt = new DateTime(2026, 1, 1) },
                new SystemSetting { SettingID = 4, SettingKey = "INSPECTION_SLA_HOURS", SettingValue = "24", Description = "Hours allowed for initial docket inspection after assignment acceptance.", DataType = "int", UpdatedAt = new DateTime(2026, 1, 1) },
                new SystemSetting { SettingID = 5, SettingKey = "MAX_LOGIN_ATTEMPTS", SettingValue = "3", Description = "Failed password attempts before an account is locked.", DataType = "int", UpdatedAt = new DateTime(2026, 1, 1) },
                new SystemSetting { SettingID = 6, SettingKey = "DOCKET_PRIORITY_HIGH_DAYS", SettingValue = "7", Description = "Business rule for flagging high priority docket age.", DataType = "int", UpdatedAt = new DateTime(2026, 1, 1) },
                new SystemSetting { SettingID = 7, SettingKey = "EVIDENCE_RETENTION_YEARS", SettingValue = "7", Description = "Business rule for evidence retention.", DataType = "int", UpdatedAt = new DateTime(2026, 1, 1) },
                new SystemSetting { SettingID = 8, SettingKey = "ARCHIVE_ELIGIBLE_DAYS", SettingValue = "90", Description = "Days after closure before a docket is eligible for archive review.", DataType = "int", UpdatedAt = new DateTime(2026, 1, 1) },
                new SystemSetting { SettingID = 9, SettingKey = "INSPECTION_REMINDER_HOURS", SettingValue = "4", Description = "Hours before inspection SLA when reminders should be raised.", DataType = "int", UpdatedAt = new DateTime(2026, 1, 1) },
                new SystemSetting { SettingID = 10, SettingKey = "TWO_FACTOR_REQUIRED", SettingValue = "false", Description = "When true, every login requires a one-time PIN after password validation.", DataType = "bool", UpdatedAt = new DateTime(2026, 1, 1) }
            );

            // Restrict deletes where removing a parent could erase operational history
            // or create SQL Server cascade cycles. True child records cascade explicitly.
            modelBuilder.Entity<Docket>()
                .HasOne(d => d.Incident)
                .WithOne(i => i.Docket)
                .HasForeignKey<Docket>(d => d.FK_IncidentID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Docket>()
                .HasOne(d => d.CreatedByUser)
                .WithMany(u => u.Dockets)
                .HasForeignKey(d => d.FK_CreatedByUserID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Docket>()
                .HasOne(d => d.Station)
                .WithMany(s => s.Dockets)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Docket>()
                .HasOne(d => d.DocketStatus)
                .WithMany(ds => ds.Dockets)
                .HasForeignKey(d => d.FK_DocketStatusID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Docket>()
                .HasOne(d => d.Investigation)
                .WithOne(i => i.Docket)
                .HasForeignKey<Docket>(d => d.FK_InvestigationID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<InvestigationAssignment>()
                .HasOne(ia => ia.Investigation)
                .WithMany(i => i.InvestigationAssignments)
                .HasForeignKey(ia => ia.FK_InvestigationID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<InvestigationAssignment>()
                .HasOne(ia => ia.User)
                .WithMany(u => u.InvestigationAssignments)
                .HasForeignKey(ia => ia.FK_UserID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<InvestigationAssignment>()
                .HasOne(ia => ia.AssignedBy)
                .WithMany()
                .HasForeignKey(ia => ia.FK_AssignedByUserID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<InvestigationNote>()
                .HasOne(n => n.Investigation)
                .WithMany(i => i.InvestigationNotes)
                .HasForeignKey(n => n.FK_InvestigationID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<NoteVersion>()
                .HasOne(nv => nv.Note)
                .WithMany(n => n.NoteVersions)
                .HasForeignKey(nv => nv.FK_NoteID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<NoteVersion>()
            .HasOne(nv => nv.User)
            .WithMany(u => u.NoteVersions)
            .HasForeignKey(nv => nv.FK_UserID)
            .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<NoteAttachment>()
                .HasOne(na => na.Note)
                .WithMany(n => n.NoteAttachments)
                .HasForeignKey(na => na.FK_NoteID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Evidence>()
                .HasOne(e => e.Docket)
                .WithMany(d => d.Evidences)
                .HasForeignKey(e => e.FK_DocketID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Evidence>()
                .HasOne(e => e.EvidenceType)
                .WithMany(et => et.Evidences)
                .HasForeignKey(e => e.FK_EvidenceTypeID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Evidence>()
                .HasOne(e => e.EvidenceLocation)
                .WithMany(el => el.Evidences)
                .HasForeignKey(e => e.FK_EvidenceLocationID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Evidence>()
            .HasOne(e => e.Investigation)
            .WithMany(i => i.Evidences)
            .HasForeignKey(e => e.FK_InvestigationID)
            .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Evidence>()
            .HasOne(e => e.User)
            .WithMany(u => u.Evidences)
            .HasForeignKey(e => e.FK_UserID)
            .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<DocketPersonLink>()
                .HasOne(dpl => dpl.Docket)
                .WithMany(d => d.DocketPersonLinks)
                .HasForeignKey(dpl => dpl.FK_DocketID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<DocketPersonLink>()
                .HasOne(dpl => dpl.Person)
                .WithMany(p => p.DocketPersonLinks)
                .HasForeignKey(dpl => dpl.FK_PersonID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<DocketPersonLink>()
                .HasOne(dpl => dpl.LinkedByUser)
                .WithMany(u => u.DocketPersonLinks)
                .HasForeignKey(dpl => dpl.FK_LinkedByUserID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<UserAuditLog>()
                .HasOne(u => u.User)
                .WithMany(u => u.UserAuditLogs)
                .HasForeignKey(u => u.FK_UserID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Notification>()
                .HasOne(n => n.User)
                .WithMany(u => u.Notifications)
                .HasForeignKey(n => n.FK_UserID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Session>()
                .HasOne(s => s.User)
                .WithMany(u => u.Sessions)
                .HasForeignKey(s => s.FK_UserID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<NoteAttachment>()
            .HasOne(na => na.User)
            .WithMany(u => u.NoteAttachments)
            .HasForeignKey(na => na.FK_UserID)
            .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Incident>()
                .HasOne(i => i.User)
                .WithMany(u => u.Incidents)
                .HasForeignKey(i => i.FK_UserID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<DocketPrivacyFlag>()
    .HasOne(dpf => dpf.AppliedByUser)
    .WithMany()
    .HasForeignKey(dpf => dpf.FK_Applied_By_UserID)
    .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<DocketPrivacyFlag>()
                .HasOne(dpf => dpf.AppliedForUser)
                .WithMany()
                .HasForeignKey(dpf => dpf.FK_Applied_For_UserID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<DocketPrivacyFlag>()
                .HasOne(dpf => dpf.RemovedByUser)
                .WithMany()
                .HasForeignKey(dpf => dpf.FK_Removed_By_UserID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<DocketPrivacyFlag>()
                .HasOne(dpf => dpf.RemovedForUser)
                .WithMany()
                .HasForeignKey(dpf => dpf.FK_Removed_For_UserID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<DocketStatusHistory>()
                .HasOne(h => h.Docket)
                .WithMany(d => d.DocketStatusHistories)
                .HasForeignKey(h => h.FK_DocketID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<EvidenceTransfer>()
                .HasOne(et => et.Evidence)
                .WithMany()
                .HasForeignKey(et => et.FK_EvidenceID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<EvidenceTransfer>()
                .HasOne(et => et.FromLocation)
                .WithMany()
                .HasForeignKey(et => et.FromLocationID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<EvidenceTransfer>()
                .HasOne(et => et.ToLocation)
                .WithMany()
                .HasForeignKey(et => et.ToLocationID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ProsecutorBundleItem>()
                .HasOne(i => i.ProsecutorBundle)
                .WithMany(b => b.Manifest)
                .HasForeignKey(i => i.FK_BundleID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserRoleHistory>()
                .HasOne(h => h.User)
                .WithMany()
                .HasForeignKey(h => h.FK_UserID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<InvestigationNoteArchiveRequest>()
                .HasOne(r => r.InvestigationNote)
                .WithMany()
                .HasForeignKey(r => r.FK_NoteID)
                .OnDelete(DeleteBehavior.Cascade);

        }
    }
}
