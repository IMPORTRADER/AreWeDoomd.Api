using Microsoft.AspNetCore.Mvc;

namespace AreWeDoomd.Api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class HelloWorldController : ControllerBase
    {
        [HttpGet(Name = "Hello")]
        public async Task<IActionResult> GetHello()
        {
            return Ok("Hello, World!");
        }
    }
}
