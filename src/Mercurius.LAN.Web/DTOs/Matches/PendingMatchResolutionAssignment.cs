using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mercurius.LAN.Web.DTOs.Matches;

/// <summary>How a waiting match relates to the signed-in administrator.</summary>
public enum PendingMatchResolutionAssignment
{
    /// <summary>The tournament's assigned admin is the signed-in administrator.</summary>
    Assigned = 0,

    /// <summary>The tournament has no assigned admin.</summary>
    Unassigned = 1,

    /// <summary>The assigned user is no longer an administrator.</summary>
    AssignedAdminUnavailable = 2,

    /// <summary>Waiting longer than about ten minutes, so every administrator sees it.</summary>
    Escalated = 3
}

/// <summary>Reads the assignment as a string and maps a missing or unknown value (older or newer backend) to null.</summary>
public sealed class PendingMatchResolutionAssignmentConverter : JsonConverter<PendingMatchResolutionAssignment?>
{
    public override PendingMatchResolutionAssignment? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if(reader.TokenType == JsonTokenType.String &&
           Enum.TryParse<PendingMatchResolutionAssignment>(reader.GetString(), ignoreCase: true, out var value) &&
           Enum.IsDefined(value))
            return value;

        // Numbers, objects and arrays are skipped so one unexpected value never fails the whole list.
        reader.Skip();
        return null;
    }

    public override void Write(Utf8JsonWriter writer, PendingMatchResolutionAssignment? value, JsonSerializerOptions options)
    {
        if(value.HasValue)
            writer.WriteStringValue(value.Value.ToString());
        else
            writer.WriteNullValue();
    }
}
