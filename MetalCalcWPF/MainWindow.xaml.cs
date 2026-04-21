using System.Windows;
using MetalCalcWPF.Services;
using MetalCalcWPF.Services.Interfaces;
using MetalCalcWPF.Services.Logging;
using MetalCalcWPF.ViewModels;

namespace MetalCalcWPF
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // Логгер создаём первым — чтобы зафиксировать всё, что делает DatabaseService
            // при старте (миграции, первичное заполнение, возможные ошибки).
            // Путь: %MyDocuments%\MetalCalc\logs\YYYY-MM-DD.log
            IAppLogger log = FileAppLogger.CreateDefault();
            log.Info("=== Запуск MetalCalcWPF ===");

            IDatabaseService databaseService = new DatabaseService(
                System.IO.Path.Combine(
                    System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments),
                    "MetalCalc", "workshop.db"),
                log);

            IMessageService messageService = new MessageService();
            IFileDialogService fileDialogService = new FileDialogService();
            IWindowService windowService = new WindowService(databaseService, messageService);
            var calculationService = new CalculationService(databaseService, log);

            DataContext = new MainViewModel(databaseService, windowService, fileDialogService, messageService, calculationService);
        }
    }
}
