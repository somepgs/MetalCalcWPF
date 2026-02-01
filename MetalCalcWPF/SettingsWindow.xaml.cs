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
            // 1. Достаем настройки из базы
            _currentSettings = _db.GetSettings();

            // 2. Раскидываем их по полям
            SalaryBox.Text = _currentSettings.OperatorMonthlySalary.ToString();
            DaysBox.Text = _currentSettings.WorkDaysPerMonth.ToString();
            HoursBox.Text = _currentSettings.WorkHoursPerDay.ToString();

            ThresholdBox.Text = _currentSettings.HeavyMaterialThresholdMm.ToString();
            HeavyCostBox.Text = _currentSettings.HeavyHandlingCostPerDetail.ToString();

            ElectricityBox.Text = _currentSettings.ElectricityPricePerKw.ToString();
            AmortizationBox.Text = _currentSettings.AmortizationPerHour.ToString();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 3. Собираем данные из полей обратно в объект
                _currentSettings.OperatorMonthlySalary = Convert.ToDouble(SalaryBox.Text);
                _currentSettings.WorkDaysPerMonth = Convert.ToInt32(DaysBox.Text);
                _currentSettings.WorkHoursPerDay = Convert.ToInt32(HoursBox.Text);

                _currentSettings.HeavyMaterialThresholdMm = Convert.ToDouble(ThresholdBox.Text);
                _currentSettings.HeavyHandlingCostPerDetail = Convert.ToDouble(HeavyCostBox.Text);

                _currentSettings.ElectricityPricePerKw = Convert.ToDouble(ElectricityBox.Text);
                _currentSettings.AmortizationPerHour = Convert.ToDouble(AmortizationBox.Text);

                // 4. Сохраняем в базу
                _db.SaveSettings(_currentSettings);

                MessageBox.Show("Настройки успешно сохранены!");
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при сохранении! Проверьте, что везде введены числа.\n" + ex.Message);
            }
        }
    }
}