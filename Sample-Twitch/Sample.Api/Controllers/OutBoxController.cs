using System;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Sample.Api.Models;
using Sample.Contracts;

namespace Sample.Api.Controllers;

[ApiController]
[Route("out-box")]
[Tags("OutBox")]
public class OutBoxController(IPublishEndpoint publishEndpoint) : ControllerBase
{
    private readonly IPublishEndpoint _publishEndpoint = publishEndpoint;

    [HttpPost]
    public async Task<IActionResult> OutBox()
    {
        var id = Guid.NewGuid();
        await _publishEndpoint.Publish<OutBoxMessage>(new
        {
            Id = id,
        });
        
        return Accepted(new
        {
            Id = id,
        });
    }
}