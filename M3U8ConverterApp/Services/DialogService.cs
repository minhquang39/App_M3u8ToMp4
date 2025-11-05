using Microsoft.Win32;

namespace M3U8ConverterApp.Services;

internal interface IDialogService
{
    string? BrowseForFfmpeg();
    string? BrowseForNm3u8DlRe();
    string? BrowseForOutput(string initialDirectory, string defaultFileName);
}

internal sealed class DialogService : IDialogService
{
    public string? BrowseForFfmpeg()
    {
        var dialog = new OpenFileDialog 
        {
            Filter = "FFmpeg executable|ffmpeg.exe|All files|*.*",
            Title = "Select ffmpeg executable"
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? BrowseForNm3u8DlRe()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "N_m3u8DL-RE executable|N_m3u8DL-RE.exe|All files|*.*",
            Title = "Select N_m3u8DL-RE executable"
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? BrowseForOutput(string initialDirectory, string defaultFileName)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "MP4 video|*.mp4|All files|*.*",
            Title = "Save converted video",
            FileName = defaultFileName,
            InitialDirectory = string.IsNullOrWhiteSpace(initialDirectory) ? null : initialDirectory
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
