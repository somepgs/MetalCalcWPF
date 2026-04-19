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
    }
}
