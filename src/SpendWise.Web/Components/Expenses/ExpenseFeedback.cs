namespace SpendWise.Web.Components.Expenses;

// Circuit-scoped, one-use feedback: refresh cannot replay a success supplied in a URL.
public sealed class ExpenseFeedback
{
    public string? Message { private get; set; }
    public string? Take()
    {
        var message = Message;
        Message = null;
        return message;
    }
}
