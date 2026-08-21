// -----------------------------------------------------------------------------
// PartCountValidation.cs
//
// Defines the validation state of the part count reported for a BrickView
// model.
//
// The validation state is used to distinguish between a part count that has
// not been validated, one that matches the expected value, and one that does
// not match.
// -----------------------------------------------------------------------------

namespace BrickView;

/// <summary>
/// Represents the validation state of a model's part count.
/// </summary>
public enum PartCountValidation {
    /// <summary>
    /// Indicates that the part count has not been validated.
    /// </summary>
    Unknown,

    /// <summary>
    /// Indicates that the actual part count matches the expected part count.
    /// </summary>
    Match,

    /// <summary>
    /// Indicates that the actual part count does not match the expected part count.
    /// </summary>
    Mismatch
}