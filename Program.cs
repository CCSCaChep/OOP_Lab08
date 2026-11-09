/****************/
// Mã sinh viên: 202418929
// Họ tên: Hoàng Chí Kiệt
/****************/

using System;
using System.Collections.Generic;

namespace Lab08_DelegateAndEvent
{
    // ================= 1. ENUMS VÀ EVENT ARGS =================
    public enum DeviceStatus { Available, Borrowed, UnderMaintenance }
    public enum AlertSeverity { Information, Warning, Critical }

    public class DeviceStatusChangedEventArgs : EventArgs
    {
        public string DeviceId { get; }
        public DeviceStatus OldStatus { get; }
        public DeviceStatus NewStatus { get; }
        public DateTime ChangedAt { get; }

        public DeviceStatusChangedEventArgs(string deviceId, DeviceStatus oldStatus, DeviceStatus newStatus, DateTime changedAt)
        {
            DeviceId = deviceId; OldStatus = oldStatus; NewStatus = newStatus; ChangedAt = changedAt;
        }
    }

    public class DeviceAlertEventArgs : EventArgs
    {
        public string DeviceId { get; }
        public string Message { get; }
        public AlertSeverity Severity { get; }
        public DateTime OccurredAt { get; }

        public DeviceAlertEventArgs(string deviceId, string message, AlertSeverity severity, DateTime occurredAt)
        {
            DeviceId = deviceId; Message = message; Severity = severity; OccurredAt = occurredAt;
        }
    }

    // ================= 2. PUBLISHER (THIẾT BỊ) =================
    public abstract class TeachingDevice
    {
        public string Id { get; }
        public string Name { get; }
        public DeviceStatus Status { get; private set; }

        public event EventHandler<DeviceStatusChangedEventArgs>? StatusChanged;
        public event EventHandler<DeviceAlertEventArgs>? AlertRaised;

        protected TeachingDevice(string id, string name)
        {
            Id = id; Name = name; Status = DeviceStatus.Available;
        }

        public void ChangeStatus(DeviceStatus newStatus)
        {
            if (newStatus == Status) return;
            
            DeviceStatus oldStatus = Status;
            Status = newStatus;
            OnStatusChanged(new DeviceStatusChangedEventArgs(Id, oldStatus, newStatus, DateTime.Now));
        }

        protected void RaiseAlert(string message, AlertSeverity severity)
        {
            OnAlertRaised(new DeviceAlertEventArgs(Id, message, severity, DateTime.Now));
        }

        protected virtual void OnStatusChanged(DeviceStatusChangedEventArgs e) => StatusChanged?.Invoke(this, e);
        protected virtual void OnAlertRaised(DeviceAlertEventArgs e) => AlertRaised?.Invoke(this, e);
    }

    public class ProjectorRemote : TeachingDevice
    {
        public int BatteryLevel { get; private set; } = 100;
        public ProjectorRemote(string id, string name) : base(id, name) { }

        public void UpdateBatteryLevel(int newLevel)
        {
            if (newLevel < 0 || newLevel > 100) throw new InvalidDeviceReadingException(Id, newLevel);
            BatteryLevel = newLevel;

            if (BatteryLevel == 0) RaiseAlert("Hết sạch pin", AlertSeverity.Critical);
            else if (BatteryLevel < 20) RaiseAlert("Pin yếu", AlertSeverity.Warning);
        }
    }

    public class AirConditionerRemote : TeachingDevice
    {
        public string AssignedRoom { get; }
        public bool IsResponsive { get; private set; } = true;
        public AirConditionerRemote(string id, string name, string room) : base(id, name) { AssignedRoom = room; }

        public void UpdateResponseState(bool isResponsive)
        {
            if (IsResponsive && !isResponsive) // Đang phản hồi -> mất phản hồi
            {
                IsResponsive = isResponsive;
                RaiseAlert("Mất kết nối điều khiển", AlertSeverity.Critical);
            }
            IsResponsive = isResponsive;
        }
    }

    public class WirelessMicrophone : TeachingDevice
    {
        public int BatteryLevel { get; private set; } = 100;
        public int SignalQuality { get; private set; } = 100;
        public WirelessMicrophone(string id, string name) : base(id, name) { }

        public void UpdateBatteryLevel(int newLevel)
        {
            if (newLevel < 0 || newLevel > 100) throw new InvalidDeviceReadingException(Id, newLevel);
            BatteryLevel = newLevel;
            CheckLimits();
        }

        public void UpdateSignalQuality(int newQuality)
        {
            if (newQuality < 0 || newQuality > 100) throw new InvalidDeviceReadingException(Id, newQuality);
            SignalQuality = newQuality;
            CheckLimits();
        }

        private void CheckLimits()
        {
            if (BatteryLevel == 0 || SignalQuality == 0) RaiseAlert("Tắt nguồn hoặc mất sóng hoàn toàn", AlertSeverity.Critical);
            else if (BatteryLevel < 30) RaiseAlert("Pin micro yếu", AlertSeverity.Warning);
            else if (SignalQuality < 40) RaiseAlert("Sóng micro yếu", AlertSeverity.Warning);
        }
    }

    // ================= 3. SUBSCRIBERS =================
    public class ConsoleNotificationService
    {
        public void HandleAlert(object? sender, DeviceAlertEventArgs e) => Console.WriteLine($"[CONSOLE - {e.Severity}] {e.DeviceId}: {e.Message}");
    }

