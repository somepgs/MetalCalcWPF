using System.Windows;
using MetalCalcWPF.Services;
using MetalCalcWPF.Services.Interfaces;
using MetalCalcWPF.ViewModels;

namespace MetalCalcWPF
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            IDatabaseService databaseService = new DatabaseService();
            IMessageService messageService = new MessageService();
            IFileDialogService fileDialogService = new FileDialogService();
            IWindowService windowService = new WindowService(databaseService, messageService);
            var calculationService = new CalculationService(databaseService);

            DataContext = new MainViewModel(databaseService, windowService, fileDialogService, messageService, calculationService);
        }
    }
}
