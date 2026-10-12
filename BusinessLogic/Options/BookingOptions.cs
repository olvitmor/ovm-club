namespace BusinessLogic.Options;

public class BookingOptions
{
    public const string SectionName = "Booking";

    public int RefundCutoffSeconds { get; set; }

    public int WaitlistCloseBeforeSeconds { get; set; }

    public TimeSpan RefundCutoff => TimeSpan.FromSeconds(RefundCutoffSeconds);

    public TimeSpan WaitlistCloseBefore => TimeSpan.FromSeconds(WaitlistCloseBeforeSeconds);
}
