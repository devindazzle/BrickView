// ============================================================================
// IoFileReader.cs
//
// Reads LEGO BrickLink Studio .io files.
//
// Studio .io files are ZIP-based containers. Older Studio files may contain
// password-protected ZIP entries using PKZIP/ZipCrypto encryption. SharpZipLib
// is used here because System.IO.Compression does not support reading these
// encrypted entries on .NET 10.
//
// The legacy Studio password is applied only when an individual ZIP entry is
// encrypted. Unencrypted .io files continue to be read normally.
//
// The reader provides three operations:
// - Validate an .io file and detect whether it contains a thumbnail.
// - Read the thumbnail PNG data.
// - Read model metadata and validate the part count.
//
// Copyright (c) 2026 BrickView
// Licensed under the MIT License.
// ============================================================================

using System.IO;
using System.Text;
using System.Text.Json;
using ICSharpCode.SharpZipLib.Zip;

namespace BrickView;

public class IoFileReader {
    // Older BrickLink Studio .io files may use this password for
    // PKZIP/ZipCrypto-protected entries.
    private const string StudioPassword =
        "soho0909";

    /// <summary>
    /// Validates an .io file and determines whether it contains a thumbnail.
    /// </summary>
    /// <param name="filePath">
    /// Full path to the .io file.
    /// </param>
    /// <returns>
    /// An IoReadResult describing whether the file could be read and whether
    /// a thumbnail entry was found.
    /// </returns>
    public IoReadResult Read(
        string filePath) {
        try {
            using (ZipFile archive =
                   new ZipFile(filePath)) {

                bool thumbnailFound =
                    false;

                // A thumbnail is optional in a Studio .io file. Inspect the
                // archive rather than treating a missing thumbnail as invalid.
                foreach (ZipEntry entry
                         in archive) {
                    string fileName =
                        Path.GetFileName(
                            entry.Name);

                    if (string.Equals(
                        fileName,
                        "thumbnail.png",
                        StringComparison.OrdinalIgnoreCase)) {

                        thumbnailFound =
                            true;

                        break;
                    }
                }

                return new IoReadResult(
                    IoReadStatus.Success,
                    thumbnailFound);
            }
        }
        catch (FileNotFoundException) {
            return new IoReadResult(
                IoReadStatus.FileNotFound,
                false,
                errorMessage:
                    "The .io file could not be found.");
        }
        catch (DirectoryNotFoundException) {
            return new IoReadResult(
                IoReadStatus.FileNotFound,
                false,
                errorMessage:
                    "The directory containing the .io file could not be found.");
        }
        catch (UnauthorizedAccessException) {
            return new IoReadResult(
                IoReadStatus.AccessDenied,
                false,
                errorMessage:
                    "Access to the .io file was denied.");
        }
        catch (ZipException) {
            // SharpZipLib reports malformed or unreadable ZIP containers
            // through ZipException. BrickView treats these as invalid .io
            // containers.
            return new IoReadResult(
                IoReadStatus.InvalidZip,
                false,
                errorMessage:
                    "The .io file is not a valid ZIP archive.");
        }
        catch (Exception exception) {
            return new IoReadResult(
                IoReadStatus.UnknownError,
                false,
                errorMessage:
                    exception.Message);
        }
    }

    /// <summary>
    /// Reads the thumbnail PNG from a Studio .io file.
    /// </summary>
    /// <param name="filePath">
    /// Full path to the .io file.
    /// </param>
    /// <returns>
    /// A ThumbnailReadResult containing the PNG data when a thumbnail is
    /// available.
    /// </returns>
    public ThumbnailReadResult ReadThumbnail(
        string filePath) {
        try {
            using (ZipFile archive =
                   new ZipFile(filePath)) {

                foreach (ZipEntry entry
                         in archive) {
                    string fileName =
                        Path.GetFileName(
                            entry.Name);

                    if (!string.Equals(
                        fileName,
                        "thumbnail.png",
                        StringComparison.OrdinalIgnoreCase)) {
                        continue;
                    }

                    // OpenEntryStream handles both encrypted and unencrypted
                    // ZIP entries transparently.
                    using (Stream stream =
                           OpenEntryStream(
                               archive,
                               entry))
                    using (MemoryStream memoryStream =
                           new MemoryStream()) {

                        stream.CopyTo(
                            memoryStream);

                        return new ThumbnailReadResult(
                            ThumbnailReadStatus.Loaded,
                            memoryStream.ToArray());
                    }
                }

                // A valid .io file does not necessarily contain a thumbnail.
                return new ThumbnailReadResult(
                    ThumbnailReadStatus.Missing);
            }
        }
        catch (FileNotFoundException) {
            return new ThumbnailReadResult(
                ThumbnailReadStatus.Error,
                errorMessage:
                    "The .io file could not be found.");
        }
        catch (DirectoryNotFoundException) {
            return new ThumbnailReadResult(
                ThumbnailReadStatus.Error,
                errorMessage:
                    "The directory containing the .io file could not be found.");
        }
        catch (UnauthorizedAccessException) {
            return new ThumbnailReadResult(
                ThumbnailReadStatus.Error,
                errorMessage:
                    "Access to the .io file was denied.");
        }
        catch (ZipException) {
            return new ThumbnailReadResult(
                ThumbnailReadStatus.InvalidFile,
                errorMessage:
                    "The .io file is not a valid ZIP archive.");
        }
        catch (Exception exception) {
            return new ThumbnailReadResult(
                ThumbnailReadStatus.Error,
                errorMessage:
                    exception.Message);
        }
    }

