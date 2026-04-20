using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
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
            WeldingProfiles = new ObservableCollection<WeldingProfile>(_databaseService.GetAllWeldingProfiles()); // ✅

            // --- ✅ СОРТАМЕНТ ПРОКАТА ---
            RolledProfiles = new ObservableCollection<RolledProfile>(_databaseService.GetAllRolledProfiles());
            RolledProfilesView = CollectionViewSource.GetDefaultView(RolledProfiles);
            RolledProfilesView.SortDescriptions.Add(new SortDescription(nameof(RolledProfile.Kind), ListSortDirection.Ascending));
            RolledProfilesView.SortDescriptions.Add(new SortDescription(nameof(RolledProfile.WeightPerMeterKg), ListSortDirection.Ascending));
            RolledProfilesView.Filter = o =>
                SelectedKindFilter == null || (o is RolledProfile rp && rp.Kind == SelectedKindFilter);

            // Список для фильтра: "Все" (null) + все значения enum
            KindFilters = new List<ProfileKind?> { null };
            foreach (var v in Enum.GetValues<ProfileKind>()) KindFilters.Add(v);

            // Для комбобоксов в колонке Kind и Материал
            AllKinds = Enum.GetValues<ProfileKind>().ToList();

            SaveCommand = new RelayCommand(_ => Save());
            AddRolledProfileCommand = new RelayCommand(_ => AddRolledProfile());
            DeleteRolledProfileCommand = new RelayCommand(p => DeleteRolledProfile(p as RolledProfile));
        }

        public ObservableCollection<MaterialType> Materials { get; }
        public ObservableCollection<MaterialProfile> LaserProfiles { get; }
        public ObservableCollection<BendingProfile> BendingProfiles { get; }
        public ObservableCollection<WeldingProfile> WeldingProfiles { get; }

        // --- ✅ СОРТАМЕНТ ---
        public ObservableCollection<RolledProfile> RolledProfiles { get; }
        public ICollectionView RolledProfilesView { get; }
        public List<ProfileKind?> KindFilters { get; }
        public List<ProfileKind> AllKinds { get; }

        private ProfileKind? _selectedKindFilter;
        public ProfileKind? SelectedKindFilter
        {
            get => _selectedKindFilter;
            set
            {
                if (_selectedKindFilter != value)
                {
                    _selectedKindFilter = value;
                    OnPropertyChanged();
                    RolledProfilesView.Refresh();
                }
            }
        }

        public RelayCommand SaveCommand { get; }
        public RelayCommand AddRolledProfileCommand { get; }
        public RelayCommand DeleteRolledProfileCommand { get; }

        private void AddRolledProfile()
        {
            var rp = new RolledProfile
            {
                Kind = SelectedKindFilter ?? ProfileKind.AngleEqual,
                SizeCode = "новый",
                GostDesignation = string.Empty,
                WeightPerMeterKg = 0,
                CompatibleMachines = (int)(CuttingMachines.BandSaw | CuttingMachines.AngleGrinder),
                IsActive = true,
            };
            RolledProfiles.Add(rp);
            RolledProfilesView.Refresh();
        }

        private void DeleteRolledProfile(RolledProfile? profile)
        {
            if (profile == null) return;
            RolledProfiles.Remove(profile);
            RolledProfilesView.Refresh();
        }

        private void Save()
        {
            _databaseService.UpdateAllMaterials(new List<MaterialType>(Materials));
            _databaseService.UpdateAllLaserProfiles(new List<MaterialProfile>(LaserProfiles));
            _databaseService.UpdateAllBendingProfiles(new List<BendingProfile>(BendingProfiles));
            _databaseService.UpdateAllWeldingProfiles(new List<WeldingProfile>(WeldingProfiles));
            _databaseService.UpdateAllRolledProfiles(new List<RolledProfile>(RolledProfiles)); // ✅

            _messageService.ShowInfo("База данных успешно обновлена!");
        }
    }
}
