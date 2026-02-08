using System;
using System.Collections.ObjectModel;
using System.Linq;
using ClosedXML.Excel;
using MetalCalcWPF.Infrastructure;
using MetalCalcWPF.Models;
using MetalCalcWPF.Services;
using MetalCalcWPF.Services.Interfaces;
using MetalCalcWPF.Utilities;

namespace MetalCalcWPF.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly IDatabaseService _databaseService;
        private readonly CalculationService _calculator;
        private readonly IWindowService _windowService;
        private readonly IFileDialogService _fileDialogService;
        private readonly IMessageService _messageService;

        private string _clientName = string.Empty;
        private string _thicknessText = string.Empty;
        private string _quantityText = "1";
        private string _widthText = string.Empty;
        private string _heightText = string.Empty;
        private string _weightText = string.Empty;
        private string _laserLengthText = string.Empty;
        private bool _useBending;
        private string _bendsCountText = "0";
        private string _bendLengthText = "0";
        private bool _useWelding;
        private string _weldLengthText = "0";
        private MaterialType? _selectedMaterial;
        private OrderHistory? _selectedHistory;
        private string _resultText = "Итого: 0 ₸";
        private string _resultDetails = string.Empty;

        public MainViewModel(
            IDatabaseService databaseService,
            IWindowService windowService,
            IFileDialogService fileDialogService,
            IMessageService messageService)
        {
            _databaseService = databaseService;
            _windowService = windowService;
            _fileDialogService = fileDialogService;
            _messageService = messageService;
            _calculator = new CalculationService(_databaseService);

            Materials = new ObservableCollection<MaterialType>(_databaseService.GetMaterials());
            History = new ObservableCollection<OrderHistory>(_databaseService.GetRecentOrders());
            SelectedMaterial = Materials.FirstOrDefault();

            CalculateCommand = new RelayCommand(_ => Calculate());
            DeleteOrderCommand = new RelayCommand(_ => DeleteSelectedOrder(), _ => SelectedHistory != null);
            ExportToExcelCommand = new RelayCommand(_ => ExportToExcel());
            OpenSettingsCommand = new RelayCommand(_ => OpenSettings());
            OpenDatabaseCommand = new RelayCommand(_ => OpenDatabaseEditor());
        }

        public ObservableCollection<MaterialType> Materials { get; }

        public ObservableCollection<OrderHistory> History { get; }

        public string ClientName
        {
            get => _clientName;
            set => SetProperty(ref _clientName, value);
        }

        public string ThicknessText
        {
            get => _thicknessText;
            set => SetProperty(ref _thicknessText, value);
        }

        public string QuantityText
        {
            get => _quantityText;
            set => SetProperty(ref _quantityText, value);
        }

        public string WidthText
        {
            get => _widthText;
            set => SetProperty(ref _widthText, value);
        }

        public string HeightText
        {
            get => _heightText;
            set => SetProperty(ref _heightText, value);
        }

        public string WeightText
        {
            get => _weightText;
            set => SetProperty(ref _weightText, value);
        }

        public string LaserLengthText
        {
            get => _laserLengthText;
            set => SetProperty(ref _laserLengthText, value);
        }

        public bool UseBending
        {
            get => _useBending;
            set => SetProperty(ref _useBending, value);
        }

        public string BendsCountText
        {
            get => _bendsCountText;
            set => SetProperty(ref _bendsCountText, value);
        }

        public string BendLengthText
        {
            get => _bendLengthText;
            set => SetProperty(ref _bendLengthText, value);
        }

        public bool UseWelding
        {
            get => _useWelding;
            set => SetProperty(ref _useWelding, value);
        }

        public string WeldLengthText
        {
            get => _weldLengthText;
            set => SetProperty(ref _weldLengthText, value);
        }

        public MaterialType? SelectedMaterial
        {
            get => _selectedMaterial;
            set => SetProperty(ref _selectedMaterial, value);
        }

        public OrderHistory? SelectedHistory
        {
            get => _selectedHistory;
            set
            {
                if (SetProperty(ref _selectedHistory, value))
                {
                    (DeleteOrderCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public string ResultText
        {
            get => _resultText;
            set => SetProperty(ref _resultText, value);
        }

        public string ResultDetails
        {
            get => _resultDetails;
            set => SetProperty(ref _resultDetails, value);
        }

        public RelayCommand CalculateCommand { get; }
        public RelayCommand DeleteOrderCommand { get; }
        public RelayCommand ExportToExcelCommand { get; }
        public RelayCommand OpenSettingsCommand { get; }
        public RelayCommand OpenDatabaseCommand { get; }

        private void Calculate()
        {
            try
            {
                var clientName = string.IsNullOrWhiteSpace(ClientName) ? "Без названия" : ClientName;

                double thicknessMm = ParseDouble(ThicknessText);
                int quantity = (int)ParseDouble(QuantityText);
                if (quantity <= 0)
                {
                    quantity = 1;
                }

                double widthMm = ParseDouble(WidthText);
                double heightMm = ParseDouble(HeightText);
                double measuredWeightKg = ParseDouble(WeightText);
                double laserLen = ParseDouble(LaserLengthText);
                int bendsCount = (int)ParseDouble(BendsCountText);
                double bendLenMm = ParseDouble(BendLengthText);
                double weldCm = ParseDouble(WeldLengthText);

                var result = _calculator.CalculateOrder(
                    widthMm, heightMm, thicknessMm, quantity, SelectedMaterial,
                    laserLen,
                    UseBending, bendsCount, bendLenMm,
                    UseWelding, weldCm,
                    measuredWeightKg
                );

                ResultText = $"Итого: {Math.Round(result.TotalPrice)} ₸";
                ResultDetails = $"Металл: {Math.Round(result.MaterialCost)} ₸\n" +
                                $"Лазер: {Math.Round(result.LaserCost)} ₸\n" +
                                $"Гибка: {Math.Round(result.BendingCost)} ₸\n" +
                                $"Сварка: {Math.Round(result.WeldingCost)} ₸";

                if (result.TotalPrice > 0)
                {
                    var descriptionParts = new System.Collections.Generic.List<string>
                    {
                        $"{quantity}шт",
                        $"{thicknessMm}мм"
                    };
                    if (measuredWeightKg > 0)
                    {
                        descriptionParts.Add($"{measuredWeightKg}кг");
                    }

                    var newOrder = new OrderHistory
                    {
                        CreatedDate = DateTime.Now,
                        ClientName = clientName,
                        Description = string.Join(" / ", descriptionParts),
                        TotalPrice = Math.Round(result.TotalPrice),
                        OperationType = result.Log
                    };

                    _databaseService.SaveOrder(newOrder);
                    ReloadHistory();
                }
            }
            catch (Exception ex)
            {
                _messageService.ShowError("Ошибка: " + ex.Message);
            }
        }

        private void DeleteSelectedOrder()
        {
            if (SelectedHistory == null)
            {
                return;
            }

            var result = _messageService.ShowConfirm($"Удалить заказ №{SelectedHistory.Id}?");
            if (result == System.Windows.MessageBoxResult.Yes)
            {
                _databaseService.DeleteOrder(SelectedHistory.Id);
                ReloadHistory();
            }
        }

        private void ExportToExcel()
        {
            try
            {
                var history = _databaseService.GetRecentOrders();

                if (history.Count == 0)
                {
                    _messageService.ShowInfo("История пуста, нечего выгружать!");
                    return;
                }

                var filePath = _fileDialogService.ShowSaveFileDialog(
                    $"Отчет_{DateTime.Now:yyyy-MM-dd}.xlsx",
                    "Excel Files|*.xlsx");

                if (string.IsNullOrWhiteSpace(filePath))
                {
                    return;
                }

                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("Заказы");

                    worksheet.Cell(1, 1).Value = "№";
                    worksheet.Cell(1, 2).Value = "Дата";
                    worksheet.Cell(1, 3).Value = "Тип";
                    worksheet.Cell(1, 4).Value = "Клиент";
                    worksheet.Cell(1, 5).Value = "Описание";
                    worksheet.Cell(1, 6).Value = "Цена (₸)";

                    var headerRange = worksheet.Range("A1:F1");
                    headerRange.Style.Font.Bold = true;
                    headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

                    int row = 2;
                    foreach (var item in history)
                    {
                        worksheet.Cell(row, 1).Value = item.Id;
                        worksheet.Cell(row, 2).Value = item.CreatedDate;
                        worksheet.Cell(row, 3).Value = item.OperationType;
                        worksheet.Cell(row, 4).Value = item.ClientName;
                        worksheet.Cell(row, 5).Value = item.Description;
                        worksheet.Cell(row, 6).Value = item.TotalPrice;
                        row++;
                    }

                    worksheet.Columns().AdjustToContents();
                    workbook.SaveAs(filePath);
                }

                _messageService.ShowInfo("Файл успешно сохранен!");
            }
            catch (Exception ex)
            {
                _messageService.ShowError("Ошибка при экспорте: " + ex.Message);
            }
        }

        private void OpenSettings()
        {
            _windowService.ShowSettings();
        }

        private void OpenDatabaseEditor()
        {
            _windowService.ShowDatabaseEditor();
            ReloadMaterials();
        }

        private void ReloadHistory()
        {
            History.Clear();
            foreach (var order in _databaseService.GetRecentOrders())
            {
                History.Add(order);
            }
        }

        private void ReloadMaterials()
        {
            Materials.Clear();
            foreach (var material in _databaseService.GetMaterials())
            {
                Materials.Add(material);
            }

            SelectedMaterial = Materials.FirstOrDefault();
        }

        private static double ParseDouble(string? text)
        {
            return NumberParser.TryParseDouble(text, out var value) ? value : 0;
        }
    }
}
