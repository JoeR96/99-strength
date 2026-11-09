namespace A2S.Api.Authentication;

/// <summary>
/// Opts a controller or a single action into personal API token access. Without it a
/// token-authenticated request is answered with 403, so anything new is Clerk-session-only until
/// someone decides otherwise. The full list of marked routes is pinned by <c>ApiTokenAllowListTests</c>;
/// adding this attribute anywhere means updating that list on purpose.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class AllowApiTokenAttribute : Attribute
{
}
