using System.IO;
using System.IO.Compression;

namespace BrickView;

public class IoFileReader
{
    public IoReadResult Read(string filePath)
    {
        try
        {
            using (ZipArchive archive = ZipFile.OpenRead(filePath))
            {
                bool thumbnailFound = false;

                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string fileName = Path.GetFileName(entry.FullName);

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
                errorMessage: "The .io file could not be found.");
        }
        catch (DirectoryNotFoundException)
        {
            return new IoReadResult(
                IoReadStatus.FileNotFound,
                false,
                errorMessage: "The directory containing the .io file could not be found.");
        }
        catch (UnauthorizedAccessException)
        {
            return new IoReadResult(
                IoReadStatus.AccessDenied,
                false,
                errorMessage: "Access to the .io file was denied.");
        }
        catch (InvalidDataException)
        {
            return new IoReadResult(
                IoReadStatus.InvalidZip,
                false,
                errorMessage: "The .io file is not a valid ZIP archive.");
        }
        catch (Exception exception)
        {
            return new IoReadResult(
                IoReadStatus.UnknownError,
                false,
                errorMessage: exception.Message);
        }
    }

    public byte[]? ReadThumbnail(string filePath)
    {
        using (ZipArchive archive = ZipFile.OpenRead(filePath))
        {
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                string fileName = Path.GetFileName(entry.FullName);

                if (string.Equals(
                    fileName,
                    "thumbnail.png",
                    StringComparison.OrdinalIgnoreCase))
                {
                    using (Stream stream = entry.Open())
                    using (MemoryStream memoryStream = new MemoryStream())
                    {
                        stream.CopyTo(memoryStream);

                        return memoryStream.ToArray();
                    }
                }
            }
        }

        return null;
    }
}