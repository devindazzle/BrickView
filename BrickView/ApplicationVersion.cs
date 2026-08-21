// -----------------------------------------------------------------------------
// ApplicationVersion.cs
//
// Provides the BrickView application version used by the user interface.
//
// The version is read from the assembly metadata generated from the project
// version, so the UI does not contain a separately maintained version string.
// -----------------------------------------------------------------------------

using System.Reflection;

namespace BrickView;

public static class ApplicationVersion {
    public static string DisplayName {
        get {
            Version? version =
                Assembly.GetExecutingAssembly()
                    .GetName()
                    .Version;

            if (version is null) {
                return "v0.0.0";
            }

            return
                $"v{version.Major}.{version.Minor}.{version.Build}";
        }
    }
}