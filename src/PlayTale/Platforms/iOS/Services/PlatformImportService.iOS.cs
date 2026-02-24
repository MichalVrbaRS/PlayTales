using PlayTale.Features.Audiobooks.Models;

namespace PlayTale.Features.Audiobooks.Services;

public sealed partial class PlatformImportService
{
#if IOS
    private async partial Task<IReadOnlyList<ImportSource>> PickPlatformSourcesAsync(CancellationToken cancellationToken)
    {
        var pickedFiles = await FilePicker.Default.PickMultipleAsync(new PickOptions
        {
            PickerTitle = "Select audiobook files (folder selection will be added in native picker step)",
            FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.iOS, new[] { "public.mp3", "public.audio" } }
            })
        });

        var files = (pickedFiles ?? Array.Empty<FileResult>())
            .Select(x => (x.FileName, x.FullPath ?? string.Empty))
            .ToList();

        return BuildSourcesFromPaths(files);
    }
#endif
}
