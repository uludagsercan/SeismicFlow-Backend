using SeismicFlow.Domain.Aggregates.Devices;
using SeismicFlow.Domain.ValueObjects;

namespace SeismicFlow.Domain.Repositories
{
    public interface IDeviceRepository
    {
        Task<Device?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<Device?> GetBySerialNumberAsync(DeviceId serialNumber, CancellationToken ct = default);
        Task<IReadOnlyList<Device>> GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default);
        Task<bool> ExistsBySerialNumberAsync(DeviceId serialNumber, CancellationToken ct = default);
        void Add(Device device);
        void Update(Device device);
    }
}
