using System;
using System.Windows;
using MetalCalcWPF.Models;
using System.Collections.Generic;
using ClosedXML.Excel;

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
                    TotalPrice = Math.Round(finalPrice),
                    OperationType = "Laser" // <--- Пишем, что это Лазер
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

        private void ExportToExcel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 1. Получаем все данные из таблицы (или из базы)
                var history = _db.GetRecentOrders();

                if (history.Count == 0)
                {
                    MessageBox.Show("История пуста, нечего выгружать!");
                    return;
                }

                // 2. Спрашиваем, куда сохранить файл
                var saveDialog = new Microsoft.Win32.SaveFileDialog();
                saveDialog.FileName = $"Отчет_{DateTime.Now:yyyy-MM-dd}.xlsx";
                saveDialog.Filter = "Excel Files|*.xlsx";

                if (saveDialog.ShowDialog() == true)
                {
                    // 3. Создаем Excel файл с помощью ClosedXML
                    using (var workbook = new XLWorkbook())
                    {
                        var worksheet = workbook.Worksheets.Add("Заказы");

                        // Заголовки (Шапка)
                        worksheet.Cell(1, 1).Value = "№";
                        worksheet.Cell(1, 2).Value = "Дата";
                        worksheet.Cell(1, 3).Value = "Тип";
                        worksheet.Cell(1, 4).Value = "Клиент";
                        worksheet.Cell(1, 5).Value = "Описание";
                        worksheet.Cell(1, 6).Value = "Цена (₸)";

                        // Жирный шрифт для шапки
                        var headerRange = worksheet.Range("A1:F1");
                        headerRange.Style.Font.Bold = true;
                        headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

                        // Заполняем данными
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

                        // Авто-ширина колонок
                        worksheet.Columns().AdjustToContents();

                        // Сохраняем
                        workbook.SaveAs(saveDialog.FileName);
                    }

                    MessageBox.Show("Файл успешно сохранен!");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при экспорте: " + ex.Message);
            }
        }
    }
}