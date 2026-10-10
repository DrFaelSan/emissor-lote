using Windows.Storage.Pickers;
using WinRT.Interop;

namespace NEO_e.WinUI.Services;

public sealed class PickerService
{
    private IntPtr _windowHandle;

    public void Initialize(IntPtr windowHandle)
    {
        _windowHandle = windowHandle;
    }

    public async Task<string?> PickFolderAsync()
    {
        var picker = new FolderPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary
        };
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow(picker);

        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }

    public async Task<string?> PickSaveFileAsync(string suggestedFileName, params (string Name, string Extension)[] fileTypes)
    {
        if (fileTypes.Length == 0)
            throw new ArgumentException("Ao menos um tipo de arquivo deve ser informado.", nameof(fileTypes));

        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = suggestedFileName
        };
        foreach (var (name, extension) in fileTypes)
            picker.FileTypeChoices.Add(name, new List<string> { extension });
        InitializeWithWindow(picker);

        var file = await picker.PickSaveFileAsync();
        return file?.Path;
    }

    private void InitializeWithWindow(object picker)
    {
        if (_windowHandle == IntPtr.Zero)
            throw new InvalidOperationException("Janela da aplicacao nao inicializada para exibir o seletor de arquivos.");

        WinRT.Interop.InitializeWithWindow.Initialize(picker, _windowHandle);
    }
}