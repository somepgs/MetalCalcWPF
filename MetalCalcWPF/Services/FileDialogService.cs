using MetalCalcWPF.Services.Interfaces;
using Microsoft.Win32;

namespace MetalCalcWPF.Services
{
    public class FileDialogService : IFileDialogService
    {
        public string? ShowSaveFileDialog(string defaultFileName, string filter)
        {
            var saveDialog = new SaveFileDialog
            {
                FileName = defaultFileName,
                Filter = filter
            };

            return saveDialog.ShowDialog() == true ? saveDialog.FileName : null;
        }
    }
}
