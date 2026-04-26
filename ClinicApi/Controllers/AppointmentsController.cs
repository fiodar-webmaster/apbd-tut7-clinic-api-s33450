using clinic_api.DTOs;
using clinic_api.Services;
using Microsoft.AspNetCore.Mvc;

namespace clinic_api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AppointmentsController : ControllerBase
{
    private readonly IAppointmentService _service;

    public AppointmentsController(IAppointmentService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAppointments([FromQuery] string? status, [FromQuery] string? patientLastName)
    {
        var appointments = await _service.GetAppointmentsAsync(status, patientLastName);
        return Ok(appointments);
    }

    [HttpGet("{idAppointment}")]
    public async Task<IActionResult> GetById(int idAppointment)
    {
        var appointment = await _service.GetAppointmentByIdAsync(idAppointment);
        
        if (appointment == null) 
            return NotFound(new ErrorResponseDto { Message = "Appointment not found." });

        return Ok(appointment);
    }

    [HttpPost]
    public async Task<IActionResult> CreateAppointmentAsync(CreateAppointmentRequestDto request)
    {
        try
        {
            var newAppointmentId = await _service.CreateAppointmentAsync(request);
            return Created($"/api/appointments/{newAppointmentId}", new { IdAppointment = newAppointmentId });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ErrorResponseDto { Message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ErrorResponseDto { Message = ex.Message });
        } 
    }



    [HttpPut("{idAppointment}")]
    public async Task<IActionResult> UpdateAppointmentAsync(int idAppointment, UpdateAppointmentRequestDto request)
    {
        try
        {
            var isUpdated = await _service.UpdateAppointmentAsync(idAppointment, request);
            if (!isUpdated)
            {
                return NotFound(new ErrorResponseDto { Message = "Appointment not found." });
            }

            return Ok("Appointment updated");

        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ErrorResponseDto { Message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ErrorResponseDto { Message = ex.Message });
        }
    }

    [HttpDelete("{idAppointment}")]
    public async Task<IActionResult> DeleteAppointmentAsync(int idAppointment)
    {
        try
        {
            var deleted = await _service.DeleteAppointmentAsync(idAppointment);
            if (!deleted)
            {
                return NotFound(new ErrorResponseDto { Message = "Appointment not found." });
            }

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ErrorResponseDto { Message = ex.Message });
        }
    }
    
}