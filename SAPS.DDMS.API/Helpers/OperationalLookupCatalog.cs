namespace SAPS.DDMS.API.Helpers
{
    public static class OperationalLookupCatalog
    {
        public static readonly (string TypeName, string Description)[] DefaultIncidentTypes =
        {
            ("Robbery", "Theft by force"),
            ("Burglary", "Breaking and entering"),
            ("Assault GBH", "Grievous bodily harm"),
            ("Murder", "Unlawful killing"),
            ("Sexual Offense", "Sexual assault"),
            ("Fraud", "Financial crime"),
            ("Corruption", "Financial crime"),
            ("Money Laundering", "Financial crime"),
            ("Theft", "Property theft"),
            ("Drug Related", "Narcotics offense"),
            ("Hijacking", "Vehicle hijacking"),
            ("Domestic Violence", "Domestic violence complaint"),
            ("Kidnapping", "Abduction or kidnapping"),
            ("Attempted Murder", "Attempted unlawful killing"),
            ("Arson", "Intentional fire setting"),
            ("Human Trafficking", "Trafficking in persons"),
            ("Cybercrime", "Digital or online criminal activity"),
            ("Stock Theft", "Theft of livestock"),
            ("Illegal Firearm Possession", "Unlawful possession of a firearm"),
            ("Public Violence", "Public disorder or violence")
        };

        public static readonly string[] IncidentLocationTypes =
        {
            "Residential Property",
            "Business Premises",
            "Street / Road",
            "Highway",
            "Public Transport Hub",
            "School / Campus",
            "Hospital / Clinic",
            "Government Facility",
            "Open Field / Rural Area",
            "Shopping Centre",
            "Informal Settlement",
            "Police Station",
            "Correctional Facility",
            "Other"
        };

        public static readonly string[] EvidenceCollectionMethods =
        {
            "Photographed",
            "Bagged and Tagged",
            "Fingerprint Lift",
            "DNA Swab",
            "Digital Extraction",
            "Scene Seizure",
            "Voluntary Handover",
            "Search and Seizure",
            "CCTV Retrieval",
            "Forensic Collection"
        };

        public static readonly string[] EvidenceItemStates =
        {
            "Sealed",
            "Unsealed",
            "Intact",
            "Damaged",
            "Wet",
            "Dry",
            "Contaminated",
            "Fragile",
            "Operational",
            "Inoperable"
        };

        public static bool ContainsIgnoreCase(IEnumerable<string> allowedValues, string? candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate))
                return false;

            return allowedValues.Any(value =>
                string.Equals(value, candidate.Trim(), StringComparison.OrdinalIgnoreCase));
        }
    }
}
