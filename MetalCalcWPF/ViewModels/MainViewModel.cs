using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using ClosedXML.Excel;
using MetalCalcWPF.Infrastructure;
using MetalCalcWPF.Models;
using MetalCalcWPF.Models.Reporting;
using MetalCalcWPF.Services;
using MetalCalcWPF.Services.Calculation;
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
        private readonly IReportingService _reportingService;

        // ====== Отчётность (Спринт 2.3) ======
        // Фильтр истории: NULL означает «без фильтра, показываем последние 50 через GetRecentOrders».
        // Храним как DateTime?, чтобы DatePicker мог быть пустым.
        private DateTime? _filterStart;
        private DateTime? _filterEnd;
        private string _historySummaryText = string.Empty;

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

        // ✅ Режим «Сортамент проката»
        private bool _useRolledProfile;
        private RolledProfile? _selectedRolledProfile;
        private string _lengthMeterText = "1";
        private string _rolledInfoText = string.Empty;
        private string _rolledSearchText = string.Empty;

        // ✅ Спринт 2.2b: выбор конкретного лазерного станка
        private CuttingMachine? _selectedLaserMachine;

        public MainViewModel(
            IDatabaseService databaseService,
            IWindowService windowService,
            IFileDialogService fileDialogService,
            IMessageService messageService,
            ICalculationService calculationService,
            IReportingService reportingService)
        {
            _databaseService = databaseService;
            _windowService = windowService;
            _fileDialogService = fileDialogService;
            _messageService = messageService;
            _calculator = calculationService;
            _reportingService = reportingService;

            Materials = new ObservableCollection<MaterialType>(_databaseService.GetMaterials());
            History = new ObservableCollection<OrderHistory>(_databaseService.GetRecentOrders());
            Breakdowns = new ObservableCollection<CalculationBreakdown>();
            SelectedMaterial = Materials.FirstOrDefault();

            // ✅ Только активные лазеры — собираем для дропдауна вкладки «Лазер»
            LaserMachines = new ObservableCollection<CuttingMachine>(
                _databaseService.GetCuttingMachinesByKind(CuttingMachineKind.Laser)
                                .Where(m => m.IsActive));
            SelectedLaserMachine = LaserMachines.FirstOrDefault();

            // ✅ Загружаем только активные профили проката
            RolledProfiles = new ObservableCollection<RolledProfile>(
                _databaseService.GetAllRolledProfiles().Where(p => p.IsActive));

            // Группировка по типу + сортировка по массе внутри группы + фильтр по поиску
            var view = CollectionViewSource.GetDefaultView(RolledProfiles);
            view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(RolledProfile.Kind)));
            view.SortDescriptions.Add(new SortDescription(nameof(RolledProfile.Kind), ListSortDirection.Ascending));
            view.SortDescriptions.Add(new SortDescription(nameof(RolledProfile.WeightPerMeterKg), ListSortDirection.Ascending));
            view.Filter = o =>
            {
                if (string.IsNullOrWhiteSpace(_rolledSearchText)) return true;
                if (o is not RolledProfile rp) return false;
                var q = _rolledSearchText.Trim();
                return (rp.SizeCode?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                    || (rp.GostDesignation?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);
            };
            RolledProfilesView = view;

            CalculateCommand = new RelayCommand(_ => Calculate());
            ClearRolledSearchCommand = new RelayCommand(_ => RolledSearchText = string.Empty);
            DeleteOrderCommand = new RelayCommand(_ => DeleteSelectedOrder(), _ => SelectedHistory != null);
            DeleteOrderByParamCommand = new RelayCommand(p => DeleteOrderByParam(p));
            ExportToExcelCommand = new RelayCommand(_ => ExportToExcel());
            OpenSettingsCommand = new RelayCommand(_ => OpenSettings());
            OpenDatabaseCommand = new RelayCommand(_ => OpenDatabaseEditor());
            ApplyHistoryFilterCommand = new RelayCommand(_ => ApplyHistoryFilter());
            ResetHistoryFilterCommand = new RelayCommand(_ => ResetHistoryFilter());
            FilterCurrentMonthCommand = new RelayCommand(_ => FilterCurrentMonth());

            // Первичная подпись под таблицей: «Последние N заказов, сумма …».
            UpdateHistorySummary();

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
                    e.PropertyName == nameof(SelectedMaterial) ||
                    e.PropertyName == nameof(UseRolledProfile) ||
                    e.PropertyName == nameof(SelectedRolledProfile) ||
                    e.PropertyName == nameof(LengthMeterText))
                {
                    ValidateAll();
                    RecalcRolledInfo();
                }
            };
        }

        private void RecalcRolledInfo()
        {
            if (!UseRolledProfile || SelectedRolledProfile == null)
            {
                RolledInfoText = string.Empty;
                return;
            }

            double lenM = ParseDouble(LengthMeterText);
            int qty = ConvertToInt(QuantityText, 1);
            if (qty <= 0) qty = 1;

            double massPerPiece = lenM * SelectedRolledProfile.WeightPerMeterKg;
            double totalMass = massPerPiece * qty;

            RolledInfoText =
                $"кг/м: {SelectedRolledProfile.WeightPerMeterKg:0.##}   •   " +
                $"масса 1 шт: {massPerPiece:0.##} кг   •   " +
                $"всего: {totalMass:0.##} кг";
        }

        public ObservableCollection<MaterialType> Materials { get; }
        public ObservableCollection<OrderHistory> History { get; }
        public ObservableCollection<RolledProfile> RolledProfiles { get; }

        /// <summary>
        /// Прозрачная детализация последнего расчёта — секция на каждый
        /// отработавший калькулятор (Металл / Лазер / Гибка / Сварка).
        /// Рисуется в UI списком Expander'ов «🔍 Детализация расчёта».
        /// </summary>
        public ObservableCollection<CalculationBreakdown> Breakdowns { get; }

        /// <summary>
        /// Активные лазерные станки из справочника <see cref="CuttingMachine"/>.
        /// Если список пуст — dropdown в UI скрыт, калькулятор работает
        /// по чистой Excel-формуле (обратная совместимость).
        /// </summary>
        public ObservableCollection<CuttingMachine> LaserMachines { get; }

        /// <summary>
        /// Выбранный лазерный станок — его Setup / MinCharge / PricePerMeterOverride
        /// подмешиваются в расчёт лазера (Спринт 2.2b).
        /// </summary>
        public CuttingMachine? SelectedLaserMachine
        {
            get => _selectedLaserMachine;
            set => SetProperty(ref _selectedLaserMachine, value);
        }

        /// <summary>
        /// Видимость дропдауна станка во вкладке «Лазер» — скрываем, если в БД
        /// нет ни одного активного лазера (свежая установка или легаси без справочника).
        /// </summary>
        public bool HasLaserMachines => LaserMachines.Count > 0;

        /// <summary>
        /// True, если есть что показать в блоке «🔍 Детализация расчёта» —
        /// скрывает Expander до первого успешного расчёта.
        /// </summary>
        public bool HasBreakdowns => Breakdowns.Count > 0;
        public ICollectionView RolledProfilesView { get; }

        public string RolledSearchText
        {
            get => _rolledSearchText;
            set
            {
                if (SetProperty(ref _rolledSearchText, value))
                    RolledProfilesView.Refresh();
            }
        }

        // ✅ Режим сортамента проката
        public bool UseRolledProfile
        {
            get => _useRolledProfile;
            set => SetProperty(ref _useRolledProfile, value);
        }

        public RolledProfile? SelectedRolledProfile
        {
            get => _selectedRolledProfile;
            set => SetProperty(ref _selectedRolledProfile, value);
        }

        public string LengthMeterText
        {
            get => _lengthMeterText;
            set => SetProperty(ref _lengthMeterText, value);
        }

        public string RolledInfoText
        {
            get => _rolledInfoText;
            set => SetProperty(ref _rolledInfoText, value);
        }

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
        public RelayCommand ClearRolledSearchCommand { get; }
        public RelayCommand DeleteOrderCommand { get; }
        public RelayCommand DeleteOrderByParamCommand { get; }
        public RelayCommand ExportToExcelCommand { get; }
        public RelayCommand OpenSettingsCommand { get; }
        public RelayCommand OpenDatabaseCommand { get; }

        /// <summary>Применить фильтр истории по выбранным датам.</summary>
        public RelayCommand ApplyHistoryFilterCommand { get; }

        /// <summary>Сбросить фильтр — показать последние 50 заказов.</summary>
        public RelayCommand ResetHistoryFilterCommand { get; }

        /// <summary>Быстрая кнопка «За текущий месяц».</summary>
        public RelayCommand FilterCurrentMonthCommand { get; }

        // ====== Свойства фильтра истории (Спринт 2.3) ======

        /// <summary>Начало отчётного периода (включительно). NULL = без фильтра.</summary>
        public DateTime? FilterStart
        {
            get => _filterStart;
            set => SetProperty(ref _filterStart, value);
        }

        /// <summary>Конец отчётного периода (включительно, в UI — так удобнее пользователю).</summary>
        public DateTime? FilterEnd
        {
            get => _filterEnd;
            set => SetProperty(ref _filterEnd, value);
        }

        /// <summary>Строка-саммари под таблицей: «За период … — 12 заказов на 345 000 ₸».</summary>
        public string HistorySummaryText
        {
            get => _historySummaryText;
            private set => SetProperty(ref _historySummaryText, value);
        }

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

                // ✅ РЕЖИМ СОРТАМЕНТА: заменяем габариты/массу на расчёт из кг/м × длина
                if (UseRolledProfile)
                {
                    if (SelectedRolledProfile == null)
                    {
                        _messageService.ShowError("Выберите профиль проката.");
                        return;
                    }
                    double lenM = ParseDouble(LengthMeterText);
                    if (lenM <= 0)
                    {
                        _messageService.ShowError("Укажите длину проката (м > 0).");
                        return;
                    }

                    double massPerPiece = lenM * SelectedRolledProfile.WeightPerMeterKg;
                    weightKg = massPerPiece * quantity;

                    // Габариты в режиме проката не используются
                    widthMm = 0;
                    heightMm = 0;

                    // Толщина для профиля берётся из стенки/полки (для валидации и резки, если понадобится)
                    if (thicknessMm <= 0)
                        thicknessMm = SelectedRolledProfile.WallThickness
                                      ?? SelectedRolledProfile.FlangeThickness
                                      ?? 3.0;
                }

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

                // ✅ ОБНОВЛЕННЫЙ ВЫЗОВ с количеством пробивок и выбранным станком (2.2b)
                var result = _calculator.CalculateOrder(
                    widthMm, heightMm, thicknessMm, quantity, SelectedMaterial,
                    laserLen, piercesCount,
                    UseBending, bendsCount, bendLenMm,
                    UseWelding, weldCm,
                    weightKg,
                    cuttingMachineId: SelectedLaserMachine?.Id
                );

                ResultText = $"Итого: {Math.Round(result.TotalPrice):N0} ₸";
                ResultDetails = $"Металл: {Math.Round(result.MaterialCost):N0} ₸\n" +
                                $"Лазер: {Math.Round(result.LaserCost):N0} ₸\n" +
                                $"Гибка: {Math.Round(result.BendingCost):N0} ₸\n" +
                                $"Сварка: {Math.Round(result.WeldingCost):N0} ₸";

                // Заменяем предыдущую детализацию новой (порядок секций = порядок пайплайна)
                Breakdowns.Clear();
                foreach (var section in result.Breakdowns)
                    Breakdowns.Add(section);
                OnPropertyChanged(nameof(HasBreakdowns));

                if (result.TotalPrice > 0)
                {
                    string description = UseRolledProfile && SelectedRolledProfile != null
                        ? $"{quantity}шт × {ParseDouble(LengthMeterText)}м · {SelectedRolledProfile.SizeCode}"
                        : $"{quantity}шт / {thicknessMm}мм";

                    var newOrder = new OrderHistory
                    {
                        CreatedDate = DateTime.Now,
                        ClientName = clientName,
                        Description = description,
                        TotalPrice = Math.Round(result.TotalPrice),
                        OperationType = result.Log,
                        // Cost-разбивка по операциям — для отчётности руководству.
                        // Округляем до тенге, как и TotalPrice (копейки в отчёте бессмысленны).
                        MaterialCost = Math.Round(result.MaterialCost),
                        LaserCost = Math.Round(result.LaserCost),
                        BendingCost = Math.Round(result.BendingCost),
                        WeldingCost = Math.Round(result.WeldingCost),
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

        /// <summary>
        /// Экспорт отчёта для руководства. Если фильтр задан — берём период фильтра,
        /// иначе отчёт строится «от даты самого старого заказа до сейчас», чтобы
        /// файл никогда не лгал периодом («последние 50» ≠ конкретный месяц).
        /// </summary>
        private void ExportToExcel()
        {
            try
            {
                // Определяем, какие заказы и за какой период идут в отчёт.
                var (orders, periodStart, periodEnd) = GetOrdersForReport();

                if (orders.Count == 0)
                {
                    _messageService.ShowInfo("За выбранный период нет заказов — нечего выгружать.");
                    return;
                }

                var filePath = _fileDialogService.ShowSaveFileDialog(
                    $"Отчет_{DateTime.Now:yyyy-MM-dd}.xlsx",
                    "Excel Files|*.xlsx");

                if (string.IsNullOrWhiteSpace(filePath)) return;

                var summary = _reportingService.BuildSummary(orders, periodStart, periodEnd);
                _reportingService.ExportToExcel(orders, summary, filePath);

                _messageService.ShowInfo(
                    $"Отчёт сохранён: {orders.Count} заказов, выручка {summary.TotalRevenue:N0} ₸\n{filePath}");
            }
            catch (Exception ex)
            {
                _messageService.ShowError("Ошибка при экспорте: " + ex.Message);
            }
        }

        /// <summary>
        /// Собирает набор заказов + период под текущее состояние UI.
        /// Если фильтр выключен — использует самую раннюю дату заказа из БД,
        /// чтобы в шапке отчёта стоял реальный диапазон, а не «с 01.01.0001».
        /// </summary>
        private (System.Collections.Generic.List<OrderHistory> orders, DateTime start, DateTime end) GetOrdersForReport()
        {
            if (TryGetFilterRange(out var start, out var end))
            {
                return (_databaseService.GetOrdersByDateRange(start, end), start, end);
            }

            var all = _databaseService.GetRecentOrders();
            var minDate = all.Count > 0 ? all.Min(o => o.CreatedDate).Date : DateTime.Today;
            var maxExclusive = DateTime.Today.AddDays(1);
            return (all, minDate, maxExclusive);
        }

        /// <summary>
        /// Нормализует введённые в DatePicker даты в полуоткрытый интервал
        /// <c>[start; end)</c>, понимаемый <see cref="IDatabaseService.GetOrdersByDateRange"/>.
        /// Пользователь вводит «включительно по 30 апреля» — в БД уходит 1 мая.
        /// </summary>
        private bool TryGetFilterRange(out DateTime start, out DateTime end)
        {
            if (FilterStart == null && FilterEnd == null)
            {
                start = default;
                end = default;
                return false;
            }

            start = FilterStart?.Date ?? DateTime.MinValue.Date;
            var endInclusive = FilterEnd?.Date ?? DateTime.Today;
            end = endInclusive.AddDays(1);
            return true;
        }

        private void ApplyHistoryFilter()
        {
            if (FilterStart != null && FilterEnd != null && FilterEnd < FilterStart)
            {
                _messageService.ShowError("Дата окончания меньше даты начала — исправьте диапазон.");
                return;
            }
            ReloadHistory();
        }

        private void ResetHistoryFilter()
        {
            FilterStart = null;
            FilterEnd = null;
            ReloadHistory();
        }

        /// <summary>
        /// Быстрая кнопка «Текущий месяц»: пресет для ПТО/начальника цеха,
        /// которые каждый 1-го числа хотят отчёт по предыдущему периоду.
        /// </summary>
        private void FilterCurrentMonth()
        {
            var today = DateTime.Today;
            var firstDay = new DateTime(today.Year, today.Month, 1);
            var lastDay = firstDay.AddMonths(1).AddDays(-1);
            FilterStart = firstDay;
            FilterEnd = lastDay;
            ReloadHistory();
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
            var orders = TryGetFilterRange(out var start, out var end)
                ? _databaseService.GetOrdersByDateRange(start, end)
                : _databaseService.GetRecentOrders();

            foreach (var order in orders)
            {
                History.Add(order);
            }

            UpdateHistorySummary();
        }

        /// <summary>
        /// Обновляет короткую KPI-строку под таблицей. Используем тот же
        /// <see cref="IReportingService"/>, что и для Excel-экспорта — одна формула,
        /// один источник правды.
        /// </summary>
        private void UpdateHistorySummary()
        {
            if (History.Count == 0)
            {
                HistorySummaryText = TryGetFilterRange(out var s, out var e)
                    ? $"За период {s:dd.MM.yyyy}–{e.AddDays(-1):dd.MM.yyyy}: заказов нет"
                    : "История пуста";
                return;
            }

            var snapshot = History.ToList();
            var periodStart = TryGetFilterRange(out var rs, out _) ? rs : snapshot.Min(o => o.CreatedDate).Date;
            var periodEnd   = TryGetFilterRange(out _, out var re) ? re : DateTime.Today.AddDays(1);

            var summary = _reportingService.BuildSummary(snapshot, periodStart, periodEnd);
            HistorySummaryText =
                $"{summary.TotalOrders} заказов на {summary.TotalRevenue:N0} ₸  •  средний чек {summary.AverageOrderValue:N0} ₸";
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
            // В режиме сортамента толщина/габариты не обязательны — считаем из профиля
            if (UseRolledProfile)
            {
                if (SelectedMaterial == null)
                    return (false, "Выберите материал.");
                if (SelectedRolledProfile == null)
                    return (false, "Выберите профиль проката.");
                int qr = ConvertToInt(QuantityText, -1);
                if (qr <= 0) return (false, "Количество должно быть целым положительным (шт).");
                if (ParseDouble(LengthMeterText) <= 0)
                    return (false, "Укажите длину проката (м > 0).");
                return (true, string.Empty);
            }

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