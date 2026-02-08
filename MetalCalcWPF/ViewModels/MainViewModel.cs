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
    public class MainViewModel : ViewModelBase, System.ComponentModel.IDataErrorInfo
    {
        private readonly IDatabaseService _databaseService;
        private readonly ICalculationService _calculator;
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
        private string _piercesCountText = "1"; // ✅ НОВОЕ: Количество отверстий
        private bool _useBending;
        private string _bendsCountText = "0";
        private string _bendLengthText = "0";
        private bool _useWelding;
        private string _weldLengthText = "0";
        private MaterialType? _selectedMaterial;
        private OrderHistory? _selectedHistory;
        private string _resultText = "Итого: 0 ₸";
        private string _resultDetails = string.Empty;
        private string _validationMessage = string.Empty;

        public MainViewModel(
            IDatabaseService databaseService,
            IWindowService windowService,
            IFileDialogService fileDialogService,
            IMessageService messageService,
            ICalculationService calculationService)
        {
            _databaseService = databaseService;
            _windowService = windowService;
            _fileDialogService = fileDialogService;
            _messageService = messageService;
            _calculator = calculationService;

            Materials = new ObservableCollection<MaterialType>(_databaseService.GetMaterials());
            History = new ObservableCollection<OrderHistory>(_databaseService.GetRecentOrders());
            SelectedMaterial = Materials.FirstOrDefault();

            CalculateCommand = new RelayCommand(_ => Calculate());
            DeleteOrderCommand = new RelayCommand(_ => DeleteSelectedOrder(), _ => SelectedHistory != null);
            DeleteOrderByParamCommand = new RelayCommand(p => DeleteOrderByParam(p));
            ExportToExcelCommand = new RelayCommand(_ => ExportToExcel());
            OpenSettingsCommand = new RelayCommand(_ => OpenSettings());
            OpenDatabaseCommand = new RelayCommand(_ => OpenDatabaseEditor());

            // Автоматическая валидация при изменении полей
            PropertyChanged += (s, e) =>
            {
                // Validate a subset of properties live
                if (e.PropertyName == nameof(ThicknessText) ||
                    e.PropertyName == nameof(QuantityText) ||
                    e.PropertyName == nameof(WidthText) ||
                    e.PropertyName == nameof(HeightText) ||
                    e.PropertyName == nameof(WeightText) ||
                    e.PropertyName == nameof(LaserLengthText) ||
                    e.PropertyName == nameof(PiercesCountText) ||
                    e.PropertyName == nameof(UseBending) ||
                    e.PropertyName == nameof(BendsCountText) ||
                    e.PropertyName == nameof(BendLengthText) ||
                    e.PropertyName == nameof(UseWelding) ||
                    e.PropertyName == nameof(WeldLengthText) ||
                    e.PropertyName == nameof(SelectedMaterial))
                {
                    ValidateAll();
                }
            };
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

        // ✅ НОВОЕ СВОЙСТВО
        public string PiercesCountText
        {
            get => _piercesCountText;
            set => SetProperty(ref _piercesCountText, value);
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

        public string ValidationMessage
        {
            get => _validationMessage;
            set => SetProperty(ref _validationMessage, value);
        }

        public RelayCommand CalculateCommand { get; }
        public RelayCommand DeleteOrderCommand { get; }
        public RelayCommand DeleteOrderByParamCommand { get; }
        public RelayCommand ExportToExcelCommand { get; }
        public RelayCommand OpenSettingsCommand { get; }
        public RelayCommand OpenDatabaseCommand { get; }

        private void Calculate()
        {
            try
            {
                // Очистим предыдущие сообщения валидации
                ValidationMessage = string.Empty;

                // Валидация основных полей
                var validation = ValidateAll();
                if (!validation.IsValid)
                {
                    // Отображаем подробности в UI и через MessageService
                    ValidationMessage = validation.Message;
                    _messageService.ShowError(validation.Message);
                    return;
                }

                var clientName = string.IsNullOrWhiteSpace(ClientName) ? "Без названия" : ClientName;

                double thicknessMm = ParseDouble(ThicknessText);

                // Количество должно быть целым положительным
                int quantity = ConvertToInt(QuantityText, 1);
                if (quantity <= 0) quantity = 1;

                double widthMm = ParseDouble(WidthText);
                double heightMm = ParseDouble(HeightText);
                double weightKg = ParseDouble(WeightText);
                double laserLen = ParseDouble(LaserLengthText);

                // Требуем указать массу партии или габариты детали
                if (weightKg <= 0 && (widthMm <= 0 || heightMm <= 0))
                {
                    _messageService.ShowError("Укажите массу партии или габариты детали (ширина и высота).");
                    return;
                }

                // Пробивки — целое неотрицательное число
                int piercesCount = ConvertToInt(PiercesCountText, 0);
                if (piercesCount < 0) piercesCount = 0;

                int bendsCount = ConvertToInt(BendsCountText, 0);
                double bendLenMm = ParseDouble(BendLengthText);
                double weldCm = ParseDouble(WeldLengthText);

                // Дополнительная валидация логики
                if (UseBending && bendsCount <= 0)
                {
                    _messageService.ShowError("Укажите количество гибов (больше 0) или отключите гибку.");
                    return;
                }

                if (UseBending && bendLenMm <= 0)
                {
                    _messageService.ShowError("Укажите общую длину гиба в мм.");
                    return;
                }

                if (UseWelding && weldCm <= 0)
                {
                    _messageService.ShowError("Укажите длину шва в см или отключите сварку.");
                    return;
                }

                if (laserLen > 0)
                {
                    var profile = _databaseService.GetProfileByThickness(thicknessMm);
                    if (profile == null)
                    {
                        _messageService.ShowError("Нет профиля резки для выбранной толщины. Добавьте профиль в базе.");
                        return;
                    }
                    if (profile.CuttingSpeed <= 0)
                    {
                        _messageService.ShowError("Скорость резки в профиле должна быть больше 0. Проверьте данные профиля.");
                        return;
                    }
                }

                // ✅ ОБНОВЛЕННЫЙ ВЫЗОВ с количеством пробивок
                var result = _calculator.CalculateOrder(
                    widthMm, heightMm, thicknessMm, quantity, SelectedMaterial,
                    laserLen, piercesCount, // ✅ Передаем количество пробивок
                    UseBending, bendsCount, bendLenMm,
                    UseWelding, weldCm,
                    weightKg
                );

                ResultText = $"Итого: {Math.Round(result.TotalPrice):N0} ₸";
                ResultDetails = $"Металл: {Math.Round(result.MaterialCost):N0} ₸\n" +
                                $"Лазер: {Math.Round(result.LaserCost):N0} ₸\n" +
                                $"Гибка: {Math.Round(result.BendingCost):N0} ₸\n" +
                                $"Сварка: {Math.Round(result.WeldingCost):N0} ₸\n\n" +
                                // Детализация расчёта лазера (если есть)
                                (!string.IsNullOrWhiteSpace(result.LaserDetails) ? ("Детали лазера: \n" + result.LaserDetails) : string.Empty);

                if (result.TotalPrice > 0)
                {
                    var newOrder = new OrderHistory
                    {
                        CreatedDate = DateTime.Now,
                        ClientName = clientName,
                        Description = $"{quantity}шт / {thicknessMm}мм",
                        TotalPrice = Math.Round(result.TotalPrice),
                        OperationType = result.Log
                    };

                    _databaseService.SaveOrder(newOrder);
                    ReloadHistory();
                }
            }
            catch (Exception ex)
            {
                _messageService.ShowError("Ошибка расчета: " + ex.Message);
            }
        }

        private void DeleteSelectedOrder()
        {
            if (SelectedHistory == null) return;

            var result = _messageService.ShowConfirm($"Удалить заказ №{SelectedHistory.Id}?");
            if (result == System.Windows.MessageBoxResult.Yes)
            {
                _databaseService.DeleteOrder(SelectedHistory.Id);
                ReloadHistory();
                _messageService.ShowInfo("Заказ удален");
            }
        }

        private void DeleteOrderByParam(object? parameter)
        {
            if (parameter is OrderHistory order)
            {
                var result = _messageService.ShowConfirm($"Удалить заказ №{order.Id}?");
                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    _databaseService.DeleteOrder(order.Id);
                    ReloadHistory();
                    _messageService.ShowInfo("Заказ удален");
                }
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

                if (string.IsNullOrWhiteSpace(filePath)) return;

                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("Заказы");

                    // Заголовки
                    worksheet.Cell(1, 1).Value = "№";
                    worksheet.Cell(1, 2).Value = "Дата";
                    worksheet.Cell(1, 3).Value = "Тип";
                    worksheet.Cell(1, 4).Value = "Клиент";
                    worksheet.Cell(1, 5).Value = "Описание";
                    worksheet.Cell(1, 6).Value = "Цена (₸)";

                    var headerRange = worksheet.Range("A1:F1");
                    headerRange.Style.Font.Bold = true;
                    headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

                    // Данные
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

                _messageService.ShowInfo($"Файл успешно сохранен!\n{filePath}");
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

        private static int ConvertToInt(string? text, int defaultValue = 0)
        {
            if (NumberParser.TryParseDouble(text, out var d))
            {
                try
                {
                    // Округляем вниз до целого
                    return (int)Math.Floor(d);
                }
                catch
                {
                    return defaultValue;
                }
            }
            return defaultValue;
        }

        private (bool IsValid, string Message) ValidateAll()
        {
            // Thickness
            if (string.IsNullOrWhiteSpace(ThicknessText) || ParseDouble(ThicknessText) <= 0)
                return (false, "Укажите корректную толщину металла (мм > 0).");

            // Material
            if (SelectedMaterial == null)
                return (false, "Выберите материал.");

            // Quantity
            int q = ConvertToInt(QuantityText, -1);
            if (q <= 0) return (false, "Количество должно быть целым положительным (шт).");

            // Dimensions or weight
            double width = ParseDouble(WidthText);
            double height = ParseDouble(HeightText);
            double weight = ParseDouble(WeightText);
            if (weight <= 0 && (width <= 0 || height <= 0))
                return (false, "Укажите массу партии или габариты детали (ширина и высота).");

            // Laser length and profile check
            double laserLen = ParseDouble(LaserLengthText);
            if (laserLen > 0)
            {
                var profile = _databaseService.GetProfileByThickness(ParseDouble(ThicknessText));
                if (profile == null) return (false, "Нет профиля резки для выбранной толщины.");
                if (profile.CuttingSpeed <= 0) return (false, "Скорость резки в профиле должна быть > 0.");
            }

            // Pierces
            int pierces = ConvertToInt(PiercesCountText, 0);
            if (pierces < 0) return (false, "Количество пробивок должно быть >= 0.");

            // Bending
            if (UseBending)
            {
                int bends = ConvertToInt(BendsCountText, 0);
                if (bends <= 0) return (false, "Укажите количество гибов (больше 0) или отключите гибку.");
                if (ParseDouble(BendLengthText) <= 0) return (false, "Укажите общую длину гиба в мм.");
            }

            // Welding
            if (UseWelding)
            {
                if (ParseDouble(WeldLengthText) <= 0) return (false, "Укажите длину шва в см или отключите сварку.");
            }

            return (true, string.Empty);
        }

        // IDataErrorInfo implementation for WPF field-level validation
        public string Error => null;

        public string this[string columnName]
        {
            get
            {
                try
                {
                    switch (columnName)
                    {
                        case nameof(ThicknessText):
                            if (string.IsNullOrWhiteSpace(ThicknessText) || ParseDouble(ThicknessText) <= 0)
                                return "Толщина должна быть числом > 0 мм";
                            break;
                        case nameof(QuantityText):
                            if (ConvertToInt(QuantityText, -1) <= 0)
                                return "Количество должно быть целым положительным";
                            break;
                        case nameof(WidthText):
                        case nameof(HeightText):
                            // validate only if weight not provided
                            if (ParseDouble(WeightText) <= 0 && (ParseDouble(WidthText) <= 0 || ParseDouble(HeightText) <= 0))
                                return "Укажите массу партии или заполните ширину и высоту в мм";
                            break;
                        case nameof(WeightText):
                            if (ParseDouble(WeightText) < 0)
                                return "Масса не может быть отрицательной";
                            break;
                        case nameof(LaserLengthText):
                            if (ParseDouble(LaserLengthText) > 0)
                            {
                                var profile = _databaseService.GetProfileByThickness(ParseDouble(ThicknessText));
                                if (profile == null) return "Нет профиля резки для выбранной толщины";
                                if (profile.CuttingSpeed <= 0) return "Скорость резки в профиле должна быть > 0";
                            }
                            break;
                        case nameof(PiercesCountText):
                            if (ConvertToInt(PiercesCountText, -1) < 0) return "Количество пробивок должно быть >= 0";
                            break;
                        case nameof(BendsCountText):
                            if (UseBending && ConvertToInt(BendsCountText, -1) <= 0) return "Укажите количество гибов (>0)";
                            break;
                        case nameof(BendLengthText):
                            if (UseBending && ParseDouble(BendLengthText) <= 0) return "Укажите длину гиба в мм";
                            break;
                        case nameof(WeldLengthText):
                            if (UseWelding && ParseDouble(WeldLengthText) <= 0) return "Укажите длину шва в см";
                            break;
                        case nameof(SelectedMaterial):
                            if (SelectedMaterial == null) return "Выберите материал";
                            break;
                    }
                }
                catch { }
                return string.Empty;
            }
        }
    }
}