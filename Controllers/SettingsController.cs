using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using OneIdentity.SafeguardDevOpsService.ConfigDb;

namespace OneIdentity.SafeguardDevOpsService.Controllers
{
    [Controller]
    [Route("config/[controller]")]
    public class SettingsController : Controller
    {
        private readonly IConfigurationRepository _configurationRepository;

        public SettingsController(IConfigurationRepository configurationRepository)
        {
            _configurationRepository = configurationRepository;
        }

        [HttpGet]
        public IEnumerable<Setting> GetSettings()
        {
            return _configurationRepository.GetAllSettings();
        }

        [HttpGet("{name}")]
        public Setting GetSetting(string name)
        {
            return _configurationRepository.GetSetting(name);
        }

        [HttpPut("{name}")]
        public void PutSetting([FromBody]Setting setting)
        {
            _configurationRepository.SetSetting(setting);

            // TODO: restart listener service?
        }

        [HttpDelete("{name}")]
        public void DeleteSetting(string name)
        {
            _configurationRepository.RemoveSetting(name);

            // TODO: restart listener service?
        }
    }
}