    /// <summary>
    /// Reads model metadata from a Studio .io file.
    ///
    /// Metadata is primarily read from the .info entry. The model.ldr file is
    /// also inspected to obtain a part count when necessary or to validate a
    /// part count already reported by .info.
    /// </summary>
    /// <param name="filePath">
    /// Full path to the .io file.
    /// </param>
    /// <returns>
    /// The available model metadata, or null when no usable metadata could be
    /// read.
    /// </returns>
    public IoModelMetadata? ReadMetadata(
        string filePath) {
        try {
            using (ZipFile archive =
                   new ZipFile(filePath)) {

                IoModelMetadata? infoMetadata =
                    ReadInfoMetadata(
                        archive);

                int? lDrawPartCount =
                    ReadLDrawPartCount(
                        archive);

                // If neither metadata source provided useful information,
                // there is nothing meaningful to return.
                if (infoMetadata is null &&
                    !lDrawPartCount.HasValue) {
                    return null;
                }

                int? partCount =
                    infoMetadata?.PartCount;

                PartCountValidation validation =
                    PartCountValidation.Unknown;

                // When both sources provide a part count, compare them so
                // inconsistencies can be surfaced by the metadata model.
                if (partCount.HasValue &&
                    lDrawPartCount.HasValue) {

                    validation =
                        partCount.Value ==
                        lDrawPartCount.Value
                            ? PartCountValidation.Match
                            : PartCountValidation.Mismatch;
                }
                else if (!partCount.HasValue &&
                         lDrawPartCount.HasValue) {

                    // Fall back to the count from model.ldr when .info does
                    // not contain a part count.
                    partCount =
                        lDrawPartCount;
                }

                return new IoModelMetadata(
                    partCount,
                    infoMetadata?.StudioVersion,
                    infoMetadata?.PartsDatabaseVersion,
                    lDrawPartCount,
                    validation);
            }
        }
        catch (FileNotFoundException) {
            return null;
        }
        catch (DirectoryNotFoundException) {
            return null;
        }
        catch (UnauthorizedAccessException) {
            return null;
        }
        catch (ZipException) {
            return null;
        }
        catch (Exception) {
            return null;
        }
    }

    /// <summary>
    /// Opens a ZIP entry for reading.
    ///
    /// Studio files can contain both encrypted and unencrypted entries. The
    /// legacy Studio password is therefore only assigned when the specific
    /// entry reports that it is encrypted.
    /// </summary>
    /// <param name="archive">
    /// The ZIP archive containing the entry.
    /// </param>
    /// <param name="entry">
    /// The ZIP entry to open.
    /// </param>
    /// <returns>
    /// A stream containing the decompressed entry data.
    /// </returns>
    private static Stream OpenEntryStream(
        ZipFile archive,
        ZipEntry entry) {
        if (entry.IsCrypted) {
            // Older Studio files use PKZIP/ZipCrypto encryption.
            // SharpZipLib requires the password to be supplied before an
            // encrypted entry can be read.
            archive.Password =
                StudioPassword;
        }

        return archive.GetInputStream(
            entry);
    }

