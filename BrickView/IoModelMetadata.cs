namespace BrickView;

public sealed class IoModelMetadata
{
    public IoModelMetadata(
        int? partCount,
        string? studioVersion,
        int? partsDatabaseVersion,
        int? lDrawPartCount,
        PartCountValidation partCountValidation)
    {
        PartCount =
            partCount;

        StudioVersion =
            studioVersion;

        PartsDatabaseVersion =
            partsDatabaseVersion;

        LDrawPartCount =
            lDrawPartCount;

        PartCountValidation =
            partCountValidation;
    }

    public int? PartCount
    {
        get;
    }

    public string? StudioVersion
    {
        get;
    }

    public int? PartsDatabaseVersion
    {
        get;
    }

    public int? LDrawPartCount
    {
        get;
    }

    public PartCountValidation PartCountValidation
    {
        get;
    }
}