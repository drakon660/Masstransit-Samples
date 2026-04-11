namespace Sample.Api.Controllers;

using System;
using System.Threading.Tasks;
using Contracts;
using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Models;


[ApiController]
[Route("order")]
[Tags("Order")]
public class OrderController(
    IRequestClient<CheckOrder> checkOrderClient,
    IRequestClient<SubmitOrder> submitOrderRequestClient,
    IPublishEndpoint publishEndpoint,
    ISendEndpointProvider sendEndpointProvider) : ControllerBase
{
    private readonly IRequestClient<CheckOrder> _checkOrderClient = checkOrderClient;
    private readonly IRequestClient<SubmitOrder> _submitOrderRequestClient = submitOrderRequestClient;
    private readonly IPublishEndpoint _publishEndpoint = publishEndpoint;
    private readonly ISendEndpointProvider _sendEndpointProvider = sendEndpointProvider;

    [HttpGet("{id}/status")]
    public async Task<IActionResult> GetStatus(Guid id)
    {
        var (status, notFound) = await _checkOrderClient.GetResponse<OrderStatus, OrderNotFound>(new {OrderId = id});

        if (status.IsCompletedSuccessfully)
        {
            var response = await status;
            return Ok(response.Message);
        }
        else
        {
            var response = await notFound;
            return NotFound(response.Message);
        }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] OrderViewModel model)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var (accepted, rejected) =
            await _submitOrderRequestClient.GetResponse<OrderSubmissionAccepted, OrderSubmissionRejected>(new
            {
                OrderId = model.Id, 
                InVar.Timestamp,
                model.CustomerNumber,
                model.PaymentCardNumber,
                model.Notes
            });

        if (accepted.IsCompletedSuccessfully)
        {
            var response = await accepted;
            return Accepted(response);
        }

        if (accepted.IsCompleted)
        {
            await accepted;
            return Problem("Order was not accepted");
        }
        else
        {
            var response = await rejected;
            return BadRequest(response.Message);
        }
    }

    [HttpPatch("{id}/accept")]
    public async Task<IActionResult> AcceptOrder(Guid id)
    {
        await _publishEndpoint.Publish<OrderAccepted>(new
        {
            OrderId = id,
            InVar.Timestamp,
        });

        return Accepted();
    }

    [HttpPut("{id}/resubmit")]
    public async Task<IActionResult> ResubmitOrder(Guid id, string customerNumber)
    {
        var endpoint = await _sendEndpointProvider.GetSendEndpoint(new Uri("queue:submit-order"));

        await endpoint.Send<SubmitOrder>(new
        {
            OrderId = id,
            InVar.Timestamp,
            CustomerNumber = customerNumber
        });

        return Accepted();
    }

    [HttpPut("change-card-number")]
    public async Task<IActionResult> ChangeCardNumber(Guid id, string cardNumber)
    {
        await _publishEndpoint.Publish<ChangeCardNumber>(new
        {
            OrderId = id,
            PaymentCardNumber = cardNumber,
        });

        return Accepted();
    }
}