    /// <summary>
    /// Reads the .info metadata entry from a Studio archive.
    ///
    /// The .info file contains information such as the Studio version, total
    /// part count and parts database version.
    /// </summary>
    /// <param name="archive">
    /// The open Studio ZIP archive.
    /// </param>
    /// <returns>
    /// Parsed metadata, or null when the .info entry is unavailable or cannot
    /// be parsed.
    /// </returns>
    private static IoModelMetadata? ReadInfoMetadata(
        ZipFile archive) {
        ZipEntry? metadataEntry =
            null;

        foreach (ZipEntry entry
                 in archive) {
            string fileName =
                Path.GetFileName(
                    entry.Name);

            if (string.Equals(
                fileName,
                ".info",
                StringComparison.OrdinalIgnoreCase)) {

                metadataEntry =
                    entry;

                break;
            }
        }

        if (metadataEntry is null) {
            return null;
        }

        try {
            using (Stream stream =
                   OpenEntryStream(
                       archive,
                       metadataEntry))
            using (JsonDocument document =
                   JsonDocument.Parse(stream)) {

                JsonElement root =
                    document.RootElement;

                string? studioVersion =
                    null;

                // Studio version is stored as a JSON string.
                if (root.TryGetProperty(
                        "version",
                        out JsonElement versionElement) &&
                    versionElement.ValueKind ==
                        JsonValueKind.String) {

                    studioVersion =
                        versionElement.GetString();
                }

                int? partCount =
                    null;

                // total_parts contains the part count reported by Studio
                // when the file was saved.
                if (root.TryGetProperty(
                        "total_parts",
                        out JsonElement partCountElement) &&
                    partCountElement.ValueKind ==
                        JsonValueKind.Number &&
                    partCountElement.TryGetInt32(
                        out int parsedPartCount)) {

                    partCount =
                        parsedPartCount;
                }

                int? partsDatabaseVersion =
                    null;

                // parts_db_version identifies the Studio parts database
                // version used when the model was saved.
                if (root.TryGetProperty(
                        "parts_db_version",
                        out JsonElement partsDatabaseElement) &&
                    partsDatabaseElement.ValueKind ==
                        JsonValueKind.Number &&
                    partsDatabaseElement.TryGetInt32(
                        out int parsedPartsDatabaseVersion)) {

                    partsDatabaseVersion =
                        parsedPartsDatabaseVersion;
                }

                return new IoModelMetadata(
                    partCount,
                    studioVersion,
                    partsDatabaseVersion,
                    null,
                    PartCountValidation.Unknown);
            }
        }
        catch (JsonException) {
            // Invalid JSON in .info should not make the entire .io file
            // unusable. The caller can still use information from model.ldr.
            return null;
        }
    }

    /// <summary>
    /// Reads the part count from model.ldr.
    ///
    /// Studio stores model information in LDraw-compatible text files.
    /// BrickView looks for the NumOfBricks meta command as a fallback or
    /// validation source for the part count.
    /// </summary>
    /// <param name="archive">
    /// The open Studio ZIP archive.
    /// </param>
    /// <returns>
    /// The part count when the NumOfBricks command can be found; otherwise
    /// null.
    /// </returns>
    private static int? ReadLDrawPartCount(
        ZipFile archive) {
        ZipEntry? modelEntry =
            null;

        foreach (ZipEntry entry
                 in archive) {
            string fileName =
                Path.GetFileName(
                    entry.Name);

            if (string.Equals(
                fileName,
                "model.ldr",
                StringComparison.OrdinalIgnoreCase)) {

                modelEntry =
                    entry;

                break;
            }
        }

        if (modelEntry is null) {
            return null;
        }

        using (Stream stream =
               OpenEntryStream(
                   archive,
                   modelEntry))
        using (StreamReader reader =
               new StreamReader(
                   stream,
                   Encoding.UTF8,
                   true)) {

            while (!reader.EndOfStream) {
                string? line =
                    reader.ReadLine();

                if (line is null) {
                    continue;
                }

                string trimmedLine =
                    line.Trim();

                // Studio writes the total number of bricks as:
                //
                // 0 NumOfBricks <count>
                //
                // Only this meta command is required here; the remaining
                // LDraw model content can be ignored.
                if (!trimmedLine.StartsWith(
                        "0 NumOfBricks ",
                        StringComparison.Ordinal)) {
                    continue;
                }

                string value =
                    trimmedLine
                        .Substring(
                            "0 NumOfBricks ".Length)
                        .Trim();

                if (int.TryParse(
                        value,
                        out int partCount)) {
                    return partCount;
                }
            }
        }

        return null;
    }
}