using System.Text.RegularExpressions;

namespace HarborOps.Domain.Organizations;

public enum OrganizationRole { Analyst, Manager, Support, Administrator, Auditor }
public enum OrganizationPermission { ViewOrganization, ViewMemberships, ManageMemberships, UpdateOrganization, ViewReferenceData, ManageReferenceData, ViewVoyageData, ManageVoyageData, ViewContainerData, ManageContainerData, ViewShipmentData, ManageShipmentData, ViewShipmentAssignments, ManageShipmentAssignments, ViewAuditEvents }

public static class OrganizationPolicy
{
    private static readonly Regex SlugPattern = new("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static string NormalizeSlug(string slug)
    {
        var normalized = (slug ?? "").Trim().ToLowerInvariant();
        if (normalized.Length is < 3 or > 80 || !SlugPattern.IsMatch(normalized))
            throw new ArgumentException("Invalid organization slug");
        return normalized;
    }

    public static string ValidateDisplayName(string name)
    {
        var value = (name ?? "").Trim();
        if (value.Length is < 1 or > 160 || value.Any(char.IsControl))
            throw new ArgumentException("Invalid display name");
        return value;
    }

    public static bool ParseRole(string? input, out OrganizationRole role)
    {
        role = default;
        return input is not null &&
            Enum.TryParse(input, ignoreCase: false, out role) &&
            Enum.IsDefined(role) &&
            string.Equals(input, role.ToString(), StringComparison.Ordinal);
    }

    public static bool ValidStatus(string? value) =>
        value is "Active" or "Suspended" or "Removed";

    public static bool Allows(OrganizationRole role, OrganizationPermission permission) =>
        permission == OrganizationPermission.ViewOrganization ||
        (permission == OrganizationPermission.ViewAuditEvents && role == OrganizationRole.Auditor) ||
        (permission == OrganizationPermission.ViewShipmentAssignments && role is (OrganizationRole.Analyst or OrganizationRole.Manager or OrganizationRole.Auditor)) ||
        (permission == OrganizationPermission.ManageShipmentAssignments && role is (OrganizationRole.Analyst or OrganizationRole.Manager)) ||
        (permission == OrganizationPermission.ViewShipmentData && role is
            (OrganizationRole.Analyst or OrganizationRole.Manager or OrganizationRole.Auditor)) ||
        (permission == OrganizationPermission.ManageShipmentData && role is
            (OrganizationRole.Analyst or OrganizationRole.Manager)) ||
        (permission == OrganizationPermission.ViewReferenceData && (role is OrganizationRole.Analyst or OrganizationRole.Manager)) ||
        (permission is (OrganizationPermission.ViewVoyageData or OrganizationPermission.ViewContainerData) &&
            role is (OrganizationRole.Analyst or OrganizationRole.Manager)) ||
        (permission is (OrganizationPermission.ManageVoyageData or OrganizationPermission.ManageContainerData) &&
            role == OrganizationRole.Manager) ||
        (permission == OrganizationPermission.ManageReferenceData && role == OrganizationRole.Manager) ||
        (role == OrganizationRole.Administrator &&
            (permission == OrganizationPermission.ViewMemberships ||
             permission == OrganizationPermission.ManageMemberships ||
             permission == OrganizationPermission.UpdateOrganization));
}
