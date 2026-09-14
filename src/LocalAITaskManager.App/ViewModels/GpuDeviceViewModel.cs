using LocalAITaskManager.Core.Models;
using LocalAITaskManager.Core.Services;

namespace LocalAITaskManager.App.ViewModels;

public sealed class GpuDeviceViewModel : ViewModelBase
{
    private string _name = string.Empty;
    private string _vramText = "—";
    private double _vramPercent;
    private string _gpuUtilizationText = "—";
    private string _temperatureText = "—";
    private string _powerText = "—";
    private string _driverVersion = "—";

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string VramText
    {
        get => _vramText;
        set => SetProperty(ref _vramText, value);
    }

    public double VramPercent
    {
        get => _vramPercent;
        set => SetProperty(ref _vramPercent, value);
    }

    public string GpuUtilizationText
    {
        get => _gpuUtilizationText;
        set => SetProperty(ref _gpuUtilizationText, value);
    }

    public string TemperatureText
    {
        get => _temperatureText;
        set => SetProperty(ref _temperatureText, value);
    }

    public string PowerText
    {
        get => _powerText;
        set => SetProperty(ref _powerText, value);
    }

    public string DriverVersion
    {
        get => _driverVersion;
        set => SetProperty(ref _driverVersion, value);
    }

    public void UpdateFromSnapshot(GpuDeviceSnapshot snapshot)
    {
        Name = snapshot.Name;
        VramText = ByteFormatter.FormatRatio(snapshot.UsedVramBytes, snapshot.TotalVramBytes);
        VramPercent = snapshot.TotalVramBytes is > 0 && snapshot.UsedVramBytes.HasValue
            ? Math.Clamp((double)snapshot.UsedVramBytes.Value / snapshot.TotalVramBytes.Value * 100.0, 0.0, 100.0)
            : 0.0;

        GpuUtilizationText = snapshot.GpuUtilizationPercent.HasValue
            ? $"{snapshot.GpuUtilizationPercent.Value:0}%"
            : "Unavailable";

        TemperatureText = snapshot.TemperatureCelsius.HasValue
            ? $"{snapshot.TemperatureCelsius.Value:0} °C"
            : "Unavailable";

        PowerText = snapshot.PowerWatts.HasValue
            ? $"{snapshot.PowerWatts.Value:0} W"
            : "Unavailable";

        DriverVersion = snapshot.DriverVersion ?? "—";
    }
}
