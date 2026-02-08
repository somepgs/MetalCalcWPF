namespace MetalCalcWPF.Services.Interfaces
{
    public interface IFileDialogService
    {
        string? ShowSaveFileDialog(string defaultFileName, string filter);
    }
}
