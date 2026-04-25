using System;
using System.Collections.Generic;
using MetalCalcWPF.Models;

namespace MetalCalcWPF.Services.Interfaces
{
    public interface IDatabaseService
    {
        WorkshopSettings GetSettings();
        void SaveSettings(WorkshopSettings settings);
        
        MaterialProfile? GetProfileByThickness(double thickness);
        BendingProfile? GetBendingProfile(double thickness);
        WeldingProfile? GetWeldingProfile(double thickness); // ✅ НОВОЕ
        
        void SaveOrder(OrderHistory order);
        void DeleteOrder(int id);
        List<OrderHistory> GetRecentOrders();

        /// <summary>
        /// Возвращает заказы в заданном диапазоне дат для отчётности руководству.
        /// Полуоткрытый интервал <c>[startInclusive; endExclusive)</c> — так легко
        /// передавать «за месяц» (например, с 1 апреля 00:00 по 1 мая 00:00), и
        /// не теряется последний день из-за времени суток.
        /// </summary>
        List<OrderHistory> GetOrdersByDateRange(DateTime startInclusive, DateTime endExclusive);
        
        List<MaterialType> GetMaterials();
        void UpdateAllMaterials(List<MaterialType> list);
        
        List<MaterialProfile> GetAllLaserProfiles();
        void UpdateAllLaserProfiles(List<MaterialProfile> list);
        
        List<BendingProfile> GetAllBendingProfiles();
        void UpdateAllBendingProfiles(List<BendingProfile> list);
        
        // ✅ НОВЫЕ МЕТОДЫ для сварки
        List<WeldingProfile> GetAllWeldingProfiles();
        void UpdateAllWeldingProfiles(List<WeldingProfile> list);

        // ✅ Сортамент (уголки, швеллеры, двутавры, профтруба и т.п.)
        List<RolledProfile> GetAllRolledProfiles();
        List<RolledProfile> GetRolledProfilesByKind(ProfileKind kind);
        RolledProfile? GetRolledProfileById(int id);
        int AddRolledProfile(RolledProfile profile);
        void UpdateRolledProfile(RolledProfile profile);
        void DeleteRolledProfile(int id);
        void UpdateAllRolledProfiles(List<RolledProfile> list);

        // ✅ Станки резки (лазер, ленточная пила, пресс-ножницы, гильотина, болгарка)
        List<CuttingMachine> GetAllCuttingMachines();
        List<CuttingMachine> GetCuttingMachinesByKind(CuttingMachineKind kind);
        CuttingMachine? GetCuttingMachineById(int id);
        int AddCuttingMachine(CuttingMachine machine);
        void UpdateCuttingMachine(CuttingMachine machine);
        void DeleteCuttingMachine(int id);
        void UpdateAllCuttingMachines(List<CuttingMachine> list);

        // Цеха и внешние клиенты (миграция v5).
        List<Workshop> GetAllWorkshops();
        Workshop? GetWorkshopById(int id);
        int AddWorkshop(Workshop workshop);
        void UpdateWorkshop(Workshop workshop);
        void DeleteWorkshop(int id);
        void UpdateAllWorkshops(List<Workshop> list);

        // Сотрудники: заявители и приёмщики заказов (миграция v5).
        List<Person> GetAllPersons();
        Person? GetPersonById(int id);
        int AddPerson(Person person);
        void UpdatePerson(Person person);
        void DeletePerson(int id);
        void UpdateAllPersons(List<Person> list);
    }
}
