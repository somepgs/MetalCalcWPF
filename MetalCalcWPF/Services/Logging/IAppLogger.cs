using System;

namespace MetalCalcWPF.Services.Logging
{
    /// <summary>
    /// Интерфейс простого файлового логгера приложения.
    ///
    /// Использование:
    /// <code>
    /// _log.Info("Заказ рассчитан: {0} тг", result.TotalPrice);
    /// _log.Warn("Профиль толщины {0} мм не найден", thickness);
    /// _log.Error("Не удалось сохранить заказ");
    /// _log.Exception(ex, "Ошибка при сохранении заказа");
    /// </code>
    ///
    /// Реализации должны быть thread-safe: логи могут писаться из разных потоков
    /// (UI, фоновые задачи, миграции).
    ///
    /// Для тестов, где файлы не нужны, есть <see cref="NullAppLogger"/>.
    /// </summary>
    public interface IAppLogger
    {
        void Info(string message, params object[] args);
        void Warn(string message, params object[] args);
        void Error(string message, params object[] args);
        void Exception(Exception ex, string message, params object[] args);
    }
}
