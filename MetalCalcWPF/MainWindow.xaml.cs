using System;
using System.Windows;
using MetalCalcWPF.Models;
using System.Collections.Generic;
using ClosedXML.Excel;
using MetalCalcWPF.Services;

namespace MetalCalcWPF
{
    public partial class MainWindow : Window
    {
        private DatabaseService _db = new DatabaseService();
        private CalculationService _calculator;

        public MainWindow()
        {
            InitializeComponent();
            UpdateHistory();
            _calculator = new CalculationService(_db);

            // Загружаем материалы в выпадающий список
            MaterialCombo.ItemsSource = _db.GetMaterials();
            MaterialCombo.SelectedIndex = 0; // Выбираем первый по умолчанию
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 1. Считываем данные из полей (это всё, что делает UI)
                string clientName = string.IsNullOrWhiteSpace(ClientBox.Text) ? "Без названия" : ClientBox.Text;

                double thicknessMm = GetSafeDouble(ThicknessBox.Text);
                int quantity = (int)GetSafeDouble(QuantityBox.Text);
                if (quantity == 0) quantity = 1;

                double widthMm = GetSafeDouble(WidthBox.Text);
                double heightMm = GetSafeDouble(HeightBox.Text);
                var selectedMaterial = MaterialCombo.SelectedItem as MaterialType;

                double laserLen = GetSafeDouble(LengthBox.Text); // В метрах

                bool useBend = IsBendingEnabled.IsChecked == true;
                int bendsCount = (int)GetSafeDouble(BendsCountBox.Text);
                double bendLenMm = GetSafeDouble(BendLengthBox.Text);

                bool useWeld = IsWeldingEnabled.IsChecked == true;
                double weldCm = GetSafeDouble(WeldLengthBox.Text);


                // 2. ВЫЗЫВАЕМ СЕРВИС (Вся математика там)
                var result = _calculator.CalculateOrder(
                    widthMm, heightMm, thicknessMm, quantity, selectedMaterial,
                    laserLen,
                    useBend, bendsCount, bendLenMm,
                    useWeld, weldCm
                );


                // 3. Вывод на экран
                ResultLabel.Text = $"Итого: {Math.Round(result.TotalPrice)} ₸";

                // !!! НОВОЕ: Показываем из чего состоит цена (Поможет понять влияние зарплаты)
                ResultDetails.Text = $"Металл: {Math.Round(result.MaterialCost)} ₸\n" +
                                     $"Лазер: {Math.Round(result.LaserCost)} ₸\n" +
                                     $"Гибка: {Math.Round(result.BendingCost)} ₸\n" +
                                     $"Сварка: {Math.Round(result.WeldingCost)} ₸";

                // 4. Сохранение
                if (result.TotalPrice > 0)
                {
                    var newOrder = new OrderHistory
                    {
                        CreatedDate = DateTime.Now,
                        ClientName = clientName,
                        Description = $"{quantity}шт / {thicknessMm}мм",
                        TotalPrice = Math.Round(result.TotalPrice),
                        OperationType = result.Log
                    };
                    _db.SaveOrder(newOrder);
                    UpdateHistory();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка: " + ex.Message);
            }
        }

        // Обработчик удаления строки (ПКМ)
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

        private void OpenDb_Click(object sender, RoutedEventArgs e)
        {
            var dbWindow = new DataEditWindow();
            dbWindow.ShowDialog();
            // После закрытия нужно обновить списки (например, материалы в выпадающем списке)
            MaterialCombo.ItemsSource = _db.GetMaterials();
            MaterialCombo.SelectedIndex = 0;
        }
    }
}