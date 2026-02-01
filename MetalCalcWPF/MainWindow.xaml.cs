using System;
using System.Windows;
using MetalCalcWPF.Models; // Подключаем наши модели

namespace MetalCalcWPF
{
    public partial class MainWindow : Window
    {
        // Создаем подключение к БД
        private DatabaseService _db = new DatabaseService();

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 1. Считываем и защищаемся от точек/запятых
                double lengthMeters = Convert.ToDouble(LengthBox.Text.Replace(".", ","));
                double thicknessMm = Convert.ToDouble(ThicknessBox.Text.Replace(".", ","));

                // 2. Ищем профиль
                MaterialProfile profile = _db.GetProfileByThickness(thicknessMm);
                if (profile == null) { ResultLabel.Text = "Нет данных!"; return; }

                WorkshopSettings settings = _db.GetSettings();

                // 3. РАСЧЕТ

                // А) Время резки
                double cuttingTimeMinutes = lengthMeters / profile.CuttingSpeed;
                double cuttingTimeHours = cuttingTimeMinutes / 60.0;

                // Б) Себестоимость часа
                bool isAir = profile.GasType == "Air" || profile.GasType == "Воздух";
                double machineCostPerHour = settings.GetHourlyBaseCost(isAir);

                if (!isAir)
                {
                    // Добавляем газ (грубо: баллон на 4 часа)
                    machineCostPerHour += settings.OxygenBottlePrice / 4.0;
                }

                // В) Чистая себестоимость резки (Время * Стоимость часа)
                double cuttingCost = cuttingTimeHours * machineCostPerHour;

                // Г) Применяем коэффициент наценки (из Excel)
                double priceForCutting = cuttingCost * profile.MarkupCoefficient;

                // Д) Пробивка + СЛОЖНОСТЬ
                double priceForPierce = profile.PiercePrice;

                // !!! НОВОЕ: Проверка на сложность (Кувалда/Кран) !!!
                double handlingExtraCost = 0;

                // Если толщина больше или равна порогу (20 мм)
                if (thicknessMm > settings.HeavyMaterialThresholdMm)
                {
                    // Добавляем наценку за тяжесть (например, +500 тг)
                    handlingExtraCost = settings.HeavyHandlingCostPerDetail;
                }

                // Финальная цена = Резка + Пробивка + Сложность
                double finalPrice = priceForCutting + priceForPierce + handlingExtraCost;

                // 4. Вывод
                ResultLabel.Text = $"Итого: {Math.Round(finalPrice)} ₸";

                // Дебаг (чтобы ты видел, добавилась ли наценка)
                // Можно удалить потом
                if (handlingExtraCost > 0)
                {
                    MessageBox.Show($"Внимание! Применена наценка за сложность (толщина > {settings.HeavyMaterialThresholdMm}мм).\n" +
                                    $"Добавлено: {handlingExtraCost} ₸ за съем детали.");
                }

                // Дебаг для проверки (сравни с Excel!)
                double costPerMinute = machineCostPerHour / 60.0;
                MessageBox.Show($"Профиль: {profile.Thickness}мм ({profile.GasType})\n" +
                                $"Цена минуты станка: {Math.Round(costPerMinute, 2)} тг (Excel=65?)\n" +
                                $"Время: {Math.Round(cuttingTimeMinutes, 2)} мин\n" +
                                $"Себестоимость реза: {Math.Round(cuttingCost, 2)} тг\n" +
                                $"Цена реза (x{profile.MarkupCoefficient}): {Math.Round(priceForCutting)} тг\n" +
                                $"Пробивка: {profile.PiercePrice} тг");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка: " + ex.Message);
            }
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            SettingsWindow settings = new SettingsWindow();
            settings.ShowDialog();
        }
    }
}