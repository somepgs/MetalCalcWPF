using System.Collections.Generic;
using System.Linq;
using System.Windows;
using MetalCalcWPF.Models;

namespace MetalCalcWPF
{
    public partial class DataEditWindow : Window
    {
        private DatabaseService _db = new DatabaseService();

        // Списки для привязки к таблицам
        private List<MaterialType> _materials;
        private List<MaterialProfile> _laserProfiles;
        private List<BendingProfile> _bendingProfiles;

        public DataEditWindow()
        {
            InitializeComponent();
            LoadData();
        }

        private void LoadData()
        {
            // Загружаем данные из базы напрямую
            _materials = _db.GetMaterials();
            _laserProfiles = _db.GetAllLaserProfiles(); // Нужно добавить этот метод в DB
            _bendingProfiles = _db.GetAllBendingProfiles(); // И этот тоже

            // Привязываем к таблицам
            MaterialsGrid.ItemsSource = _materials;
            LaserGrid.ItemsSource = _laserProfiles;
            BendingGrid.ItemsSource = _bendingProfiles;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // Сохраняем все списки обратно в базу
            _db.UpdateAllMaterials(_materials);
            _db.UpdateAllLaserProfiles(_laserProfiles);
            _db.UpdateAllBendingProfiles(_bendingProfiles);

            MessageBox.Show("База данных успешно обновлена!");
            this.Close();
        }
    }
}