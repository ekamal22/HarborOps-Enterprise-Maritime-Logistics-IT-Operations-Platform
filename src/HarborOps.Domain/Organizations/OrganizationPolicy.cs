using System.Text.RegularExpressions;

namespace HarborOps.Domain.Organizations;

public enum OrganizationRole { Analyst, Manager, Support, Administrator, Auditor }
public enum OrganizationPermission { ViewOrganization, ViewMemberships, ManageMemberships, UpdateOrganization }

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

    public static bool ParseRole(string? input, out OrganizationRole role) =>
        Enum.TryParse(input, ignoreCase: false, out role) && Enum.IsDefined(role);

    public static bool ValidStatus(string? value) =>
        value is "Active" or "Suspended" or "Removed";

    public static bool Allows(OrganizationRole role, OrganizationPermission permission) =>
        permission == OrganizationPermission.ViewOrganization ||
        (role == OrganizationRole.Administrator &&
            (permission == OrganizationPermission.ViewMemberships ||
             permission == OrganizationPermission.ManageMemberships ||
             permission == OrganizationPermission.UpdateOrganization));
}
