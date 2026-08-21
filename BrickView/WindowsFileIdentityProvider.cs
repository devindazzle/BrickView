// -----------------------------------------------------------------------------
// WindowsFileIdentityProvider.cs
//
// Provides a stable identity for files on Windows.
//
// BrickView uses this identity to distinguish a renamed file from a deleted
// file followed by a newly added file. The identity is based on the Windows
// file system's volume serial number and file index rather than the file path.
//
// This class contains the Windows-specific interop required to obtain that
// information. The rest of the BrickView domain remains independent of the
// Windows API.
//
// The provider intentionally returns null when the file cannot be identified.
// Callers can then decide how to handle files for which Windows does not
// provide the required identity information.
// -----------------------------------------------------------------------------

using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

namespace BrickView;

/// <summary>
/// Provides stable model identities for files using Windows file-system
/// metadata rather than file paths.
/// </summary>
/// <remarks>
/// The combination of volume serial number and file index is used to identify
/// the physical file represented by a Windows file handle. This allows
/// BrickView to recognize a file after it has been renamed without treating
/// the renamed file as a new model.
///
/// This class contains the Windows-specific interop boundary. The returned
/// value is converted into BrickView's platform-independent
/// <see cref="ModelIdentity"/> type before leaving the provider.
/// </remarks>
public sealed class WindowsFileIdentityProvider {
    /// <summary>
    /// Attempts to obtain a stable identity for the specified Windows file.
    /// </summary>
    /// <param name="filePath">
    /// The path of the file whose stable identity should be retrieved.
    /// </param>
    /// <returns>
    /// A <see cref="ModelIdentity"/> when the file can be opened and its
    /// Windows file information can be read; otherwise, <see langword="null"/>.
    /// </returns>
    /// <remarks>
    /// The file is opened with read/write/delete sharing so that obtaining its
    /// identity does not prevent other application operations such as writing,
    /// renaming or deleting the file.
    /// </remarks>
    public ModelIdentity? TryGetIdentity(
        string filePath) {
        if (string.IsNullOrWhiteSpace(
                filePath)) {
            return null;
        }

        if (!File.Exists(
                filePath)) {
            return null;
        }

        // FILE_FLAG_BACKUP_SEMANTICS allows CreateFile to obtain a handle using
        // the Windows file-system semantics required by the identity API.
        using SafeFileHandle fileHandle =
            CreateFile(
                filePath,
                0,
                FileShare.ReadWrite |
                FileShare.Delete,
                IntPtr.Zero,
                OPEN_EXISTING,
                FILE_FLAG_BACKUP_SEMANTICS,
                IntPtr.Zero);

        if (fileHandle.IsInvalid) {
            return null;
        }

        if (!GetFileInformationByHandle(
                fileHandle,
                out BY_HANDLE_FILE_INFORMATION fileInformation)) {
            return null;
        }

        string identity =
            CreateIdentityValue(
                fileInformation);

        return new ModelIdentity(
            identity);
    }

    /// <summary>
    /// Creates the stable BrickView identity string from Windows file metadata.
    /// </summary>
    /// <param name="fileInformation">
    /// The file information returned by Windows for the opened file handle.
    /// </param>
    /// <returns>
    /// A normalized identity string consisting of the volume serial number
    /// and file index.
    /// </returns>
    /// <remarks>
    /// The volume serial number identifies the volume. The file index identifies
    /// the file on that volume. Together they provide the stable file identity
    /// required by BrickView.
    /// </remarks>
    private static string CreateIdentityValue(
        BY_HANDLE_FILE_INFORMATION fileInformation) {
        return string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{fileInformation.VolumeSerialNumber:X8}-" +
            $"{fileInformation.FileIndexHigh:X8}" +
            $"{fileInformation.FileIndexLow:X8}");
    }

    /// <summary>
    /// Specifies that CreateFile should open an existing file rather than
    /// create or overwrite it.
    /// </summary>
    private const uint OPEN_EXISTING = 3;

    /// <summary>
    /// Specifies the Windows file-opening behavior required when obtaining
    /// file-system information through CreateFile.
    /// </summary>
    private const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;

    /// <summary>
    /// Opens a Windows file handle used to obtain file-system metadata.
    /// </summary>
    /// <param name="fileName">
    /// The path of the file to open.
    /// </param>
    /// <param name="desiredAccess">
    /// The requested access rights for the handle.
    /// </param>
    /// <param name="shareMode">
    /// The sharing mode applied to the opened file.
    /// </param>
    /// <param name="securityAttributes">
    /// Optional Windows security attributes.
    /// </param>
    /// <param name="creationDisposition">
    /// Specifies how the file should be opened or created.
    /// </param>
    /// <param name="flagsAndAttributes">
    /// Windows file flags and attributes.
    /// </param>
    /// <param name="templateFile">
    /// Optional template file handle.
    /// </param>
    /// <returns>
    /// A safe Windows file handle.
    /// </returns>
    [DllImport(
        "kernel32.dll",
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    private static extern SafeFileHandle CreateFile(
        string fileName,
        uint desiredAccess,
        FileShare shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    /// <summary>
    /// Retrieves file-system information for an opened Windows file handle.
    /// </summary>
    /// <param name="fileHandle">
    /// The Windows file handle for which information should be retrieved.
    /// </param>
    /// <param name="fileInformation">
    /// Receives the file-system information returned by Windows.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the information was retrieved successfully;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    [DllImport(
        "kernel32.dll",
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(
        SafeFileHandle fileHandle,
        out BY_HANDLE_FILE_INFORMATION fileInformation);

    /// <summary>
    /// Represents the native Windows structure containing metadata for an
    /// opened file handle.
    /// </summary>
    [StructLayout(
        LayoutKind.Sequential)]
    private struct BY_HANDLE_FILE_INFORMATION {
        /// <summary>
        /// Gets or sets the native Windows file attribute flags.
        /// </summary>
        public uint FileAttributes;

        /// <summary>
        /// Gets or sets the native file creation timestamp.
        /// </summary>
        public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;

        /// <summary>
        /// Gets or sets the native last-access timestamp.
        /// </summary>
        public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;

        /// <summary>
        /// Gets or sets the native last-write timestamp.
        /// </summary>
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;

        /// <summary>
        /// Gets or sets the serial number of the volume containing the file.
        /// </summary>
        public uint VolumeSerialNumber;

        /// <summary>
        /// Gets or sets the high 32 bits of the file size.
        /// </summary>
        public uint FileSizeHigh;

        /// <summary>
        /// Gets or sets the low 32 bits of the file size.
        /// </summary>
        public uint FileSizeLow;

        /// <summary>
        /// Gets or sets the number of hard links associated with the file.
        /// </summary>
        public uint NumberOfLinks;

        /// <summary>
        /// Gets or sets the high 32 bits of the Windows file index.
        /// </summary>
        public uint FileIndexHigh;

        /// <summary>
        /// Gets or sets the low 32 bits of the Windows file index.
        /// </summary>
        public uint FileIndexLow;
    }
}