using Microsoft.AspNetCore.Mvc;
using OneIdentity.SafeguardDevOpsService.ConfigDb;

namespace OneIdentity.SafeguardDevOpsService.Controllers
{

    [Controller]
    [Route("config/[controller]")]
    public class InitializeController : Controller
    {
        private readonly IConfigurationRepository _configurationRepository;

        public InitializeController(IConfigurationRepository configurationRepository)
        {
            _configurationRepository = configurationRepository;
        }

        [HttpPost]
        public void GetSettings()
        {
            // initialize here?
        }
    }
}
