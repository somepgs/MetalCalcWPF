using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MetalCalcWPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 1. Берем текст из полей и превращаем в числа
                // Используем double, так как длина может быть 1.5 метра
                double length = Convert.ToDouble(LengthBox.Text);
                double thickness = Convert.ToDouble(ThicknessBox.Text);

                // 2. Простая логика цены (потом усложним)
                // Допустим, 1 метр реза стоит 200 тенге * толщину
                double pricePerMeter = 200 * thickness;

                double totalCost = length * pricePerMeter;

                // 3. Выводим результат в текстовую метку
                ResultLabel.Text = $"Итого: {totalCost} ₸";
            }
            catch
            {
                // Если ввели буквы вместо цифр
                ResultLabel.Text = "Ошибка! Введите числа.";
            }
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            // Создаем экземпляр окна настроек
            SettingsWindow settings = new SettingsWindow();

            // Показываем его как "диалоговое" (блокирует главное окно, пока не закроешь настройки)
            settings.ShowDialog();
        }
    }
}