using clinic_api.DTOs;

namespace clinic_api.Services;

public interface IAppointmentService
{
    Task<IEnumerable<AppointmentListDto>> GetAppointmentsAsync(string? status, string? patientLastName);
    
    Task<AppointmentDetailsDto?> GetAppointmentByIdAsync(int idAppointment);
    
    Task<int> CreateAppointmentAsync(CreateAppointmentRequestDto request);
    
    Task<bool> UpdateAppointmentAsync(int idAppointment, UpdateAppointmentRequestDto request);
    
    Task<bool> DeleteAppointmentAsync(int idAppointment);
}