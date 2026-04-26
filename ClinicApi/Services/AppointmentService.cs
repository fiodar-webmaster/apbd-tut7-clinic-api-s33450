using Microsoft.Data.SqlClient;
using clinic_api.DTOs;

namespace clinic_api.Services;

public class AppointmentService : IAppointmentService
{
    private readonly string _connectionString;

    public AppointmentService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
                            ?? throw new InvalidOperationException("Connection string missing.");
    }
    
    private SqlConnection GetConnection() => new SqlConnection(_connectionString);
    
    public async Task<IEnumerable<AppointmentListDto>> GetAppointmentsAsync(string? status, string? patientLastName)
    {
        var result = new List<AppointmentListDto>();
        const string sql = @"
            SELECT 
                a.IdAppointment, 
                a.AppointmentDate, 
                a.Status, 
                a.Reason, 
                p.FirstName + ' ' + p.LastName as PatientFullName, 
                p.Email AS PatientEmail
            FROM dbo.Appointments a 
            JOIN dbo.Patients p ON a.IdPatient = p.IdPatient 
            WHERE (@Status IS NULL OR a.Status = @Status) 
              AND (@LastName IS NULL OR p.LastName = @LastName)
            ORDER BY a.AppointmentDate DESC";

        await using var connection = GetConnection();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Status", string.IsNullOrEmpty(status) ? DBNull.Value : status);
        command.Parameters.AddWithValue("@LastName", string.IsNullOrEmpty(patientLastName) ? DBNull.Value : patientLastName);

        await connection.OpenAsync();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(MapToListDto(reader));
        }
        return result;
    }
    
    public async Task<AppointmentDetailsDto?> GetAppointmentByIdAsync(int id)
    {
        const string sql = @"
            SELECT 
                a.IdAppointment,
                a.AppointmentDate,
                a.Status,
                a.Reason,
                a.InternalNotes,
                a.CreatedAt,
                p.FirstName + ' ' + p.LastName as PatientFullName, 
                p.Email AS PatientEmail, 
                p.PhoneNumber AS PatientPhoneNumber, 
                d.FirstName + ' ' + d.LastName as DoctorFullName, 
                d.LicenseNumber AS DoctorLicenseNumber
            FROM dbo.Appointments a 
            JOIN dbo.Patients p ON a.IdPatient = p.IdPatient 
            JOIN dbo.Doctors d ON a.IdDoctor = d.IdDoctor 
            WHERE a.IdAppointment = @Id";

        await using var connection = GetConnection();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", id);

        await connection.OpenAsync();
        await using var reader = await command.ExecuteReaderAsync();
        
        if (await reader.ReadAsync())
        {
            return MapToDetailsDto(reader);
        }
        return null;
    }

    public async Task<int> CreateAppointmentAsync(CreateAppointmentRequestDto request)
    {
        if (request.AppointmentDate < DateTime.Now)
            throw new ArgumentException("AppointmentDate cannot be in the past");

        if (string.IsNullOrEmpty(request.Reason) || request.Reason.Length > 250)
        {
            throw new ArgumentException("Reason must be not null and up to 250 characters.");
        }
        
        await using var connection = GetConnection();
        await connection.OpenAsync();
        
        const string checkExistenceSql = @"
    SELECT 
        (SELECT COUNT(1) FROM dbo.Patients WHERE IdPatient = @IdPatient) AS PatientCount,
        (SELECT COUNT(1) FROM dbo.Doctors WHERE IdDoctor = @IdDoctor) AS DoctorCount;
";
        
        await using var existenceCommand = new SqlCommand(checkExistenceSql, connection);
        existenceCommand.Parameters.AddWithValue("@IdPatient", request.IdPatient);
        existenceCommand.Parameters.AddWithValue("@IdDoctor", request.IdDoctor);
        
        await using var existenceReader = await existenceCommand.ExecuteReaderAsync();
        await existenceReader.ReadAsync();

        var patientExists = existenceReader.GetInt32(0) > 0;
        var doctorExists = existenceReader.GetInt32(1) > 0;
        
        await existenceReader.CloseAsync();
        
        if (!patientExists)
            throw new ArgumentException("The specified patient does not exist.");

        if (!doctorExists)
            throw new ArgumentException("The specified doctor does not exist.");
        
        const string checkConflictSql = @"
        SELECT COUNT(1) 
        FROM dbo.Appointments 
        WHERE IdDoctor = @IdDoctor 
          AND AppointmentDate = @AppointmentDate
          AND Status != 'Cancelled'";
        
        
        await using var checkCommand = new SqlCommand(checkConflictSql, connection);
        
        checkCommand.Parameters.AddWithValue("@IdDoctor", request.IdDoctor);
        checkCommand.Parameters.AddWithValue("@AppointmentDate", request.AppointmentDate);

        var conflictCount = (int)await checkCommand.ExecuteScalarAsync();
        if (conflictCount > 0)
            throw new InvalidOperationException("Conflict");

        const string insertSql = @"
        INSERT INTO dbo.Appointments (IdPatient, IdDoctor, AppointmentDate, Reason, Status)
        OUTPUT INSERTED.IdAppointment
        VALUES (@IdPatient, @IdDoctor, @AppointmentDate, @Reason, 'Scheduled')";

        await using var insertCommand = new SqlCommand(insertSql, connection);
        insertCommand.Parameters.AddWithValue("@IdPatient", request.IdPatient);
        insertCommand.Parameters.AddWithValue("@IdDoctor", request.IdDoctor);
        insertCommand.Parameters.AddWithValue("@AppointmentDate", request.AppointmentDate);
        insertCommand.Parameters.AddWithValue("@Reason", request.Reason);

        return (int)await insertCommand.ExecuteScalarAsync();
    }
    
    
    public async Task<bool> UpdateAppointmentAsync(int idAppointment, UpdateAppointmentRequestDto request)
    {
        await using var connection = GetConnection();
        await connection.OpenAsync();
        
        
        
        const string checkExistenceSql = @"
    SELECT 
        (SELECT COUNT(1) FROM dbo.Patients WHERE IdPatient = @IdPatient) AS PatientCount,
        (SELECT COUNT(1) FROM dbo.Doctors WHERE IdDoctor = @IdDoctor) AS DoctorCount;
";
        
        await using var existenceCommand = new SqlCommand(checkExistenceSql, connection);
        existenceCommand.Parameters.AddWithValue("@IdPatient", request.IdPatient);
        existenceCommand.Parameters.AddWithValue("@IdDoctor", request.IdDoctor);
        
        await using var existenceReader = await existenceCommand.ExecuteReaderAsync();
        await existenceReader.ReadAsync();

        var patientExists = existenceReader.GetInt32(0) > 0;
        var doctorExists = existenceReader.GetInt32(1) > 0;
        
        await existenceReader.CloseAsync();
        
        if (!patientExists)
            throw new ArgumentException("The specified patient does not exist.");

        if (!doctorExists)
            throw new ArgumentException("The specified doctor does not exist.");
        
        
        const string currentStateSql = @"
        SELECT Status, AppointmentDate 
        FROM dbo.Appointments
        WHERE IdAppointment = @IdAppointment;
        ";
        
        
        await using var command = new SqlCommand(checkExistenceSql, connection);
        command.Parameters.AddWithValue("@IdAppointment", idAppointment);

        await connection.OpenAsync();
        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return false;
        }
        
        var currentStatus = reader.GetString(reader.GetOrdinal("Status"));
        var currentDate = reader.GetDateTime(reader.GetOrdinal("AppointmentDate"));

        await reader.CloseAsync();
        
        
        if (request.Status != "Scheduled" && request.Status != "Completed" && request.Status != "Cancelled")
        {
            throw new ArgumentException("Invalid appointment status for update.");
        }

        if (currentStatus == "Completed" && request.AppointmentDate != currentDate)
        {
            throw new InvalidOperationException("Can't change the date of a completed appointment.");
        }
        
        const string conflictSql = @"
        SELECT 1
        FROM dbo.Appointments 
        WHERE IdDoctor = @IdDoctor 
        AND AppointmentDate = @AppointmentDate
        AND IdAppointment != @IdAppointment 
        AND Status != 'Cancelled';
        ";
        
        await using var conflictCommand = new SqlCommand(conflictSql, connection);
        conflictCommand.Parameters.AddWithValue("@IdDoctor", request.IdDoctor);
        conflictCommand.Parameters.AddWithValue("@IdAppointment", idAppointment);
        conflictCommand.Parameters.AddWithValue("@AppointmentDate", request.AppointmentDate);
        var result = await conflictCommand.ExecuteScalarAsync();
        
        if (result != null)
        {
            throw new InvalidOperationException("Time conflict: the doctor is busy during that time.");
        }
        
        const string updateSql = @"
        UPDATE dbo.Appointments
        SET IdPatient = @IdPatient,
            IdDoctor = @IdDoctor,
            AppointmentDate = @NewDate, 
            Status = @Status,
            Reason = @Reason,
            InternalNotes = @InternalNotes
        WHERE IdAppointment = @IdAppointment;";
        
        
        await using var updateCommand = new SqlCommand(updateSql, connection);
        updateCommand.Parameters.AddWithValue("@IdPatient", request.IdPatient);
        updateCommand.Parameters.AddWithValue("@IdDoctor", request.IdDoctor);
        updateCommand.Parameters.AddWithValue("@NewDate", request.AppointmentDate);
        updateCommand.Parameters.AddWithValue("@Status", request.Status);
        updateCommand.Parameters.AddWithValue("@Reason", request.Reason);
        updateCommand.Parameters.AddWithValue("@InternalNotes", (object?)request.InternalNotes ?? DBNull.Value);
        updateCommand.Parameters.AddWithValue("@IdAppointment", idAppointment);
        
        return Convert.ToBoolean(await updateCommand.ExecuteNonQueryAsync());

    }

    public async Task<bool> DeleteAppointmentAsync(int idAppointment)
    {
        const string checkIfExistsSql = @"
        SELECT Status
        FROM dbo.Appointments 
        WHERE  IdAppointment = @IdAppointment;
";
        
        await using var connection = GetConnection();
        await using var command = new SqlCommand(checkIfExistsSql, connection);
        command.Parameters.AddWithValue("@IdAppointment", idAppointment);
        
        await connection.OpenAsync();
        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return false;
        }
        
        string status = reader.GetString(reader.GetOrdinal("Status"));
        await reader.CloseAsync();
        if (status == "Completed")
        {
            throw new InvalidOperationException("Cannot delete a completed appointment.");
        }

        const string deleteSql = @"
        DELETE FROM dbo.Appointments
        WHERE IdAppointment = @IdAppointment;
