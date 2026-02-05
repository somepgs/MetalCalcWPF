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
            UpdateHistory();

            // Загружаем материалы в выпадающий список
            MaterialCombo.ItemsSource = _db.GetMaterials();
            MaterialCombo.SelectedIndex = 0; // Выбираем первый по умолчанию
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // --- 1. СБОР ОБЩИХ ДАННЫХ ---
                string clientName = ClientBox.Text;
                if (string.IsNullOrWhiteSpace(clientName)) clientName = "Без названия";

                // !!! ИСПРАВЛЕНИЕ: Раньше тут стояло QuantityBox.Text !!!
                // Теперь мы правильно берем толщину из поля Толщины
                double thicknessMm = GetSafeDouble(ThicknessBox.Text);

                double quantity = GetSafeDouble(QuantityBox.Text);
                if (quantity == 0) quantity = 1; // Защита: минимум 1 деталь

                WorkshopSettings settings = _db.GetSettings();

                double totalPerDetail = 0; // Цена за ОДНУ штуку
                string operationsLog = ""; // Строка для истории (что делали)

                // --- 0. РАСЧЕТ МЕТАЛЛА ---
                // Считываем габариты
                double widthMm = GetSafeDouble(WidthBox.Text);
                double heightMm = GetSafeDouble(HeightBox.Text);

                if (widthMm > 0 && heightMm > 0)
                {
                    // Получаем выбранный материал
                    var selectedMaterial = MaterialCombo.SelectedItem as MaterialType;
                    if (selectedMaterial != null)
                    {
                        // 1. Считаем ВЕС (кг) = Длина(м) * Ширина(м) * Толщина(мм) * Плотность
                        // Формула: (мм * мм * мм * г/см3) / 1 000 000 = кг
                        double weightKg = (widthMm * heightMm * thicknessMm * selectedMaterial.Density) / 1000000.0;

                        // 2. ОПРЕДЕЛЯЕМ ЦЕНУ ЗАКУПА (Твои условия)
                        double costPricePerKg = selectedMaterial.BasePricePerKg;

                        // Если это Черная сталь (Ст3) - применяем твою сетку цен
                        if (selectedMaterial.Name.Contains("Ст3"))
                        {
                            if (thicknessMm <= 12) costPricePerKg = 355;
                            else if (thicknessMm <= 25) costPricePerKg = 375;
                            else costPricePerKg = 385; // для 30-40 мм
                        }

                        // 3. ДОБАВЛЯЕМ НАЦЕНКУ (из настроек, например 30%)
                        double sellPricePerKg = costPricePerKg * (1 + settings.MaterialMarkupPercent / 100.0);

                        // 4. СТОИМОСТЬ МЕТАЛЛА
                        double materialCost = weightKg * sellPricePerKg;

                        totalPerDetail += materialCost;

                        // Добавляем в лог инфо о металле
                        operationsLog += $"Metal({Math.Round(weightKg, 2)}kg) ";

                        // Можно вывести инфо куда-то, но пока просто прибавим к сумме
                    }
                }

                // --- 2. РАСЧЕТ ЛАЗЕРА (Если введена длина) ---
                if (!string.IsNullOrWhiteSpace(LengthBox.Text) && LengthBox.Text != "0")
                {
                    double lengthMeters = Convert.ToDouble(LengthBox.Text.Replace(".", ","));
                    MaterialProfile profile = _db.GetProfileByThickness(thicknessMm);

                    if (profile != null)
                    {
                        double cuttingTimeHours = (lengthMeters / profile.CuttingSpeed) / 60.0;
                        bool isAir = profile.GasType == "Air" || profile.GasType == "Воздух";
                        double machineCostPerHour = settings.GetHourlyBaseCost(isAir);
                        if (!isAir) machineCostPerHour += settings.OxygenBottlePrice / 4.0;

                        double laserCost = (cuttingTimeHours * machineCostPerHour * profile.MarkupCoefficient)
                                         + profile.PiercePrice;

                        // Сложность (Кувалда)
                        if (thicknessMm > settings.HeavyMaterialThresholdMm)
                            laserCost += settings.HeavyHandlingCostPerDetail;

                        totalPerDetail += laserCost;
                        operationsLog += "Laser ";
                    }
                }

                // --- 3. РАСЧЕТ ГИБКИ ---
                if (IsBendingEnabled.IsChecked == true)
                {
                    double bends = GetSafeDouble(BendsCountBox.Text);
                    double bendLength = GetSafeDouble(BendLengthBox.Text); // Считываем длину гиба

                    // 1. Ищем профиль
                    BendingProfile bendProfile = _db.GetBendingProfile(thicknessMm);

                    double pricePerOneBend = 0;
                    double setupPriceTotal = 0;

                    if (bendProfile != null)
                    {
                        // 2. ВЫБИРАЕМ ЦЕНУ ПО ДЛИНЕ (Логика из Excel)
                        if (bendLength <= 1500)
                        {
                            pricePerOneBend = bendProfile.PriceLen1500;
                        }
                        else if (bendLength <= 3000)
                        {
                            pricePerOneBend = bendProfile.PriceLen3000;
                        }
                        else
                        {
                            pricePerOneBend = bendProfile.PriceLen6000;
                            // Проверка на предел станка
                            if (bendLength > 6050) MessageBox.Show("Внимание! Длина гиба больше длины станка (6м)!");
                        }

                        // 3. Считаем стоимость гибов
                        double bendCost = bends * pricePerOneBend;

                        // 4. Добавляем наладку (делим на всю партию)
                        setupPriceTotal = bendProfile.SetupPrice;
                        double setupPerDetail = setupPriceTotal / quantity;

                        double totalBendingCost = bendCost + setupPerDetail;

                        // Вывод инфо
                        BendInfoLabel.Text = $"Матрица: V{bendProfile.V_Die} (Мин.полка {bendProfile.MinFlange})\n" +
                                             $"Цена гиба: {pricePerOneBend} ₸ (за длину {bendLength} мм)\n" +
                                             $"Наладка: {bendProfile.SetupPrice} ₸ (на партию)";

                        totalPerDetail += totalBendingCost;
                        operationsLog += $"+ Bend({bends}x{bendLength}mm) ";
                    }
                    else
                    {
                        // Если профиля нет (например, 40мм) - берем базу
                        totalPerDetail += bends * settings.BendingBasePrice;
                        BendInfoLabel.Text = "Нет профиля! Базовая цена.";
                    }
                }

                // --- 4. РАСЧЕТ СВАРКИ ---
                if (IsWeldingEnabled.IsChecked == true)
                {
                    double weldCm = GetSafeDouble(WeldLengthBox.Text);
                    double weldCost = weldCm * settings.WeldingCostPerCm;

                    totalPerDetail += weldCost;
                    operationsLog += $"+ Weld({weldCm}cm) ";
                }

                // --- 5. ИТОГ ---
                double finalTotal = totalPerDetail * quantity; // Умножаем на партию

                ResultLabel.Text = $"Итого: {Math.Round(finalTotal)} ₸";
                ResultDetails.Text = $"{quantity} шт по {Math.Round(totalPerDetail)} ₸\n({operationsLog})";

                // --- 6. СОХРАНЕНИЕ ---
                // !!! ИСПРАВЛЕНИЕ: Если сумма 0, не сохраняем в базу !!!
                if (finalTotal > 0)
                {
                    var newOrder = new OrderHistory
                    {
                        CreatedDate = DateTime.Now,
                        ClientName = clientName,
                        Description = $"{quantity}шт / {thicknessMm}мм",
                        TotalPrice = Math.Round(finalTotal),
                        OperationType = operationsLog // Записываем состав работ
                    };

                    _db.SaveOrder(newOrder);
                    UpdateHistory();
                }
                else
                {
                    // Можно вывести сообщение, если нужно, или просто ничего не делать
                    // MessageBox.Show("Расчет равен 0, запись не сохранена.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка: " + ex.Message);
            }
        }

        // !!! НОВОЕ: Обработчик удаления строки (ПКМ)
        private void DeleteOrder_Click(object sender, RoutedEventArgs e)
        {
            // Получаем выделенную строку
            if (HistoryGrid.SelectedItem is OrderHistory selectedOrder)
            {
                var result = MessageBox.Show($"Удалить заказ №{selectedOrder.Id}?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    _db.DeleteOrder(selectedOrder.Id);
                    UpdateHistory(); // Обновляем таблицу
                }
            }
        }

        // Метод для обновления таблицы
        private void UpdateHistory()
        {
            List<OrderHistory> history = _db.GetRecentOrders();
            HistoryGrid.ItemsSource = history;
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            SettingsWindow settings = new SettingsWindow();
            settings.ShowDialog();
        }

        private void ExportToExcel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var history = _db.GetRecentOrders();

                if (history.Count == 0)
                {
                    MessageBox.Show("История пуста, нечего выгружать!");
                    return;
                }

                var saveDialog = new Microsoft.Win32.SaveFileDialog();
                saveDialog.FileName = $"Отчет_{DateTime.Now:yyyy-MM-dd}.xlsx";
                saveDialog.Filter = "Excel Files|*.xlsx";

                if (saveDialog.ShowDialog() == true)
                {
                    using (var workbook = new XLWorkbook())
                    {
                        var worksheet = workbook.Worksheets.Add("Заказы");

                        worksheet.Cell(1, 1).Value = "№";
                        worksheet.Cell(1, 2).Value = "Дата";
                        worksheet.Cell(1, 3).Value = "Тип";
                        worksheet.Cell(1, 4).Value = "Клиент";
                        worksheet.Cell(1, 5).Value = "Описание";
                        worksheet.Cell(1, 6).Value = "Цена (₸)";

                        var headerRange = worksheet.Range("A1:F1");
                        headerRange.Style.Font.Bold = true;
                        headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

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

        private double GetSafeDouble(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;
            text = text.Replace(".", ",");
            if (double.TryParse(text, out double result)) return result;
            return 0;
        }
    }
}