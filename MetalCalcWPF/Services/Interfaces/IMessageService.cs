using System.Windows;

namespace MetalCalcWPF.Services.Interfaces
{
    public interface IMessageService
    {
        void ShowInfo(string message, string title = "Информация");
        void ShowError(string message, string title = "Ошибка");
        MessageBoxResult ShowConfirm(string message, string title = "Подтверждение");
    }
}
