#pragma warning disable 1591

namespace OneIdentity.DevOps.Common
{
    public interface ISettingsRepository
    {
        ISetting GetSetting(string name);
        void SetSetting(ISetting value);
        void RemoveSetting(string name);

    }
}
