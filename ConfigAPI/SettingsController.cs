using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;

namespace SafeguardDevOpsService.ConfigAPI
{
    [ApiController]
    [Route("config/[controller]")]
    public class SettingsController : ControllerBase
    {
        private readonly IConfigurationRepository _configurationRepository;

        public SettingsController(IConfigurationRepository configurationRepository)
        {
            _configurationRepository = configurationRepository;
        }

        [HttpGet]
        public IEnumerable<Setting> GetSettings()
        {
            // TODO: errors
            return _configurationRepository.GetAllSettings();
        }

        [HttpGet("{name}")]
        public Setting GetSetting(string name)
        {
            // TODO: errors
            return _configurationRepository.GetSetting(name);
        }

        [HttpPut("{name}")]
        public void PutSetting(string name, Setting value)
        {
            if (!name.Equals(value.Name))
                throw new Exception("bad put TODO:");
            _configurationRepository.SetSetting(value);
        }
    }
}
