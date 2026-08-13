using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace BrickView;

public class IoFileReader
{
    public IoReadResult Read(
        string filePath)
    {
        try
        {
            using (ZipArchive archive =
                   ZipFile.OpenRead(filePath))
            {
                bool thumbnailFound = false;

                foreach (ZipArchiveEntry entry
                         in archive.Entries)
                {
                    string fileName =
                        Path.GetFileName(
                            entry.FullName);

                    if (string.Equals(
                        fileName,
                        "thumbnail.png",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        thumbnailFound = true;
                        break;
                    }
                }

                return new IoReadResult(
                    IoReadStatus.Success,
                    thumbnailFound);
            }
        }
        catch (FileNotFoundException)
        {
            return new IoReadResult(
                IoReadStatus.FileNotFound,
                false,
                errorMessage:
                    "The .io file could not be found.");
        }
        catch (DirectoryNotFoundException)
        {
            return new IoReadResult(
                IoReadStatus.FileNotFound,
                false,
                errorMessage:
                    "The directory containing the .io file could not be found.");
        }
        catch (UnauthorizedAccessException)
        {
            return new IoReadResult(
                IoReadStatus.AccessDenied,
                false,
                errorMessage:
                    "Access to the .io file was denied.");
        }
        catch (InvalidDataException)
        {
            return new IoReadResult(
                IoReadStatus.InvalidZip,
                false,
                errorMessage:
                    "The .io file is not a valid ZIP archive.");
        }
        catch (Exception exception)
        {
            return new IoReadResult(
                IoReadStatus.UnknownError,
                false,
                errorMessage:
                    exception.Message);
        }
    }

    public ThumbnailReadResult ReadThumbnail(
        string filePath)
    {
        try
        {
            using (ZipArchive archive =
                   ZipFile.OpenRead(filePath))
            {
                foreach (ZipArchiveEntry entry
                         in archive.Entries)
                {
                    string fileName =
                        Path.GetFileName(
                            entry.FullName);

                    if (string.Equals(
                        fileName,
                        "thumbnail.png",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        using (Stream stream =
                               entry.Open())
                        using (MemoryStream memoryStream =
                               new MemoryStream())
                        {
                            stream.CopyTo(
                                memoryStream);

                            return new ThumbnailReadResult(
                                ThumbnailReadStatus.Loaded,
                                memoryStream.ToArray());
                        }
                    }
                }

                return new ThumbnailReadResult(
                    ThumbnailReadStatus.Missing);
            }
        }
        catch (FileNotFoundException)
        {
            return new ThumbnailReadResult(
                ThumbnailReadStatus.Error,
                errorMessage:
                    "The .io file could not be found.");
        }
        catch (DirectoryNotFoundException)
        {
            return new ThumbnailReadResult(
                ThumbnailReadStatus.Error,
                errorMessage:
                    "The directory containing the .io file could not be found.");
        }
        catch (UnauthorizedAccessException)
        {
            return new ThumbnailReadResult(
                ThumbnailReadStatus.Error,
                errorMessage:
                    "Access to the .io file was denied.");
        }
        catch (InvalidDataException)
        {
            return new ThumbnailReadResult(
                ThumbnailReadStatus.InvalidFile,
                errorMessage:
                    "The .io file is not a valid ZIP archive.");
        }
        catch (Exception exception)
        {
            return new ThumbnailReadResult(
                ThumbnailReadStatus.Error,
                errorMessage:
                    exception.Message);
        }
    }

    public IoModelMetadata? ReadMetadata(
        string filePath)
    {
        try
        {
            using (ZipArchive archive =
                   ZipFile.OpenRead(filePath))
            {
                IoModelMetadata? infoMetadata =
                    ReadInfoMetadata(
                        archive);

                int? lDrawPartCount =
                    ReadLDrawPartCount(
                        archive);

                if (infoMetadata is null &&
                    !lDrawPartCount.HasValue)
                {
                    return null;
                }

                int? partCount =
                    infoMetadata?.PartCount;

                PartCountValidation validation =
                    PartCountValidation.Unknown;

                if (partCount.HasValue &&
                    lDrawPartCount.HasValue)
                {
                    validation =
                        partCount.Value ==
                        lDrawPartCount.Value
                            ? PartCountValidation.Match
                            : PartCountValidation.Mismatch;
                }
                else if (!partCount.HasValue &&
                         lDrawPartCount.HasValue)
                {
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
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (InvalidDataException)
        {
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static IoModelMetadata? ReadInfoMetadata(
        ZipArchive archive)
    {
        ZipArchiveEntry? metadataEntry =
            null;

        foreach (ZipArchiveEntry entry
                 in archive.Entries)
        {
            string fileName =
                Path.GetFileName(
                    entry.FullName);

            if (string.Equals(
                fileName,
                ".info",
                StringComparison.OrdinalIgnoreCase))
            {
                metadataEntry =
                    entry;

                break;
            }
        }

        if (metadataEntry is null)
        {
            return null;
        }

        try
        {
            using (Stream stream =
                   metadataEntry.Open())
            {
                using (JsonDocument document =
                       JsonDocument.Parse(stream))
                {
                    JsonElement root =
                        document.RootElement;

                    string? studioVersion =
                        null;

                    if (root.TryGetProperty(
                            "version",
                            out JsonElement versionElement) &&
                        versionElement.ValueKind ==
                            JsonValueKind.String)
                    {
                        studioVersion =
                            versionElement.GetString();
                    }

                    int? partCount =
                        null;

                    if (root.TryGetProperty(
                            "total_parts",
                            out JsonElement partCountElement) &&
                        partCountElement.ValueKind ==
                            JsonValueKind.Number &&
                        partCountElement.TryGetInt32(
                            out int parsedPartCount))
                    {
                        partCount =
                            parsedPartCount;
                    }

                    int? partsDatabaseVersion =
                        null;

                    if (root.TryGetProperty(
                            "parts_db_version",
                            out JsonElement partsDatabaseElement) &&
                        partsDatabaseElement.ValueKind ==
                            JsonValueKind.Number &&
                        partsDatabaseElement.TryGetInt32(
                            out int parsedPartsDatabaseVersion))
                    {
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
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static int? ReadLDrawPartCount(
        ZipArchive archive)
    {
        ZipArchiveEntry? modelEntry =
            null;

        foreach (ZipArchiveEntry entry
                 in archive.Entries)
        {
            string fileName =
                Path.GetFileName(
                    entry.FullName);

            if (string.Equals(
                fileName,
                "model.ldr",
                StringComparison.OrdinalIgnoreCase))
            {
                modelEntry =
                    entry;

                break;
            }
        }

        if (modelEntry is null)
        {
            return null;
        }

        using (Stream stream =
               modelEntry.Open())
        using (StreamReader reader =
               new StreamReader(
                   stream,
                   Encoding.UTF8,
                   true))
        {
            while (!reader.EndOfStream)
            {
                string? line =
                    reader.ReadLine();

                if (line is null)
                {
                    continue;
                }

                string trimmedLine =
                    line.Trim();

                if (!trimmedLine.StartsWith(
                        "0 NumOfBricks ",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                string value =
                    trimmedLine.Substring(
                        "0 NumOfBricks ".Length)
                    .Trim();

                if (int.TryParse(
                        value,
                        out int partCount))
                {
                    return partCount;
                }
            }
        }

        return null;
    }
}