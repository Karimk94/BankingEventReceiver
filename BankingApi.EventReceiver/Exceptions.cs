namespace BankingApi.EventReceiver
{
    public class TransientFailureException(string message) : Exception(message)
    {
    }

    public class NonTransientFailureException(string message) : Exception(message)
    {
    }
}
