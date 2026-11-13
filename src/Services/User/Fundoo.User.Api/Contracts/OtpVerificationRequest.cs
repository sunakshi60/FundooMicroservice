namespace Fundoo.User.Api.Contracts
{
    public class OtpVerificationRequest
    {
        public Guid ChallengeId { get; set; }

        public string Otp { get; set; } = string.Empty;
    }
}
