using CityPark.Application.Common;

namespace CityPark.Api.Auth;

/// <summary>Named authorization policies, so endpoints refer to a policy and not to role strings.</summary>
public static class AuthPolicies
{
    public const string Admin = AppRoles.Admin;
}
