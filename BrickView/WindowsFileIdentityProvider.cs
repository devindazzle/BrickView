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
// -----------------------------------------------------------------------------

using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

namespace BrickView;

public sealed class WindowsFileIdentityProvider {
    public ModelIdentity? TryGetIdentity(
        string filePath) {
        if (string.IsNullOrWhiteSpace(filePath)) {
            return null;
        }

        if (!File.Exists(filePath)) {
            return null;
        }

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

    private static string CreateIdentityValue(
        BY_HANDLE_FILE_INFORMATION fileInformation) {
        // The volume serial number identifies the volume. The file index
        // identifies the file on that volume. Together they provide the
        // stable file identity required by BrickView.
        return string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{fileInformation.VolumeSerialNumber:X8}-" +
            $"{fileInformation.FileIndexHigh:X8}" +
            $"{fileInformation.FileIndexLow:X8}");
    }

    private const uint OPEN_EXISTING = 3;

    private const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;

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

    [DllImport(
        "kernel32.dll",
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(
        SafeFileHandle fileHandle,
        out BY_HANDLE_FILE_INFORMATION fileInformation);

    [StructLayout(
        LayoutKind.Sequential)]
    private struct BY_HANDLE_FILE_INFORMATION {
        public uint FileAttributes;

        public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;

        public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;

        public System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;

        public uint VolumeSerialNumber;

        public uint FileSizeHigh;

        public uint FileSizeLow;

        public uint NumberOfLinks;

        public uint FileIndexHigh;

        public uint FileIndexLow;
    }
}