    public class DeviceAuditLogger
    {
        private readonly List<string> entries = new();
        public IReadOnlyList<string> Entries => entries.AsReadOnly();
        public void HandleStatusChanged(object? sender, DeviceStatusChangedEventArgs e)
        {
            string log = $"[LOG] {e.ChangedAt:HH:mm:ss} | {e.DeviceId}: {e.OldStatus} -> {e.NewStatus}";
            entries.Add(log);
            Console.WriteLine(log);
        }
    }

    public class MaintenanceService
    {
        private readonly List<string> requests = new();
        public IReadOnlyList<string> Requests => requests.AsReadOnly();
        public void HandleCriticalAlert(object? sender, DeviceAlertEventArgs e)
        {
            if (e.Severity != AlertSeverity.Critical) return;
            string req = $"[MAINTENANCE] Yêu cầu bảo trì cho {e.DeviceId}: {e.Message}";
            requests.Add(req);
            Console.WriteLine(req);
        }
    }

    // ================= 4. DELEGATE & FILTER =================
    public delegate bool AlertFilter(DeviceAlertEventArgs alert);
    public class FilteredAlertSubscriber
    {
        private readonly AlertFilter filter;
        private readonly Action<DeviceAlertEventArgs> action;
        public FilteredAlertSubscriber(AlertFilter filter, Action<DeviceAlertEventArgs> action)
        {
            this.filter = filter; this.action = action;
        }
        public void HandleAlert(object? sender, DeviceAlertEventArgs e) { if (filter(e)) action(e); }
    }

    // ================= 5. COORDINATOR =================
    public class DeviceMonitoringCoordinator : IDisposable
    {
        private readonly IEnumerable<TeachingDevice> devices;
        private readonly ConsoleNotificationService notificationService;
        private readonly DeviceAuditLogger auditLogger;
        private readonly MaintenanceService maintenanceService;

        public DeviceMonitoringCoordinator(IEnumerable<TeachingDevice> devices, ConsoleNotificationService notificationService, DeviceAuditLogger auditLogger, MaintenanceService maintenanceService)
        {
            this.devices = devices; this.notificationService = notificationService; this.auditLogger = auditLogger; this.maintenanceService = maintenanceService;
        }

        public void Subscribe()
        {
            foreach (var d in devices)
            {
                d.StatusChanged += auditLogger.HandleStatusChanged;
                d.AlertRaised += notificationService.HandleAlert;
                d.AlertRaised += maintenanceService.HandleCriticalAlert;
            }
        }

        public void Unsubscribe()
        {
            foreach (var d in devices)
            {
                d.StatusChanged -= auditLogger.HandleStatusChanged;
                d.AlertRaised -= notificationService.HandleAlert;
                d.AlertRaised -= maintenanceService.HandleCriticalAlert;
            }
        }

        public void Dispose() => Unsubscribe();
    }

    public class InvalidDeviceReadingException : Exception
    {
        public string DeviceId { get; }
        public int InvalidValue { get; }
        public InvalidDeviceReadingException(string id, int val) : base($"Thiết bị {id} có giá trị đọc không hợp lệ: {val}") { DeviceId = id; InvalidValue = val; }
    }

    // ================= 6. MAIN (KIỂM THỬ) =================
    class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            var proj = new ProjectorRemote("PRJ_01", "Máy chiếu 1");
            var ac = new AirConditionerRemote("AC_01", "Điều hòa 1", "D9-201");
            var mic = new WirelessMicrophone("MIC_01", "Micro 1");
            var devices = new List<TeachingDevice> { proj, ac, mic };

            var consoleService = new ConsoleNotificationService();
            var logger = new DeviceAuditLogger();
            var maintenance = new MaintenanceService();

            // Đăng ký event
            using var coordinator = new DeviceMonitoringCoordinator(devices, consoleService, logger, maintenance);
            coordinator.Subscribe();

            Console.WriteLine("--- TEST 1: THAY ĐỔI TRẠNG THÁI (Log) ---");
            proj.ChangeStatus(DeviceStatus.Borrowed);
            ac.ChangeStatus(DeviceStatus.UnderMaintenance);

            Console.WriteLine("\n--- TEST 2: CẢNH BÁO WARNING ---");
            proj.UpdateBatteryLevel(15);
            mic.UpdateSignalQuality(30);

            Console.WriteLine("\n--- TEST 3: CẢNH BÁO CRITICAL (Bảo trì nhảy vào) ---");
            proj.UpdateBatteryLevel(0);
            ac.UpdateResponseState(false);

            Console.WriteLine("\n--- TEST 4: CUSTOM DELEGATE FILTER ---");
            var customFilter = new FilteredAlertSubscriber(
                alert => alert.Message.Contains("pin"), 
                alert => Console.WriteLine($"[CUSTOM] Sếp ơi, {alert.DeviceId} lỗi pin kìa!")
            );
            mic.AlertRaised += customFilter.HandleAlert; // Test filter bắt lỗi pin
            mic.UpdateBatteryLevel(25);

            Console.WriteLine("\n--- TEST 5: NGOẠI LỆ ĐỌC DỮ LIỆU ---");
            try { mic.UpdateBatteryLevel(150); }
            catch (InvalidDeviceReadingException ex) { Console.WriteLine($"[BẮT LỖI] {ex.Message}"); }
        }
    }
}