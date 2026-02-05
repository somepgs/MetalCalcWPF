using System;
using System.Windows;
using MetalCalcWPF.Models;

namespace MetalCalcWPF
{
    public partial class SettingsWindow : Window
    {
        private DatabaseService _db = new DatabaseService();
        private WorkshopSettings _currentSettings;

        public SettingsWindow()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void LoadSettings()
        {
            _currentSettings = _db.GetSettings();

            // Экономика
            SalaryBox.Text = _currentSettings.OperatorMonthlySalary.ToString();
            DaysBox.Text = _currentSettings.WorkDaysPerMonth.ToString();
            HoursBox.Text = _currentSettings.WorkHoursPerDay.ToString();

            BendSalaryBox.Text = _currentSettings.BendingOperatorSalary.ToString(); // ЗП Гибочника

            ElectricityBox.Text = _currentSettings.ElectricityPricePerKw.ToString();
            AmortizationBox.Text = _currentSettings.AmortizationPerHour.ToString();

            // Материалы
            MaterialMarkupBox.Text = _currentSettings.MaterialMarkupPercent.ToString();

            // Технология
            ThresholdBox.Text = _currentSettings.HeavyMaterialThresholdMm.ToString();
            HeavyCostBox.Text = _currentSettings.HeavyHandlingCostPerDetail.ToString();
            WeldCostBox.Text = _currentSettings.WeldingCostPerCm.ToString();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Собираем обратно
                _currentSettings.OperatorMonthlySalary = Convert.ToDouble(SalaryBox.Text);
                _currentSettings.WorkDaysPerMonth = Convert.ToInt32(DaysBox.Text);
                _currentSettings.WorkHoursPerDay = Convert.ToInt32(HoursBox.Text);

                _currentSettings.BendingOperatorSalary = Convert.ToDouble(BendSalaryBox.Text);

                _currentSettings.ElectricityPricePerKw = Convert.ToDouble(ElectricityBox.Text);
                _currentSettings.AmortizationPerHour = Convert.ToDouble(AmortizationBox.Text);

                _currentSettings.MaterialMarkupPercent = Convert.ToDouble(MaterialMarkupBox.Text);

                _currentSettings.HeavyMaterialThresholdMm = Convert.ToDouble(ThresholdBox.Text);
                _currentSettings.HeavyHandlingCostPerDetail = Convert.ToDouble(HeavyCostBox.Text);
                _currentSettings.WeldingCostPerCm = Convert.ToDouble(WeldCostBox.Text);

                _db.SaveSettings(_currentSettings);
                MessageBox.Show("Настройки сохранены!");
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка сохранения (проверьте числа): " + ex.Message);
            }
        }
    }
}