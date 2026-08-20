// -----------------------------------------------------------------------------
// ModelIdentity.cs
//
// Represents the stable identity of a BrickView model independently of its
// current file path or file name.
//
// The actual Windows file identity is resolved elsewhere. This class keeps
// the domain layer independent of Windows-specific file system APIs.
// -----------------------------------------------------------------------------

namespace BrickView;

public sealed class ModelIdentity {
    public string Value { get; }

    public ModelIdentity(
        string value) {
        if (string.IsNullOrWhiteSpace(value)) {
            throw new ArgumentException(
                "A model identity cannot be empty.",
                nameof(value));
        }

        Value =
            value;
    }

    public override string ToString() {
        return Value;
    }

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

    public override int GetHashCode() {
        return StringComparer.Ordinal.GetHashCode(
            Value);
    }
}