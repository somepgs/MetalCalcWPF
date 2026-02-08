using System;
using MetalCalcWPF.Infrastructure;
using MetalCalcWPF.Models;
using MetalCalcWPF.Services.Interfaces;
using MetalCalcWPF.Utilities;

namespace MetalCalcWPF.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private readonly IDatabaseService _databaseService;
        private readonly IMessageService _messageService;
        private WorkshopSettings _settings;

        private string _salaryText = string.Empty;
        private string _daysText = string.Empty;
        private string _hoursText = string.Empty;
        private string _bendingSalaryText = string.Empty;
        private string _electricityText = string.Empty;
        private string _amortizationText = string.Empty;
        private string _materialMarkupText = string.Empty;
        private string _thresholdText = string.Empty;
        private string _heavyCostText = string.Empty;
        private string _weldCostText = string.Empty;

        // ✅ НОВЫЕ ПОЛЯ ДЛЯ КИСЛОРОДА
        private string _oxygenVolumeText = string.Empty;
        private string _oxygenPressureText = string.Empty;
        private string _oxygenFlowText = string.Empty;
        private string _oxygenPriceText = string.Empty;
        private string _oxygenCalculationInfo = string.Empty;
        // Новые параметры лазера
        private string _laserSetupText = string.Empty;
        private string _laserMinChargeText = string.Empty;
        private string _pierceTimeText = string.Empty;

        public SettingsViewModel(IDatabaseService databaseService, IMessageService messageService)
        {
            _databaseService = databaseService;
            _messageService = messageService;
            _settings = _databaseService.GetSettings();

            SaveCommand = new RelayCommand(_ => Save());
            AddLaserProfileCommand = new RelayCommand(_ => AddLaserProfile());
            RemoveLaserProfileCommand = new RelayCommand(p => RemoveLaserProfile(p));
            LoadFromSettings(_settings);

            // Подписка на изменения для автоматического пересчета
            PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(OxygenVolumeText) ||
                    e.PropertyName == nameof(OxygenPressureText) ||
                    e.PropertyName == nameof(OxygenFlowText) ||
                    e.PropertyName == nameof(OxygenPriceText))
                {
                    UpdateOxygenCalculation();
                }
            };
        }

        public RelayCommand SaveCommand { get; }
        public RelayCommand AddLaserProfileCommand { get; }
        public RelayCommand RemoveLaserProfileCommand { get; }

        public string SalaryText
        {
            get => _salaryText;
            set => SetProperty(ref _salaryText, value);
        }

        public string DaysText
        {
            get => _daysText;
            set => SetProperty(ref _daysText, value);
        }

        public string HoursText
        {
            get => _hoursText;
            set => SetProperty(ref _hoursText, value);
        }

        public string BendingSalaryText
        {
            get => _bendingSalaryText;
            set => SetProperty(ref _bendingSalaryText, value);
        }

        public string ElectricityText
        {
            get => _electricityText;
            set => SetProperty(ref _electricityText, value);
        }

        public string AmortizationText
        {
            get => _amortizationText;
            set => SetProperty(ref _amortizationText, value);
        }

        public string MaterialMarkupText
        {
            get => _materialMarkupText;
            set => SetProperty(ref _materialMarkupText, value);
        }

        public string ThresholdText
        {
            get => _thresholdText;
            set => SetProperty(ref _thresholdText, value);
        }

        public string HeavyCostText
        {
            get => _heavyCostText;
            set => SetProperty(ref _heavyCostText, value);
        }

        public string WeldCostText
        {
            get => _weldCostText;
            set => SetProperty(ref _weldCostText, value);
        }

        // ✅ НОВЫЕ СВОЙСТВА ДЛЯ КИСЛОРОДА
        public string OxygenVolumeText
        {
            get => _oxygenVolumeText;
            set => SetProperty(ref _oxygenVolumeText, value);
        }

        public string OxygenPressureText
        {
            get => _oxygenPressureText;
            set => SetProperty(ref _oxygenPressureText, value);
        }

        public string OxygenFlowText
        {
            get => _oxygenFlowText;
            set => SetProperty(ref _oxygenFlowText, value);
        }

        public string OxygenPriceText
        {
            get => _oxygenPriceText;
            set => SetProperty(ref _oxygenPriceText, value);
        }

        public string OxygenCalculationInfo
        {
            get => _oxygenCalculationInfo;
            set => SetProperty(ref _oxygenCalculationInfo, value);
        }

        public string LaserSetupText
        {
            get => _laserSetupText;
            set => SetProperty(ref _laserSetupText, value);
        }

        public string LaserMinChargeText
        {
            get => _laserMinChargeText;
            set => SetProperty(ref _laserMinChargeText, value);
        }

        public string PierceTimeText
        {
            get => _pierceTimeText;
            set => SetProperty(ref _pierceTimeText, value);
        }

        // Список профилей резки для редактирования
        public System.Collections.ObjectModel.ObservableCollection<MaterialProfile> LaserProfiles { get; private set; } = new System.Collections.ObjectModel.ObservableCollection<MaterialProfile>();

        private MaterialProfile? _selectedLaserProfile;
        public MaterialProfile? SelectedLaserProfile
        {
            get => _selectedLaserProfile;
            set => SetProperty(ref _selectedLaserProfile, value);
        }

        private void LoadFromSettings(WorkshopSettings settings)
        {
            SalaryText = settings.OperatorMonthlySalary.ToString();
            DaysText = settings.WorkDaysPerMonth.ToString();
            HoursText = settings.WorkHoursPerDay.ToString();
            BendingSalaryText = settings.BendingOperatorSalary.ToString();
            ElectricityText = settings.ElectricityPricePerKw.ToString();
            AmortizationText = settings.AmortizationPerHour.ToString();
            MaterialMarkupText = settings.MaterialMarkupPercent.ToString();
            ThresholdText = settings.HeavyMaterialThresholdMm.ToString();
            HeavyCostText = settings.HeavyHandlingCostPerDetail.ToString();
            WeldCostText = settings.WeldingCostPerCm.ToString();

            // ✅ Кислород
            OxygenVolumeText = settings.OxygenBottleVolumeLiters.ToString();
            OxygenPressureText = settings.OxygenBottlePressureAtm.ToString();
            OxygenFlowText = settings.OxygenFlowRateLpm.ToString();
            OxygenPriceText = settings.OxygenBottlePrice.ToString();

            // Новые параметры лазера
            LaserSetupText = settings.LaserSetupCostPerJob.ToString();
            LaserMinChargeText = settings.LaserMinChargePerJob.ToString();
            PierceTimeText = settings.PierceTimeSeconds.ToString();

            UpdateOxygenCalculation();

            // Загрузка профилей резки
            LaserProfiles.Clear();
            foreach (var p in _databaseService.GetAllLaserProfiles())
            {
                LaserProfiles.Add(p);
            }
            SelectedLaserProfile = LaserProfiles.Count > 0 ? LaserProfiles[0] : null;
        }

        private void AddLaserProfile()
        {
            var p = new MaterialProfile { Thickness = 0, GasType = "Air", CuttingSpeed = 1, PiercePrice = 0, MarkupCoefficient = 100 };
            LaserProfiles.Add(p);
            SelectedLaserProfile = p;
        }

        private void RemoveLaserProfile(object? param)
        {
            if (param is MaterialProfile p && LaserProfiles.Contains(p))
            {
                LaserProfiles.Remove(p);
            }
        }

        // ✅ АВТОМАТИЧЕСКИЙ РАСЧЕТ ПОКАЗАТЕЛЕЙ КИСЛОРОДА
        private void UpdateOxygenCalculation()
        {
            try
            {
                double volume = ParseDouble(OxygenVolumeText);
                double pressure = ParseDouble(OxygenPressureText);
                double flow = ParseDouble(OxygenFlowText);
                double price = ParseDouble(OxygenPriceText);

                if (volume > 0 && pressure > 0 && flow > 0)
                {
                    // Общий объем кислорода (литры)
                    double totalLiters = volume * pressure;

                    // Время работы (минуты)
                    double workTimeMinutes = totalLiters / flow;
                    double workTimeHours = workTimeMinutes / 60.0;

                    // Стоимость за минуту и за час
                    double costPerMinute = price / workTimeMinutes;
                    double costPerHour = costPerMinute * 60.0;

                    OxygenCalculationInfo = $"Общий объем: {totalLiters:N0} л\n" +
                                          $"Время работы: {Math.Round(workTimeHours, 1)} часа ({Math.Round(workTimeMinutes)} мин)\n" +
                                          $"Стоимость: {Math.Round(costPerMinute, 2)} тг/мин = {Math.Round(costPerHour):N0} тг/час";
                }
                else
                {
                    OxygenCalculationInfo = "Укажите все параметры для расчета";
                }
            }
            catch
            {
                OxygenCalculationInfo = "Ошибка в расчетах - проверьте введенные значения";
            }
        }

        private void Save()
        {
            try
            {
                _settings.OperatorMonthlySalary = ParseDecimal(SalaryText);
                _settings.WorkDaysPerMonth = (int)ParseDouble(DaysText);
                _settings.WorkHoursPerDay = (int)ParseDouble(HoursText);
                _settings.BendingOperatorSalary = ParseDecimal(BendingSalaryText);
                _settings.ElectricityPricePerKw = ParseDecimal(ElectricityText);
                _settings.AmortizationPerHour = ParseDecimal(AmortizationText);
                _settings.MaterialMarkupPercent = ParseDecimal(MaterialMarkupText);
                _settings.HeavyMaterialThresholdMm = ParseDouble(ThresholdText);
                _settings.HeavyHandlingCostPerDetail = ParseDecimal(HeavyCostText);
                _settings.WeldingCostPerCm = ParseDecimal(WeldCostText);

                // ✅ Кислород
                _settings.OxygenBottleVolumeLiters = ParseDouble(OxygenVolumeText);
                _settings.OxygenBottlePressureAtm = ParseDouble(OxygenPressureText);
                _settings.OxygenFlowRateLpm = ParseDouble(OxygenFlowText);
                _settings.OxygenBottlePrice = ParseDecimal(OxygenPriceText);

                // Новые параметры лазера
                _settings.LaserSetupCostPerJob = ParseDecimal(LaserSetupText);
                _settings.LaserMinChargePerJob = ParseDecimal(LaserMinChargeText);
                _settings.PierceTimeSeconds = ParseDouble(PierceTimeText);

                _databaseService.SaveSettings(_settings);

                // Сохраняем профили резки
                _databaseService.UpdateAllLaserProfiles(new System.Collections.Generic.List<MaterialProfile>(LaserProfiles));

                _messageService.ShowInfo("Настройки сохранены!");
            }
            catch (Exception ex)
            {
                _messageService.ShowError("Ошибка сохранения (проверьте числа): " + ex.Message);
            }
        }

        private static double ParseDouble(string? text)
        {
            return NumberParser.TryParseDouble(text, out var value) ? value : 0;
        }

        private static decimal ParseDecimal(string? text)
        {
            if (NumberParser.TryParseDouble(text, out var d))
            {
                return (decimal)d;
            }
            return 0m;
        }
    }
}