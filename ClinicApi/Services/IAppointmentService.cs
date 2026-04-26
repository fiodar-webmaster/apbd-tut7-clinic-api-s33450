using clinic_api.DTOs;

namespace clinic_api.Services;

public interface IAppointmentService
{
    // Promise 1: I will give you a list of appointments, optionally filtered.
    Task<IEnumerable<AppointmentListDto>> GetAppointmentsAsync(string? status, string? patientLastName);

    // Promise 2: I will find one specific appointment or return null.
    Task<AppointmentDetailsDto?> GetAppointmentByIdAsync(int idAppointment);

    // Promise 3: I will create an appointment and return the new ID.
    Task<int> CreateAppointmentAsync(CreateAppointmentRequestDto request);

    // Promise 4: I will update an existing appointment.
    Task<bool> UpdateAppointmentAsync(int idAppointment, UpdateAppointmentRequestDto request);

    // Promise 5: I will delete an appointment if the rules allow it.
    Task<bool> DeleteAppointmentAsync(int idAppointment);
}