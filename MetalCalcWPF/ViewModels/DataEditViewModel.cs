using System.Collections.ObjectModel;
using MetalCalcWPF.Infrastructure;
using MetalCalcWPF.Models;
using MetalCalcWPF.Services.Interfaces;

namespace MetalCalcWPF.ViewModels
{
    public class DataEditViewModel : ViewModelBase
    {
        private readonly IDatabaseService _databaseService;
        private readonly IMessageService _messageService;

        public DataEditViewModel(IDatabaseService databaseService, IMessageService messageService)
        {
            _databaseService = databaseService;
            _messageService = messageService;

            Materials = new ObservableCollection<MaterialType>(_databaseService.GetMaterials());
            LaserProfiles = new ObservableCollection<MaterialProfile>(_databaseService.GetAllLaserProfiles());
            BendingProfiles = new ObservableCollection<BendingProfile>(_databaseService.GetAllBendingProfiles());
            WeldingProfiles = new ObservableCollection<WeldingProfile>(_databaseService.GetAllWeldingProfiles()); // ✅ НОВОЕ

            SaveCommand = new RelayCommand(_ => Save());
        }

        public ObservableCollection<MaterialType> Materials { get; }
        public ObservableCollection<MaterialProfile> LaserProfiles { get; }
        public ObservableCollection<BendingProfile> BendingProfiles { get; }
        public ObservableCollection<WeldingProfile> WeldingProfiles { get; } // ✅ НОВОЕ

        public RelayCommand SaveCommand { get; }

        private void Save()
        {
            _databaseService.UpdateAllMaterials(new System.Collections.Generic.List<MaterialType>(Materials));
            _databaseService.UpdateAllLaserProfiles(new System.Collections.Generic.List<MaterialProfile>(LaserProfiles));
            _databaseService.UpdateAllBendingProfiles(new System.Collections.Generic.List<BendingProfile>(BendingProfiles));
            _databaseService.UpdateAllWeldingProfiles(new System.Collections.Generic.List<WeldingProfile>(WeldingProfiles)); // ✅ НОВОЕ

            _messageService.ShowInfo("База данных успешно обновлена!");
        }
    }
}
