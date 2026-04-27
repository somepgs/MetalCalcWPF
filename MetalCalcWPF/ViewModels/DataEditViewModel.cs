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

            // --- ✅ СТАНКИ РЕЗКИ (Спринт 2.2) ---
            CuttingMachines = new ObservableCollection<CuttingMachine>(_databaseService.GetAllCuttingMachines());
            AllCuttingMachineKinds = Enum.GetValues<CuttingMachineKind>().ToList();

            // --- ✅ ЦЕХА И КЛИЕНТЫ (миграция v5) ---
            Workshops = new ObservableCollection<Workshop>(_databaseService.GetAllWorkshops());
            // Русские ярлыки для колонки «Тип» в гриде — иначе видно «Internal»/«ExternalClient».
            AllWorkshopKindChoices = new List<WorkshopKindChoice>
            {
                new(WorkshopKind.Internal,       "Цех (внутренний)"),
                new(WorkshopKind.ExternalClient, "Внешний клиент"),
            };

            // --- ✅ СОТРУДНИКИ (миграция v5) ---
            Persons = new ObservableCollection<Person>(_databaseService.GetAllPersons());

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

            AddCuttingMachineCommand = new RelayCommand(_ => AddCuttingMachine());
            DeleteCuttingMachineCommand = new RelayCommand(m => DeleteCuttingMachine(m as CuttingMachine));

            AddWorkshopCommand = new RelayCommand(_ => AddWorkshop());
            DeleteWorkshopCommand = new RelayCommand(w => DeleteWorkshop(w as Workshop));

            AddPersonCommand = new RelayCommand(_ => AddPerson());
            DeletePersonCommand = new RelayCommand(p => DeletePerson(p as Person));
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

        // --- ✅ СТАНКИ РЕЗКИ ---
        public ObservableCollection<CuttingMachine> CuttingMachines { get; }
        public List<CuttingMachineKind> AllCuttingMachineKinds { get; }

        // --- ✅ ЦЕХА (миграция v5) ---
        public ObservableCollection<Workshop> Workshops { get; }
        public List<WorkshopKindChoice> AllWorkshopKindChoices { get; }

        // --- ✅ СОТРУДНИКИ (миграция v5) ---
        public ObservableCollection<Person> Persons { get; }

        public RelayCommand SaveCommand { get; }
        public RelayCommand AddRolledProfileCommand { get; }
        public RelayCommand DeleteRolledProfileCommand { get; }
        public RelayCommand AddCuttingMachineCommand { get; }
        public RelayCommand DeleteCuttingMachineCommand { get; }
        public RelayCommand AddWorkshopCommand { get; }
        public RelayCommand DeleteWorkshopCommand { get; }
        public RelayCommand AddPersonCommand { get; }
        public RelayCommand DeletePersonCommand { get; }

        private void AddRolledProfile()
        {
            var rp = new RolledProfile
            {
                Kind = SelectedKindFilter ?? ProfileKind.AngleEqual,
                SizeCode = "новый",
                GostDesignation = string.Empty,
                WeightPerMeterKg = 0,
                CompatibleMachines = (int)(Models.CuttingMachines.BandSaw | Models.CuttingMachines.AngleGrinder),
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

        private void AddCuttingMachine()
        {
            CuttingMachines.Add(new CuttingMachine
            {
                Name = "Новый станок",
                Kind = CuttingMachineKind.Laser,
                OperatorMonthlySalary = 400_000m,
                PowerConsumptionKw = 20.0,
                AmortizationPerHour = 500m,
                SetupCostPerJob = 1000m,
                MinChargePerJob = 500m,
                PricePerMeterOverride = null,
                IsActive = true,
                Notes = string.Empty,
            });
        }

        private void DeleteCuttingMachine(CuttingMachine? machine)
        {
            if (machine == null) return;
            CuttingMachines.Remove(machine);
        }

        private void AddWorkshop()
        {
            Workshops.Add(new Workshop
            {
                Name = "Новый цех / клиент",
                Kind = WorkshopKind.Internal,
                IsActive = true,
                Notes = string.Empty,
            });
        }

        private void DeleteWorkshop(Workshop? workshop)
        {
            if (workshop == null) return;
            Workshops.Remove(workshop);
        }

        private void AddPerson()
        {
            Persons.Add(new Person
            {
                FullName = "Новый сотрудник",
                Position = "—",
                WorkshopId = null,
                CanSubmit = true,
                CanAccept = false,
                IsActive = true,
                Notes = string.Empty,
            });
        }

        private void DeletePerson(Person? person)
        {
            if (person == null) return;
            Persons.Remove(person);
        }

        private void Save()
        {
            _databaseService.UpdateAllMaterials(new List<MaterialType>(Materials));
            _databaseService.UpdateAllLaserProfiles(new List<MaterialProfile>(LaserProfiles));
            _databaseService.UpdateAllBendingProfiles(new List<BendingProfile>(BendingProfiles));
            _databaseService.UpdateAllWeldingProfiles(new List<WeldingProfile>(WeldingProfiles));
            _databaseService.UpdateAllRolledProfiles(new List<RolledProfile>(RolledProfiles));
            _databaseService.UpdateAllCuttingMachines(new List<CuttingMachine>(CuttingMachines));
            _databaseService.UpdateAllWorkshops(new List<Workshop>(Workshops));
            _databaseService.UpdateAllPersons(new List<Person>(Persons));

            _messageService.ShowInfo("База данных успешно обновлена!");
        }
    }
}