";
        
        await using var deleteCommand = new SqlCommand(deleteSql, connection);
        deleteCommand.Parameters.AddWithValue("@IdAppointment", idAppointment);
        
        return Convert.ToBoolean(await deleteCommand.ExecuteNonQueryAsync());
    }

    private AppointmentListDto MapToListDto(SqlDataReader reader) => new AppointmentListDto
    {
        IdAppointment = reader.GetInt32(reader.GetOrdinal("IdAppointment")),
        AppointmentDate = reader.GetDateTime(reader.GetOrdinal("AppointmentDate")),
        Status = reader.GetString(reader.GetOrdinal("Status")),
        Reason = reader.GetString(reader.GetOrdinal("Reason")),
        PatientFullName = reader.GetString(reader.GetOrdinal("PatientFullName")),
        PatientEmail = reader.GetString(reader.GetOrdinal("PatientEmail"))
    };

    private AppointmentDetailsDto MapToDetailsDto(SqlDataReader reader) => new AppointmentDetailsDto
    {
        IdAppointment = reader.GetInt32(reader.GetOrdinal("IdAppointment")),
        AppointmentDate = reader.GetDateTime(reader.GetOrdinal("AppointmentDate")),
        Status = reader.GetString(reader.GetOrdinal("Status")),
        Reason = reader.GetString(reader.GetOrdinal("Reason")),
        InternalNotes = reader.IsDBNull(reader.GetOrdinal("InternalNotes")) ? string.Empty : reader.GetString(reader.GetOrdinal("InternalNotes")),
        CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
        PatientFullName = reader.GetString(reader.GetOrdinal("PatientFullName")),
        PatientEmail = reader.GetString(reader.GetOrdinal("PatientEmail")),
        PatientPhoneNumber = reader.IsDBNull(reader.GetOrdinal("PatientPhoneNumber")) ? string.Empty : reader.GetString(reader.GetOrdinal("PatientPhoneNumber")),
        DoctorFullName = reader.GetString(reader.GetOrdinal("DoctorFullName")),
        DoctorLicenseNumber = reader.GetString(reader.GetOrdinal("DoctorLicenseNumber"))
    };
}