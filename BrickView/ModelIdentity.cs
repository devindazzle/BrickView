// -----------------------------------------------------------------------------
// ModelIdentity.cs
//
// Represents the stable domain identity of a BrickView model independently of
// its current file path or file name.
//
// Responsibilities:
// - Stores the stable identity value of a model.
// - Prevents creation of an invalid empty identity.
// - Provides value-based equality and hashing so identities can safely be
//   compared and used as dictionary or set keys.
//
// The actual Windows file identity is resolved elsewhere. This class keeps the
// domain layer independent of Windows-specific file system APIs.
// -----------------------------------------------------------------------------

namespace BrickView;

/// <summary>
/// Represents the stable identity of a BrickView model independently of its
/// current file path or file name.
/// </summary>
public sealed class ModelIdentity {
    /// <summary>
    /// Gets the stable identity value of the model.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Initializes a new model identity.
    /// </summary>
    /// <param name="value">
    /// The stable identity value assigned to the model.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="value"/> is null, empty or consists only
    /// of white-space characters.
    /// </exception>
    public ModelIdentity(
        string value) {
        // A model identity is the key used to recognize the same model across
        // file renames, so an empty identity cannot represent a valid model.
        if (string.IsNullOrWhiteSpace(value)) {
            throw new ArgumentException(
                "A model identity cannot be empty.",
                nameof(value));
        }

        Value =
            value;
    }

    /// <summary>
    /// Returns the identity value as its string representation.
    /// </summary>
    /// <returns>The stable identity value.</returns>
    public override string ToString() {
        return Value;
    }

    /// <summary>
    /// Determines whether this identity is equal to another object.
    /// Equality is based on the exact identity value using ordinal comparison.
    /// </summary>
    /// <param name="obj">
    /// The object to compare with this identity.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when <paramref name="obj"/> is another
    /// <see cref="ModelIdentity"/> with the same identity value;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    public override bool Equals(
        object? obj) {
        if (obj is not ModelIdentity other) {
            return false;
        }

        return string.Equals(
            Value,
            other.Value,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Returns a hash code based on the stable identity value using ordinal
    /// string comparison semantics.
    /// </summary>
    /// <returns>The hash code for this model identity.</returns>
    public override int GetHashCode() {
        return StringComparer.Ordinal.GetHashCode(
            Value);
    }
}