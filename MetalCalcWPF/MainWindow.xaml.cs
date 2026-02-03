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
                // --- 1. СБОР ОБЩИХ ДАННЫХ ---
                string clientName = ClientBox.Text;
                if (string.IsNullOrWhiteSpace(clientName)) clientName = "Без названия";

                double thicknessMm = GetSafeDouble(QuantityBox.Text);
                double quantity = GetSafeDouble(QuantityBox.Text);
                if (quantity == 0) quantity = 1; // Защита: минимум 1 деталь

                WorkshopSettings settings = _db.GetSettings();

                double totalPerDetail = 0; // Цена за ОДНУ штуку
                string operationsLog = ""; // Строка для истории (что делали)

                // --- 2. РАСЧЕТ ЛАЗЕРА (Если введена длина) ---
                if (!string.IsNullOrWhiteSpace(LengthBox.Text) && LengthBox.Text != "0")
                {
                    double lengthMeters = Convert.ToDouble(LengthBox.Text.Replace(".", ","));
                    MaterialProfile profile = _db.GetProfileByThickness(thicknessMm);

                    if (profile != null)
                    {
                        // (Копируем твою логику расчета лазера сюда)
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

                    // 1. Ищем профиль гибки для этой толщины
                    BendingProfile bendProfile = _db.GetBendingProfile(thicknessMm);

                    double bendPrice = 0;

                    if (bendProfile != null)
                    {
                        // Считаем: (Цена * Гибы)
                        bendPrice = bends * bendProfile.PricePerBend;

                        // Добавляем наладку (ОДИН РАЗ на всю партию, поэтому делим на кол-во)
                        // Например: Наладка 1000 тг. Деталей 10. +100 тг к каждой детали.
                        double setupCostPerDetail = settings.BendingSetupPrice / quantity;
                        bendPrice += setupCostPerDetail;

                        // Выводим инфо о матрице
                        BendInfoLabel.Text = $"Матрица: V{bendProfile.V_Die}\n" +
                                             $"Мин. полка: {bendProfile.MinFlange} мм\n" +
                                             $"Цена гиба: {bendProfile.PricePerBend} ₸";

                        // ПРОВЕРКА ТЕХНОЛОГИИ (Предупреждение)
                        // Если юзер ввел "0" гибов, но галочка стоит - не страшно.
                        // А вот если конструктор заложит полку меньше минимума - это беда.
                        // (Пока мы не знаем длину полки из интерфейса, но выводим инфо оператору)
                    }
                    else
                    {
                        // Если толщина 40мм и нет матрицы - считаем по базовой цене
                        bendPrice = bends * settings.BendingBasePrice;
                        BendInfoLabel.Text = "Нет профиля! Расчет по базовой ставке.";
                    }

                    totalPerDetail += bendPrice;
                    operationsLog += $"+ Bend({bends}x{bendProfile?.V_Die}) ";
                }
                else
                {
                    BendInfoLabel.Text = "Расчет выключен";
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
                var newOrder = new OrderHistory
                {
                    CreatedDate = DateTime.Now,
                    ClientName = clientName,
                    Description = $"{quantity}шт / {thicknessMm}мм",
                    TotalPrice = Math.Round(finalTotal),
                    OperationType = operationsLog // Записываем состав работ (Лазер + Гибка...)
                };

                _db.SaveOrder(newOrder);
                UpdateHistory();
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

        // Вспомогательный метод: Безопасное превращение текста в число
        private double GetSafeDouble(string text)
        {
            // Если пусто или пробелы - возвращаем 0
            if (string.IsNullOrWhiteSpace(text)) return 0;

            // Пробуем превратить (учитываем и точку, и запятую)
            text = text.Replace(".", ",");
            if (double.TryParse(text, out double result))
            {
                return result;
            }

            // Если ввели буквы "abc" - возвращаем 0
            return 0;
        }
    }
}