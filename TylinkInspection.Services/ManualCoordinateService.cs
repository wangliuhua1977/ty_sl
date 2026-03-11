using TylinkInspection.Core.Contracts;
using TylinkInspection.Core.Models;

namespace TylinkInspection.Services;

public sealed class ManualCoordinateService : IManualCoordinateService
{
    private static readonly StringComparer TextComparer = StringComparer.OrdinalIgnoreCase;
    private readonly IManualCoordinateStore _store;
    private readonly object _syncRoot = new();

    public ManualCoordinateService(IManualCoordinateStore store)
    {
        _store = store;
    }

    public IReadOnlyList<ManualCoordinateRecord> GetAll()
    {
        lock (_syncRoot)
        {
            return _store.Load()
                .OrderBy(item => item.DeviceCode, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    public ManualCoordinateRecord? Get(string deviceCode)
    {
        if (string.IsNullOrWhiteSpace(deviceCode))
        {
            return null;
        }

        lock (_syncRoot)
        {
            return _store.Load()
                .FirstOrDefault(item => TextComparer.Equals(item.DeviceCode, deviceCode.Trim()));
        }
    }

    public ManualCoordinateRecord Save(ManualCoordinateRecord record)
    {
        if (string.IsNullOrWhiteSpace(record.DeviceCode))
        {
            throw new InvalidOperationException("设备编码不能为空。");
        }

        ValidateCoordinate(record.Longitude, record.Latitude);

        lock (_syncRoot)
        {
            var records = _store.Load()
                .Where(item => !TextComparer.Equals(item.DeviceCode, record.DeviceCode))
                .ToList();
            var normalized = new ManualCoordinateRecord
            {
                DeviceCode = record.DeviceCode.Trim(),
                Longitude = record.Longitude,
                Latitude = record.Latitude,
                Remark = record.Remark?.Trim() ?? string.Empty,
                UpdatedAt = record.UpdatedAt == default ? DateTimeOffset.Now : record.UpdatedAt,
                CoordinateSystem = string.IsNullOrWhiteSpace(record.CoordinateSystem) ? "GCJ-02" : record.CoordinateSystem.Trim()
            };

            records.Add(normalized);
            _store.Save(records
                .OrderBy(item => item.DeviceCode, StringComparer.OrdinalIgnoreCase)
                .ToList());
            return normalized;
        }
    }

    public bool Clear(string deviceCode)
    {
        if (string.IsNullOrWhiteSpace(deviceCode))
        {
            return false;
        }

        lock (_syncRoot)
        {
            var trimmedCode = deviceCode.Trim();
            var records = _store.Load().ToList();
            var removed = records.RemoveAll(item => TextComparer.Equals(item.DeviceCode, trimmedCode)) > 0;
            if (removed)
            {
                _store.Save(records);
            }

            return removed;
        }
    }

    private static void ValidateCoordinate(double longitude, double latitude)
    {
        if (longitude is < -180 or > 180)
        {
            throw new InvalidOperationException("人工经度必须在 -180 到 180 之间。");
        }

        if (latitude is < -90 or > 90)
        {
            throw new InvalidOperationException("人工纬度必须在 -90 到 90 之间。");
        }
    }
}
