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

        public SettingsViewModel(IDatabaseService databaseService, IMessageService messageService)
        {
            _databaseService = databaseService;
            _messageService = messageService;
            _settings = _databaseService.GetSettings();

            SaveCommand = new RelayCommand(_ => Save());
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

            UpdateOxygenCalculation();
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
                _settings.OperatorMonthlySalary = ParseDouble(SalaryText);
                _settings.WorkDaysPerMonth = (int)ParseDouble(DaysText);
                _settings.WorkHoursPerDay = (int)ParseDouble(HoursText);
                _settings.BendingOperatorSalary = ParseDouble(BendingSalaryText);
                _settings.ElectricityPricePerKw = ParseDouble(ElectricityText);
                _settings.AmortizationPerHour = ParseDouble(AmortizationText);
                _settings.MaterialMarkupPercent = ParseDouble(MaterialMarkupText);
                _settings.HeavyMaterialThresholdMm = ParseDouble(ThresholdText);
                _settings.HeavyHandlingCostPerDetail = ParseDouble(HeavyCostText);
                _settings.WeldingCostPerCm = ParseDouble(WeldCostText);

                // ✅ Кислород
                _settings.OxygenBottleVolumeLiters = ParseDouble(OxygenVolumeText);
                _settings.OxygenBottlePressureAtm = ParseDouble(OxygenPressureText);
                _settings.OxygenFlowRateLpm = ParseDouble(OxygenFlowText);
                _settings.OxygenBottlePrice = ParseDouble(OxygenPriceText);

                _databaseService.SaveSettings(_settings);
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
    }
}