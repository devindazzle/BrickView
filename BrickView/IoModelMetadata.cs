// -----------------------------------------------------------------------------
// IoModelMetadata.cs
//
// Represents metadata extracted from a BrickLink Studio .io model file.
//
// The model stores the part count reported by Studio, Studio and parts database
// versions, an optional part count obtained from model.ldr, and the validation
// result when both part-count sources are available.
//
// The class contains data only. Parsing and validation are handled by
// IoFileReader.
// -----------------------------------------------------------------------------

namespace BrickView;

/// <summary>
/// Contains metadata extracted from a BrickLink Studio .io model.
/// </summary>
public sealed class IoModelMetadata {
    /// <summary>
    /// Creates a model metadata instance.
    /// </summary>
    /// <param name="partCount">
    /// The part count reported by Studio's .info metadata.
    /// </param>
    /// <param name="studioVersion">
    /// The Studio version recorded in the model.
    /// </param>
    /// <param name="partsDatabaseVersion">
    /// The Studio parts database version recorded in the model.
    /// </param>
    /// <param name="lDrawPartCount">
    /// The part count extracted from model.ldr, when available.
    /// </param>
    /// <param name="partCountValidation">
    /// The validation result when the two part-count sources can be compared.
    /// </param>
    public IoModelMetadata(
        int? partCount,
        string? studioVersion,
        int? partsDatabaseVersion,
        int? lDrawPartCount,
        PartCountValidation partCountValidation) {
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

    /// <summary>
    /// Gets the part count reported by Studio's .info metadata.
    /// </summary>
    public int? PartCount {
        get;
    }

    /// <summary>
    /// Gets the Studio version recorded in the model.
    /// </summary>
    public string? StudioVersion {
        get;
    }

    /// <summary>
    /// Gets the Studio parts database version recorded in the model.
    /// </summary>
    public int? PartsDatabaseVersion {
        get;
    }

    /// <summary>
    /// Gets the part count extracted from model.ldr, when available.
    /// </summary>
    public int? LDrawPartCount {
        get;
    }

    /// <summary>
    /// Gets the validation result comparing the available part-count sources.
    /// </summary>
    public PartCountValidation PartCountValidation {
        get;
    }
}