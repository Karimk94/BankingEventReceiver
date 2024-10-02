using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace BankingApi.EventReceiver
{
    public class MessageWorker(IServiceBusReceiver serviceBusReceiver, BankingApiDbContext dbContext)
    {
        private readonly IServiceBusReceiver _serviceBusReceiver = serviceBusReceiver;
        private readonly BankingApiDbContext _dbContext = dbContext;

        public async Task Start()
        {
            while (true)
            {
                var message = await _serviceBusReceiver.Peek();
                if (message == null)
                {
                    await Task.Delay(10000); //wait 10 seconds if no messages are available
                    continue;
                }

                try
                {
                    await ProcessMessage(message);
                }
                catch (TransientFailureException)
                {
                    await RetryWithBackoff(message);
                }
                catch (NonTransientFailureException)
                {
                    await _serviceBusReceiver.MoveToDeadLetter(message);
                }
            }
        }

        private async Task ProcessMessage(EventMessage message)
        {
            var transactionMessage = JsonConvert.DeserializeObject<TransactionMessage>(message.MessageBody);

            var messageType = transactionMessage?.MessageType.ToLower();

            if (messageType != "credit" && messageType != "debit")
            {
                throw new NonTransientFailureException("Invalid message type");
            }

            var transaction = await _dbContext.Database.BeginTransactionAsync();

            try
            {
                var bankAccount = await _dbContext.BankAccounts.FirstOrDefaultAsync(b => b.Id == transactionMessage.BankAccountId);

                if (bankAccount == null)
                {
                    throw new NonTransientFailureException("Bank account not found");
                }

                if (messageType == "credit")
                {
                    bankAccount.Balance += transactionMessage.Amount;
                }
                else if (messageType == "debit")
                {
                    if (bankAccount.Balance < transactionMessage.Amount)
                    {
                        throw new NonTransientFailureException("Insufficient balance");
                    }
                    bankAccount.Balance -= transactionMessage.Amount;
                }

                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();
                await _serviceBusReceiver.Complete(message);
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private async Task RetryWithBackoff(EventMessage message)
        {
            int[] delays = { 5, 25, 125 };
            foreach (var delay in delays)
            {
                await Task.Delay(TimeSpan.FromSeconds(delay));
                try
                {
                    await ProcessMessage(message);
                    return;
                }
                catch (TransientFailureException)
                {
                    //no action needed, continue to next retry.
                }
            }
            await _serviceBusReceiver.MoveToDeadLetter(message);
        }
    }
}
