namespace OneIdentity.SafeguardDevOpsService.ConfigDb
{
    public class Setting
    {
        private string _id;

        public string Id 
        { 
            get => Name;
            set => _id = value;
        }
        public string Name { get; set; }
        public string Value { get; set; }
    }
}
