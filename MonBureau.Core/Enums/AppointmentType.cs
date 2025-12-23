namespace MonBureau.Core.Enums

{ /// <summary>
  /// Appointment types
  /// </summary>
    public enum AppointmentType
    {
        Consultation = 0,
        CourtHearing = 1,
        ClientMeeting = 2,
        Mediation = 3,
        Deposition = 4,
        PhoneCall = 5,
        VideoConference = 6,
        Other = 99
    }
}