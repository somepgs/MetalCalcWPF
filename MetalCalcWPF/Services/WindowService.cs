using MetalCalcWPF.Services.Interfaces;
using MetalCalcWPF.ViewModels;

namespace MetalCalcWPF.Services
{
    public class WindowService : IWindowService
    {
        private readonly IDatabaseService _databaseService;
        private readonly IMessageService _messageService;

        public WindowService(IDatabaseService databaseService, IMessageService messageService)
        {
            _databaseService = databaseService;
            _messageService = messageService;
        }

        public void ShowSettings()
        {
            var window = new SettingsWindow
            {
                DataContext = new SettingsViewModel(_databaseService, _messageService)
            };

            window.ShowDialog();
        }

        public void ShowDatabaseEditor()
        {
            var window = new DataEditWindow
            {
                DataContext = new DataEditViewModel(_databaseService, _messageService)
            };

            window.ShowDialog();
        }
    }
}
