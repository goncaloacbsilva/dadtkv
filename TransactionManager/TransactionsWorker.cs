using System.Collections.Concurrent;
using Shared;
using TransactionManager;

class ActionWithResult<T>
{
    public Func<Task<T>> Task { get; }
    public TaskCompletionSource<T> Result { get; }

    public ActionWithResult(Func<Task<T>> task, TaskCompletionSource<T> result)
    {
        Task = task ?? throw new ArgumentNullException(nameof(task));
        Result = result ?? throw new ArgumentNullException(nameof(result));
    }
}

class TransactionsWorker {

    private BlockingCollection<ActionWithResult<TxSubmitResponse>> _pendingTransactions;
    private Queue<Transaction> _pendingTransactionsObjects;
    private LogManager _logManager;

    public TransactionsWorker(BlockingCollection<ActionWithResult<TxSubmitResponse>> pendingTransactions, Queue<Transaction> pendingTransactionsObjects, LogManager logManager) {
        _pendingTransactions = pendingTransactions;
        _pendingTransactionsObjects = pendingTransactionsObjects;
        _logManager = logManager;
    }

    public async Task Start() {
        _logManager.Logger.Debug("[Transactions Worker]: Waiting for transactions...");

        try {
            while(true) {
                if (_pendingTransactions.Any()) {
                    var taskWithResult = _pendingTransactions.Take();
                    var response = await taskWithResult.Task.Invoke();
                    _pendingTransactionsObjects.Dequeue();
                    taskWithResult.Result.SetResult(response);
                }
            }
        } catch (Exception e) {
            _logManager.Logger.Fatal("[Transactions Worker]: FATAL: {0}", e);
            //Environment.Exit(1);
        }
    }
}
