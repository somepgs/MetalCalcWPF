using System;
using System.Windows;
using MetalCalcWPF.Models;
using System.Collections.Generic;

namespace MetalCalcWPF
{
    public partial class MainWindow : Window
    {
        private DatabaseService _db = new DatabaseService();

        public MainWindow()
        {
            InitializeComponent();
            UpdateHistory(); // Загружаем таблицу при запуске
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 1. Сбор данных
                string clientName = ClientBox.Text; // Имя клиента
                if (string.IsNullOrWhiteSpace(clientName)) clientName = "Без названия";

                double lengthMeters = Convert.ToDouble(LengthBox.Text.Replace(".", ","));
                double thicknessMm = Convert.ToDouble(ThicknessBox.Text.Replace(".", ","));

                // 2. Расчет (Тот же самый, что и был)
                MaterialProfile profile = _db.GetProfileByThickness(thicknessMm);
                if (profile == null) { ResultLabel.Text = "Нет профиля!"; return; }

                WorkshopSettings settings = _db.GetSettings();

                // --- ЛОГИКА РАСЧЕТА (Кратко) ---
                double cuttingTimeMinutes = lengthMeters / profile.CuttingSpeed;
                double cuttingTimeHours = cuttingTimeMinutes / 60.0;

                bool isAir = profile.GasType == "Air" || profile.GasType == "Воздух";
                double machineCostPerHour = settings.GetHourlyBaseCost(isAir);
                if (!isAir) machineCostPerHour += settings.OxygenBottlePrice / 4.0;

                double cuttingCost = cuttingTimeHours * machineCostPerHour;
                double priceForCutting = cuttingCost * profile.MarkupCoefficient;
                double priceForPierce = profile.PiercePrice;

                // Сложность
                double handlingExtraCost = 0;
                if (thicknessMm > settings.HeavyMaterialThresholdMm)
                    handlingExtraCost = settings.HeavyHandlingCostPerDetail;

                double finalPrice = priceForCutting + priceForPierce + handlingExtraCost;

                // 3. Вывод на экран
                ResultLabel.Text = $"Итого: {Math.Round(finalPrice)} ₸";

                // 4. !!! СОХРАНЕНИЕ В ИСТОРИЮ !!!
                var newOrder = new OrderHistory
                {
                    CreatedDate = DateTime.Now,
                    ClientName = clientName,
                    Description = $"{thicknessMm}мм / {lengthMeters}м",
                    TotalPrice = Math.Round(finalPrice)
                };

                _db.SaveOrder(newOrder); // Пишем в БД
                UpdateHistory();         // Обновляем таблицу справа мгновенно
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка: " + ex.Message);
            }
        }

        // Метод для обновления таблицы
        private void UpdateHistory()
        {
            List<OrderHistory> history = _db.GetRecentOrders();
            HistoryGrid.ItemsSource = history; // Привязываем данные к таблице
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            SettingsWindow settings = new SettingsWindow();
            settings.ShowDialog();
            // После закрытия настроек пересчитывать ничего не надо,
            // но следующий расчет уже будет по новым ценам.
        }
    }
}