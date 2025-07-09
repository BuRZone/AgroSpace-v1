using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Cors;
using AgroSpace.Models;
using AgroSpace.Services;

namespace AgroSpace.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableCors]
public class FieldsController : ControllerBase
{
    private readonly IKmlService _kmlService;

    public FieldsController(IKmlService kmlService)
    {
        _kmlService = kmlService;
    }

    [HttpGet]
    public ActionResult<List<FieldResponse>> GetAllFields()
    {
        try
        {
            var fields = _kmlService.GetAllFields();
            var response = fields.Select(field => new FieldResponse
            {
                Id = field.Id,
                Name = field.Name,
                Size = field.Size,
                Locations = new FieldLocations
                {
                    Center = field.Center,
                    Polygon = field.Polygon
                }
            }).ToList();

            return Ok(response);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    [HttpGet("{id}/size")]
    public ActionResult<double> GetFieldSize(int id)
    {
        try
        {
            var size = _kmlService.GetFieldSize(id);
            if (size.HasValue)
            {
                return Ok(size.Value);
            }

            return NotFound(new { error = "Field not found" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    [HttpPost("distance")]
    public ActionResult<double> CalculateDistance([FromBody] DistanceRequest request)
    {
        try
        {
            var distance = _kmlService.CalculateDistance(request.FieldId, request.Lat, request.Lng);
            return Ok(distance);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    [HttpPost("point-location")]
    public ActionResult<object> IsPointInField([FromBody] PointLocationRequest request)
    {
        try
        {
            var result = _kmlService.IsPointInField(request.Lat, request.Lng);
            if (result != null)
            {
                return Ok(result);
            }

            return Ok(false);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }
} 