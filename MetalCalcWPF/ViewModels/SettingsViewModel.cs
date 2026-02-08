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

        public SettingsViewModel(IDatabaseService databaseService, IMessageService messageService)
        {
            _databaseService = databaseService;
            _messageService = messageService;
            _settings = _databaseService.GetSettings();

            SaveCommand = new RelayCommand(_ => Save());
            LoadFromSettings(_settings);
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
