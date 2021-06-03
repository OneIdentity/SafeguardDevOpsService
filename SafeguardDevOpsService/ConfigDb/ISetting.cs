#pragma warning disable 1591

namespace OneIdentity.DevOps.ConfigDb
{
    public interface ISetting
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }
}
