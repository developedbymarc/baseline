using backend.Services.Missions;

namespace backend.Auth;

public enum CallerRole
{
    Crew,
    Ground,
}

// Mock auth for the demo (spec: mock login only, no real authentication).
// The client sends   X-Role: crew | ground   and, for crew,   X-Crew-Id: <crew member guid>.
// Production would replace this with real authentication and encryption at rest (spec section 6).
public sealed record Caller(CallerRole Role, Guid? CrewId)
{
    public const string RoleHeader = "X-Role";
    public const string CrewIdHeader = "X-Crew-Id";

    public bool IsGround => Role == CallerRole.Ground;

    public static Caller From(HttpRequest request)
    {
        var role = request.Headers[RoleHeader].ToString().Trim().ToLowerInvariant();
        switch (role)
        {
            case "ground":
                return new Caller(CallerRole.Ground, null);
            case "crew":
                var raw = request.Headers[CrewIdHeader].ToString().Trim();
                if (!Guid.TryParse(raw, out var id))
                    throw new ApiProblem(401, "NO_CREW_ID", $"Role crew needs a valid {CrewIdHeader} header (crew member id).");
                return new Caller(CallerRole.Crew, id);
            case "":
                throw new ApiProblem(401, "NO_ROLE", $"Send the {RoleHeader} header: crew or ground.");
            default:
                throw new ApiProblem(401, "NO_ROLE", $"{RoleHeader} must be crew or ground, not '{role}'.");
        }
    }

    public void RequireGround()
    {
        if (!IsGround) throw new ApiProblem(403, "FORBIDDEN", "Only ground can use this endpoint.");
    }

    // Crew may act only on their own data.
    public void RequireCrew(Guid crewId)
    {
        if (Role != CallerRole.Crew) throw new ApiProblem(403, "FORBIDDEN", "Only the crew member can do this.");
        if (CrewId != crewId) throw new ApiProblem(403, "FORBIDDEN", "Crew members can only access their own data.");
    }

    // Crew: own data. Ground: any crew member (delivered data only; the caller filters).
    public void RequireCrewOrGround(Guid crewId)
    {
        if (!IsGround) RequireCrew(crewId);
    }
}
